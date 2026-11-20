using System.Collections.Concurrent;
using System.Net.Sockets;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

public sealed class ClientSession
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N")[..8];
    public string Username { get; set; } = "Player";
    public int Elo { get; set; } = 1200;
    public string CurrentRoomId { get; set; } = string.Empty;
    public TcpClient Tcp { get; }
    public Stream Stream { get; }
    public PlayerProfile? Player { get; init; }
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ClientSession(TcpClient tcp, Stream stream)
    {
        Tcp = tcp;
        Stream = stream;
    }

    public async Task SendAsync(GamePacket packet, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            if (Tcp.Connected)
                await GameWire.WriteAsync(Stream, packet, ct);
        }
        catch { }
        finally
        {
            _writeLock.Release();
        }
    }
}

public sealed class GameRoom
{
    public required string RoomId { get; init; }
    public required string GameMode { get; init; }
    public required ClientSession Player1 { get; init; } // Đi trước (Trắng / X)
    public required ClientSession Player2 { get; init; } // Đi sau (Đen / O)

    public ClientSession GetOpponent(ClientSession me) =>
        me.SessionId == Player1.SessionId ? Player2 : Player1;
}

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly List<(ClientSession Session, string GameMode)> _waitingQueue = new();
    private readonly object _queueLock = new();

    /// <summary>
    /// Đưa người chơi vào hàng chờ; nếu đã có người chờ cùng chế độ chơi thì ghép thành 1 phòng ngay.
    /// </summary>
    public GameRoom? EnqueueOrMatch(ClientSession player, string gameMode)
    {
        string mode = string.IsNullOrWhiteSpace(gameMode) ? "CHESS" : gameMode.ToUpperInvariant();

        lock (_queueLock)
        {
            _waitingQueue.RemoveAll(x => x.Session.SessionId == player.SessionId || !x.Session.Tcp.Connected);

            int idx = _waitingQueue.FindIndex(x => x.GameMode == mode);
            if (idx >= 0)
            {
                var opponent = _waitingQueue[idx].Session;
                _waitingQueue.RemoveAt(idx);

                string roomId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                var room = new GameRoom
                {
                    RoomId = roomId,
                    GameMode = mode,
                    Player1 = opponent,
                    Player2 = player
                };

                opponent.CurrentRoomId = roomId;
                player.CurrentRoomId = roomId;
                _rooms[roomId] = room;
                return room;
            }

            _waitingQueue.Add((player, mode));
            return null;
        }
    }

    public void CancelQueue(ClientSession player)
    {
        lock (_queueLock)
        {
            _waitingQueue.RemoveAll(x => x.Session.SessionId == player.SessionId);
        }
    }

    public GameRoom? GetRoom(string roomId) =>
        !string.IsNullOrEmpty(roomId) && _rooms.TryGetValue(roomId, out var room) ? room : null;

    public async Task RemoveClientAsync(ClientSession client, bool notifyOpponent = true)
    {
        CancelQueue(client);

        if (string.IsNullOrEmpty(client.CurrentRoomId)) return;

        if (_rooms.TryRemove(client.CurrentRoomId, out var room))
        {
            var opponent = room.GetOpponent(client);
            opponent.CurrentRoomId = string.Empty;

            if (notifyOpponent)
            {
                await opponent.SendAsync(new GamePacket(
                    PacketType.OPPONENT_LEFT,
                    Sender: client.Username,
                    RoomId: room.RoomId,
                    GameMode: room.GameMode,
                    Payload: $"{client.Username} đã rời khỏi trận đấu."
                ));
            }
        }
        client.CurrentRoomId = string.Empty;
    }
}