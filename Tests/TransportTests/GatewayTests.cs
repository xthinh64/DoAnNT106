using System.Net;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

public sealed class GatewayTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LanExportFromSelfSignedPfxRequiresSanAndSupportsGatewayTrust(bool includeSan)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Chess Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        if (includeSan)
        {
            var san = new SubjectAlternativeNameBuilder();
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
        }
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, generated.Export(X509ContentType.Pfx, "test"));
            using var gatewayCertificate = new GatewayCertificate(path, "test");
            if (!includeSan)
            {
                Assert.Throws<InvalidOperationException>(gatewayCertificate.ExportLanTrustCertificate);
                return;
            }
            using var exported = new X509Certificate2(gatewayCertificate.ExportLanTrustCertificate());
            Assert.False(exported.HasPrivateKey);
            Assert.Equal(generated.Thumbprint, exported.Thumbprint);

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var certContext = gatewayCertificate.Context;
            var serverHandshake = Task.Run(async () =>
            {
                using var peer = await listener.AcceptTcpClientAsync(deadline.Token);
                using var tls = new SslStream(peer.GetStream());
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificateContext = certContext,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, deadline.Token);
            });
            using var tcp = new TcpClient();
            var clientHandshake = new GatewayClient($"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}", exported)
                .ConnectAsync(tcp, deadline.Token);
            try
            {
                await serverHandshake;
                using var connection = await clientHandshake;
                Assert.True(connection.IsAuthenticated);
            }
            finally
            {
                if (clientHandshake.IsCompletedSuccessfully) (await clientHandshake).Dispose();
                else if (clientHandshake.IsFaulted) _ = clientHandshake.Exception;
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task TrustedGatewayDistributesTwoMatchesAndRejectsInvalidTrustAndReusedTickets()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = deadline.Token;
        using var rootKey = RSA.Create(2048);
        var caRequest = new CertificateRequest("CN=Test Gateway CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=127.0.0.1", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        leafRequest.CertificateExtensions.Add(san.Build());
        using var issued = leafRequest.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1),
            RandomNumberGenerator.GetBytes(16));
        using var withKey = issued.CopyWithPrivateKey(leafKey);
        using var leaf = new X509Certificate2(withKey.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.UserKeySet);

        await using var workers = new GameWorkerPool(capacity: 2, admissionCapacity: 4);
        var port1 = new TcpListener(IPAddress.Loopback, 0);
        var port2 = new TcpListener(IPAddress.Loopback, 0);
        port1.Start(); port2.Start();
        int first = ((IPEndPoint)port1.LocalEndpoint).Port;
        int second = ((IPEndPoint)port2.LocalEndpoint).Port;
        port1.Stop(); port2.Stop();
        workers.Start(first, second, typeof(GameServer).Assembly.Location);
        await workers.WaitUntilReadyAsync(ct);

        var auth = new CountingAuth();
        var context = SslStreamCertificateContext.Create(leaf, new X509Certificate2Collection(root), offline: true);
        await using var server = new GameServer(IPAddress.Loopback, context, new GameSessions(auth), auth, workers, 0);
        server.Start(ct);
        var login = new AuthRequest("signin", "alice", "test-password");
        await Assert.ThrowsAsync<AuthenticationException>(() => new GatewayClient($"127.0.0.1:{server.Port}").SendAsync(login, ct));
        await Assert.ThrowsAsync<AuthenticationException>(() => new GatewayClient($"localhost:{server.Port}", root).SendAsync(login, ct));
        Assert.Equal(0, auth.RequestCount);

        var client = new GatewayClient($"127.0.0.1:{server.Port}", root);
        var firstLogin = await client.SendAsync(login, ct);
        Assert.NotNull(firstLogin.SessionToken);
        var ticket = await client.RequestTicketAsync(firstLogin.SessionToken, ct);
        Assert.NotNull(ticket.Token);
        using var aTcp = new TcpClient();
        using var a = await OpenAsync(client, aTcp, ticket.Token, PacketType.AUTHENTICATED, ct);
        using var replay = new TcpClient();
        using var denied = await OpenAsync(client, replay, ticket.Token, PacketType.ERROR, ct);
        Assert.NotEqual(ticket.Token, (await client.RequestTicketAsync(firstLogin.SessionToken, ct)).Token);

        using var bTcp = new TcpClient();
        using var cTcp = new TcpClient();
        using var dTcp = new TcpClient();
        using var b = await SignInAndConnectAsync(client, bTcp, "bob", ct);
        using var c = await SignInAndConnectAsync(client, cTcp, "carol", ct);
        using var d = await SignInAndConnectAsync(client, dTcp, "dan", ct);

        workers.SetDraining(1, true);
        string chess = await MatchAsync(a, b, "CHESS", ct);
        workers.SetDraining(1, false);
        workers.SetDraining(0, true);
        Assert.Equal(WorkerStatus.Draining, workers.GetSnapshot(0).Status);
        string caro = await MatchAsync(c, d, "CARO", ct);
        Assert.NotEqual(chess, caro);
        Assert.Equal(1, workers.GetSnapshot(0).ReservedRooms);
        Assert.Equal(1, workers.GetSnapshot(1).ReservedRooms);
        await WaitForAsync(() => workers.GetSnapshot(0).Sessions == 2 && workers.GetSnapshot(1).Sessions == 2, ct);
        await GameWire.WriteAsync(a, new GamePacket(PacketType.MOVE, Payload: "e2e4"), ct);
        var move = await GameWire.ReadAsync<GamePacket>(b, ct);
        Assert.Equal(("alice", chess, "e2e4"), (move?.Sender, move?.RoomId, move?.Payload));
        await GameWire.WriteAsync(c, new GamePacket(PacketType.CHAT, Payload: "hi"), ct);
        var message = await GameWire.ReadAsync<GamePacket>(d, ct);
        Assert.Equal(("carol", caro, "hi"), (message?.Sender, message?.RoomId, message?.Payload));

        using var eTcp = new TcpClient();
        using var fTcp = new TcpClient();
        using var e = await SignInAndConnectAsync(client, eTcp, "eva", ct);
        using var f = await SignInAndConnectAsync(client, fTcp, "felix", ct);
        workers.SetDraining(0, false);
        int originalWorker = workers.GetSnapshot(0).ProcessId!.Value;
        using var crashed = Process.GetProcessById(originalWorker);
        crashed.Kill();
        await crashed.WaitForExitAsync(ct);
        Assert.Equal(WorkerStatus.Offline, workers.GetSnapshot(0).Status);
        Assert.True((await client.SendAsync(login, ct)).Success);
        await MatchAsync(e, f, "CHESS", ct);
        Assert.Equal(2, workers.GetSnapshot(1).ReservedRooms);
        Assert.Equal(WorkerStatus.Full, workers.GetSnapshot(1).Status);

        await WaitForAsync(() => workers.GetSnapshot(0) is { Status: WorkerStatus.Ready, ProcessId: int pid } &&
            pid != originalWorker, ct);
        workers.SetDraining(0, true);
        using var gTcp = new TcpClient();
        using var hTcp = new TcpClient();
        using var g = await SignInAndConnectAsync(client, gTcp, "gia", ct);
        using var h = await SignInAndConnectAsync(client, hTcp, "ha", ct);
        await GameWire.WriteAsync(g, new GamePacket(PacketType.FIND_MATCH, GameMode: "CARO"), ct);
        await GameWire.WriteAsync(h, new GamePacket(PacketType.FIND_MATCH, GameMode: "CARO"), ct);
        Assert.Equal(PacketType.ERROR, (await GameWire.ReadAsync<GamePacket>(g, ct))?.Type);
        Assert.Equal(PacketType.ERROR, (await GameWire.ReadAsync<GamePacket>(h, ct))?.Type);
        Assert.Equal(2, workers.GetSnapshot(1).ReservedRooms);

        workers.SetDraining(0, false);
        await MatchAsync(g, h, "CARO", ct);
        Assert.Equal(1, workers.GetSnapshot(0).ReservedRooms);
    }

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition()) await Task.Delay(50, ct);
    }

    private sealed class CountingAuth : IAuthService
    {
        private int requests;
        public int RequestCount => Volatile.Read(ref requests);
        private readonly ClusterTests.Service inner = new();

        public Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct)
        {
            Interlocked.Increment(ref requests);
            return inner.HandleAsync(request, ct);
        }
    }

    private static async Task<SslStream> OpenAsync(GatewayClient client, TcpClient tcp, string ticket, PacketType expected, CancellationToken ct)
    {
        var tls = await client.ConnectAsync(tcp, ct);
        try
        {
            await GameWire.WriteAsync(tls, new GatewayRequest(GatewayRequest.CurrentVersion, "game", Token: ticket), ct);
            Assert.Equal(expected, (await GameWire.ReadAsync<GamePacket>(tls, ct))?.Type);
            return tls;
        }
        catch { tls.Dispose(); throw; }
    }

    private static async Task<SslStream> SignInAndConnectAsync(GatewayClient client, TcpClient tcp, string user, CancellationToken ct)
    {
        var login = await client.SendAsync(new AuthRequest("signin", user, "test-password"), ct);
        var ticket = await client.RequestTicketAsync(login.SessionToken!, ct);
        return await OpenAsync(client, tcp, ticket.Token!, PacketType.AUTHENTICATED, ct);
    }

    private static async Task<string> MatchAsync(Stream a, Stream b, string mode, CancellationToken ct)
    {
        string aId = Guid.NewGuid().ToString("N"), bId = Guid.NewGuid().ToString("N");
        await GameWire.WriteAsync(a, new GamePacket(PacketType.FIND_MATCH, GameMode: mode, QueueAttemptId: aId), ct);
        await GameWire.WriteAsync(b, new GamePacket(PacketType.FIND_MATCH, GameMode: mode, QueueAttemptId: bId), ct);
        var first = await GameWire.ReadAsync<GamePacket>(a, ct);
        var second = await GameWire.ReadAsync<GamePacket>(b, ct);
        Assert.Equal(PacketType.MATCH_FOUND, first?.Type);
        Assert.Equal(PacketType.MATCH_FOUND, second?.Type);
        Assert.Equal(aId, first!.QueueAttemptId);
        Assert.Equal(bId, second!.QueueAttemptId);
        Assert.Equal(first.RoomId, second.RoomId);
        return first.RoomId;
    }

    [Fact]
    public async Task DefaultPoolEnforcesNMinusOneAdmissionAndReportsHeartbeatMetrics()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var ct = deadline.Token;
        using var rootKey = RSA.Create(2048);
        var caRequest = new CertificateRequest("CN=Test N-1 CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=127.0.0.1", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        leafRequest.CertificateExtensions.Add(san.Build());
        using var issued = leafRequest.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1),
            RandomNumberGenerator.GetBytes(16));
        using var withKey = issued.CopyWithPrivateKey(leafKey);
        using var leaf = new X509Certificate2(withKey.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.UserKeySet);

        await using var workers = new GameWorkerPool(capacity: 2);
        Assert.Equal(2, workers.AdmissionCapacity);
        var port1 = new TcpListener(IPAddress.Loopback, 0);
        var port2 = new TcpListener(IPAddress.Loopback, 0);
        port1.Start(); port2.Start();
        int first = ((IPEndPoint)port1.LocalEndpoint).Port;
        int second = ((IPEndPoint)port2.LocalEndpoint).Port;
        port1.Stop(); port2.Stop();
        workers.Start(first, second, typeof(GameServer).Assembly.Location);
        await workers.WaitUntilReadyAsync(ct);

        var auth = new CountingAuth();
        var context = SslStreamCertificateContext.Create(leaf, new X509Certificate2Collection(root), offline: true);
        await using var server = new GameServer(IPAddress.Loopback, context, new GameSessions(auth), auth, workers, 0);
        server.Start(ct);
        var client = new GatewayClient($"127.0.0.1:{server.Port}", root);

        using var aTcp = new TcpClient(); using var bTcp = new TcpClient();
        using var cTcp = new TcpClient(); using var dTcp = new TcpClient();
        using var eTcp = new TcpClient(); using var fTcp = new TcpClient();
        using var a = await SignInAndConnectAsync(client, aTcp, "alice", ct);
        using var b = await SignInAndConnectAsync(client, bTcp, "bob", ct);
        using var c = await SignInAndConnectAsync(client, cTcp, "carol", ct);
        using var d = await SignInAndConnectAsync(client, dTcp, "dan", ct);
        using var e = await SignInAndConnectAsync(client, eTcp, "eva", ct);
        using var f = await SignInAndConnectAsync(client, fTcp, "felix", ct);

        string room1 = await MatchAsync(a, b, "CHESS", ct);
        await MatchAsync(c, d, "CARO", ct);
        Assert.Equal(2, workers.GetSnapshot(0).ReservedRooms + workers.GetSnapshot(1).ReservedRooms);

        await GameWire.WriteAsync(a, new GamePacket(PacketType.MOVE, RoomId: room1, Payload: "e2e4"), ct);
        Assert.Equal("e2e4", (await GameWire.ReadAsync<GamePacket>(b, ct))?.Payload);
        await WaitForAsync(() =>
        {
            var s0 = workers.GetSnapshot(0);
            var s1 = workers.GetSnapshot(1);
            return s0.CpuPercent >= 0 && s1.CpuPercent >= 0 && s0.InFlightRequests == 0 && s1.InFlightRequests == 0 &&
                   s0.Sessions + s1.Sessions == 4;
        }, ct);

        await AssertAdmissionRejectedAsync(e, f, ct);

        // A dead PID must not erase admission demand for its still-active room.
        int crashedPid = workers.GetSnapshot(0).ProcessId!.Value;
        using var crashed = Process.GetProcessById(crashedPid);
        crashed.Kill();
        await crashed.WaitForExitAsync(ct);

        await AssertAdmissionRejectedAsync(e, f, ct);
        await WaitForAsync(() => workers.GetSnapshot(0) is { Status: WorkerStatus.Ready, ProcessId: int pid } &&
            pid != crashedPid, ct);
        await AssertAdmissionRejectedAsync(e, f, ct);
    }

    private static async Task AssertAdmissionRejectedAsync(Stream first, Stream second, CancellationToken ct)
    {
        await GameWire.WriteAsync(first, new GamePacket(PacketType.FIND_MATCH, GameMode: "CHESS"), ct);
        await GameWire.WriteAsync(second, new GamePacket(PacketType.FIND_MATCH, GameMode: "CHESS"), ct);
        Assert.Equal(PacketType.ERROR, (await GameWire.ReadAsync<GamePacket>(first, ct))?.Type);
        Assert.Equal(PacketType.ERROR, (await GameWire.ReadAsync<GamePacket>(second, ct))?.Type);
    }
}
