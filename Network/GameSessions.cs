using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace OnlineChessGame.Network;

// Tokens remain in memory, expire after 15 minutes and can open exactly one game connection.
public sealed class GameSessions : IAuthService
{
    private readonly IAuthService auth;
    private readonly ConcurrentDictionary<string, (PlayerProfile Player, DateTimeOffset Expires)> tokens = new();
    public GameSessions(IAuthService auth) => this.auth = auth;

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

    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
