using System.Net.Sockets;
using System.Text.Json;
using MySqlConnector;
using OnlineChessGame.Network;
using System.Collections.Concurrent;

namespace OnlineChessGame.Server;

internal sealed class DurableMatches(IMatchStore store, GameWorkerPool workers)
{
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private sealed class PendingRecovery
    {
        public required OpenMatchRecord Record { get; init; }
        public required MatchState ReclaimedState { get; set; }
        public required DateTimeOffset DeadlineUtc { get; init; }
        public ClientSession? FirstSession { get; set; }
        public ClientSession? SecondSession { get; set; }
        public bool Completed { get; set; }
        public object SyncRoot { get; } = new();
    }

    private readonly ConcurrentDictionary<string, PendingRecovery> _pendingRooms = new();
    private readonly ConcurrentDictionary<int, string> _pendingUserRooms = new();

    public async Task StartAsync(GameRoom room, CancellationToken ct)
    {
        long now = Now;
        var state = new MatchState(1, 0, "", 1, 600_000, 600_000, now, now);
        await store.CreateAsync(room.RoomId, room.GameMode, room.Player1.Player!.UserId, room.Player2.Player!.UserId, state, ct);
        room.State = state;
    }

    public async Task RecoverOnStartupAsync(CancellationToken ct)
    {
        var openMatches = await store.ReadOpenMatchesAsync(ct);
        foreach (var record in openMatches)
        {
            // Chính sách 1: Ván chưa đánh nước nào (Revision == 0) -> chốt hủy ngay
            if (record.Revision <= 0)
            {
                long now = Now;
                var aborted = record.State with
                {
                    Result = "ABORTED",
                    Reason = "Ván đấu chưa bắt đầu trước khi Gateway khởi động lại.",
                    Revision = record.Revision + 1,
                    ObservedAt = now
                };
                await store.AbortMatchAsync(record.RoomId, record.Generation, aborted, ct);
                continue;
            }

            // Nhận lại quyền điều phối bằng cách tăng Generation + 1 (fencing)
            var reclaimedState = record.State with
            {
                Generation = record.Generation + 1,
                ObservedAt = Now
            };
            if (!await store.TryReclaimAsync(record.RoomId, record.Generation, reclaimedState, ct))
                continue;

            var pending = new PendingRecovery
            {
                Record = record,
                ReclaimedState = reclaimedState,
                DeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(90)
            };

            _pendingRooms[record.RoomId] = pending;
            _pendingUserRooms[record.FirstUserId] = record.RoomId;
            _pendingUserRooms[record.SecondUserId] = record.RoomId;

            Console.WriteLine($"[Recovery] Da nhan lai quyen dieu phoi phong {record.RoomId} (Gen {reclaimedState.Generation}, Rev {record.Revision})");
            _ = MonitorAbandonedAsync(pending, ct);
        }
    }

    public async Task<bool> TryResumePlayerAsync(ClientSession session, RoomManager rooms, CancellationToken ct)
    {
        if (session.Player is null) return false;
        int userId = session.Player.UserId;

        if (!_pendingUserRooms.TryGetValue(userId, out var roomId) ||
            !_pendingRooms.TryGetValue(roomId, out var pending))
            return false;

        bool isFirst;
        bool bothConnected;
        ClientSession? p1;
        ClientSession? p2;
        MatchState currentState;

        lock (pending.SyncRoot)
        {
            if (pending.Completed || DateTimeOffset.UtcNow > pending.DeadlineUtc)
                return false;

            isFirst = userId == pending.Record.FirstUserId;
            if (isFirst)
                pending.FirstSession = session;
            else if (userId == pending.Record.SecondUserId)
                pending.SecondSession = session;
            else
                return false;

            session.CurrentRoomId = roomId;
            session.Elo = pending.Record.GameType == "CARO"
                ? session.Player.CaroElo
                : session.Player.ChessElo;

            p1 = pending.FirstSession;
            p2 = pending.SecondSession;
            bothConnected = p1 is not null && p2 is not null;
            if (bothConnected)
                pending.Completed = true;

            currentState = pending.ReclaimedState;
        }

        string sidePayload = isFirst
            ? $"FIRST|{p2?.Elo ?? 1200}"
            : $"SECOND|{p1?.Elo ?? 1200}";
        string opponentName = isFirst
            ? (p2?.Username ?? "Đối thủ")
            : (p1?.Username ?? "Đối thủ");

        // Đẩy bàn cờ từ MySQL về ngay cho client vừa đăng nhập
        await session.SendAsync(new GamePacket(
            PacketType.MATCH_FOUND,
            Sender: opponentName,
            RoomId: roomId,
            GameMode: pending.Record.GameType,
            Payload: sidePayload,
            State: currentState with { ObservedAt = Now }
        ), ct);

        if (!bothConnected)
        {
            // Mới có 1 người quay lại -> tạm khóa chờ đối thủ
            await session.SendAsync(new GamePacket(
                PacketType.RECOVERING,
                RoomId: roomId,
                GameMode: pending.Record.GameType
            ), ct);
            return true;
        }

        // Cả 2 người chơi đã quay lại -> gỡ khỏi hàng chờ và nạp lại vào Worker
        _pendingRooms.TryRemove(roomId, out _);
        _pendingUserRooms.TryRemove(pending.Record.FirstUserId, out _);
        _pendingUserRooms.TryRemove(pending.Record.SecondUserId, out _);

        var room = new GameRoom
        {
            RoomId = roomId,
            GameMode = pending.Record.GameType,
            Player1 = p1!,
            Player2 = p2!,
            State = currentState
        };

        var worker = await workers.OpenRoomAsync(room, ct, isFailover: true);
        if (!room.AttachWorker(worker))
        {
            worker.Dispose();
            return true;
        }

        // Bù thời gian lúc Gateway tắt để không trừ oan đồng hồ của người đang tới lượt
        var resumedState = currentState with
        {
            TurnStartedAt = Now,
            ObservedAt = Now
        };
        if (await store.UpdateAsync(roomId, currentState, resumedState, ct))
            room.State = resumedState;

        rooms.RegisterRecoveredRoom(room);
        room.MarkReady();

        Console.WriteLine($"[Recovery] Phong {roomId} da phuc hoi thanh cong: {p1!.Username} vs {p2!.Username}");
        await PublishAsync(room, Snapshot(room, room.State!), ct);
        return true;
    }

    // Chính sách 2: Quá 90 giây mà chưa đủ 2 người quay lại -> chốt hủy ván bỏ dở
    private async Task MonitorAbandonedAsync(PendingRecovery pending, CancellationToken ct)
    {
        try
        {
            var delay = pending.DeadlineUtc - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ClientSession? waitingPlayer;
        lock (pending.SyncRoot)
        {
            if (pending.Completed) return;
            pending.Completed = true;
            waitingPlayer = pending.FirstSession ?? pending.SecondSession;
        }

        string roomId = pending.Record.RoomId;
        _pendingRooms.TryRemove(roomId, out _);
        _pendingUserRooms.TryRemove(pending.Record.FirstUserId, out _);
        _pendingUserRooms.TryRemove(pending.Record.SecondUserId, out _);

        long now = Now;
        var aborted = pending.ReclaimedState with
        {
            Result = "ABORTED",
            Reason = "Hết thời gian chờ người chơi kết nối lại sau khi khởi động lại Gateway.",
            Revision = pending.ReclaimedState.Revision + 1,
            ObservedAt = now
        };
        await store.AbortMatchAsync(roomId, (int)pending.ReclaimedState.Generation, aborted, ct);

        if (waitingPlayer is not null)
        {
            waitingPlayer.CurrentRoomId = string.Empty;
            await waitingPlayer.SendAsync(new GamePacket(
                PacketType.OPPONENT_LEFT,
                RoomId: roomId,
                GameMode: pending.Record.GameType,
                Payload: "Đối thủ không quay lại kịp thời. Ván đấu đã bị hủy."
            ), ct);
        }
    }

    public async Task RunAsync(RoomManager rooms, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await Task.WhenAll(rooms.ActiveRooms().Select(room => TickAsync(room, ct)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task TickAsync(GameRoom room, CancellationToken ct)
    {
        if (!room.IsReady || room.Closing || room.State is not { Result: null } state) return;
        if (!room.NeedsReload && !room.Recovering && room.Worker?.IsAvailable == true && state.Remaining(state.Turn, Now) > 0) return;
        await HandleAsync(room, room.Player1, new GamePacket(PacketType.CLOCK_EXPIRED), ct);
    }

    public async Task HandleAsync(GameRoom room, ClientSession sender, GamePacket packet, CancellationToken ct)
    {
        await room.MatchGate.WaitAsync(ct);
        try
        {
            if (room.Closing || room.State is not { Result: null }) return;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            var token = deadline.Token;
            var worker = await EnsureWorkerAsync(room, token);
            if (worker is null || room.State is not { Result: null } old) return;
            if (packet.Type == PacketType.CLOCK_EXPIRED && old.Remaining(old.Turn, Now) > 0) return;

            var response = await worker.RelayAsync(packet with
            {
                RoomId = room.RoomId, GameMode = room.GameMode, Sender = sender.SessionId,
                State = old with { ObservedAt = Now }
            }, token);
            if (response.Type == PacketType.ERROR)
            {
                await sender.SendAsync(Snapshot(room, old, response.Payload), token);
                return;
            }
            if (response.State is not { } candidate || candidate.Generation != old.Generation)
                throw new InvalidDataException("Invalid worker match response.");
            if (candidate.Revision == old.Revision) return;
            if (candidate.Revision != old.Revision + 1)
                throw new InvalidDataException("Invalid worker revision.");

            room.NeedsReload = true;
            if (!await store.UpdateAsync(room.RoomId, old, candidate, token))
                throw new IOException("Match revision or generation changed.");
            room.State = candidate;
            room.NeedsReload = false;
            await PublishAsync(room, Snapshot(room, candidate), ct);
            if (candidate.Result is not null) room.DetachWorker();
        }
        catch (Exception ex) when (IsTransient(ex) && !ct.IsCancellationRequested)
        {
            if (!room.NeedsReload)
            {
                room.Worker?.MarkFailed();
                room.DetachWorker();
            }
            await NotifyRecoveryAsync(room, ct);
        }
        finally { room.MatchGate.Release(); }
    }

    public async Task RelayAsync(GameRoom room, ClientSession sender, GamePacket packet, CancellationToken ct)
    {
        await room.MatchGate.WaitAsync(ct);
        try
        {
            if (room.Closing || room.Worker is not { IsAvailable: true } worker || room.Recovering) return;
            var forwarded = await worker.RelayAsync(packet with
            {
                RoomId = room.RoomId, GameMode = room.GameMode, Sender = sender.SessionId, State = null
            }, ct);
            await room.GetOpponent(sender).SendAsync(forwarded with { Sender = sender.Username }, ct);
        }
        catch (Exception ex) when (IsTransient(ex) && !ct.IsCancellationRequested)
        {
            room.Worker?.MarkFailed();
            room.DetachWorker();
            await NotifyRecoveryAsync(room, ct);
        }
        finally { room.MatchGate.Release(); }
    }

    public async Task AbandonAsync(GameRoom room)
    {
        room.Closing = true;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        bool entered = false;
        try
        {
            await room.MatchGate.WaitAsync(deadline.Token);
            entered = true;
            if (room.State is null) return;
            if (await store.ReadAsync(room.RoomId, deadline.Token) is not { Result: null } old) return;
            long now = Now;
            var result = old with { Result = "ABORTED", Reason = "Người chơi đã rời trận.",
                Revision = old.Revision + 1, ObservedAt = now, FirstMilliseconds = old.Remaining(1, now),
                SecondMilliseconds = old.Remaining(2, now), TurnStartedAt = now };
            // The caller sends OPPONENT_LEFT after removing the room.
            if (await store.UpdateAsync(room.RoomId, old, result, deadline.Token)) room.State = result;
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            Console.Error.WriteLine($"Could not record room abandonment ({ex.GetType().Name}).");
        }
        finally { if (entered) room.MatchGate.Release(); }
    }

    private async Task<WorkerRoom?> EnsureWorkerAsync(GameRoom room, CancellationToken ct)
    {
        if (room.Worker is { IsAvailable: true } current && !room.NeedsReload && !room.Recovering) return current;
        await NotifyRecoveryAsync(room, ct);
        var saved = await store.ReadAsync(room.RoomId, ct) ?? throw new IOException("Room snapshot unavailable.");
        room.State = saved;
        room.NeedsReload = false;
        if (saved.Result is not null)
        {
            room.Recovering = false;
            room.RecoveryStartedAt = null;
            await PublishAsync(room, Snapshot(room, saved), ct);
            room.DetachWorker();
            return null;
        }
        long pausedMs = room.RecoveryStartedAt is long pausedAt ? Math.Max(0, Now - pausedAt) : 0;
        if (room.Worker is not { IsAvailable: true } surviving)
        {
            room.DetachWorker();
            surviving = await workers.OpenRoomAsync(room, ct, isFailover: true);
            var fenced = saved with
            {
                Generation = saved.Generation + 1,
                TurnStartedAt = saved.TurnStartedAt + pausedMs
            };
            try
            {
                if (!await store.UpdateAsync(room.RoomId, saved, fenced, ct))
                    throw new IOException("Could not transfer match ownership.");
                if (!room.AttachWorker(surviving))
                {
                    surviving.Dispose();
                    return null;
                }
            }
            catch { surviving.Dispose(); throw; }
            room.State = fenced;
        }
        else if (pausedMs > 0)
        {
            var compensated = saved with { TurnStartedAt = saved.TurnStartedAt + pausedMs };
            if (await store.UpdateAsync(room.RoomId, saved, compensated, ct))
                room.State = compensated;
        }
        room.Recovering = false;
        room.RecoveryStartedAt = null;
        await PublishAsync(room, Snapshot(room, room.State!), ct);
        return surviving;
    }

    private static async Task NotifyRecoveryAsync(GameRoom room, CancellationToken ct)
    {
        if (room.Recovering) return;
        room.Recovering = true;
        room.RecoveryStartedAt = Now;
        await PublishAsync(room, new GamePacket(PacketType.RECOVERING, RoomId: room.RoomId, GameMode: room.GameMode), ct);
    }

    private static GamePacket Snapshot(GameRoom room, MatchState state, string message = "") =>
        new(PacketType.STATE, RoomId: room.RoomId, GameMode: room.GameMode, Payload: message,
            State: state with { ObservedAt = Now });

    private static Task PublishAsync(GameRoom room, GamePacket message, CancellationToken ct) =>
        Task.WhenAll(room.Player1.SendAsync(message, ct), room.Player2.SendAsync(message, ct));

    private static bool IsTransient(Exception ex) =>
        ex is IOException or SocketException or ObjectDisposedException or InvalidDataException or OperationCanceledException
            or MySqlException or InvalidOperationException or JsonException;
}
