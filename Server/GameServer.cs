using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

public sealed class GameServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly RoomManager _roomManager = new();
    private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
    private readonly CancellationTokenSource _stop = new();
    private Task? _acceptLoop;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public GameServer(IPAddress bindAddress, int port = 9000)
    {
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
        var session = new ClientSession(tcp);
        _clients[session.SessionId] = session;

        try
        {
            while (!ct.IsCancellationRequested && tcp.Connected)
            {
                var packet = await GameWire.ReadAsync<GamePacket>(session.Stream, ct);
                if (packet is null) break;

                if (!string.IsNullOrWhiteSpace(packet.Sender))
                    session.Username = packet.Sender;

                await RoutePacketAsync(session, packet, ct);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            _clients.TryRemove(session.SessionId, out _);
            await _roomManager.RemoveClientAsync(session, notifyOpponent: true);
            tcp.Dispose();
        }
    }

    private async Task RoutePacketAsync(ClientSession session, GamePacket packet, CancellationToken ct)
    {
        switch (packet.Type)
        {
            case PacketType.FIND_MATCH:
                {
                    if (int.TryParse(packet.Payload, out int elo))
                        session.Elo = elo;

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
        if (_acceptLoop is not null)
            await _acceptLoop;
        _stop.Dispose();
    }
}