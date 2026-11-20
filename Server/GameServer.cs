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
    private readonly X509Certificate2 certificate;
    private readonly GameSessions sessions;
    private readonly RoomManager _roomManager = new();
    private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
    private readonly CancellationTokenSource _stop = new();
    private Task? _acceptLoop;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public int ClientCount => _clients.Count;

    public GameServer(IPAddress bindAddress, X509Certificate2 certificate, GameSessions sessions, int port = 9000)
    {
        this.certificate = certificate;
        this.sessions = sessions;
        _listener = new TcpListener(bindAddress, port);
    }

    public void Start(CancellationToken ct = default)
    {
        ct.Register(() => _stop.Cancel());
        _listener.Start(128);
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_stop.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(tcp, ct), ct);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        using var tls = new SslStream(tcp.GetStream(), false);
        ClientSession? session = null;

        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshake.CancelAfter(TimeSpan.FromSeconds(10));
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, handshake.Token);
            var login = await GameWire.ReadAsync<GamePacket>(tls, handshake.Token);
            var player = login?.Type == PacketType.AUTHENTICATE ? sessions.Consume(login.Payload) : null;
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

                    var room = _roomManager.EnqueueOrMatch(session, packet.GameMode);
                    if (room is not null)
                    {
                        Console.WriteLine($"[Match] Phong {room.RoomId} ({room.GameMode}): {room.Player1.Username} vs {room.Player2.Username}");

                        await room.Player1.SendAsync(new GamePacket(
                            PacketType.MATCH_FOUND,
                            Sender: room.Player2.Username,
                            RoomId: room.RoomId,
                            GameMode: room.GameMode,
                            Payload: $"FIRST|{room.Player2.Elo}"
                        ), ct);

                        await room.Player2.SendAsync(new GamePacket(
                            PacketType.MATCH_FOUND,
                            Sender: room.Player1.Username,
                            RoomId: room.RoomId,
                            GameMode: room.GameMode,
                            Payload: $"SECOND|{room.Player1.Elo}"
                        ), ct);
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
                    if (room is null) return;

                    var opponent = room.GetOpponent(session);
                    await opponent.SendAsync(packet with { Sender = session.Username, RoomId = room.RoomId }, ct);
                    break;
                }

            case PacketType.LEAVE_ROOM:
                {
                    await _roomManager.RemoveClientAsync(session, notifyOpponent: true);
                    break;
                }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        foreach (var session in _clients.Values)
            session.Tcp.Dispose();
        if (_acceptLoop is not null)
            await _acceptLoop;
        _stop.Dispose();
    }
}
