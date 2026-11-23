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
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Match Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        using var generated = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var cert = new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.UserKeySet);
        using var trust = new X509Certificate2(cert.RawData);
        using var listener1 = new TcpListener(IPAddress.Loopback, 0);
        using var listener2 = new TcpListener(IPAddress.Loopback, 0);
        listener1.Start();
        listener2.Start();
        int port1 = ((IPEndPoint)listener1.LocalEndpoint).Port;
        int port2 = ((IPEndPoint)listener2.LocalEndpoint).Port;
        listener1.Stop();
        listener2.Stop();
        await using var workers = new GameWorkerPool();
        workers.Start(port1, port2, typeof(GameServer).Assembly.Location);
        await workers.WaitUntilReadyAsync(ct);
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
        await Task.Delay(50, ct);
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
        Assert.Equal(confirmed.TurnStartedAt, recovered.TurnStartedAt);
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
        private MatchState? state;
        public int Results { get; private set; }
        private bool block;
        public bool LoseNextCommitResponse;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void BlockNextCommit() => block = true;

        public Task CreateAsync(string roomId, string mode, int firstId, int secondId, MatchState initial, CancellationToken ct)
        {
            lock (this) state = initial;
            return Task.CompletedTask;
        }

        public Task<MatchState?> ReadAsync(string roomId, CancellationToken ct)
        {
            lock (this) return Task.FromResult(state);
        }

        public async Task<bool> UpdateAsync(string roomId, MatchState previous, MatchState next, CancellationToken ct)
        {
            if (block)
            {
                block = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
            }
            lock (this)
            {
                if (state is null || state.Result is not null ||
                    state.Generation != previous.Generation || state.Revision != previous.Revision) return false;
                state = next;
                if (next.Result is not null) Results++;
                if (LoseNextCommitResponse) { LoseNextCommitResponse = false; throw new IOException("Simulated lost commit reply."); }
                return true;
            }
        }
    }
}
