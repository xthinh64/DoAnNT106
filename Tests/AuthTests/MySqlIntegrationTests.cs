using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MySqlConnector;
using OnlineChessGame.Auth;
using OnlineChessGame.Network;
using Xunit;

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AUTH_TEST_MYSQL")))
            Skip = "Set AUTH_TEST_MYSQL to a disposable loopback database server (CREATE DATABASE privilege).";
    }
}

public class MySqlIntegrationTests
{
    private sealed class Database : IAsyncDisposable
    {
        private readonly string admin;
        private readonly string name = "auth_test_" + Guid.NewGuid().ToString("N");
        public string ConnectionString { get; private set; } = "";
        public Database()
        {
            var builder = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("AUTH_TEST_MYSQL") ?? throw new InvalidOperationException("AUTH_TEST_MYSQL is required."));
            if (builder.Server is not ("127.0.0.1" or "localhost" or "::1")) throw new InvalidOperationException("Tests require an explicitly disposable loopback server.");
            builder.Database = "";
            admin = builder.ConnectionString;
        }
        public async Task Initialize()
        {
            await using var c = new MySqlConnection(admin); await c.OpenAsync();
            await new MySqlCommand($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci", c).ExecuteNonQueryAsync();
            var builder = new MySqlConnectionStringBuilder(admin) { Database = name };
            ConnectionString = builder.ConnectionString;
            await Sql("CREATE TABLE Users(user_id INT AUTO_INCREMENT PRIMARY KEY,username VARCHAR(50) UNIQUE NOT NULL,password_hash VARCHAR(255) NOT NULL,display_name VARCHAR(100)); CREATE TABLE UserGameStats(stat_id INT AUTO_INCREMENT PRIMARY KEY,user_id INT NOT NULL,game_type ENUM('CHESS','CARO') NOT NULL,elo INT DEFAULT 1200,wins INT DEFAULT 0,losses INT DEFAULT 0,draws INT DEFAULT 0,FOREIGN KEY(user_id) REFERENCES Users(user_id) ON DELETE CASCADE, UNIQUE KEY unique_user_game(user_id,game_type));");
        }
        public async Task<object?> Sql(string sql)
        {
            await using var c = new MySqlConnection(ConnectionString); await c.OpenAsync();
            return await new MySqlCommand(sql,c).ExecuteScalarAsync();
        }
        public async ValueTask DisposeAsync()
        {
            MySqlConnection.ClearAllPools();
            await using var c = new MySqlConnection(admin); await c.OpenAsync();
            await new MySqlCommand($"DROP DATABASE IF EXISTS `{name}`", c).ExecuteNonQueryAsync();
        }
    }

    // Real end-to-end regression: no fake service or direct worker connections.
    // AUTH_TEST_MYSQL must point at a dedicated disposable loopback instance.
    [DatabaseFact]
    public async Task TlsGatewayBothWorkersAuthenticateAgainstDisposableMySql()
    {
        await using var db = new Database();
        await db.Initialize();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Persist/reload the key for Windows Schannel (ephemeral keys are unsupported).
        using var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.UserKeySet);
        await using var cluster = new AuthCluster(new MySqlAuthService(db.ConnectionString), certificate, 0, 0, 0);
        await cluster.StartAsync();
        var client = new AuthClient($"127.0.0.1:{cluster.GatewayPort}", certificate.GetCertHashString(HashAlgorithmName.SHA256));

        var signup = await client.SendAsync(new AuthRequest("signup", "tls_alice", "network password!", "TLS Alice"));
        Assert.True(signup.Success, signup.Message);
        Assert.NotNull(signup.Player);
        var stored = Assert.IsType<string>(await db.Sql("SELECT password_hash FROM Users WHERE username='tls_alice'"));
        Assert.NotEqual("network password!", stored);
        Assert.True(PasswordHasher.Verify("network password!", stored));
        Assert.False(PasswordHasher.Verify("wrong password", stored));
        Assert.Equal(2L, Convert.ToInt64(await db.Sql("SELECT COUNT(*) FROM UserGameStats")));
        // Database-side mutation proves signin reads persisted data, not a cached signup response.
        await db.Sql("UPDATE UserGameStats SET elo=1642 WHERE game_type='CHESS'");
        var signin = await client.SendAsync(new AuthRequest("signin", "tls_alice", "network password!"));
        Assert.True(signin.Success, signin.Message);
        Assert.Equal(signup.Player.UserId, signin.Player!.UserId);
        Assert.Equal("TLS Alice", signin.Player.DisplayName);
        Assert.Equal(1642, signin.Player.ChessElo);
        Assert.Equal(1200, signin.Player.CaroElo);
        var wrong = await client.SendAsync(new AuthRequest("signin", "tls_alice", "wrong password"));
        Assert.False(wrong.Success);
        Assert.Equal("invalid_credentials", wrong.Code);
        Assert.Null(wrong.Player);
        // Fourth sequential gateway request exercises successful signin on worker 2 again.
        Assert.True((await client.SendAsync(new AuthRequest("signin", "tls_alice", "network password!"))).Success);
        Assert.Equal(2L, cluster.Worker1Requests);
        Assert.Equal(2L, cluster.Worker2Requests);
        Assert.Equal(stored, await db.Sql("SELECT password_hash FROM Users WHERE username='tls_alice'"));
    }

    [DatabaseFact]
    public async Task ConcurrentDuplicateSignupCreatesOnlyOneAccount()
    {
        await using var db = new Database(); await db.Initialize();
        var service = new MySqlAuthService(db.ConnectionString);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => service.HandleAsync(new AuthRequest("signup", "same", "password!"), default)));
        Assert.Single(results, r => r.Success);
        Assert.All(results.Where(r => !r.Success), r => Assert.Equal("username_taken", r.Code));
        Assert.Equal(1L, Convert.ToInt64(await db.Sql("SELECT COUNT(*) FROM Users")));
        Assert.Equal(2L, Convert.ToInt64(await db.Sql("SELECT COUNT(*) FROM UserGameStats")));
    }

    [DatabaseFact]
    public async Task FailedStatsInsertRollsBackUser()
    {
        await using var db = new Database(); await db.Initialize();
        await db.Sql("CREATE TRIGGER reject_stats BEFORE INSERT ON UserGameStats FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='test failure'");
        var result = await new MySqlAuthService(db.ConnectionString).HandleAsync(new AuthRequest("signup", "rollback", "password!"), default);
        Assert.Equal("service_unavailable", result.Code);
        Assert.Equal(0L, Convert.ToInt64(await db.Sql("SELECT COUNT(*) FROM Users")));
    }

    [DatabaseFact]
    public async Task LegacyPasswordsRequireResetUnlessExplicitMigrationAndWrongCredentialsAreGeneric()
    {
        await using var db = new Database(); await db.Initialize();
        await db.Sql("INSERT INTO Users(username,password_hash,display_name) VALUES('old','abc','Old'); INSERT INTO UserGameStats(user_id,game_type) VALUES(1,'CHESS'),(1,'CARO')");
        var service = new MySqlAuthService(db.ConnectionString);
        var wrong = await service.HandleAsync(new AuthRequest("signin", "old", "wrong"), default);
        var missing = await service.HandleAsync(new AuthRequest("signin", "missing", "wrong"), default);
        Assert.Equal(wrong, missing);
        Assert.Equal("invalid_credentials", wrong.Code);
        Assert.Equal("reset_required", (await service.HandleAsync(new AuthRequest("signin", "old", "abc"), default)).Code);
        Assert.True((await new MySqlAuthService(db.ConnectionString, true).HandleAsync(new AuthRequest("signin", "old", "abc"), default)).Success);
        Assert.True(PasswordHasher.Verify("abc", (string)(await db.Sql("SELECT password_hash FROM Users"))!));
        Assert.True((await service.HandleAsync(new AuthRequest("signin", "old", "abc"), default)).Success);
    }

    [DatabaseFact]
    public async Task SignupPersistsHashAndTwoStatsAndSigninReadsRealProfile()
    {
        await using var db = new Database(); await db.Initialize();
        var service = new MySqlAuthService(db.ConnectionString);
        var signup = await service.HandleAsync(new AuthRequest("signup", " alice ", " pass word ", "Alice"), default);
        Assert.True(signup.Success, signup.Message);
        Assert.Equal("alice", signup.Player!.Username);
        Assert.Equal(1200, signup.Player.ChessElo);
        Assert.Equal(2L, Convert.ToInt64(await db.Sql("SELECT COUNT(*) FROM UserGameStats")));
        var stored = (string)(await db.Sql("SELECT password_hash FROM Users"))!;
        Assert.True(PasswordHasher.Verify(" pass word ", stored));
        await db.Sql("UPDATE UserGameStats SET elo=1532 WHERE game_type='CHESS'; UPDATE UserGameStats SET elo=1456 WHERE game_type='CARO';");
        var login = await service.HandleAsync(new AuthRequest("signin", "alice", " pass word "), default);
        Assert.True(login.Success);
        Assert.Equal(signup.Player.UserId, login.Player!.UserId);
        Assert.Equal("Alice", login.Player.DisplayName);
        Assert.Equal(1532, login.Player.ChessElo);
        Assert.Equal(1456, login.Player.CaroElo);
    }
}
