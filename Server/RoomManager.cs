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
    public PlayerProfile? Player { get; set; }
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ClientSession(TcpClient tcp, Stream stream)
    {
        Tcp = tcp;
        Stream = stream;
    }

    public async Task SendAsync(GamePacket packet, CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        bool entered = false;
        try
        {
            await _writeLock.WaitAsync(deadline.Token);
            entered = true;
            if (Tcp.Connected)
                await GameWire.WriteAsync(Stream, packet, deadline.Token);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or
            InvalidDataException or OperationCanceledException)
        {
            // A timed-out frame may be only partially written.
            if (entered || !ct.IsCancellationRequested) Tcp.Dispose();
        }
        finally
        {
            if (entered) _writeLock.Release();
        }
    }
}

public sealed class GameRoom
{
    private readonly object workerGate = new();
    private readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WorkerRoom? worker;
    private bool closed;
    internal SemaphoreSlim MatchGate { get; } = new(1, 1);
    internal MatchState? State { get; set; }
    internal bool NeedsReload { get; set; }
    internal bool Recovering { get; set; }
    internal long? RecoveryStartedAt { get; set; }
    internal bool Closing { get; set; }
    internal bool IsClosed { get { lock (workerGate) return closed; } }
    internal WorkerRoom? Worker { get { lock (workerGate) return worker; } }
    internal Task<bool> WaitUntilReadyAsync(CancellationToken ct) => ready.Task.WaitAsync(ct);
    internal bool IsReady => ready.Task.IsCompletedSuccessfully && ready.Task.Result;
    internal void MarkReady() => ready.TrySetResult(true);

    internal bool AttachWorker(WorkerRoom value)
    {
        lock (workerGate)
        {
            if (closed) return false;
            worker?.Dispose();
            worker = value;
            return true;
        }
    }

    internal void DetachWorker()
    {
        lock (workerGate)
        {
            worker?.Dispose();
            worker = null;
        }
    }

    internal void Close()
    {
        lock (workerGate)
        {
            closed = true;
            Closing = true;
            ready.TrySetResult(false);
            worker?.Dispose();
            worker = null;
        }
    }

    public required string RoomId { get; init; }
    public required string GameMode { get; init; }
    public required ClientSession Player1 { get; set; } // Đi trước (Trắng / X)
    public required ClientSession Player2 { get; set; } // Đi sau (Đen / O)
    public string? Player1QueueAttemptId { get; init; }
    public string? Player2QueueAttemptId { get; init; }

    public ClientSession GetOpponent(ClientSession me) =>
        me.SessionId == Player1.SessionId ? Player2 : Player1;
}

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly List<(ClientSession Session, string GameMode, string? QueueAttemptId)> _waitingQueue = new();
    private readonly object _queueLock = new();

    /// <summary>
    /// Đưa người chơi vào hàng chờ; nếu đã có người chờ cùng chế độ chơi thì ghép thành 1 phòng ngay.
    /// </summary>
    public GameRoom? EnqueueOrMatch(ClientSession player, string gameMode, bool distinctAccounts = false, string? queueAttemptId = null)
    {
        string mode = string.IsNullOrWhiteSpace(gameMode) ? "CHESS" : gameMode.ToUpperInvariant();

        lock (_queueLock)
        {
            _waitingQueue.RemoveAll(x => x.Session.SessionId == player.SessionId || !x.Session.Tcp.Connected);

            int idx = _waitingQueue.FindIndex(x => x.GameMode == mode &&
                (!distinctAccounts || x.Session.Player?.UserId != player.Player?.UserId));
            if (idx >= 0)
            {
                var waiting = _waitingQueue[idx];
                var opponent = waiting.Session;
                _waitingQueue.RemoveAt(idx);

                string roomId = Guid.NewGuid().ToString("N");
                var room = new GameRoom
                {
                    RoomId = roomId,
                    GameMode = mode,
                    Player1 = opponent,
                    Player2 = player,
                    Player1QueueAttemptId = waiting.QueueAttemptId,
                    Player2QueueAttemptId = queueAttemptId
                };

                opponent.CurrentRoomId = roomId;
                player.CurrentRoomId = roomId;
                _rooms[roomId] = room;
                return room;
            }

            _waitingQueue.Add((player, mode, queueAttemptId));
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
        GameRoom? room;
        ClientSession opponent;
        lock (_queueLock)
        {
            _waitingQueue.RemoveAll(x => x.Session.SessionId == client.SessionId);
            if (string.IsNullOrEmpty(client.CurrentRoomId)) return;
            string id = client.CurrentRoomId;
            client.CurrentRoomId = string.Empty;
            if (!_rooms.TryGetValue(id, out room) ||
                (!ReferenceEquals(room.Player1, client) && !ReferenceEquals(room.Player2, client))) return;
            if (!_rooms.TryRemove(id, out room)) return;
            room.Close();
            opponent = room.GetOpponent(client);
            opponent.CurrentRoomId = string.Empty;
        }
        if (notifyOpponent && room.State?.Result is (null or "ABORTED"))
            await opponent.SendAsync(new GamePacket(
                PacketType.OPPONENT_LEFT,
                Sender: client.Username,
                RoomId: room.RoomId,
                GameMode: room.GameMode,
                Payload: $"{client.Username} đã rời khỏi trận đấu."
            ));
    }

    internal void RemoveClosedRoom(GameRoom room)
    {
        room.Close();
        _rooms.TryRemove(new KeyValuePair<string, GameRoom>(room.RoomId, room));
    }

    internal GameRoom[] ActiveRooms() => _rooms.Values.ToArray();

    internal void RegisterRecoveredRoom(GameRoom room) => _rooms[room.RoomId] = room;
}
