namespace OnlineChessGame.Network;

public sealed record AuthRequest(string Action, string Username, string Password, string? DisplayName = null);
public sealed record PlayerProfile(int UserId, string Username, string DisplayName, int ChessElo, int CaroElo);
public sealed record AuthResponse(bool Success, string Code, string Message, PlayerProfile? Player = null, string? GameToken = null, string? SessionToken = null);
public interface IPlayerProfileReader
{
    Task<PlayerProfile?> ReadProfileAsync(int userId, CancellationToken ct);
}

public interface IAuthService
{
    Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct);
}
