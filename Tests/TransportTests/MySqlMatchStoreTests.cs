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
    public Task MigrationIsRepeatableAndPreservesExistingScores() =>
        WithDatabaseAsync(async (store, db, ct) =>
        {
            await ExecuteAsync(db,"INSERT INTO UserGameStats(user_id,game_type,elo,wins) VALUES(1,'CHESS',1450,7)",ct);
            var sql = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory,"elo_v1.sql"),ct);
            string migration = string.Join('\n',sql.Skip(1));
            await ExecuteAsync(db,migration,ct);
            await ExecuteAsync(db,migration,ct);
            await using var count = new MySqlCommand("SELECT COUNT(*) FROM UserGameStats",db);
            Assert.Equal(10L,Convert.ToInt64(await count.ExecuteScalarAsync(ct)));
            var profile = await new OnlineChessGame.Auth.MySqlAuthService(db.ConnectionString).ReadProfileAsync(1,ct);
            Assert.Equal(1450,profile!.ChessElo); Assert.Equal(1200,profile.CaroElo);
        });

    [DisposableDatabaseFact]
    public Task SettlementIsAtomicExactlyOnceAndPoolsAreIndependent() =>
        WithDatabaseAsync(async (store, db, ct) =>
        {
            var start = new MatchState(1, 0, "", 1, 600000, 600000, 1, 1);
            await store.CreateAsync("rated", "CHESS", 1, 2, start, ct);
            var win = start with { Revision = 1, Result = "FIRST" };
            var other = new MySqlMatchStore(db.ConnectionString);
            var results = await Task.WhenAll(store.UpdateAsync("rated", start, win, ct),
                other.UpdateAsync("rated", start, win with { Result = "SECOND" }, ct));
            Assert.Single(results, x => x);
            await using var query = new MySqlCommand("SELECT elo,wins,losses,draws FROM UserGameStats WHERE user_id=1 AND game_type='CHESS'", db);
            await using (var reader = await query.ExecuteReaderAsync(ct))
            {
                Assert.True(await reader.ReadAsync(ct));
                Assert.Equal(results[0] ? 1216 : 1184, reader.GetInt32(0));
                Assert.Equal(results[0] ? 1 : 0, reader.GetInt32(1));
                Assert.Equal(results[0] ? 0 : 1, reader.GetInt32(2));
                Assert.Equal(0, reader.GetInt32(3));
            }
            await using var caro = new MySqlCommand("SELECT COUNT(*) FROM UserGameStats WHERE game_type='CARO'", db);
            Assert.Equal(0L, Convert.ToInt64(await caro.ExecuteScalarAsync(ct)));
        });

    [DisposableDatabaseFact]
    public Task DrawAbortLegacyAndSharedPlayerSettlementsRemainConsistent() =>
        WithDatabaseAsync(async (store, db, ct) =>
        {
            var start = new MatchState(1,0,"",1,600000,600000,1,1);
            await store.CreateAsync("one", "CHESS", 1, 2, start, ct);
            await store.CreateAsync("two", "CHESS", 3, 1, start, ct);
            await Task.WhenAll(store.UpdateAsync("one", start, start with { Revision=1,Result="DRAW" },ct),
                new MySqlMatchStore(db.ConnectionString).UpdateAsync("two",start,start with { Revision=1,Result="SECOND" },ct));
            var settled = (await store.ReadAsync("two",ct))!;
            Assert.Equal(EloPolicy.Version, settled.Rating!.Policy);
            Assert.Equal(1216, settled.Rating.SecondAfter);
            await store.CreateAsync("caro","CARO",1,2,start,ct);
            Assert.True(await store.UpdateAsync("caro",start,start with { Revision=1,Result="SECOND" },ct));
            await store.CreateAsync("abort","CARO",1,2,start,ct);
            Assert.True(await store.UpdateAsync("abort",start,start with { Revision=1,Result="ABORTED" },ct));
            Assert.Null((await store.ReadAsync("abort",ct))!.Rating);
            await store.CreateAsync("legacy","CHESS",1,2,start,ct);
            await ExecuteAsync(db,"UPDATE OnlineMatches SET result='FIRST' WHERE room_id='legacy'",ct);
            Assert.False(await store.UpdateAsync("legacy",start,start with { Revision=1,Result="FIRST" },ct));
            await using var query = new MySqlCommand("SELECT game_type,elo,wins,losses,draws FROM UserGameStats WHERE user_id=1 ORDER BY game_type",db);
            await using var row = await query.ExecuteReaderAsync(ct);
            Assert.True(await row.ReadAsync(ct));
            Assert.Equal("CHESS",row.GetString(0)); Assert.InRange(row.GetInt32(1),1215,1216);
            Assert.Equal(1,row.GetInt32(2)); Assert.Equal(0,row.GetInt32(3)); Assert.Equal(1,row.GetInt32(4));
            Assert.True(await row.ReadAsync(ct));
            Assert.Equal("CARO",row.GetString(0)); Assert.Equal(1184,row.GetInt32(1));
            Assert.Equal(0,row.GetInt32(2)); Assert.Equal(1,row.GetInt32(3)); Assert.Equal(0,row.GetInt32(4));
        });

    [DisposableDatabaseFact]
    public Task SettlementFailureRollsBackBothStatsAndTerminalSnapshot() =>
        WithDatabaseAsync(async (store, db, ct) =>
        {
            var start = new MatchState(1,0,"",1,600000,600000,1,1);
            await store.CreateAsync("rollback","CHESS",1,2,start,ct);
            await ExecuteAsync(db,"CREATE TRIGGER reject_terminal BEFORE UPDATE ON OnlineMatches FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='injected settlement failure'",ct);
            await Assert.ThrowsAsync<MySqlException>(() => store.UpdateAsync("rollback",start,start with { Revision=1,Result="FIRST" },ct));
            Assert.Equal(start,await store.ReadAsync("rollback",ct));
            await using var query = new MySqlCommand("SELECT COUNT(*) FROM UserGameStats",db);
            Assert.Equal(0L,Convert.ToInt64(await query.ExecuteScalarAsync(ct)));
            await ExecuteAsync(db,"DROP TRIGGER reject_terminal",ct);
            Assert.True(await store.UpdateAsync("rollback",start,start with { Revision=1,Result="FIRST" },ct));
            Assert.NotNull((await store.ReadAsync("rollback",ct))!.Rating);
        });

    [DisposableDatabaseFact]
    public Task MigrationPersistsSnapshotAndFencesStaleAndDuplicateTerminalWrites() =>
        WithDatabaseAsync(async (store, _, ct) =>
        {
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
            var persisted = await store.ReadAsync(room, ct);
            Assert.NotNull(persisted!.Rating);
            Assert.Equal(outcomes[0] ? first : second, persisted with { Rating = null });
        });

    [DisposableDatabaseFact]
    public Task StartupListsOnlyOpenMatchesAndAllowsOneReclaimAndOneAbort() =>
        WithDatabaseAsync(async (store, _, ct) =>
        {
            string room = Guid.NewGuid().ToString("N");
            string closedRoom = Guid.NewGuid().ToString("N");
            var saved = new MatchState(1, 1, "7,7", 2, 599_000, 600_000, 2, 2);
            var initial = new MatchState(1, 0, "", 1, 600_000, 600_000, 1, 1);
            await store.CreateAsync(room, "CARO", 17, 42, saved, ct);
            await store.CreateAsync(closedRoom, "CHESS", 2, 3, initial, ct);
            Assert.True(await store.UpdateAsync(closedRoom, initial, initial with { Revision = 1, Result = "DRAW" }, ct));

            var record = Assert.Single(await store.ReadOpenMatchesAsync(ct));
            Assert.Equal(new OpenMatchRecord(room, "CARO", 17, 42, 1, 1, saved), record);
            var claimed = saved with { Generation = 2 };
            var claims = await Task.WhenAll(store.TryReclaimAsync(room, 1, claimed, ct),
                store.TryReclaimAsync(room, 1, claimed, ct));
            Assert.Single(claims, applied => applied);
            Assert.Equal(record with { Generation = 2, State = claimed }, Assert.Single(await store.ReadOpenMatchesAsync(ct)));

            var aborted = claimed with { Result = "ABORTED", Revision = claimed.Revision + 1 };
            Assert.False(await store.AbortMatchAsync(room, 1, aborted, ct));
            Assert.True(await store.AbortMatchAsync(room, 2, aborted, ct));
            Assert.False(await store.AbortMatchAsync(room, 2, aborted, ct));
            Assert.False(await store.TryReclaimAsync(room, 2, claimed with { Generation = 3 }, ct));
            Assert.Equal(aborted, await store.ReadAsync(room, ct));
            Assert.Empty(await store.ReadOpenMatchesAsync(ct));
        });

    [DisposableDatabaseFact]
    public Task ReclaimRejectsSnapshotSupersededByAcknowledgedMove() =>
        WithDatabaseAsync(async (store, _, ct) =>
        {
            string room = Guid.NewGuid().ToString("N");
            var saved = new MatchState(1, 1, "e2e4", 2, 599_000, 600_000, 2, 2);
            await store.CreateAsync(room, "CHESS", 1, 2, saved, ct);
            var candidate = Assert.Single(await store.ReadOpenMatchesAsync(ct));
            var confirmed = saved with { Revision = 2, Moves = "e2e4 e7e5", Turn = 1 };
            Assert.True(await store.UpdateAsync(room, saved, confirmed, ct));

            Assert.False(await store.TryReclaimAsync(room, candidate.Generation,
                candidate.State with { Generation = candidate.Generation + 1 }, ct));
            Assert.Equal(confirmed, await store.ReadAsync(room, ct));
        });

    [DisposableDatabaseFact]
    public Task AbortPersistsTerminalRevisionInRowAndSnapshot() =>
        WithDatabaseAsync(async (store, schema, ct) =>
        {
            string room = Guid.NewGuid().ToString("N");
            var saved = new MatchState(1, 0, "", 1, 600_000, 600_000, 1, 1);
            await store.CreateAsync(room, "CHESS", 1, 2, saved, ct);
            var aborted = saved with { Revision = 1, Result = "ABORTED" };
            Assert.True(await store.AbortMatchAsync(room, 1, aborted, ct));
            Assert.Equal(aborted, await store.ReadAsync(room, ct));
            await using var query = new MySqlCommand("SELECT revision FROM OnlineMatches WHERE room_id=@room", schema);
            query.Parameters.AddWithValue("@room", room);
            Assert.Equal(aborted.Revision, Assert.IsType<long>(await query.ExecuteScalarAsync(ct)));
        });

    [DisposableDatabaseFact]
    public Task AbortWithoutExplicitResultPersistsAbortedInBothRepresentations() =>
        WithDatabaseAsync(async (store, schema, ct) =>
        {
            string room = Guid.NewGuid().ToString("N");
            var saved = new MatchState(3, 4, "e2e4", 2, 599_000, 600_000, 2, 2);
            await store.CreateAsync(room, "CHESS", 1, 2, saved, ct);
            var aborted = saved with { Revision = 5 };
            Assert.True(await store.AbortMatchAsync(room, 3, aborted, ct));
            await AssertPersistedAsync(store, schema, room, aborted with { Result = "ABORTED" }, ct);
            Assert.Empty(await store.ReadOpenMatchesAsync(ct));
        });

    [DisposableDatabaseFact]
    public Task AbortRejectsSnapshotSupersededByAcknowledgedMove() =>
        WithDatabaseAsync(async (store, schema, ct) =>
        {
            string room = Guid.NewGuid().ToString("N");
            var saved = new MatchState(1, 1, "e2e4", 2, 599_000, 600_000, 2, 2);
            await store.CreateAsync(room, "CHESS", 1, 2, saved, ct);
            var confirmed = saved with { Revision = 2, Moves = "e2e4 e7e5", Turn = 1 };
            Assert.True(await store.UpdateAsync(room, saved, confirmed, ct));
            Assert.False(await store.AbortMatchAsync(room, 1, saved with { Revision = 2, Result = "ABORTED" }, ct));
            await AssertPersistedAsync(store, schema, room, confirmed, ct);
        });

    [DisposableDatabaseFact]
    public Task CompetingGatewaysCommitExactlyOneTerminalSnapshot() =>
        WithDatabaseAsync(async (store, schema, ct) =>
        {
            string room = Guid.NewGuid().ToString("N");
            var saved = new MatchState(3, 4, "7,7", 2, 599_000, 600_000, 2, 2);
            await store.CreateAsync(room, "CARO", 1, 2, saved, ct);
            var first = saved with { Revision = 5, Result = "ABORTED", Reason = "gateway-one" };
            var second = first with { Reason = "gateway-two" };
            var outcomes = await Task.WhenAll(store.AbortMatchAsync(room, 3, first, ct),
                store.AbortMatchAsync(room, 3, second, ct));
            Assert.Single(outcomes, applied => applied);
            await AssertPersistedAsync(store, schema, room, outcomes[0] ? first : second, ct);
            Assert.Empty(await store.ReadOpenMatchesAsync(ct));
        });

    [DisposableDatabaseFact]
    public Task ReclaimRejectsGenerationThatDoesNotAdvanceFence() =>
        WithDatabaseAsync(async (store, schema, ct) =>
        {
            string room = Guid.NewGuid().ToString("N");
            var saved = new MatchState(3, 4, "7,7", 2, 599_000, 600_000, 2, 2);
            await store.CreateAsync(room, "CARO", 1, 2, saved, ct);
            Assert.False(await store.TryReclaimAsync(room, 3, saved, ct));
            Assert.False(await store.TryReclaimAsync(room, 3, saved with { Generation = 2 }, ct));
            await AssertPersistedAsync(store, schema, room, saved, ct);
        });

    private static async Task AssertPersistedAsync(MySqlMatchStore store, MySqlConnection schema,
        string room, MatchState expected, CancellationToken ct)
    {
        Assert.Equal(expected, await store.ReadAsync(room, ct));
        await using var query = new MySqlCommand(
            "SELECT generation, revision, result FROM OnlineMatches WHERE room_id=@room", schema);
        query.Parameters.AddWithValue("@room", room);
        await using var row = await query.ExecuteReaderAsync(ct);
        Assert.True(await row.ReadAsync(ct));
        Assert.Equal(expected.Generation, row.GetInt32("generation"));
        Assert.Equal(expected.Revision, row.GetInt32("revision"));
        Assert.Equal(expected.Result, row.IsDBNull(2) ? null : row.GetString("result"));
        Assert.False(await row.ReadAsync(ct));
    }

    internal static async Task WithDatabaseAsync(Func<MySqlMatchStore, MySqlConnection, CancellationToken, Task> test)
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
            var auth = await File.ReadAllLinesAsync(Path.Combine(AppContext.BaseDirectory, "game_auth.sql"), ct);
            await ExecuteAsync(schema, string.Join('\n', auth.Skip(3)), ct);
            foreach (int id in new[] { 1, 2, 3, 17, 42 })
                await ExecuteAsync(schema, $"INSERT INTO Users(user_id,username,password_hash) VALUES({id},'test{id}','unused')", ct);
            await test(new MySqlMatchStore(config.ConnectionString), schema, ct);
        }
        finally { await ExecuteAsync(admin, $"DROP DATABASE `{database}`", CancellationToken.None); }
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
