using OnlineChessGame.Network;

namespace OnlineChessGame;

// Connection settings live in memory only. The pin must be provisioned out-of-band.
internal static class AuthConnection
{
    public static string Endpoint { get; set; } = Environment.GetEnvironmentVariable("CHESS_AUTH_ENDPOINT") ?? "127.0.0.1:8888";
    public static PlayerProfile? Player { get; set; }

    public static AuthClient CreateClient()
    {
        string pin = Environment.GetEnvironmentVariable("CHESS_CERT_PIN") ?? "";
        if (string.IsNullOrWhiteSpace(pin))
            throw new InvalidOperationException("CHESS_CERT_PIN must be configured before connecting.");
        return new AuthClient(Endpoint, pin);
    }
}
