using System.Text.Json;
using MySqlConnector;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

public interface IMatchStore
{
    Task CreateAsync(string roomId, string mode, int firstId, int secondId, MatchState state, CancellationToken ct);
    Task<MatchState?> ReadAsync(string roomId, CancellationToken ct);
    Task<bool> UpdateAsync(string roomId, MatchState previous, MatchState next, CancellationToken ct);
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

    private static void AddSnapshot(MySqlCommand command, string roomId, MatchState state)
    {
        command.Parameters.AddWithValue("@id", roomId);
        command.Parameters.AddWithValue("@generation", state.Generation);
        command.Parameters.AddWithValue("@revision", state.Revision);
        command.Parameters.AddWithValue("@snapshot", JsonSerializer.Serialize(state));
    }
}
