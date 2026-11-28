using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Security.Authentication;
using OnlineChessGame.Server;
using OnlineChessGame.Network;
using Xunit;

public sealed class LanGatewayTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Production_client_validates_configured_non_loopback_identity(bool includeLanIdentity)
    {
        var address = LanAddress();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Chess LAN Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(ServerNetworkOptions.CreateSubjectAlternativeNames(IPAddress.Any,
            includeLanIdentity ? address.ToString() : null).Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.UserKeySet);
        using var publicCertificate = new X509Certificate2(certificate.Export(X509ContentType.Cert));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var workers = new GameWorkerPool();
        var auth = new ClusterTests.Service();
        var context = SslStreamCertificateContext.Create(certificate, null, offline: true);
        await using var server = new GameServer(ServerNetworkOptions.ResolveBindAddress(null), context,
            new GameSessions(auth), auth, workers, 0);
        server.Start(timeout.Token);
        var client = new GatewayClient($"{address}:{server.Port}", publicCertificate);
        using var tcp = new TcpClient();
        if (includeLanIdentity)
        {
            using var connection = await client.ConnectAsync(tcp, timeout.Token);
            Assert.True(connection.IsAuthenticated);
        }
        else
            await Assert.ThrowsAsync<AuthenticationException>(() => client.ConnectAsync(tcp, timeout.Token));
    }

    private static IPAddress LanAddress() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(network => network.OperationalStatus == OperationalStatus.Up)
        .SelectMany(network => network.GetIPProperties().UnicastAddresses)
        .Select(unicast => unicast.Address)
        .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
        ?? throw Xunit.Sdk.SkipException.ForSkip("No operational non-loopback IPv4 interface is available.");

    [Fact]
    public async Task Default_auth_gateway_accepts_non_loopback_connections()
    {
        var address = LanAddress();
        using var certificate = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), certificate, 0, 0, 0);
        await cluster.StartAsync();
        var client = new AuthClient($"{address}:{cluster.GatewayPort}",
            certificate.GetCertHashString(HashAlgorithmName.SHA256));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var response = await client.SendAsync(new("signin", "alice", "secret"), timeout.Token);

        Assert.True(response.Success);
        Assert.Equal("alice", response.Player!.Username);
    }
}
