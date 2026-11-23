using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

public sealed class GameServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly SslStreamCertificateContext certificate;
    private readonly GameSessions sessions;
    private readonly GameWorkerPool? workers;
    private readonly IAuthService? auth;
    private readonly DurableMatches? durable;
    private readonly RoomManager _roomManager = new();
    private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
    private readonly ConcurrentDictionary<TcpClient, Task> connections = new();
    private readonly SemaphoreSlim slots = new(256);
    private readonly CancellationTokenSource _stop = new();
    private Task? _acceptLoop;
    private Task? matchMonitor;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public int ClientCount => _clients.Count;
    public int ConnectionCount => connections.Count;

    public GameServer(IPAddress bindAddress, X509Certificate2 certificate, GameSessions sessions, int port = 9000)
        : this(bindAddress, SslStreamCertificateContext.Create(certificate, null, offline: true), sessions, port)
    {
    }

    private GameServer(IPAddress bindAddress, SslStreamCertificateContext certificate, GameSessions sessions, int port)
    {
        this.certificate = certificate;
        this.sessions = sessions;
        _listener = new TcpListener(bindAddress, port);
    }

    public GameServer(IPAddress bindAddress, SslStreamCertificateContext certificate, GameSessions sessions,
        IAuthService auth, GameWorkerPool workers, int port = 8888, IMatchStore? matchStore = null) : this(bindAddress, certificate, sessions, port)
    {
        this.auth = auth;
        this.workers = workers;
        if (matchStore is not null) durable = new DurableMatches(matchStore, workers);
    }

    public void Start(CancellationToken ct = default)
    {
        ct.Register(() => _stop.Cancel());
        _listener.Start(128);
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_stop.Token));
        if (durable is not null)
            matchMonitor = Task.Run(() => durable.RunAsync(_roomManager, _stop.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(ct);
                if (!slots.Wait(0)) { tcp.Dispose(); continue; }
                var task = HandleClientAsync(tcp, ct);
                connections[tcp] = task;
                _ = task.ContinueWith(_ =>
                {
                    slots.Release();
                    connections.TryRemove(tcp, out var ignored);
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        ClientSession? session = null;

        try
        {
            using var tls = new SslStream(tcp.GetStream(), false);
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshake.CancelAfter(TimeSpan.FromSeconds(10));
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificateContext = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, handshake.Token);
            string? ticket;
            if (auth is not null)
            {
                var request = await GameWire.ReadAsync<GatewayRequest>(tls, handshake.Token);
                if (request?.Version != GatewayRequest.CurrentVersion) return;
                switch (request.Operation)
                {
                    case "auth" when request.Auth is { } login:
                        var result = await auth.HandleAsync(login, handshake.Token);
                        if (result is { Success: true, Player: { } profile } && login.Action == "signin")
                            result = result with { SessionToken = sessions.CreateSession(profile) };
                        await GameWire.WriteAsync(tls, result, handshake.Token);
                        return;
                    case "ticket":
                        await GameWire.WriteAsync(tls, new GameTicket(sessions.IssueTicket(request.Token)), handshake.Token);
                        return;
                    case "game":
                        ticket = request.Token;
                        break;
                    default:
                        return;
                }
            }
            else
            {
                var login = await GameWire.ReadAsync<GamePacket>(tls, handshake.Token);
                ticket = login?.Type == PacketType.AUTHENTICATE ? login.Payload : null;
            }
            var player = ticket is not null ? sessions.Consume(ticket) : null;
            if (player is null)
            {
                await GameWire.WriteAsync(tls, new GamePacket(PacketType.ERROR, Payload: "Phiên game không hợp lệ. Đăng nhập lại."), handshake.Token);
                return;
            }
            session = new ClientSession(tcp, tls) { Username = player.Username, Player = player };
            _clients[session.SessionId] = session;
            await session.SendAsync(new GamePacket(PacketType.AUTHENTICATED), handshake.Token);
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
            lifetime.CancelAfter(TimeSpan.FromHours(8));
            ct = lifetime.Token;
            while (!ct.IsCancellationRequested && tcp.Connected)
            {
                var packet = await GameWire.ReadAsync<GamePacket>(session.Stream, ct);
                if (packet is null) break;


                await RoutePacketAsync(session, packet, ct);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            if (session is not null)
            {
                _clients.TryRemove(session.SessionId, out _);
                if (durable is not null && _roomManager.GetRoom(session.CurrentRoomId) is { } room)
                    await durable.AbandonAsync(room);
                await _roomManager.RemoveClientAsync(session, notifyOpponent: true);
            }
            tcp.Dispose();
        }
    }

    private async Task RoutePacketAsync(ClientSession session, GamePacket packet, CancellationToken ct)
    {
        switch (packet.Type)
        {
            case PacketType.FIND_MATCH:
                {
                    if (!string.IsNullOrEmpty(session.CurrentRoomId) || packet.GameMode is not ("CHESS" or "CARO")) return;
                    session.Elo = packet.GameMode == "CARO" ? session.Player!.CaroElo : session.Player!.ChessElo;

                    var room = _roomManager.EnqueueOrMatch(session, packet.GameMode, distinctAccounts: durable is not null);
                    if (room is not null)
                    {
                        if (workers is not null)
                        {
                            try
                            {
                                var worker = await workers.OpenRoomAsync(room, ct);
                                if (!room.AttachWorker(worker)) { worker.Dispose(); return; }
                                if (durable is not null)
                                {
                                    using var start = CancellationTokenSource.CreateLinkedTokenSource(ct);
                                    start.CancelAfter(TimeSpan.FromSeconds(5));
                                    await room.MatchGate.WaitAsync(start.Token);
                                    try
                                    {
                                        if (room.Closing || room.Worker is null) return;
                                        await durable.StartAsync(room, start.Token);
                                    }
                                    finally { room.MatchGate.Release(); }
                                }
                            }
                            catch (Exception ex) when (ex is IOException or MySqlConnector.MySqlException or OperationCanceledException)
                            {
                                await _roomManager.RemoveClientAsync(session, notifyOpponent: false);
                                if (ct.IsCancellationRequested) return;
                                var unavailable = new GamePacket(PacketType.ERROR, GameMode: room.GameMode,
                                    Payload: ex is MySqlConnector.MySqlException
                                        ? "Kho lưu trận đang lỗi. Liên hệ quản trị viên."
                                        : "Game server đang bận hoặc bảo trì. Vui lòng tìm trận lại.");
                                await Task.WhenAll(room.Player1.SendAsync(unavailable, ct), room.Player2.SendAsync(unavailable, ct));
                                return;
                            }
                        }
                        Console.WriteLine($"[Match] Phong {room.RoomId} ({room.GameMode}): {room.Player1.Username} vs {room.Player2.Username}");

                        await room.Player1.SendAsync(new GamePacket(
                            PacketType.MATCH_FOUND,
                            Sender: room.Player2.Username,
                            RoomId: room.RoomId,
                            GameMode: room.GameMode,
                            Payload: $"FIRST|{room.Player2.Elo}", State: room.State
                        ), ct);

                        await room.Player2.SendAsync(new GamePacket(
                            PacketType.MATCH_FOUND,
                            Sender: room.Player1.Username,
                            RoomId: room.RoomId,
                            GameMode: room.GameMode,
                            Payload: $"SECOND|{room.Player1.Elo}", State: room.State
                        ), ct);
                        room.MarkReady();
                    }
                    break;
                }

            case PacketType.CANCEL_MATCH:
                {
                    _roomManager.CancelQueue(session);
                    break;
                }

            case PacketType.MOVE:
            case PacketType.CHAT:
            case PacketType.SURRENDER:
            case PacketType.DRAW_REQUEST:
            case PacketType.DRAW_RESPONSE:
                {
                    var room = _roomManager.GetRoom(session.CurrentRoomId);
                    if (room is null || (durable is not null && packet.RoomId != room.RoomId) ||
                        !await room.WaitUntilReadyAsync(ct)) return;
                    if (durable is not null)
                    {
                        if (packet.Type == PacketType.CHAT) await durable.RelayAsync(room, session, packet, ct);
                        else await durable.HandleAsync(room, session, packet with { Payload = packet.Payload ?? "" }, ct);
                        return;
                    }

                    var opponent = room.GetOpponent(session);
                    var message = packet with { Sender = session.Username, RoomId = room.RoomId, GameMode = room.GameMode };
                    if (workers is not null)
                    {
                        var worker = room.Worker;
                        if (worker is null) return;
                        try
                        {
                            var forwarded = await worker.RelayAsync(message with { Sender = session.SessionId }, ct);
                            message = forwarded with { Sender = session.Username };
                        }
                        catch (Exception ex) when (ex is IOException or InvalidDataException or SocketException or InvalidOperationException or OperationCanceledException)
                        {
                            ct.ThrowIfCancellationRequested();
                            await session.SendAsync(new GamePacket(PacketType.ERROR, Payload: "Mất kết nối tới game worker."), ct);
                            await _roomManager.RemoveClientAsync(session, notifyOpponent: true);
                            return;
                        }
                    }
                    await opponent.SendAsync(message, ct);
                    break;
                }

            case PacketType.LEAVE_ROOM:
                {
                    if (durable is not null && _roomManager.GetRoom(session.CurrentRoomId) is { } room)
                    {
                        if (packet.RoomId != room.RoomId) return;
                        await durable.AbandonAsync(room);
                    }
                    await _roomManager.RemoveClientAsync(session, notifyOpponent: true);
                    break;
                }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        if (_acceptLoop is not null)
            await _acceptLoop;
        foreach (var tcp in connections.Keys) tcp.Dispose();
        await Task.WhenAll(connections.Values);
        if (matchMonitor is not null) await matchMonitor;
        _stop.Dispose();
    }
}
