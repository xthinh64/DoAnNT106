using OnlineChessGame.Network;
using System.Text.Json;

namespace OnlineChessGame;

// Public deployment settings ship beside the client executable; environment overrides remain available.
internal static class AuthConnection
{
    private sealed record ClientConfig(string? Endpoint, string? CertificatePin);

    private static readonly ClientConfig? Config = ReadConfig();

    public static string Endpoint { get; set; } = Environment.GetEnvironmentVariable("CHESS_AUTH_ENDPOINT")
        ?? Config?.Endpoint ?? "127.0.0.1:8888";
    public static PlayerProfile? Player { get; set; }

    public static AuthClient CreateClient()
    {
        string pin = Environment.GetEnvironmentVariable("CHESS_CERT_PIN")
            ?? Config?.CertificatePin ?? "";
        if (string.IsNullOrWhiteSpace(pin))
            throw new InvalidOperationException("Chưa cấu hình certificate pin. Kiểm tra client-config.json cạnh ứng dụng.");
        return new AuthClient(Endpoint, pin);
    }

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
