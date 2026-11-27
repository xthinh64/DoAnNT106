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
    Task<bool> TryReclaimAsync(string roomId, int expectedGeneration, MatchState nextState, CancellationToken ct);
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
        await using var command = new MySqlCommand("""
            UPDATE OnlineMatches SET generation=@generation, revision=@revision, snapshot=@snapshot, result=@result
            WHERE room_id=@id AND generation=@old_generation AND revision=@old_revision AND result IS NULL
            """, connection);
        AddSnapshot(command, roomId, next);
        command.Parameters.AddWithValue("@result", next.Result);
        command.Parameters.AddWithValue("@old_generation", previous.Generation);
        command.Parameters.AddWithValue("@old_revision", previous.Revision);
        return await command.ExecuteNonQueryAsync(ct) == 1;
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
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            UPDATE OnlineMatches
            SET generation=@generation, snapshot=@snapshot
            WHERE room_id=@id AND generation=@old_generation AND result IS NULL
            """, connection);
        AddSnapshot(command, roomId, nextState);
        command.Parameters.AddWithValue("@old_generation", expectedGeneration);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<bool> AbortMatchAsync(string roomId, int expectedGeneration, MatchState abortedState, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            UPDATE OnlineMatches
            SET result=@result, snapshot=@snapshot
            WHERE room_id=@id AND generation=@old_generation AND result IS NULL
            """, connection);
        command.Parameters.AddWithValue("@id", roomId);
        command.Parameters.AddWithValue("@result", abortedState.Result ?? "ABORTED");
        command.Parameters.AddWithValue("@snapshot", JsonSerializer.Serialize(abortedState));
        command.Parameters.AddWithValue("@old_generation", expectedGeneration);
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
