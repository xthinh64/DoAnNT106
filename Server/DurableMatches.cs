using System.Net.Sockets;
using System.Text.Json;
using MySqlConnector;
using OnlineChessGame.Network;
using System.Collections.Concurrent;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TransportTests")]

namespace OnlineChessGame.Server;

internal sealed class DurableMatches(IMatchStore store, GameWorkerPool workers, TimeSpan? reconnectWindow = null)
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
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public GameRoom? Room { get; set; }
        public bool LeaveRequested { get; set; }
    }

    // The first reconciled snapshot is the baseline for this recovery episode.
    // Never derive another full pause credit from a possibly committed compensation.
    private readonly ConcurrentDictionary<string, MatchState> _recoveryBaselines = new();
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
                DeadlineUtc = DateTimeOffset.UtcNow + (reconnectWindow ?? TimeSpan.FromSeconds(90))
            };

            workers.ReserveRecoveryDemand(record.RoomId);
            _pendingRooms[record.RoomId] = pending;
            _pendingUserRooms[record.FirstUserId] = record.RoomId;
            _pendingUserRooms[record.SecondUserId] = record.RoomId;

            Console.WriteLine($"[Recovery] Da nhan lai quyen dieu phoi phong {record.RoomId} (Gen {reclaimedState.Generation}, Rev {record.Revision})");

        }
    }

    // Clocks freeze at detected socket loss. Restart freezes the last stored balances.
    // This is single-active-Gateway recovery, not a distributed ownership lease.
    public async Task DisconnectAsync(ClientSession session, RoomManager rooms, CancellationToken ct)
    {
        rooms.CancelQueue(session);
        if (_pendingRooms.TryGetValue(session.CurrentRoomId, out var pending))
        {
            await pending.Gate.WaitAsync(ct);
            bool completed;
            try {
                completed = pending.Completed;
                if (!completed) {
                    if (ReferenceEquals(pending.FirstSession, session)) pending.FirstSession = null;
                    if (ReferenceEquals(pending.SecondSession, session)) pending.SecondSession = null;
                    session.CurrentRoomId = "";
                }
            } finally { pending.Gate.Release(); }
            if (completed) await DisconnectAsync(session, rooms, ct);
            return;
        }
        var room = rooms.GetRoom(session.CurrentRoomId);
        if (room is null) return;
        await room.MatchGate.WaitAsync(ct);
        bool released = false;
        try
        {
            if (!ReferenceEquals(room.Player1, session) && !ReferenceEquals(room.Player2, session)) return;
            // Another socket may have disconnected while this one waited on MatchGate.
            if (_pendingRooms.TryGetValue(room.RoomId, out pending))
            {
                room.MatchGate.Release();
                released = true;
                await DisconnectAsync(session, rooms, ct);
                return;
            }
            if (room.NeedsReload)
            {
                room.State = await store.ReadAsync(room.RoomId, ct) ?? throw new IOException("Room snapshot unavailable.");
                room.NeedsReload = false;
            }
            if (room.State is not { Result: null } state)
            {
                room.Recovering = false;
                room.RecoveryStartedAt = null;
                _recoveryBaselines.TryRemove(room.RoomId, out _);
                room.DetachWorker();
                if (room.State is { } terminal) await PublishAsync(room, Snapshot(room, terminal), ct);
                await rooms.RemoveClientAsync(session, false);
                return;
            }
            long now = Now;
            long freezeAt = room.RecoveryStartedAt ?? now;
            var baseline = _recoveryBaselines.TryRemove(room.RoomId, out var recoveryBaseline) ? recoveryBaseline : state;
            var frozen = state with { FirstMilliseconds = baseline.Remaining(1, freezeAt), SecondMilliseconds = baseline.Remaining(2, freezeAt),
                TurnStartedAt = now, ObservedAt = now };
            pending = new PendingRecovery {
                Record = new OpenMatchRecord(room.RoomId, room.GameMode, room.Player1.Player!.UserId, room.Player2.Player!.UserId,
                    (int)state.Generation, (int)state.Revision, state),
                ReclaimedState = frozen, Room = room,
                DeadlineUtc = DateTimeOffset.UtcNow + (reconnectWindow ?? TimeSpan.FromSeconds(90)),
                FirstSession = ReferenceEquals(room.Player1, session) ? null : room.Player1,
                SecondSession = ReferenceEquals(room.Player2, session) ? null : room.Player2 };
            room.Recovering = true;
            room.DetachWorker();
            _pendingRooms[room.RoomId] = pending;
            _pendingUserRooms[pending.Record.FirstUserId] = room.RoomId;
            _pendingUserRooms[pending.Record.SecondUserId] = room.RoomId;
            session.CurrentRoomId = "";
            await PublishAsync(room, new GamePacket(PacketType.RECOVERING, RoomId: room.RoomId, GameMode: room.GameMode), ct);
        }
        finally { if (!released) room.MatchGate.Release(); }
    }

    public async Task LeaveAsync(GameRoom room, ClientSession session, RoomManager rooms, CancellationToken ct)
    {
        if (!ReferenceEquals(room.Player1, session) && !ReferenceEquals(room.Player2, session)) return;
        await DisconnectAsync(session, rooms, ct);
        if (!_pendingRooms.TryGetValue(room.RoomId, out var pending)) return;
        await pending.Gate.WaitAsync(ct);
        try {
            if (!ReferenceEquals(room.Player1, session) && !ReferenceEquals(room.Player2, session)) return;
            pending.LeaveRequested = true;
        }
        finally { pending.Gate.Release(); }
        await AdvancePendingAsync(pending, rooms, ct);
    }

    public async Task<bool> LeavePendingAsync(ClientSession session, RoomManager rooms, CancellationToken ct)
    {
        if (!_pendingRooms.TryGetValue(session.CurrentRoomId, out var pending)) return false;
        await pending.Gate.WaitAsync(ct);
        try {
            if (!ReferenceEquals(pending.FirstSession, session) && !ReferenceEquals(pending.SecondSession, session)) return false;
            pending.LeaveRequested = true;
        } finally { pending.Gate.Release(); }
        await AdvancePendingAsync(pending, rooms, ct);
        return true;
    }

    public async Task<bool> TryResumePlayerAsync(ClientSession session, RoomManager rooms, CancellationToken ct)
    {
        if (session.Player is null) return false;
        if (!_pendingUserRooms.ContainsKey(session.Player.UserId))
        {
            var active = rooms.ActiveRooms().FirstOrDefault(r => !r.Closing && r.State is { Result: null } &&
                (r.Player1.Player?.UserId == session.Player.UserId || r.Player2.Player?.UserId == session.Player.UserId));
            if (active is not null)
            {
                var previous = active.Player1.Player?.UserId == session.Player.UserId ? active.Player1 : active.Player2;
                await DisconnectAsync(previous, rooms, ct);
                previous.Tcp.Dispose();
            }
        }
        if (!_pendingUserRooms.TryGetValue(session.Player.UserId, out var id) ||
            !_pendingRooms.TryGetValue(id, out var pending)) return false;
        await pending.Gate.WaitAsync(ct);
        try
        {
            if (pending.Completed) return false;
            bool first = session.Player.UserId == pending.Record.FirstUserId;
            var previous = first ? pending.FirstSession : pending.SecondSession;
            if (previous is not null && !ReferenceEquals(previous, session)) { previous.CurrentRoomId = ""; previous.Tcp.Dispose(); }
            if (first) pending.FirstSession = session; else pending.SecondSession = session;
            session.CurrentRoomId = id;
            session.Elo = pending.Record.GameType == "CARO" ? session.Player.CaroElo : session.Player.ChessElo;
            var opponent = first ? pending.SecondSession : pending.FirstSession;
            await session.SendAsync(new GamePacket(PacketType.MATCH_FOUND, Sender: opponent?.Username ?? "Opponent",
                RoomId: id, GameMode: pending.Record.GameType, Payload: $"{(first ? "FIRST" : "SECOND")}|{opponent?.Elo ?? 1200}|RESUME",
                State: pending.ReclaimedState with { TurnStartedAt = Now, ObservedAt = Now }), ct);
            if (pending.FirstSession is null || pending.SecondSession is null)
                await session.SendAsync(new GamePacket(PacketType.RECOVERING, RoomId: id, GameMode: pending.Record.GameType), ct);
        }
        finally { pending.Gate.Release(); }
        await AdvancePendingAsync(pending, rooms, ct);
        return true;
    }

    private void RemovePending(PendingRecovery pending)
    {
        pending.Completed = true;
        workers.ReleaseRecoveryDemand(pending.Record.RoomId);
        _pendingRooms.TryRemove(pending.Record.RoomId, out _);
        _pendingUserRooms.TryRemove(pending.Record.FirstUserId, out _);
        _pendingUserRooms.TryRemove(pending.Record.SecondUserId, out _);
    }

    private async Task AdvancePendingAsync(PendingRecovery pending, RoomManager rooms, CancellationToken ct)
    {
        await pending.Gate.WaitAsync(ct);
        try
        {
            if (pending.Completed) return;
            string id = pending.Record.RoomId;
            if (pending.LeaveRequested || DateTimeOffset.UtcNow >= pending.DeadlineUtc)
            {
                var saved = await store.ReadAsync(id, ct) ?? throw new IOException("Missing recovery record.");
                if (saved.Generation != pending.ReclaimedState.Generation) { RemovePending(pending); return; }
                if (saved.Result is null)
                {
                    var aborted = saved with { Result = "ABORTED", Reason = "Reconnect expired or player left.", Revision = saved.Revision + 1, ObservedAt = Now };
                    if (!await store.UpdateAsync(id, saved, aborted, ct)) return;
                }
                RemovePending(pending);
                if (pending.Room is not null) rooms.RemoveClosedRoom(pending.Room);
                foreach (var player in new[] { pending.FirstSession, pending.SecondSession })
                    if (player is not null) {
                        player.CurrentRoomId = "";
                        await player.SendAsync(new GamePacket(PacketType.OPPONENT_LEFT, RoomId: id, GameMode: pending.Record.GameType), ct);
                    }
                return;
            }
            if (pending.FirstSession is null || pending.SecondSession is null) return;
            var room = pending.Room ??= new GameRoom { RoomId = id, GameMode = pending.Record.GameType,
                Player1 = pending.FirstSession, Player2 = pending.SecondSession, State = pending.ReclaimedState };
            await room.MatchGate.WaitAsync(ct);
            try
            {
                if (pending.FirstSession is null || pending.SecondSession is null) return;
                room.Player1 = pending.FirstSession;
                room.Player2 = pending.SecondSession;
                var saved = await store.ReadAsync(id, ct) ?? throw new IOException("Missing recovery record.");
                if (saved.Result is not null || saved.Generation != pending.ReclaimedState.Generation) return;
                if (saved.Revision != pending.ReclaimedState.Revision)
                    throw new IOException("Recovery snapshot was superseded.");
                room.State = saved;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                var worker = await workers.OpenRoomAsync(room, timeout.Token, isFailover: true);
                try
                {
                    var resumed = pending.ReclaimedState with { TurnStartedAt = Now, ObservedAt = Now };
                    if (!await store.UpdateAsync(id, saved, resumed, timeout.Token)) return;
                    if (!room.AttachWorker(worker)) return;
                    room.State = resumed;
                    worker = null!;
                }
                finally { worker?.Dispose(); }
                room.Recovering = false;
                room.RecoveryStartedAt = null;
                rooms.RegisterRecoveredRoom(room);
                room.MarkReady();
                RemovePending(pending);
                await PublishAsync(room, Snapshot(room, room.State!), ct);
            }
            finally { room.MatchGate.Release(); }
        }
        catch (Exception ex) when (IsTransient(ex) && !ct.IsCancellationRequested) { }
        finally { pending.Gate.Release(); }
    }

    public void StopRecovery()
    {
        foreach (var pending in _pendingRooms.Values)
        {
            workers.ReleaseRecoveryDemand(pending.Record.RoomId);
            pending.Room?.Close();
        }
    }

    public async Task RunAsync(RoomManager rooms, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await Task.WhenAll(_pendingRooms.Values.Select(p => AdvancePendingAsync(p, rooms, ct)));
                await Task.WhenAll(rooms.ActiveRooms().Where(room => !_pendingRooms.ContainsKey(room.RoomId)).Select(room => TickAsync(room, ct)));
            }
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
            if (room.Closing || room.Recovering && _pendingRooms.ContainsKey(room.RoomId) || room.State is not { Result: null } ||
                (!ReferenceEquals(sender, room.Player1) && !ReferenceEquals(sender, room.Player2))) return;
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
            // Read committed enrichment before publication; leave NeedsReload set on ambiguous failures.
            candidate = await store.ReadAsync(room.RoomId, token) ?? throw new IOException("Committed snapshot unavailable.");
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
            if (room.Closing || room.Worker is not { IsAvailable: true } worker || room.Recovering ||
                (!ReferenceEquals(sender, room.Player1) && !ReferenceEquals(sender, room.Player2))) return;
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
            _recoveryBaselines.TryRemove(room.RoomId, out _);
            await PublishAsync(room, Snapshot(room, saved), ct);
            room.DetachWorker();
            return null;
        }
        var baseline = _recoveryBaselines.GetOrAdd(room.RoomId, saved);
        long pausedMs = room.RecoveryStartedAt is long pausedAt ? Math.Max(0, Now - pausedAt) : 0;
        long compensatedStart = baseline.TurnStartedAt + pausedMs;
        if (room.Worker is not { IsAvailable: true } surviving)
        {
            room.DetachWorker();
            surviving = await workers.OpenRoomAsync(room, ct, isFailover: true);
            var fenced = saved with
            {
                Generation = saved.Generation + 1,
                TurnStartedAt = compensatedStart
            };
            try
            {
                room.NeedsReload = true;
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
            var compensated = saved with { TurnStartedAt = compensatedStart };
            room.NeedsReload = true;
            if (!await store.UpdateAsync(room.RoomId, saved, compensated, ct))
                throw new IOException("Could not compensate match clock.");
            room.State = compensated;
        }
        room.NeedsReload = false;
        room.Recovering = false;
        room.RecoveryStartedAt = null;
        _recoveryBaselines.TryRemove(room.RoomId, out _);
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
