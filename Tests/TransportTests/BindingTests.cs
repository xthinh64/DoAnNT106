using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using OnlineChessGame.Network;
using Xunit;

public sealed class BindingTests
{
    [Theory]
    [InlineData("0.0.0.0", "127.0.0.1")]
    [InlineData("::", "::1")]
    [InlineData("127.0.0.2", "127.0.0.2")]
    public async Task Gateway_bind_does_not_expose_workers(string bind, string connect)
    {
        using var cert = ClusterTests.Certificate();
        await using var cluster = new AuthCluster(new ClusterTests.Service(), cert, 0, 0, 0, IPAddress.Parse(bind));
        await cluster.StartAsync();
        // Query the OS socket table, not the constructor or private listener fields.
        var endpoints = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        Assert.Contains(endpoints, ep => ep.Port == cluster.GatewayPort && ep.Address.Equals(IPAddress.Parse(bind)));
        foreach (int port in new[] { cluster.Worker1Port, cluster.Worker2Port })
        {
            var worker = Assert.Single(endpoints, ep => ep.Port == port);
            Assert.Equal(IPAddress.Loopback, worker.Address);
        }
        var client = new AuthClient($"[{connect}]:{cluster.GatewayPort}", cert.GetCertHashString(HashAlgorithmName.SHA256));
        for (int i = 0; i < 2; i++)
            Assert.True((await client.SendAsync(new("signin", "alice", "secret"))).Success);
        Assert.Equal(1, cluster.Worker1Requests);
        Assert.Equal(1, cluster.Worker2Requests);
    }
}
