using MySqlConnector;
using OnlineChessGame.Network;

namespace OnlineChessGame.Auth;

public sealed class MySqlAuthService(string connectionString, bool allowLegacyMigration = false) : IAuthService
{
    private static readonly string DummyHash = PasswordHasher.Hash("dummy-not-an-account-password");
    private static AuthResponse Invalid() => new(false, "invalid_request", "Invalid authentication request.");
    private static AuthResponse Failed() => new(false, "invalid_credentials", "Invalid username or password.");

    public async Task<AuthResponse> HandleAsync(AuthRequest request, CancellationToken ct = default)
    {
        try { return await HandleCoreAsync(request, ct); }
        catch (MySqlException e) when (!ct.IsCancellationRequested && e.Number == 1062 && request?.Action == "signup")
        { return new(false, "username_taken", "Username is unavailable."); }
        catch (MySqlException) when (!ct.IsCancellationRequested)
        { return new(false, "service_unavailable", "Authentication service is temporarily unavailable."); }
    }

    private async Task<AuthResponse> HandleCoreAsync(AuthRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request is null || request.Action is not ("signin" or "signup") || request.Username is null || request.Password is null) return Invalid();
        var username = request.Username.Trim();
        var display = request.DisplayName?.Trim();
        if (username.Length is < 1 or > 50 || request.Username.Length > 256 || request.Password.Length is < 1 or > 128 ||
            request.DisplayName?.Length > 100 || (request.Action == "signup" && request.Password.Length < 8)) return Invalid();
        display = string.IsNullOrEmpty(display) ? username : display;
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        if (request.Action == "signup")
        {
            var hash = PasswordHasher.Hash(request.Password);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var insert = new MySqlCommand("INSERT INTO Users(username,password_hash,display_name) VALUES(@username,@hash,@display)", connection, transaction);
            insert.Parameters.AddWithValue("@username", username);
            insert.Parameters.AddWithValue("@hash", hash);
            insert.Parameters.AddWithValue("@display", display);
            await insert.ExecuteNonQueryAsync(ct);
            var id = checked((int)insert.LastInsertedId);
            await using var stats = new MySqlCommand("INSERT INTO UserGameStats(user_id,game_type) VALUES(@id,'CHESS'),(@id,'CARO')",connection,transaction);
            stats.Parameters.AddWithValue("@id",id);
            await stats.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return new(true,"ok","Account created.",new PlayerProfile(id,username,display,1200,1200));
        }
        await using var select = new MySqlCommand("SELECT u.user_id,u.username,u.display_name,u.password_hash,c.elo,r.elo FROM Users u LEFT JOIN UserGameStats c ON c.user_id=u.user_id AND c.game_type='CHESS' LEFT JOIN UserGameStats r ON r.user_id=u.user_id AND r.game_type='CARO' WHERE u.username=@username",connection);
        select.Parameters.AddWithValue("@username",username);
        await using var reader = await select.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            PasswordHasher.Verify(request.Password,DummyHash);
            return Failed();
        }
        var stored = reader.GetString(3);
        var profile = new PlayerProfile(reader.GetInt32(0),reader.GetString(1),reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),reader.IsDBNull(4) ? 1200 : reader.GetInt32(4),reader.IsDBNull(5) ? 1200 : reader.GetInt32(5));
        await reader.DisposeAsync();
        if (!stored.StartsWith("pbkdf2-", StringComparison.Ordinal))
        {
            // Always pay the normal hashing cost, including legacy failures.
            PasswordHasher.Verify(request.Password,DummyHash);
            var expected = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stored));
            var actual = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Password));
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actual,expected)) return Failed();
            if (!allowLegacyMigration) return new(false,"reset_required","Password reset is required.");
            await using var migrate = new MySqlCommand("UPDATE Users SET password_hash=@hash WHERE user_id=@id AND BINARY password_hash=BINARY @old",connection);
            migrate.Parameters.AddWithValue("@hash", PasswordHasher.Hash(request.Password));
            migrate.Parameters.AddWithValue("@id", profile.UserId);
            migrate.Parameters.AddWithValue("@old", stored);
            if (await migrate.ExecuteNonQueryAsync(ct) != 1) return Failed();
        }
        else if (!PasswordHasher.Verify(request.Password,stored)) return Failed();
        return new(true,"ok","Signed in.",profile);
    }
}
