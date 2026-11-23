using OnlineChessGame.Network;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;

namespace OnlineChessGame;

// Public deployment settings ship beside the client executable; environment overrides remain available.
internal static class AuthConnection
{
    private sealed record ClientConfig(string? Endpoint, string? TrustedRootCertificate);

    private static readonly ClientConfig? Config = ReadConfig();
    private static readonly Lazy<X509Certificate2?> TrustedRoot = new(() =>
    {
        if (string.IsNullOrWhiteSpace(Config?.TrustedRootCertificate)) return null;
        try { return new X509Certificate2(Path.GetFullPath(Config.TrustedRootCertificate, AppContext.BaseDirectory)); }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new InvalidOperationException("Không đọc được chứng chỉ CA LAN đã cấu hình.", ex);
        }
    });

    public static string Endpoint { get; set; } = Environment.GetEnvironmentVariable("CHESS_AUTH_ENDPOINT")
        ?? Config?.Endpoint ?? "127.0.0.1:8888";
    public static PlayerProfile? Player { get; set; }
    public static string? SessionToken { get; set; }

    public static GatewayClient CreateClient() => new(Endpoint, TrustedRoot.Value);

    private static ClientConfig? ReadConfig()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "client-config.json");
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<ClientConfig>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("client-config.json không phải JSON hợp lệ.", exception);
        }
    }
}
