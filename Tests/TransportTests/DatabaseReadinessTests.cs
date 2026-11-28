using MySqlConnector;
using OnlineChessGame.Network;
using OnlineChessGame.Server;
using Xunit;

public sealed class DatabaseReadinessTests
{
    [MySqlMatchStoreTests.DisposableDatabaseFact]
    public Task LeastPrivilegeUpgradeEnablesReadinessAndSettlement() =>
        MySqlMatchStoreTests.WithDatabaseAsync(async (_, admin, ct) =>
        {
            string user = "elo_" + Guid.NewGuid().ToString("N")[..24];
            string db = admin.Database;
            async Task Sql(string sql) { await using var c = new MySqlCommand(sql, admin); await c.ExecuteNonQueryAsync(ct); }
            await Sql($"CREATE USER '{user}'@'127.0.0.1' IDENTIFIED BY 'disposable-only'");
            try
            {
                await Sql($"GRANT SELECT,INSERT ON `{db}`.Users TO '{user}'@'127.0.0.1'");
                await Sql($"GRANT SELECT,INSERT ON `{db}`.UserGameStats TO '{user}'@'127.0.0.1'");
                await Sql($"GRANT SELECT,INSERT,UPDATE ON `{db}`.OnlineMatches TO '{user}'@'127.0.0.1'");
                var config = new MySqlConnectionStringBuilder(admin.ConnectionString) { UserID = user, Password = "disposable-only", Pooling = false };
                var error = await Assert.ThrowsAsync<DatabaseReadinessException>(() => DatabaseReadiness.CheckAsync(config.ConnectionString, ct));
                Assert.Contains("migration", error.Message);
                await Sql($"GRANT UPDATE ON `{db}`.UserGameStats TO '{user}'@'127.0.0.1'");
                await DatabaseReadiness.CheckAsync(config.ConnectionString, ct);
                await Assert.ThrowsAsync<DatabaseReadinessException>(() => DatabaseReadiness.CheckAsync(config.ConnectionString, ct, allowLegacyMigration: true));
                await Sql($"GRANT UPDATE(password_hash) ON `{db}`.Users TO '{user}'@'127.0.0.1'");
                await DatabaseReadiness.CheckAsync(config.ConnectionString, ct, allowLegacyMigration: true);
                await using (var count = new MySqlCommand("SELECT COUNT(*) FROM UserGameStats", admin))
                    Assert.Equal(0L, Convert.ToInt64(await count.ExecuteScalarAsync(ct)));
                var store = new MySqlMatchStore(config.ConnectionString);
                var start = new MatchState(1,0,"",1,600000,600000,1,1);
                await store.CreateAsync("least-privilege", "CHESS", 1, 2, start, ct);
                Assert.True(await store.UpdateAsync("least-privilege", start, start with { Revision=1, Result="FIRST" }, ct));
                Assert.Equal(1216, (await new OnlineChessGame.Auth.MySqlAuthService(config.ConnectionString).ReadProfileAsync(1,ct))!.ChessElo);
                // Exercise the production decorator chains against persisted MySQL ratings.
                foreach (bool gui in new[] { false, true })
                {
                    OnlineChessGame.Network.IAuthService auth = new OnlineChessGame.Auth.AuthAdmissionService(new OnlineChessGame.Auth.MySqlAuthService(config.ConnectionString));
                    if (gui) auth = new OnlineChessGame.ServerManager.AuthMonitor(auth);
                    var sessions = new OnlineChessGame.Network.GameSessions(auth);
                    var old = new OnlineChessGame.Network.PlayerProfile(1,"test1","",1200,1200);
                    string login = sessions.CreateSession(old);
                    Assert.Equal(1216, (await sessions.RefreshAsync(old,ct)).ChessElo);
                    Assert.Equal(1216, (await sessions.RefreshAsync(sessions.Consume(sessions.IssueTicket(login)!)!,ct)).ChessElo);
                }
            }
            finally { await Sql($"DROP USER '{user}'@'127.0.0.1'"); }
        });

    [MySqlMatchStoreTests.DisposableDatabaseFact]
    public Task SchemaPrerequisitesAreCheckedWithoutWrites() =>
        MySqlMatchStoreTests.WithDatabaseAsync(async (_, db, ct) =>
        {
            async Task Sql(string sql) { await using var c = new MySqlCommand(sql, db); await c.ExecuteNonQueryAsync(ct); }
            await DatabaseReadiness.CheckAsync(db.ConnectionString, ct);
            await Sql("ALTER TABLE UserGameStats DROP COLUMN draws");
            await Assert.ThrowsAsync<DatabaseReadinessException>(() => DatabaseReadiness.CheckAsync(db.ConnectionString,ct));
            await Sql("ALTER TABLE UserGameStats ADD draws INT DEFAULT 0");
            await Sql("ALTER TABLE UserGameStats ADD INDEX user_lookup(user_id), DROP INDEX unique_user_game");
            await Assert.ThrowsAsync<DatabaseReadinessException>(() => DatabaseReadiness.CheckAsync(db.ConnectionString,ct));
            await Sql("ALTER TABLE UserGameStats ADD UNIQUE KEY unique_user_game(user_id,game_type)");
            await Sql("ALTER TABLE OnlineMatches ENGINE=MyISAM");
            await Assert.ThrowsAsync<DatabaseReadinessException>(() => DatabaseReadiness.CheckAsync(db.ConnectionString,ct));
        });
}
