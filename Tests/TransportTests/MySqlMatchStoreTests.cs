using MySqlConnector;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

public sealed class MySqlMatchStoreTests
{
    public sealed class DisposableDatabaseFactAttribute : FactAttribute
    {
        public DisposableDatabaseFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MATCH_TEST_MYSQL")))
                Skip = "Set MATCH_TEST_MYSQL to a disposable loopback MySQL server with CREATE DATABASE permission.";
        }
    }

    [DisposableDatabaseFact]
    public async Task MigrationPersistsSnapshotAndFencesStaleAndDuplicateTerminalWrites()
    {
        var config = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MATCH_TEST_MYSQL")
            ?? throw new InvalidOperationException("MATCH_TEST_MYSQL is required."));
        Assert.Contains(config.Server, new[] { "localhost", "127.0.0.1", "::1" });
        config.Database = "";
        config.Pooling = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        await using var admin = new MySqlConnection(config.ConnectionString);
        await admin.OpenAsync(ct);
        string database = $"phase4_test_{Guid.NewGuid():N}";
        await ExecuteAsync(admin, $"CREATE DATABASE `{database}`", ct);
        try
        {
            config.Database = database;
            await using var schema = new MySqlConnection(config.ConnectionString);
            await schema.OpenAsync(ct);
            string[] migration = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory, "online_matches.sql"), ct);
            await ExecuteAsync(schema, string.Join('\n', migration.Skip(1)), ct); // Skip USE onlinechessdb.
            var store = new MySqlMatchStore(config.ConnectionString);
            var original = new MatchState(1, 0, "", 1, 600_000, 600_000, 1, 1);
            string room = Guid.NewGuid().ToString("N");
            await store.CreateAsync(room, "CHESS", 1, 2, original, ct);
            var confirmed = original with { Revision = 1, Moves = "e2e4", Turn = 2, TurnStartedAt = 3, FirstMilliseconds = 599_998 };
            Assert.True(await store.UpdateAsync(room, original, confirmed, ct));
            Assert.Equal(confirmed, await store.ReadAsync(room, ct));
            var recovered = confirmed with { Generation = 2 };
            Assert.True(await store.UpdateAsync(room, confirmed, recovered, ct));
            Assert.False(await store.UpdateAsync(room, confirmed, confirmed with { Revision = 2, Result = "DRAW" }, ct));
            var first = recovered with { Revision = 2, Result = "FIRST" };
            var second = first with { Result = "SECOND" };
            bool[] outcomes = await Task.WhenAll(store.UpdateAsync(room, recovered, first, ct),
                store.UpdateAsync(room, recovered, second, ct));
            Assert.Single(outcomes, applied => applied);
            Assert.Equal(outcomes[0] ? first : second, await store.ReadAsync(room, ct));
        }
        finally { await ExecuteAsync(admin, $"DROP DATABASE `{database}`", CancellationToken.None); }
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
