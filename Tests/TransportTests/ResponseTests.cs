using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OnlineChessGame.Network;
using Xunit;

public sealed class ResponseTests
{
    [Fact]
    public async Task Malformed_pinned_TLS_response_is_invalid_data()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => SendWithResponseAsync("{"));
        Assert.IsType<JsonException>(error.InnerException);
    }

    [Theory]
    [InlineData("{}", "signin")]
    [InlineData("{\"Success\":false,\"Code\":null,\"Message\":\"Denied\"}", "signin")]
    [InlineData("{\"Success\":false,\"Code\":\"DENIED\",\"Message\":null}", "signin")]
    [InlineData("{\"Success\":true,\"Code\":\"OK\",\"Message\":\"Welcome\"}", "signin")]
    [InlineData("{\"Success\":true,\"Code\":\"OK\",\"Message\":\"Welcome\"}", "signup")]
    public async Task Invalid_response_contract_is_rejected(string response, string action)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => SendWithResponseAsync(response, action));
    }

    [Fact]
    public async Task Failure_response_does_not_require_player()
    {
        var response = await SendWithResponseAsync("{\"Success\":false,\"Code\":\"DENIED\",\"Message\":\"Denied\"}");
        Assert.False(response.Success);
        Assert.Null(response.Player);
    }

    private static async Task<AuthResponse> SendWithResponseAsync(string response, string action = "signin")
    {
        using var cert = ClusterTests.Certificate();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        async Task ServeAsync()
        {
            using var peer = await listener.AcceptTcpClientAsync(deadline.Token);
            using var tls = new SslStream(peer.GetStream(), false);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = cert,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, deadline.Token);
            var header = new byte[4];
            await tls.ReadExactlyAsync(header, deadline.Token);
            int size = BinaryPrimitives.ReadInt32BigEndian(header);
            Assert.InRange(size, 1, 16384);
            var requestBytes = new byte[size];
            await tls.ReadExactlyAsync(requestBytes, deadline.Token);
            var request = JsonSerializer.Deserialize<AuthRequest>(requestBytes)!;
            Assert.Equal(action, request.Action);
            var payload = Encoding.UTF8.GetBytes(response);
            BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
            await tls.WriteAsync(header, deadline.Token);
            await tls.WriteAsync(payload, deadline.Token);
            await tls.FlushAsync(deadline.Token);
        }
        var server = ServeAsync();
        var client = new AuthClient($"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}", cert.GetCertHashString(HashAlgorithmName.SHA256));
        try { return await client.SendAsync(new(action, "alice", "secret"), deadline.Token); }
        finally { await server; }
    }
}
