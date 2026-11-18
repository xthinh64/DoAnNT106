using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OnlineChessGame.Network;
using Xunit;

public sealed class ClusterTests
{
    internal static X509Certificate2 Certificate()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.UserKeySet);
    }
    internal sealed class Service : IAuthService
    {
        public Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct) => Task.FromResult(
            request.Action == "signin"
                ? new AuthResponse(true, "OK", request.Username, new PlayerProfile(1, request.Username, "Player", 1200, 1200))
                : new AuthResponse(false, "INVALID_ACTION", "Unsupported action."));
    }
    [Fact]
    public async Task Gateway_round_trip_returns_profile()
    {
        using var cert = Certificate();
        await using var cluster = new AuthCluster(new Service(), cert, 0, 0, 0);
        await cluster.StartAsync();
        var client = new AuthClient($"127.0.0.1:{cluster.GatewayPort}", cert.GetCertHashString(HashAlgorithmName.SHA256));
        var result = await client.SendAsync(new AuthRequest("signin", "alice", "secret"));
        Assert.True(result.Success);
        Assert.Equal("alice", result.Player!.Username);
    }
}
