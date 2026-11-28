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
    private readonly object lifecycleGate = new();
    private Task? startupTask;
    private Task? disposalTask;
    private bool disposing;
    private CancellationTokenRegistration lifetimeCancellation;
    private Task? _acceptLoop;
    private Task? matchMonitor;
    private readonly SemaphoreSlim resumeGate = new(1, 1);

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
        IAuthService auth, GameWorkerPool workers, int port = 8888, IMatchStore? matchStore = null, TimeSpan? reconnectWindow = null) : this(bindAddress, certificate, sessions, port)
    {
        this.auth = auth;
        this.workers = workers;
        if (matchStore is not null) durable = new DurableMatches(matchStore, workers, reconnectWindow);
    }

    // Compatibility for synchronous callers. Run the entire async operation off the
    // caller's synchronization context; WinForms callers should await StartAsync.
    public void Start(CancellationToken ct = default) =>
        Task.Run(() => StartAsync(ct), CancellationToken.None).GetAwaiter().GetResult();

    // One startup attempt per server; repeated calls share its outcome and the
    // first caller's lifetime token. Disposal prevents any later startup.
    public Task StartAsync(CancellationToken ct = default)
    {
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposing, this);
            return startupTask ??= StartCoreAsync(ct);
        }
    }

    private async Task StartCoreAsync(CancellationToken ct)
    {
        lifetimeCancellation = ct.Register(() => _stop.Cancel());
        try
        {
            // No game or auth request can race the startup scan. Bound only the
            // recovery phase, not the lifetime of the successfully started server.
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(30));
            if (durable is not null)
                await durable.RecoverOnStartupAsync(startup.Token).ConfigureAwait(false);
            startup.Token.ThrowIfCancellationRequested();
            _listener.Start(128);
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_stop.Token));
            if (durable is not null)
                matchMonitor = Task.Run(() => durable.RunAsync(_roomManager, _stop.Token));
        }
        catch
        {
            _stop.Cancel();
            _listener.Stop();
            durable?.StopRecovery();
            throw;
        }
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
            player = await sessions.RefreshAsync(player, handshake.Token);
            session = new ClientSession(tcp, tls) { Username = player.Username, Player = player };
            _clients[session.SessionId] = session;
            await session.SendAsync(new GamePacket(PacketType.AUTHENTICATED, Player: player), handshake.Token);

            if (durable is not null)
            {
                await resumeGate.WaitAsync(ct);
                try { await durable.TryResumePlayerAsync(session, _roomManager, ct); }
                finally { resumeGate.Release(); }
            }

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
                if (durable is not null && !_stop.IsCancellationRequested)
                    await durable.DisconnectAsync(session, _roomManager, _stop.Token);
                else if (durable is null)
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
                    session.Player = await sessions.RefreshAsync(session.Player!, ct);
                    session.Elo = packet.GameMode == "CARO" ? session.Player!.CaroElo : session.Player!.ChessElo;

                    var room = _roomManager.EnqueueOrMatch(session, packet.GameMode, distinctAccounts: durable is not null, queueAttemptId: packet.QueueAttemptId);
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
                            Payload: $"FIRST|{room.Player2.Elo}", State: room.State, QueueAttemptId: room.Player1QueueAttemptId
                        ), ct);

                        await room.Player2.SendAsync(new GamePacket(
                            PacketType.MATCH_FOUND,
                            Sender: room.Player1.Username,
                            RoomId: room.RoomId,
                            GameMode: room.GameMode,
                            Payload: $"SECOND|{room.Player1.Elo}", State: room.State, QueueAttemptId: room.Player2QueueAttemptId
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
                    if (durable is not null && packet.RoomId == session.CurrentRoomId &&
                        await durable.LeavePendingAsync(session, _roomManager, ct)) return;
                    if (durable is not null && _roomManager.GetRoom(session.CurrentRoomId) is { } room)
                    {
                        if (packet.RoomId != room.RoomId ||
                            (!ReferenceEquals(room.Player1, session) && !ReferenceEquals(room.Player2, session))) return;
                        await durable.LeaveAsync(room, session, _roomManager, ct);
                        return;
                    }
                    await _roomManager.RemoveClientAsync(session, notifyOpponent: true);
                    break;
                }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifecycleGate)
        {
            disposing = true;
            return new ValueTask(disposalTask ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        _stop.Cancel();
        // Startup owns recovery cleanup and may still use _stop in its catch.
        // Observe its failure here; StartAsync remains responsible for reporting it.
        if (startupTask is not null)
        {
            try { await startupTask.ConfigureAwait(false); }
            catch { }
        }
        _listener.Stop();
        if (_acceptLoop is not null)
            await _acceptLoop;
        foreach (var tcp in connections.Keys) tcp.Dispose();
        await Task.WhenAll(connections.Values);
        if (matchMonitor is not null) await matchMonitor;
        durable?.StopRecovery();
        foreach (var room in _roomManager.ActiveRooms()) room.Close();
        lifetimeCancellation.Dispose();
        _stop.Dispose();
    }
}
