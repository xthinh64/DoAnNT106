using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using OnlineChessGame.Network;
using Xunit;

public sealed class MetricsTests
{
    [Fact]
    public async Task Idle_gateway_connection_counts_both_legs_and_stop_clears_them()
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        using var peer = new TcpClient();
        await peer.ConnectAsync(IPAddress.Loopback, cluster.GatewayPort);
        await EventuallyAsync(() => cluster.ActiveGatewayConnections == 1 &&
            cluster.ActiveWorker1Connections + cluster.ActiveWorker2Connections == 1);
        Assert.Equal(0, cluster.Worker1Requests + cluster.Worker2Requests);
        await cluster.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, cluster.ActiveGatewayConnections);
        Assert.Equal(0, cluster.ActiveWorker1Connections);
        Assert.Equal(0, cluster.ActiveWorker2Connections);
    }

    [Fact]
    public async Task Rejected_tls_handshake_releases_active_counts_without_counting_auth()
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        var client = new AuthClient($"127.0.0.1:{cluster.GatewayPort}", new string('0', 64));
        await Assert.ThrowsAsync<System.Security.Authentication.AuthenticationException>(() => client.SendAsync(new("signin", "alice", "secret")));
        await EventuallyAsync(() => cluster.ActiveGatewayConnections == 0 &&
            cluster.ActiveWorker1Connections == 0 && cluster.ActiveWorker2Connections == 0);
        Assert.Equal(0, cluster.Worker1Requests + cluster.Worker2Requests);
        var trusted = new AuthClient($"127.0.0.1:{cluster.GatewayPort}", cert.GetCertHashString(HashAlgorithmName.SHA256));
        Assert.True((await trusted.SendAsync(new("signin", "alice", "secret"))).Success);
    }

    private static async Task EventuallyAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition()) await Task.Delay(10, deadline.Token);
    }
}
