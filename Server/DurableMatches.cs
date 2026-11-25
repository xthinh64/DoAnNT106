using System.Net.Sockets;
using System.Text.Json;
using MySqlConnector;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

internal sealed class DurableMatches(IMatchStore store, GameWorkerPool workers)
{
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task StartAsync(GameRoom room, CancellationToken ct)
    {
        long now = Now;
        var state = new MatchState(1, 0, "", 1, 600_000, 600_000, now, now);
        await store.CreateAsync(room.RoomId, room.GameMode, room.Player1.Player!.UserId, room.Player2.Player!.UserId, state, ct);
        room.State = state;
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
