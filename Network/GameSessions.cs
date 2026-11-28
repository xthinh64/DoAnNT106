using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace OnlineChessGame.Network;

// Tokens remain in memory, expire after 15 minutes and can open exactly one game connection.
public sealed class GameSessions : IAuthService
{
    private readonly IAuthService auth;
    private readonly ConcurrentDictionary<string, (PlayerProfile Player, DateTimeOffset Expires)> tokens = new();
    private readonly ConcurrentDictionary<string, (PlayerProfile Player, DateTimeOffset Expires)> loginSessions = new();
    public GameSessions(IAuthService auth) => this.auth = auth;

    public async Task<PlayerProfile> RefreshAsync(PlayerProfile player, CancellationToken ct) =>
        auth is IPlayerProfileReader reader
            ? await reader.ReadProfileAsync(player.UserId, ct) ?? throw new IOException("Player no longer exists.")
            : player;

    public async Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct)
    {
        var response = await auth.HandleAsync(request, ct);
        if (!response.Success || request.Action != "signin" || response.Player is null) return response;
        foreach (var item in tokens)
            if (item.Value.Expires <= DateTimeOffset.UtcNow) tokens.TryRemove(item.Key, out _);
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        tokens[Digest(token)] = (response.Player, DateTimeOffset.UtcNow.AddMinutes(15));
        return response with { GameToken = token };
    }

    public PlayerProfile? Consume(string token)
    {
        if (token.Length != 64) return null;
        return tokens.TryRemove(Digest(token), out var session) && session.Expires > DateTimeOffset.UtcNow
            ? session.Player : null;
    }

    public string CreateSession(PlayerProfile player)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in loginSessions)
            if (item.Value.Expires <= now) loginSessions.TryRemove(item.Key, out _);
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        loginSessions[Digest(token)] = (player, now.AddHours(8));
        return token;
    }

    public string? IssueTicket(string? sessionToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (sessionToken?.Length != 64 || !loginSessions.TryGetValue(Digest(sessionToken), out var session) ||
            session.Expires <= now)
            return null;
        foreach (var item in tokens)
            if (item.Value.Expires <= now) tokens.TryRemove(item.Key, out _);
        string ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = now.AddSeconds(30);
        tokens[Digest(ticket)] = (session.Player, expires < session.Expires ? expires : session.Expires);
        return ticket;
    }

    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
