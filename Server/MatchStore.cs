using System.Text.Json;
using MySqlConnector;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

public sealed record OpenMatchRecord(
    string RoomId,
    string GameType,
    int FirstUserId,
    int SecondUserId,
    int Generation,
    int Revision,
    MatchState State);

public interface IMatchStore
{
    Task CreateAsync(string roomId, string mode, int firstId, int secondId, MatchState state, CancellationToken ct);
    Task<MatchState?> ReadAsync(string roomId, CancellationToken ct);
    Task<bool> UpdateAsync(string roomId, MatchState previous, MatchState next, CancellationToken ct);

    Task<IReadOnlyList<OpenMatchRecord>> ReadOpenMatchesAsync(CancellationToken ct);
    // Reclaim preserves the revision that was read and advances its generation fence.
    Task<bool> TryReclaimAsync(string roomId, int expectedGeneration, MatchState nextState, CancellationToken ct);
    // Abort creates the terminal revision: expected stored revision is abortedState.Revision - 1.
    Task<bool> AbortMatchAsync(string roomId, int expectedGeneration, MatchState abortedState, CancellationToken ct);
}

public sealed class MySqlMatchStore(string connectionString) : IMatchStore
{
    public async Task CreateAsync(string roomId, string mode, int firstId, int secondId, MatchState state, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            INSERT INTO OnlineMatches (room_id, game_type, first_user_id, second_user_id, generation, revision, snapshot)
            VALUES (@id, @mode, @first, @second, @generation, @revision, @snapshot)
            """, connection);
        AddSnapshot(command, roomId, state);
        command.Parameters.AddWithValue("@mode", mode);
        command.Parameters.AddWithValue("@first", firstId);
        command.Parameters.AddWithValue("@second", secondId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<MatchState?> ReadAsync(string roomId, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new MySqlCommand("SELECT snapshot FROM OnlineMatches WHERE room_id=@id", connection);
        command.Parameters.AddWithValue("@id", roomId);
        return await command.ExecuteScalarAsync(ct) is string snapshot ? JsonSerializer.Deserialize<MatchState>(snapshot) : null;
    }

    public async Task<bool> UpdateAsync(string roomId, MatchState previous, MatchState next, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        // The stored row, not the worker/client snapshot, owns participants and pool.
        await using var read = new MySqlCommand("SELECT first_user_id,second_user_id,game_type FROM OnlineMatches WHERE room_id=@id AND generation=@g AND revision=@r AND result IS NULL FOR UPDATE", connection, tx);
        read.Parameters.AddWithValue("@id", roomId);
        read.Parameters.AddWithValue("@g", previous.Generation);
        read.Parameters.AddWithValue("@r", previous.Revision);
        int first, second; string mode;
        await using (var row = await read.ExecuteReaderAsync(ct))
        {
            if (!await row.ReadAsync(ct)) return false;
            first = row.GetInt32(0); second = row.GetInt32(1); mode = row.GetString(2);
        }
        next = next with { Rating = null };
        if (next.Result is "FIRST" or "SECOND" or "DRAW")
        {
            if (first == second || mode is not ("CHESS" or "CARO")) throw new InvalidDataException("Invalid rated participants/pool.");
            var ratings = new Dictionary<int, int>();
            foreach (int id in new[] { first, second }.Order())
            {
                await using var seed = new MySqlCommand("INSERT INTO UserGameStats(user_id,game_type,elo,wins,losses,draws) VALUES(@u,@m,1200,0,0,0) ON DUPLICATE KEY UPDATE user_id=user_id", connection, tx);
                seed.Parameters.AddWithValue("@u", id); seed.Parameters.AddWithValue("@m", mode);
                await seed.ExecuteNonQueryAsync(ct);
                await using var stat = new MySqlCommand("SELECT COALESCE(elo,1200) FROM UserGameStats WHERE user_id=@u AND game_type=@m FOR UPDATE", connection, tx);
                stat.Parameters.AddWithValue("@u", id); stat.Parameters.AddWithValue("@m", mode);
                ratings[id] = Convert.ToInt32(await stat.ExecuteScalarAsync(ct));
            }
            double score = next.Result == "FIRST" ? 1 : next.Result == "DRAW" ? 0.5 : 0;
            int delta = EloPolicy.Delta(ratings[first], ratings[second], score);
            next = next with { Rating = new(EloPolicy.Version, mode, first, second, ratings[first], ratings[second], delta) };
            foreach (int id in new[] { first, second }.Order())
            {
                double ownScore = id == first ? score : 1 - score;
                await using var stat = new MySqlCommand("UPDATE UserGameStats SET elo=@elo,wins=COALESCE(wins,0)+@w,losses=COALESCE(losses,0)+@l,draws=COALESCE(draws,0)+@d WHERE user_id=@u AND game_type=@m", connection, tx);
                stat.Parameters.AddWithValue("@elo", checked(ratings[id] + (id == first ? delta : -delta)));
                stat.Parameters.AddWithValue("@w", ownScore == 1 ? 1 : 0); stat.Parameters.AddWithValue("@l", ownScore == 0 ? 1 : 0); stat.Parameters.AddWithValue("@d", ownScore == 0.5 ? 1 : 0);
                stat.Parameters.AddWithValue("@u", id); stat.Parameters.AddWithValue("@m", mode);
                await stat.ExecuteNonQueryAsync(ct);
            }
        }
        await using var command = new MySqlCommand("""
            UPDATE OnlineMatches SET generation=@generation, revision=@revision, snapshot=@snapshot, result=@result
            WHERE room_id=@id AND generation=@old_generation AND revision=@old_revision AND result IS NULL
            """, connection, tx);
        AddSnapshot(command, roomId, next);
        command.Parameters.AddWithValue("@result", next.Result);
        command.Parameters.AddWithValue("@old_generation", previous.Generation);
        command.Parameters.AddWithValue("@old_revision", previous.Revision);
        if (await command.ExecuteNonQueryAsync(ct) != 1) return false;
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OpenMatchRecord>> ReadOpenMatchesAsync(CancellationToken ct)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT room_id, game_type, first_user_id, second_user_id, generation, revision, snapshot
            FROM OnlineMatches
            WHERE result IS NULL
            """, connection);

        var matches = new List<OpenMatchRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var state = JsonSerializer.Deserialize<MatchState>(reader.GetString("snapshot"));
            if (state is null) continue;

            matches.Add(new OpenMatchRecord(
                reader.GetString("room_id"),
                reader.GetString("game_type"),
                reader.GetInt32("first_user_id"),
                reader.GetInt32("second_user_id"),
                reader.GetInt32("generation"),
                reader.GetInt32("revision"),
                state));
        }

        return matches;
    }

    public async Task<bool> TryReclaimAsync(string roomId, int expectedGeneration, MatchState nextState, CancellationToken ct)
    {
        // A successful claim must consume the old generation fence, never leave it reusable.
        if (nextState.Generation <= expectedGeneration) return false;
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            UPDATE OnlineMatches
            SET generation=@generation, revision=@revision, snapshot=@snapshot, result=@result
            WHERE room_id=@id AND generation=@old_generation AND revision=@old_revision AND result IS NULL
            """, connection);
        AddSnapshot(command, roomId, nextState);
        command.Parameters.AddWithValue("@result", nextState.Result);
        command.Parameters.AddWithValue("@old_generation", expectedGeneration);
        // Reclaim advances only the generation; its revision is the one read at startup.
        command.Parameters.AddWithValue("@old_revision", nextState.Revision);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<bool> AbortMatchAsync(string roomId, int expectedGeneration, MatchState abortedState, CancellationToken ct)
    {
        abortedState = abortedState with { Result = abortedState.Result ?? "ABORTED" };
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            UPDATE OnlineMatches
            SET generation=@generation, revision=@revision, snapshot=@snapshot, result=@result
            WHERE room_id=@id AND generation=@old_generation AND revision=@old_revision AND result IS NULL
            """, connection);
        AddSnapshot(command, roomId, abortedState);
        command.Parameters.AddWithValue("@result", abortedState.Result ?? "ABORTED");
        command.Parameters.AddWithValue("@old_generation", expectedGeneration);
        // Aborting creates exactly one terminal revision from the state that was read.
        command.Parameters.AddWithValue("@old_revision", abortedState.Revision - 1);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    private static void AddSnapshot(MySqlCommand command, string roomId, MatchState state)
    {
        command.Parameters.AddWithValue("@id", roomId);
        command.Parameters.AddWithValue("@generation", state.Generation);
        command.Parameters.AddWithValue("@revision", state.Revision);
        command.Parameters.AddWithValue("@snapshot", JsonSerializer.Serialize(state));
    }
}
