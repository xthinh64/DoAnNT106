using System.Net;
using System.Net.Sockets;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

public sealed class RecoveryOverlapTests
{
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static ClientSession Session(int id) => new(new TcpClient(), Stream.Null)
    { Player = new PlayerProfile(id, $"p{id}", "", 1200, 1200), CurrentRoomId = "overlap" };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisconnectReconcilesLostMoveCommitBeforeFreezing(bool terminal)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        await using var workers = await Workers(ct);
        var store = new Store(); var recovery = new DurableMatches(store, workers);
        var rooms = new RoomManager();
        var room = new GameRoom { RoomId = "overlap", GameMode = "CHESS", Player1 = Session(1), Player2 = Session(2) };
        rooms.RegisterRecoveredRoom(room);
        await recovery.StartAsync(room, ct);
        room.AttachWorker(await workers.OpenRoomAsync(room, ct));
        store.LoseReply = true;
        await recovery.HandleAsync(room, room.Player1, terminal
            ? new GamePacket(PacketType.SURRENDER, ExpectedRevision: 0)
            : new GamePacket(PacketType.MOVE, Payload: "e2e4", ExpectedRevision: 0), ct);
        Assert.True(room.NeedsReload);
        Assert.Equal(0, room.State!.Revision);
        var committed = store.Saved!;
        await recovery.DisconnectAsync(room.Player1, rooms, ct);
        Assert.Equal(committed.Revision, room.State!.Revision);
        Assert.Equal(committed.Result, room.State.Result);
        if (terminal) { Assert.False(room.Recovering); return; }
        var replacement = Session(1);
        Assert.True(await recovery.TryResumePlayerAsync(replacement, rooms, ct));
        Assert.False(room.Recovering);
        Assert.Equal("e2e4", room.State.Moves);
        Assert.Equal(1, room.State.Revision);
        room.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostClockCompensationReplyDoesNotCreditPauseTwice(bool survivingWorker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var ct = timeout.Token;
        await using var workers = await Workers(ct);
        var store = new Store(); var recovery = new DurableMatches(store, workers);
        var room = new GameRoom { RoomId = "overlap", GameMode = "CHESS", Player1 = Session(1), Player2 = Session(2) };
        await recovery.StartAsync(room, ct);
        if (survivingWorker) room.AttachWorker(await workers.OpenRoomAsync(room, ct));
        long freezeAt = Now;
        room.Recovering = true; room.RecoveryStartedAt = freezeAt;
        room.State = store.Saved = room.State! with { TurnStartedAt = freezeAt - 1000 };
        long expected = room.State.Remaining(1, freezeAt);
        await Task.Delay(600, ct);
        store.LoseReply = true;
        await recovery.HandleAsync(room, room.Player1, new GamePacket(PacketType.CLOCK_EXPIRED), ct);
        Assert.True(room.Recovering);
        await Task.Delay(600, ct);
        await recovery.HandleAsync(room, room.Player1, new GamePacket(PacketType.CLOCK_EXPIRED), ct);
        Assert.False(room.Recovering);
        Assert.InRange(room.State!.Remaining(1, Now), expected - 250, expected + 100);
        room.Close();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisconnectAfterAmbiguousCompensationKeepsOriginalFreeze(bool survivingWorker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var ct = timeout.Token;
        await using var workers = await Workers(ct);
        var store = new Store(); var recovery = new DurableMatches(store, workers); var rooms = new RoomManager();
        var room = new GameRoom { RoomId = "overlap", GameMode = "CHESS", Player1 = Session(1), Player2 = Session(2) };
        rooms.RegisterRecoveredRoom(room); await recovery.StartAsync(room, ct);
        if (survivingWorker) room.AttachWorker(await workers.OpenRoomAsync(room, ct));
        long freezeAt = Now;
        room.State = store.Saved = room.State! with { TurnStartedAt = freezeAt - 1000 };
        room.Recovering = true; room.RecoveryStartedAt = freezeAt;
        long expected = room.State.Remaining(1, freezeAt);
        await Task.Delay(600, ct);
        store.LoseReply = true;
        await recovery.HandleAsync(room, room.Player1, new GamePacket(PacketType.CLOCK_EXPIRED), ct);
        await Task.Delay(600, ct);
        await recovery.DisconnectAsync(room.Player1, rooms, ct);
        Assert.True(await recovery.TryResumePlayerAsync(Session(1), rooms, ct));
        Assert.False(room.Recovering);
        Assert.InRange(room.State!.Remaining(1, Now), expected - 250, expected + 100);
        room.Close();
    }

    [Fact]
    public async Task DisconnectDuringWorkerRecoveryDoesNotChargeFrozenInterval()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var ct = timeout.Token;
        await using var workers = await Workers(ct);
        var store = new Store(); var recovery = new DurableMatches(store, workers); var rooms = new RoomManager();
        var room = new GameRoom { RoomId = "overlap", GameMode = "CHESS", Player1 = Session(1), Player2 = Session(2) };
        rooms.RegisterRecoveredRoom(room); await recovery.StartAsync(room, ct);
        long freezeAt = Now;
        room.State = store.Saved = room.State! with { TurnStartedAt = freezeAt - 1000 };
        room.Recovering = true; room.RecoveryStartedAt = freezeAt;
        long expected = room.State.Remaining(1, freezeAt);
        await Task.Delay(700, ct);
        await recovery.DisconnectAsync(room.Player1, rooms, ct);
        Assert.True(await recovery.TryResumePlayerAsync(Session(1), rooms, ct));
        Assert.False(room.Recovering);
        Assert.InRange(room.State!.Remaining(1, Now), expected - 250, expected + 100);
        room.Close();
    }

    private static async Task<GameWorkerPool> Workers(CancellationToken ct)
    {
        using var a = new TcpListener(IPAddress.Loopback, 0); using var b = new TcpListener(IPAddress.Loopback, 0);
        a.Start(); b.Start(); int p = ((IPEndPoint)a.LocalEndpoint).Port, q = ((IPEndPoint)b.LocalEndpoint).Port;
        a.Stop(); b.Stop(); var pool = new GameWorkerPool();
        try { pool.Start(p, q, typeof(GameServer).Assembly.Location); await pool.WaitUntilReadyAsync(ct); return pool; }
        catch { await pool.DisposeAsync(); throw; }
    }

    private sealed class Store : IMatchStore
    {
        public MatchState? Saved;
        public bool LoseReply;
        public Task CreateAsync(string id, string mode, int first, int second, MatchState state, CancellationToken ct)
        { Saved = state; return Task.CompletedTask; }
        public Task<MatchState?> ReadAsync(string id, CancellationToken ct) => Task.FromResult(Saved);
        public Task<IReadOnlyList<OpenMatchRecord>> ReadOpenMatchesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OpenMatchRecord>>(Array.Empty<OpenMatchRecord>());
        public Task<bool> TryReclaimAsync(string id, int generation, MatchState next, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> AbortMatchAsync(string id, int generation, MatchState next, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(string id, MatchState previous, MatchState next, CancellationToken ct)
        {
            if (Saved!.Generation != previous.Generation || Saved.Revision != previous.Revision || Saved.Result is not null) return Task.FromResult(false);
            Saved = next;
            if (LoseReply) { LoseReply = false; throw new IOException("Committed, reply lost"); }
            return Task.FromResult(true);
        }
    }
}
