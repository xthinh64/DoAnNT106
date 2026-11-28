using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

public sealed class MatchRecoveryTests
{
    [Theory]
    [InlineData("CHESS", "e2e4", "e7e5", "e7e4")]
    [InlineData("CARO", "7,7", "7,8", "7,7")]
    public async Task ConfirmedMovesSurviveWorkerCrashAndResultCommitsOnce(string mode, string first, string second, string illegal)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var ct = deadline.Token;
        using var cert = CreateCertificate();
        using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        workers.SetDraining(1, true);
        var auth = new FixtureAuth();
        var store = new FixtureStore();
        var context = SslStreamCertificateContext.Create(cert, null, offline: true);
        await using var gateway = new GameServer(IPAddress.Loopback, context, new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var aTcp = new TcpClient();
        using var bTcp = new TcpClient();
        using var a = await ConnectAsync(client, aTcp, "a", ct);
        using var b = await ConnectAsync(client, bTcp, "b", ct);
        await GameWire.WriteAsync(a, new GamePacket(PacketType.FIND_MATCH, GameMode: mode), ct);
        await GameWire.WriteAsync(b, new GamePacket(PacketType.FIND_MATCH, GameMode: mode), ct);
        var matchA = await ReadAsync(a, PacketType.MATCH_FOUND, ct);
        var matchB = await ReadAsync(b, PacketType.MATCH_FOUND, ct);
        Stream one = matchA.Payload.StartsWith("FIRST|", StringComparison.Ordinal) ? a : b;
        Stream two = ReferenceEquals(one, a) ? b : a;
        string roomId = matchA.RoomId;
        Assert.Equal(roomId, matchB.RoomId);
        Assert.Equal(1, matchA.State?.Generation);
        await MoveAsync(two, roomId, second, 0, ct);
        Assert.NotEmpty((await ReadAsync(two, PacketType.STATE, ct)).Payload);

        store.BlockNextCommit();
        await MoveAsync(one, roomId, first, 0, ct);
        var pending = ReadAsync(one, PacketType.STATE, ct);
        await store.Entered.Task.WaitAsync(ct);
        Assert.False(pending.IsCompleted);
        store.Release.TrySetResult();
        var confirmed = (await pending).State!;
        Assert.Equal(first, confirmed.Moves);
        Assert.Equal(1, confirmed.Revision);
        Assert.Equal(confirmed, (await ReadAsync(two, PacketType.STATE, ct)).State);

        workers.SetDraining(1, false);
        using var crashed = Process.GetProcessById(workers.GetSnapshot(0).ProcessId!.Value);
        crashed.Kill();
        await crashed.WaitForExitAsync(ct);
        await ReadAsync(one, PacketType.RECOVERING, ct);
        await ReadAsync(two, PacketType.RECOVERING, ct);
        var recovered = (await ReadAsync(one, PacketType.STATE, ct)).State!;
        Assert.Equal(recovered, (await ReadAsync(two, PacketType.STATE, ct)).State);
        Assert.Equal(2, recovered.Generation);
        Assert.Equal(confirmed.Revision, recovered.Revision);
        Assert.Equal(confirmed.Moves, recovered.Moves);
        Assert.True(recovered.TurnStartedAt >= confirmed.TurnStartedAt);
        Assert.False(await store.UpdateAsync(roomId, confirmed, confirmed with { Revision = 2, Result = "DRAW" }, ct));

        await MoveAsync(two, roomId, second, 0, ct);
        Assert.NotEmpty((await ReadAsync(two, PacketType.STATE, ct)).Payload);
        await MoveAsync(two, roomId, illegal, 1, ct);
        Assert.NotEmpty((await ReadAsync(two, PacketType.STATE, ct)).Payload);
        store.LoseNextCommitResponse = true;
        await MoveAsync(two, roomId, second, 1, ct);
        await ReadAsync(one, PacketType.RECOVERING, ct);
        await ReadAsync(two, PacketType.RECOVERING, ct);
        var updated = (await ReadAsync(one, PacketType.STATE, ct)).State!;
        Assert.Equal(updated, (await ReadAsync(two, PacketType.STATE, ct)).State);
        Assert.Equal($"{first} {second}", updated.Moves);
        Assert.Equal(2, updated.Revision);

        await GameWire.WriteAsync(two, new GamePacket(PacketType.SURRENDER, RoomId: roomId, ExpectedRevision: 2), ct);
        var result = (await ReadAsync(one, PacketType.STATE, ct)).State!;
        Assert.Equal("FIRST", result.Result);
        Assert.Equal(result, (await ReadAsync(two, PacketType.STATE, ct)).State);
        Assert.False(await store.UpdateAsync(roomId, result, result with { Revision = 4, Result = "SECOND" }, ct));
        Assert.Equal(1, store.Results);
    }

    [Fact]
    public async Task ClockFreezesDuringProlongedRecoveryAndReportsApplyMetrics()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await VerifyWorkerFailoverAsync(new FixtureStore(), deadline.Token);
    }

    [MySqlMatchStoreTests.DisposableDatabaseFact]
    public Task ConfirmedChessMovesSurviveWorkerFailoverWithMySql() =>
        MySqlMatchStoreTests.WithDatabaseAsync((store, _, ct) => VerifyWorkerFailoverAsync(store, ct));

    private static async Task VerifyWorkerFailoverAsync(IMatchStore store, CancellationToken ct)
    {
        using var cert = CreateCertificate();
        using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        workers.SetDraining(1, true);
        var auth = new FixtureAuth();
        var context = SslStreamCertificateContext.Create(cert, null, offline: true);
        await using var gateway = new GameServer(IPAddress.Loopback, context, new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var aTcp = new TcpClient(); using var bTcp = new TcpClient();
        using var a = await ConnectAsync(client, aTcp, "a", ct);
        using var b = await ConnectAsync(client, bTcp, "b", ct);
        await GameWire.WriteAsync(a, new GamePacket(PacketType.FIND_MATCH, GameMode: "CHESS"), ct);
        await GameWire.WriteAsync(b, new GamePacket(PacketType.FIND_MATCH, GameMode: "CHESS"), ct);
        var matchA = await ReadAsync(a, PacketType.MATCH_FOUND, ct);
        await ReadAsync(b, PacketType.MATCH_FOUND, ct);
        Stream one = matchA.Payload.StartsWith("FIRST|", StringComparison.Ordinal) ? a : b;
        Stream two = ReferenceEquals(one, a) ? b : a;
        string roomId = matchA.RoomId;

        await MoveAsync(one, roomId, "e2e4", 0, ct);
        var confirmed = (await ReadAsync(one, PacketType.STATE, ct)).State!;
        Assert.Equal(confirmed, (await ReadAsync(two, PacketType.STATE, ct)).State);
        var saved = await store.ReadAsync(roomId, ct);
        Assert.Equal("e2e4", saved?.Moves);
        Assert.Equal(confirmed.Revision, saved?.Revision);

        while (workers.GetSnapshot(0).AvgApplyMs <= 0) await Task.Delay(50, ct);
        // Keep both slots unavailable until the test explicitly opens the survivor.
        workers.SetDraining(0, true);

        using var crashed = Process.GetProcessById(workers.GetSnapshot(0).ProcessId!.Value);
        crashed.Kill();
        await crashed.WaitForExitAsync(ct);

        await ReadAsync(one, PacketType.RECOVERING, ct);
        await ReadAsync(two, PacketType.RECOVERING, ct);
        long recoveryNoticeAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await Task.Delay(1200, ct);
        workers.SetDraining(1, false);

        var recovered = (await ReadAsync(one, PacketType.STATE, ct)).State!;
        Assert.Equal(recovered, (await ReadAsync(two, PacketType.STATE, ct)).State);
        Assert.Equal(1, workers.GetSnapshot(1).ReservedRooms);
        Assert.Equal(2, recovered.Generation);
        Assert.Equal(confirmed.Revision, recovered.Revision);
        Assert.Equal(confirmed.Moves, recovered.Moves);
        Assert.Equal(confirmed.Turn, recovered.Turn);
        Assert.True(recovered.TurnStartedAt - confirmed.TurnStartedAt >= 900);
        // Time spent waiting for the heartbeat and detecting the crash is still chargeable.
        long remainingAtNotice = confirmed.Remaining(confirmed.Turn, recoveryNoticeAt);
        Assert.InRange(recovered.Remaining(recovered.Turn, recovered.ObservedAt),
            remainingAtNotice - 500, confirmed.SecondMilliseconds);

        await MoveAsync(two, roomId, "e7e5", recovered.Revision, ct);
        var continued = (await ReadAsync(one, PacketType.STATE, ct)).State!;
        Assert.Equal(continued, (await ReadAsync(two, PacketType.STATE, ct)).State);
        Assert.Equal(recovered.Generation, continued.Generation);
        Assert.Equal(recovered.Revision + 1, continued.Revision);
        Assert.Equal("e2e4 e7e5", continued.Moves);
        var persisted = await store.ReadAsync(roomId, ct);
        Assert.NotNull(persisted);
        // Snapshot delivery refreshes ObservedAt after the database write.
        Assert.Equal(continued, persisted with { ObservedAt = continued.ObservedAt });
    }

    [Theory]
    [InlineData("CHESS", "e2e4", "e7e5")]
    [InlineData("CARO", "7,7", "7,8")]
    public async Task GatewayStartupRestoresSavedMatchWhenBothAccountsReconnect(string mode, string firstMove, string nextMove)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var ct = deadline.Token;
        var store = new FixtureStore();
        long lastMoveAt = DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeMilliseconds();
        var initial = new MatchState(4, 0, "", 1, 600_000, 600_000, lastMoveAt, lastMoveAt);
        var saved = initial with { Revision = 1, Moves = firstMove, Turn = 2, FirstMilliseconds = 590_000 };
        await store.CreateAsync("unplayed", mode, 'c', 'd', initial, ct);
        await store.CreateAsync("ongoing", mode, 'a', 'b', saved, ct);

        using var cert = CreateCertificate();
        using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        var auth = new FixtureAuth();
        var context = SslStreamCertificateContext.Create(cert, null, offline: true);
        await using var gateway = new GameServer(IPAddress.Loopback, context, new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct);
        var unplayed = await store.ReadAsync("unplayed", ct);
        Assert.Equal("ABORTED", unplayed?.Result);
        Assert.Equal(1, unplayed?.Revision);
        Assert.False(await store.UpdateAsync("ongoing", saved, saved with { Result = "DRAW" }, ct));

        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var bTcp = new TcpClient();
        using var b = await ConnectAsync(client, bTcp, "b", ct);
        var waiting = await ReadAsync(b, PacketType.MATCH_FOUND, ct);
        Assert.Equal("ongoing", waiting.RoomId);
        Assert.Equal(mode, waiting.GameMode);
        Assert.StartsWith("SECOND|", waiting.Payload);
        Assert.Equal(firstMove, waiting.State?.Moves);
        Assert.Equal(saved.Generation + 1, waiting.State?.Generation);
        Assert.Equal("ongoing", (await ReadAsync(b, PacketType.RECOVERING, ct)).RoomId);

        using var aTcp = new TcpClient();
        using var a = await ConnectAsync(client, aTcp, "a", ct);
        var rejoined = await ReadAsync(a, PacketType.MATCH_FOUND, ct);
        Assert.Equal(waiting.RoomId, rejoined.RoomId);
        Assert.StartsWith("FIRST|", rejoined.Payload);
        var resumed = (await ReadAsync(b, PacketType.STATE, ct)).State!;
        Assert.Equal(resumed, (await ReadAsync(a, PacketType.STATE, ct)).State);
        Assert.Equal(saved.Revision, resumed.Revision);
        Assert.Equal(saved.Turn, resumed.Turn);
        Assert.Equal(saved.FirstMilliseconds, resumed.FirstMilliseconds);
        Assert.Equal(saved.SecondMilliseconds, resumed.SecondMilliseconds);
        Assert.Null(resumed.Result);

        await MoveAsync(b, waiting.RoomId, nextMove, resumed.Revision, ct);
        var confirmed = (await ReadAsync(a, PacketType.STATE, ct)).State!;
        Assert.Equal(confirmed, (await ReadAsync(b, PacketType.STATE, ct)).State);
        Assert.Equal($"{firstMove} {nextMove}", confirmed.Moves);
        Assert.Equal(saved.Revision + 1, confirmed.Revision);
        Assert.Equal(1, confirmed.Turn);
        Assert.Equal(confirmed.Moves, (await store.ReadAsync(waiting.RoomId, ct))?.Moves);
    }

    [Theory]
    [InlineData("CHESS", "e2e4", "e7e5")]
    [InlineData("CARO", "7,7", "7,8")]
    public async Task DroppedClientResumesConfirmedMatchWithFreshTicket(string mode, string firstMove, string secondMove)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var ct = deadline.Token;
        using var cert = CreateCertificate();
        using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        var auth = new FixtureAuth();
        var store = new FixtureStore();
        var context = SslStreamCertificateContext.Create(cert, null, offline: true);
        await using var gateway = new GameServer(IPAddress.Loopback, context, new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var aTcp = new TcpClient();
        using var bTcp = new TcpClient();
        using var a = await ConnectAsync(client, aTcp, "a", ct);
        using var b = await ConnectAsync(client, bTcp, "b", ct);
        var match = await MatchAndConfirmFirstMoveAsync(a, b, mode, firstMove, ct);

        bTcp.Dispose();
        Assert.Equal(match.Id, (await ReadAsync(a, PacketType.RECOVERING, ct)).RoomId);
        Assert.Null((await store.ReadAsync(match.Id, ct))?.Result);
        using var reconnectTcp = new TcpClient();
        using var reconnect = await ConnectAsync(client, reconnectTcp, "b", ct);
        Assert.Equal(match.Id, (await ReadAsync(reconnect, PacketType.MATCH_FOUND, ct)).RoomId);
        var resumed = (await ReadAsync(a, PacketType.STATE, ct)).State!;
        Assert.Equal(resumed, (await ReadAsync(reconnect, PacketType.STATE, ct)).State);
        Assert.Equal(firstMove, resumed.Moves);
        var second = ReferenceEquals(match.Second, b) ? reconnect : a;
        await MoveAsync(second, match.Id, secondMove, resumed.Revision, ct);
        var continued = (await ReadAsync(a, PacketType.STATE, ct)).State!;
        Assert.Equal(continued, (await ReadAsync(reconnect, PacketType.STATE, ct)).State);
        Assert.Equal($"{firstMove} {secondMove}", continued.Moves);
        Assert.Equal(2, continued.Revision);
    }

    [Fact]
    public async Task BothRoomsContinueOnSurvivorAtAdmissionLimit()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var ct = deadline.Token;
        using var cert = CreateCertificate();
        using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct, capacity: 2);
        var auth = new FixtureAuth();
        var store = new FixtureStore();
        var context = SslStreamCertificateContext.Create(cert, null, offline: true);
        await using var gateway = new GameServer(IPAddress.Loopback, context, new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var aTcp = new TcpClient();
        using var bTcp = new TcpClient();
        using var cTcp = new TcpClient();
        using var dTcp = new TcpClient();
        using var a = await ConnectAsync(client, aTcp, "a", ct);
        using var b = await ConnectAsync(client, bTcp, "b", ct);
        using var c = await ConnectAsync(client, cTcp, "c", ct);
        using var d = await ConnectAsync(client, dTcp, "d", ct);

        workers.SetDraining(1, true);
        var chess = await MatchAndConfirmFirstMoveAsync(a, b, "CHESS", "e2e4", ct);
        workers.SetDraining(1, false);
        var caro = await MatchAndConfirmFirstMoveAsync(c, d, "CARO", "7,7", ct);
        Assert.NotEqual(chess.Id, caro.Id);
        Assert.Equal(2, workers.AdmissionCapacity);
        Assert.Equal(1, workers.GetSnapshot(0).ReservedRooms);
        Assert.Equal(1, workers.GetSnapshot(1).ReservedRooms);

        // Prevent the supervisor's replacement process from taking the room back.
        workers.SetDraining(0, true);
        using var failed = Process.GetProcessById(workers.GetSnapshot(0).ProcessId!.Value);
        failed.Kill();
        await failed.WaitForExitAsync(ct);
        await ReadAsync(a, PacketType.RECOVERING, ct);
        await ReadAsync(b, PacketType.RECOVERING, ct);
        var recovered = (await ReadAsync(a, PacketType.STATE, ct)).State!;
        Assert.Equal(recovered, (await ReadAsync(b, PacketType.STATE, ct)).State);
        Assert.Equal(2, recovered.Generation);
        Assert.Equal(1, recovered.Revision);
        Assert.Equal("e2e4", recovered.Moves);
        Assert.Equal(2, workers.GetSnapshot(1).ReservedRooms);

        await MoveAsync(chess.Second, chess.Id, "e7e5", recovered.Revision, ct);
        var chessState = (await ReadAsync(a, PacketType.STATE, ct)).State!;
        Assert.Equal(chessState, (await ReadAsync(b, PacketType.STATE, ct)).State);
        Assert.Equal("e2e4 e7e5", chessState.Moves);
        Assert.Equal(2, chessState.Revision);

        await MoveAsync(caro.Second, caro.Id, "7,8", 1, ct);
        var caroState = (await ReadAsync(c, PacketType.STATE, ct)).State!;
        Assert.Equal(caroState, (await ReadAsync(d, PacketType.STATE, ct)).State);
        Assert.Equal("7,7 7,8", caroState.Moves);
        Assert.Equal(1, caroState.Generation);
        Assert.Equal(2, caroState.Revision);
    }

    [Fact]
    public async Task ExpiryRetriesLostCommitReplyAndFinalizesOnce()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = deadline.Token;
        var store = new FixtureStore();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await store.CreateAsync("expiry", "CHESS", 'a', 'b',
            new MatchState(1, 1, "e2e4", 2, 590000, 600000, now, now), ct);
        using var cert = CreateCertificate();
        using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        var auth = new FixtureAuth();
        await using var gateway = new GameServer(IPAddress.Loopback,
            SslStreamCertificateContext.Create(cert, null, offline: true), new GameSessions(auth), auth, workers, 0,
            store, TimeSpan.FromSeconds(2));
        gateway.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var tcp = new TcpClient();
        using var a = await ConnectAsync(client, tcp, "a", ct);
        await ReadAsync(a, PacketType.MATCH_FOUND, ct);
        await ReadAsync(a, PacketType.RECOVERING, ct);
        store.LoseNextCommitResponse = true;
        await ReadAsync(a, PacketType.OPPONENT_LEFT, ct);
        Assert.Equal("ABORTED", (await store.ReadAsync("expiry", ct))?.Result);
        Assert.Equal(1, store.Results);
        await Task.Delay(700, ct);
        Assert.Equal(1, store.Results);
        await GameWire.WriteAsync(a, new GamePacket(PacketType.FIND_MATCH, GameMode: "CHESS"), ct);
    }

    [Fact]
    public async Task WaitingSocketIsRemovedAndUnavailableWorkerResumeRetries()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var ct = deadline.Token;
        var store = new FixtureStore();
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await store.CreateAsync("waiting", "CHESS", 'a', 'b',
            new MatchState(1, 1, "e2e4", 2, 590000, 600000, now, now), ct);
        using var cert = CreateCertificate();
        using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        workers.SetDraining(0, true); workers.SetDraining(1, true);
        var auth = new FixtureAuth();
        await using var gateway = new GameServer(IPAddress.Loopback,
            SslStreamCertificateContext.Create(cert, null, offline: true), new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var oldTcp = new TcpClient();
        using var old = await ConnectAsync(client, oldTcp, "a", ct);
        await ReadAsync(old, PacketType.MATCH_FOUND, ct); await ReadAsync(old, PacketType.RECOVERING, ct);
        oldTcp.Dispose();
        while (gateway.ClientCount != 0) await Task.Delay(10, ct);
        using var bTcp = new TcpClient(); using var b = await ConnectAsync(client, bTcp, "b", ct);
        await ReadAsync(b, PacketType.MATCH_FOUND, ct); await ReadAsync(b, PacketType.RECOVERING, ct);
        using var aTcp = new TcpClient(); using var a = await ConnectAsync(client, aTcp, "a", ct);
        await ReadAsync(a, PacketType.MATCH_FOUND, ct);
        await Task.Delay(700, ct);
        workers.SetDraining(1, false);
        var state = (await ReadAsync(a, PacketType.STATE, ct)).State!;
        Assert.Equal(state, (await ReadAsync(b, PacketType.STATE, ct)).State);
        Assert.Equal("e2e4", state.Moves);
        await MoveAsync(b, "waiting", "e7e5", state.Revision, ct);
        Assert.Equal("e2e4 e7e5", (await ReadAsync(a, PacketType.STATE, ct)).State?.Moves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupDoesNotListenBeforeRecoveryAndFailureRollsBack(bool fail)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = deadline.Token;
        using var cert = CreateCertificate();
        await using var workers = await StartWorkersAsync(ct);
        var auth = new FixtureAuth();
        var store = new FixtureStore { DelayStartup = true, FailStartup = fail };
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        await using var gateway = new GameServer(IPAddress.Loopback,
            SslStreamCertificateContext.Create(cert, null, offline: true), new GameSessions(auth), auth, workers, port, store);
        var startup = Task.Run(() => gateway.Start(ct), ct);
        await store.StartupEntered.Task.WaitAsync(ct);
        using (var tcp = new TcpClient())
            await Assert.ThrowsAsync<SocketException>(async () => await tcp.ConnectAsync(IPAddress.Loopback, port, ct));
        store.StartupRelease.TrySetResult();
        if (fail)
        {
            await Assert.ThrowsAsync<IOException>(() => startup);
            using var tcp = new TcpClient();
            await Assert.ThrowsAsync<SocketException>(async () => await tcp.ConnectAsync(IPAddress.Loopback, port, ct));
        }
        else
        {
            await startup;
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, port, ct);
        }
    }

    [Fact]
    public async Task FreshTicketReplacesLiveSeatWithoutOldSocketAbortingRoom()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var ct = deadline.Token;
        using var cert = CreateCertificate();
        using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        var auth = new FixtureAuth(); var store = new FixtureStore();
        await using var gateway = new GameServer(IPAddress.Loopback,
            SslStreamCertificateContext.Create(cert, null, offline: true), new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var aTcp = new TcpClient(); using var bTcp = new TcpClient();
        using var a = await ConnectAsync(client, aTcp, "a", ct);
        using var b = await ConnectAsync(client, bTcp, "b", ct);
        var match = await MatchAndConfirmFirstMoveAsync(a, b, "CHESS", "e2e4", ct);
        using var replacementTcp = new TcpClient();
        using var replacement = await ConnectAsync(client, replacementTcp, "b", ct);
        Assert.Equal(match.Id, (await ReadAsync(replacement, PacketType.MATCH_FOUND, ct)).RoomId);
        await ReadAsync(a, PacketType.RECOVERING, ct);
        var resumed = (await ReadAsync(a, PacketType.STATE, ct)).State!;
        Assert.Equal(resumed, (await ReadAsync(replacement, PacketType.STATE, ct)).State);
        bTcp.Dispose();
        await Task.Delay(100, ct);
        using var cTcp = new TcpClient(); using var c = await ConnectAsync(client, cTcp, "c", ct);
        await GameWire.WriteAsync(c, new GamePacket(PacketType.MOVE, RoomId: match.Id, Sender: "b", Payload: "e7e5", ExpectedRevision: 1), ct);
        await GameWire.WriteAsync(c, new GamePacket(PacketType.LEAVE_ROOM, RoomId: match.Id, Sender: "b"), ct);
        var second = ReferenceEquals(match.Second, b) ? replacement : a;
        await MoveAsync(second, match.Id, "e7e5", 1, ct);
        Assert.Equal("e2e4 e7e5", (await ReadAsync(a, PacketType.STATE, ct)).State?.Moves);
        Assert.Null((await store.ReadAsync(match.Id, ct))?.Result);
    }

    [Fact]
    public async Task ExplicitLeaveRetriesStorageFailureBeforeNotifyingOpponent()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var ct = deadline.Token;
        using var cert = CreateCertificate(); using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        var auth = new FixtureAuth(); var store = new FixtureStore();
        await using var gateway = new GameServer(IPAddress.Loopback, SslStreamCertificateContext.Create(cert, null, offline: true),
            new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct); var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var aTcp = new TcpClient(); using var bTcp = new TcpClient();
        using var a = await ConnectAsync(client, aTcp, "a", ct); using var b = await ConnectAsync(client, bTcp, "b", ct);
        var match = await MatchAndConfirmFirstMoveAsync(a, b, "CHESS", "e2e4", ct);
        store.FailNextUpdate = true;
        await GameWire.WriteAsync(b, new GamePacket(PacketType.LEAVE_ROOM, RoomId: match.Id), ct);
        await ReadAsync(a, PacketType.RECOVERING, ct);
        await ReadAsync(a, PacketType.OPPONENT_LEFT, ct);
        Assert.Equal("ABORTED", (await store.ReadAsync(match.Id, ct))?.Result);
        Assert.Equal(1, store.Results);
    }

    [Fact]
    public async Task GracefulShutdownPreservesConfirmedOpenMatch()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var ct = deadline.Token;
        using var cert = CreateCertificate(); using var trust = new X509Certificate2(cert.RawData);
        await using var workers = await StartWorkersAsync(ct);
        var auth = new FixtureAuth(); var store = new FixtureStore();
        var gateway = new GameServer(IPAddress.Loopback, SslStreamCertificateContext.Create(cert, null, offline: true),
            new GameSessions(auth), auth, workers, 0, store);
        gateway.Start(ct); var client = new GatewayClient($"127.0.0.1:{gateway.Port}", trust);
        using var aTcp = new TcpClient(); using var bTcp = new TcpClient();
        using var a = await ConnectAsync(client, aTcp, "a", ct); using var b = await ConnectAsync(client, bTcp, "b", ct);
        var match = await MatchAndConfirmFirstMoveAsync(a, b, "CHESS", "e2e4", ct);
        await gateway.DisposeAsync();
        var saved = await store.ReadAsync(match.Id, ct);
        Assert.Equal("e2e4", saved?.Moves); Assert.Null(saved?.Result);
        Assert.Single(await store.ReadOpenMatchesAsync(ct)); Assert.Equal(0, store.Results);
    }

    private static async Task<(string Id, Stream Second)> MatchAndConfirmFirstMoveAsync(
        Stream a, Stream b, string mode, string move, CancellationToken ct)
    {
        await GameWire.WriteAsync(a, new GamePacket(PacketType.FIND_MATCH, GameMode: mode), ct);
        await GameWire.WriteAsync(b, new GamePacket(PacketType.FIND_MATCH, GameMode: mode), ct);
        var match = await ReadAsync(a, PacketType.MATCH_FOUND, ct);
        Assert.Equal(match.RoomId, (await ReadAsync(b, PacketType.MATCH_FOUND, ct)).RoomId);
        bool aFirst = match.Payload.StartsWith("FIRST|", StringComparison.Ordinal);
        await MoveAsync(aFirst ? a : b, match.RoomId, move, 0, ct);
        var confirmed = (await ReadAsync(a, PacketType.STATE, ct)).State!;
        Assert.Equal(confirmed, (await ReadAsync(b, PacketType.STATE, ct)).State);
        Assert.Equal(move, confirmed.Moves);
        Assert.Equal(1, confirmed.Revision);
        return (match.RoomId, aFirst ? b : a);
    }

    [MySqlMatchStoreTests.DisposableDatabaseFact]
    public Task MySqlTerminalSettlementIsPublishedAndSameSessionQueuesLatestRating() =>
        MySqlMatchStoreTests.WithDatabaseAsync(async (store, db, ct) =>
        {
            using var cert = CreateCertificate(); using var trust = new X509Certificate2(cert.RawData);
            await using var workers = await StartWorkersAsync(ct);
            var auth = new RatingAuth(db.ConnectionString);
            await using var gateway = new GameServer(IPAddress.Loopback,
                SslStreamCertificateContext.Create(cert,null,offline:true),new GameSessions(auth),auth,workers,0,store);
            await gateway.StartAsync(ct);
            var client = new GatewayClient($"127.0.0.1:{gateway.Port}",trust);
            using var at = new TcpClient(); using var bt = new TcpClient();
            using var a = await ConnectAsync(client,at,"a",ct); using var b = await ConnectAsync(client,bt,"b",ct);
            await GameWire.WriteAsync(a,new GamePacket(PacketType.FIND_MATCH),ct);
            await GameWire.WriteAsync(b,new GamePacket(PacketType.FIND_MATCH),ct);
            var ma = await ReadAsync(a,PacketType.MATCH_FOUND,ct); await ReadAsync(b,PacketType.MATCH_FOUND,ct);
            bool aFirst = ma.Payload.StartsWith("FIRST|");
            await GameWire.WriteAsync(b,new GamePacket(PacketType.SURRENDER,RoomId:ma.RoomId,ExpectedRevision:0),ct);
            var terminal = (await ReadAsync(a,PacketType.STATE,ct)).State!;
            Assert.Equal(terminal,(await ReadAsync(b,PacketType.STATE,ct)).State);
            Assert.Equal(aFirst ? "FIRST" : "SECOND",terminal.Result);
            Assert.NotNull(terminal.Rating);
            Assert.Equal(1216,terminal.Rating!.Apply(new PlayerProfile(1,"a","",1200,1200)).ChessElo);
            var stored = (await store.ReadAsync(ma.RoomId,ct))!;
            Assert.Equal(terminal with { ObservedAt = stored.ObservedAt },stored);
            await GameWire.WriteAsync(a,new GamePacket(PacketType.LEAVE_ROOM,RoomId:ma.RoomId),ct);
            await GameWire.WriteAsync(b,new GamePacket(PacketType.LEAVE_ROOM,RoomId:ma.RoomId),ct);
            await GameWire.WriteAsync(a,new GamePacket(PacketType.FIND_MATCH),ct);
            await GameWire.WriteAsync(b,new GamePacket(PacketType.FIND_MATCH),ct);
            async Task<GamePacket> NextMatch(Stream stream)
            {
                while (true)
                {
                    var packet = (await GameWire.ReadAsync<GamePacket>(stream,ct))!;
                    if (packet.Type == PacketType.MATCH_FOUND) return packet;
                    Assert.Equal(PacketType.STATE,packet.Type);
                    Assert.Equal(terminal.Rating,packet.State!.Rating);
                }
            }
            var nextA = await NextMatch(a); var nextB = await NextMatch(b);
            Assert.Contains("|1184",nextA.Payload); Assert.Contains("|1216",nextB.Payload);
        });

    private sealed class RatingAuth(string connectionString) : IAuthService, IPlayerProfileReader
    {
        readonly OnlineChessGame.Auth.MySqlAuthService reader = new(connectionString);
        public Task<PlayerProfile?> ReadProfileAsync(int id,CancellationToken ct) => reader.ReadProfileAsync(id,ct);
        public async Task<AuthResponse> HandleAsync(AuthRequest request,CancellationToken ct) =>
            new(true,"ok","",await ReadProfileAsync(request.Username == "a" ? 1 : 2,ct));
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Match Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        using var generated = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        return new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.UserKeySet);
    }

    private static async Task<GameWorkerPool> StartWorkersAsync(CancellationToken ct, int capacity = GameWorkerPool.DefaultRoomCapacity)
    {
        using var listener1 = new TcpListener(IPAddress.Loopback, 0);
        using var listener2 = new TcpListener(IPAddress.Loopback, 0);
        listener1.Start();
        listener2.Start();
        int port1 = ((IPEndPoint)listener1.LocalEndpoint).Port;
        int port2 = ((IPEndPoint)listener2.LocalEndpoint).Port;
        listener1.Stop();
        listener2.Stop();
        var workers = new GameWorkerPool(capacity);
        try
        {
            workers.Start(port1, port2, typeof(GameServer).Assembly.Location);
            await workers.WaitUntilReadyAsync(ct);
            return workers;
        }
        catch
        {
            await workers.DisposeAsync();
            throw;
        }
    }

    private static Task MoveAsync(Stream stream, string room, string move, long revision, CancellationToken ct) =>
        GameWire.WriteAsync(stream, new GamePacket(PacketType.MOVE, RoomId: room, Payload: move, ExpectedRevision: revision), ct);

    private static async Task<GamePacket> ReadAsync(Stream stream, PacketType type, CancellationToken ct)
    {
        var packet = await GameWire.ReadAsync<GamePacket>(stream, ct);
        Assert.NotNull(packet);
        Assert.Equal(type, packet.Type);
        return packet;
    }

    private static async Task<SslStream> ConnectAsync(GatewayClient client, TcpClient tcp, string user, CancellationToken ct)
    {
        var login = await client.SendAsync(new AuthRequest("signin", user, "test-password"), ct);
        var ticket = await client.RequestTicketAsync(login.SessionToken!, ct);
        var tls = await client.ConnectAsync(tcp, ct);
        try
        {
            await GameWire.WriteAsync(tls, new GatewayRequest(GatewayRequest.CurrentVersion, "game", Token: ticket.Token), ct);
            await ReadAsync(tls, PacketType.AUTHENTICATED, ct);
            return tls;
        }
        catch { tls.Dispose(); throw; }
    }

    private sealed class FixtureAuth : IAuthService
    {
        public Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct) =>
            Task.FromResult(new AuthResponse(true, "ok", "", new PlayerProfile(request.Username[0], request.Username, "", 1200, 1200)));
    }

    private sealed class FixtureStore : IMatchStore
    {
        private readonly Dictionary<string, OpenMatchRecord> matches = new();
        public int Results { get; private set; }
        private bool block;
        public bool DelayStartup;
        public bool FailStartup;
        public TaskCompletionSource StartupEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StartupRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool LoseNextCommitResponse;
        public bool FailNextUpdate;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void BlockNextCommit() => block = true;

        public Task CreateAsync(string roomId, string mode, int firstId, int secondId, MatchState initial, CancellationToken ct)
        {
            lock (matches)
                matches.Add(roomId, new OpenMatchRecord(roomId, mode, firstId, secondId,
                    checked((int)initial.Generation), checked((int)initial.Revision), initial));
            return Task.CompletedTask;
        }

        public Task<MatchState?> ReadAsync(string roomId, CancellationToken ct)
        {
            lock (matches) return Task.FromResult(matches.GetValueOrDefault(roomId)?.State);
        }

        public async Task<IReadOnlyList<OpenMatchRecord>> ReadOpenMatchesAsync(CancellationToken ct)
        {
            if (DelayStartup) { StartupEntered.TrySetResult(); await StartupRelease.Task.WaitAsync(ct); }
            if (FailStartup) throw new IOException("Startup store unavailable.");
            lock (matches)
                return matches.Values.Where(match => match.State.Result is null).ToArray();
        }

        public Task<bool> TryReclaimAsync(string roomId, int expectedGeneration, MatchState next, CancellationToken ct) =>
            Task.FromResult(Write(roomId, expectedGeneration, null, next));

        public Task<bool> AbortMatchAsync(string roomId, int expectedGeneration, MatchState next, CancellationToken ct) =>
            Task.FromResult(Write(roomId, expectedGeneration, null, next));

        private bool Write(string roomId, long generation, long? revision, MatchState next)
        {
            lock (matches)
            {
                if (!matches.TryGetValue(roomId, out var match) || match.State.Result is not null ||
                    match.Generation != generation || (revision.HasValue && match.Revision != revision))
                    return false;

                matches[roomId] = match with
                {
                    Generation = checked((int)next.Generation),
                    Revision = checked((int)next.Revision),
                    State = next
                };
                if (next.Result is not null) Results++;
                return true;
            }
        }

        public async Task<bool> UpdateAsync(string roomId, MatchState previous, MatchState next, CancellationToken ct)
        {
            if (FailNextUpdate) { FailNextUpdate = false; throw new IOException("Store unavailable."); }
            if (block)
            {
                block = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
            }
            bool written = Write(roomId, previous.Generation, previous.Revision, next);
            if (written && LoseNextCommitResponse)
            {
                LoseNextCommitResponse = false;
                throw new IOException("Simulated lost commit reply.");
            }
            return written;
        }
    }
}
