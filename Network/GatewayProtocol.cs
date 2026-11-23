using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace OnlineChessGame.Network;

public sealed record GatewayRequest(int Version, string Operation, AuthRequest? Auth = null, string? Token = null)
{
    public const int CurrentVersion = 3;
}

public sealed record GameTicket(string? Token);

public sealed class GatewayClient(string endpoint, X509Certificate2? trustedRoot = null)
{
    public async Task<SslStream> ConnectAsync(TcpClient tcp, CancellationToken ct)
    {
        if (!Uri.TryCreate($"tcp://{endpoint}", UriKind.Absolute, out var uri) ||
            uri.Port is < 1 or > 65535 || uri.Host.Length == 0 ||
            uri.AbsolutePath != "/" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Expected host:port.", nameof(endpoint));

        await tcp.ConnectAsync(uri.IdnHost, uri.Port, ct);
        var tls = new SslStream(tcp.GetStream());
        try
        {
            var options = new SslClientAuthenticationOptions
            {
                TargetHost = uri.IdnHost, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            };
            if (trustedRoot is not null)
            {
                // App-local LAN CAs can be provisioned without an online revocation service.
                options.CertificateChainPolicy = new X509ChainPolicy
                {
                    TrustMode = X509ChainTrustMode.CustomRootTrust,
                    RevocationMode = X509RevocationMode.NoCheck
                };
                options.CertificateChainPolicy.CustomTrustStore.Add(trustedRoot);
            }
            await tls.AuthenticateAsClientAsync(options, ct);
            return tls;
        }
        catch
        {
            tls.Dispose();
            throw;
        }
    }

    public async Task<AuthResponse> SendAsync(AuthRequest request, CancellationToken ct = default)
    {
        var response = await ExchangeAsync<AuthResponse>(new(GatewayRequest.CurrentVersion, "auth", Auth: request), ct);
        if (response.Code is null || response.Message is null ||
            (response.Success && (response.Player is null || (request.Action == "signin" && response.SessionToken is null))))
            throw new IOException("Invalid gateway authentication response.");
        return response;
    }

    public Task<GameTicket> RequestTicketAsync(string token, CancellationToken ct) =>
        ExchangeAsync<GameTicket>(new(GatewayRequest.CurrentVersion, "ticket", Token: token), ct);

    private async Task<T> ExchangeAsync<T>(GatewayRequest request, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var tcp = new TcpClient();
        using var tls = await ConnectAsync(tcp, deadline.Token);
        await GameWire.WriteAsync(tls, request, deadline.Token);
        try
        {
            return await GameWire.ReadAsync<T>(tls, deadline.Token)
                ?? throw new IOException("Gateway closed before sending a response.");
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
        {
            throw new IOException("Invalid gateway response.", ex);
        }
    }
}
