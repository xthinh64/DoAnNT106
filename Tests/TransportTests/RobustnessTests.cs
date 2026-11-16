using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using OnlineChessGame.Network;
using Xunit;

public sealed class RobustnessTests
{
    [Fact]
    public async Task Wrong_certificate_pin_fails_closed_before_authentication()
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        var client = new AuthClient($"127.0.0.1:{cluster.GatewayPort}", new string('0', 64));
        await Assert.ThrowsAsync<AuthenticationException>(() => client.SendAsync(new("signin", "alice", "secret")));
        Assert.Equal(0, cluster.Worker1Requests + cluster.Worker2Requests);
    }
    [Fact]
    public async Task Gateway_distributes_requests_to_both_workers()
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        var client = new AuthClient($"localhost:{cluster.GatewayPort}", cert.GetCertHashString(HashAlgorithmName.SHA256));
        for (int i = 0; i < 8; i++) Assert.True((await client.SendAsync(new("signin", "alice", "secret"))).Success);
        Assert.Equal(4, cluster.Worker1Requests);
        Assert.Equal(4, cluster.Worker2Requests);
    }
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(16385, false)]
    [InlineData(1, true)]
    public async Task Invalid_frame_closes_only_its_connection(int size, bool malformedJson)
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using (var tcp = new TcpClient())
        {
            await tcp.ConnectAsync(IPAddress.Loopback, cluster.Worker1Port, timeout.Token);
            // This fixture pins the generated certificate; it does not accept arbitrary peers.
            using var tls = new SslStream(tcp.GetStream(), false, (_, peer, _, _) => peer is not null && peer.GetRawCertData().SequenceEqual(cert.RawData));
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, timeout.Token);
            var header = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(header, size);
            await tls.WriteAsync(header, timeout.Token);
            if (malformedJson) await tls.WriteAsync(Encoding.UTF8.GetBytes("{"), timeout.Token);
            var buffer = new byte[1];
            try { Assert.Equal(0, await tls.ReadAsync(buffer, timeout.Token)); }
            catch (IOException) { } // TLS peers may send close-notify or reset.
        }
        var client = new AuthClient($"127.0.0.1:{cluster.GatewayPort}", cert.GetCertHashString(HashAlgorithmName.SHA256));
        Assert.True((await client.SendAsync(new("signin", "alice", "secret"))).Success);
    }
    [Fact]
    public async Task Stop_cancels_idle_connections_and_closes_all_listeners()
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        int[] ports = [cluster.GatewayPort, cluster.Worker1Port, cluster.Worker2Port];
        using var idle = new TcpClient();
        await idle.ConnectAsync(IPAddress.Loopback, ports[0]);
        await cluster.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        foreach (int port in ports)
        {
            using var tcp = new TcpClient();
            await Assert.ThrowsAsync<SocketException>(async () => await tcp.ConnectAsync(IPAddress.Loopback, port));
        }
    }
    [Fact]
    public async Task Unavailable_worker_falls_back_before_forwarding_auth_request()
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        // Fault injection only: stop listener one without changing the public production API.
        var listener = (TcpListener)typeof(AuthCluster).GetField("worker1", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(cluster)!;
        listener.Stop();
        var client = new AuthClient($"127.0.0.1:{cluster.GatewayPort}", cert.GetCertHashString(HashAlgorithmName.SHA256));
        Assert.True((await client.SendAsync(new("signin", "alice", "secret"))).Success);
        Assert.Equal(0, cluster.Worker1Requests);
        Assert.Equal(1, cluster.Worker2Requests);
    }
    [Fact]
    public async Task Partially_occupied_gateway_still_serves_authentication()
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        var idle = new List<TcpClient>();
        try
        {
            // Idle TLS sessions occupy the gateway and a worker without sending auth bytes.
            for (int i = 0; i < 70; i++)
            {
                var tcp = new TcpClient();
                idle.Add(tcp);
                await tcp.ConnectAsync(IPAddress.Loopback, cluster.GatewayPort);
            }
            await Task.Delay(300);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var client = new AuthClient($"127.0.0.1:{cluster.GatewayPort}", cert.GetCertHashString(HashAlgorithmName.SHA256));
            Assert.True((await client.SendAsync(new("signin", "alice", "secret"), deadline.Token)).Success);
        }
        finally { foreach (var tcp in idle) tcp.Dispose(); }
    }
    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost:0")]
    [InlineData("localhost:65536")]
    [InlineData("::1:8888")]
    [InlineData("https://localhost:8888")]
    public void Endpoint_requires_explicit_valid_port(string endpoint) => Assert.Throws<ArgumentException>(() => new AuthClient(endpoint, new string('A', 64)));
}
