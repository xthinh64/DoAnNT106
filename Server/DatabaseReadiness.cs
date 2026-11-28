using MySqlConnector;

namespace OnlineChessGame.Server;

public sealed class DatabaseReadinessException() : InvalidOperationException("Database readiness failed. Apply the controlled Elo schema migration and runtime permission upgrade; verify MySQL connectivity.");

/// <summary>Read-only preflight: metadata, zero-row SELECTs and EXPLAIN only; never migrates or writes data.</summary>
public static class DatabaseReadiness
{
    public static async Task CheckAsync(string connectionString, CancellationToken ct = default, bool allowLegacyMigration = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var token = deadline.Token;
        try
        {
            await using var db = new MySqlConnection(connectionString);
            await db.OpenAsync(token);
            async Task<object?> Scalar(string sql)
            {
                await using var cmd = new MySqlCommand(sql, db);
                return await cmd.ExecuteScalarAsync(token);
            }
            async Task Read(string sql)
            {
                await using var cmd = new MySqlCommand(sql, db);
                await using var reader = await cmd.ExecuteReaderAsync(token);
                do { while (await reader.ReadAsync(token)) { } } while (await reader.NextResultAsync(token));
            }
            await Read("SELECT user_id,username,display_name,password_hash FROM Users LIMIT 0");
            await Read("SELECT user_id,game_type,elo,wins,losses,draws FROM UserGameStats LIMIT 0");
            await Read("SELECT room_id,game_type,first_user_id,second_user_id,generation,revision,snapshot,result FROM OnlineMatches LIMIT 0");
            if (Convert.ToInt32(await Scalar("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('Users','UserGameStats','OnlineMatches') AND ENGINE='InnoDB'")) != 3)
                throw new DatabaseReadinessException();
            if (Convert.ToInt32(await Scalar("""
                SELECT COUNT(*) FROM (
                  SELECT INDEX_NAME FROM information_schema.STATISTICS
                  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='UserGameStats' AND NON_UNIQUE=0
                  GROUP BY INDEX_NAME
                  HAVING COUNT(*)=2 AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) IN ('user_id,game_type','game_type,user_id')
                    AND SUM(SUB_PART IS NOT NULL)=0
                ) AS keys_ready
                """)) < 1) throw new DatabaseReadinessException();
            // EXPLAIN (never EXPLAIN ANALYZE) checks effective grants, including active roles,
            // without executing DML, firing triggers, acquiring write locks or touching rows.
            await Read("EXPLAIN INSERT INTO Users(username,password_hash,display_name) VALUES ('readiness','unused','unused')");
            if (allowLegacyMigration) await Read("EXPLAIN UPDATE Users SET password_hash='unused' WHERE user_id=0");
            await Read("EXPLAIN INSERT INTO UserGameStats(user_id,game_type,elo,wins,losses,draws) VALUES (0,'CHESS',1200,0,0,0)");
            await Read("EXPLAIN UPDATE UserGameStats SET user_id=user_id,elo=1200,wins=0,losses=0,draws=0 WHERE user_id=0 AND game_type='CHESS'");
            await Read("EXPLAIN INSERT INTO OnlineMatches(room_id,game_type,first_user_id,second_user_id,generation,revision,snapshot,result) VALUES ('readiness','CHESS',0,0,1,0,'{}',NULL)");
            await Read("EXPLAIN UPDATE OnlineMatches SET generation=1,revision=0,snapshot='{}',result=NULL WHERE room_id='readiness'");
        }
        catch (MySqlException) { throw new DatabaseReadinessException(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new DatabaseReadinessException(); }
    }
}
