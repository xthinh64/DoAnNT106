using System.Buffers.Binary;
using System.Text.Json;

namespace OnlineChessGame.Network;

public enum PacketType
{
    AUTHENTICATE,
    AUTHENTICATED,
    FIND_MATCH,     // Client bấm "Tìm trận Đấu Rank"
    CANCEL_MATCH,   // Client bấm "HỦY TÌM TRẬN"
    MATCH_FOUND,    // Server ghép xong 2 người -> mở bàn cờ
    MOVE,           // Gửi tọa độ nước đi
    CHAT,           // Gửi luồng tin nhắn chat trong trận
    SURRENDER,      // Báo đầu hàng
    DRAW_REQUEST,   // Gửi lời mời cầu hòa
    DRAW_RESPONSE,  // Phản hồi đồng ý / từ chối cầu hòa
    LEAVE_ROOM,     // Thoát trận về sảnh
    OPPONENT_LEFT,  // Báo đối thủ thoát / mất kết nối
    ERROR,
    STATE,
    CLOCK_EXPIRED,
    RECOVERING
}

public sealed record GamePacket(
    PacketType Type,
    string Sender = "",
    string RoomId = "",
    string GameMode = "CHESS", // "CHESS" hoặc "CARO"
    string Payload = "",
    long? ExpectedRevision = null,
    MatchState? State = null
);

public sealed record MatchState(long Generation, long Revision, string Moves, int Turn,
    long FirstMilliseconds, long SecondMilliseconds, long TurnStartedAt, long ObservedAt,
    string? Result = null, string? Reason = null, int DrawOfferedBy = 0)
{
    public const int MaxMoves = 1024;

    public long Remaining(int side, long now) =>
        Math.Max(0, (side == 1 ? FirstMilliseconds : SecondMilliseconds) -
            (Result is null && Turn == side ? Math.Max(0, now - TurnStartedAt) : 0));
}

public static class GameWire
{
    public const int MaxFrame = 16 * 1024;

    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken ct = default)
    {
        var header = new byte[4];
        try
        {
            await stream.ReadExactlyAsync(header, ct);
        }
        catch (EndOfStreamException)
        {
            return default;
        }

        int size = BinaryPrimitives.ReadInt32BigEndian(header);
        if (size <= 0 || size > MaxFrame)
            throw new InvalidDataException("Invalid game frame length.");

        var payload = new byte[size];
        await stream.ReadExactlyAsync(payload, ct);
        return JsonSerializer.Deserialize<T>(payload);
    }

    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken ct = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        if (payload.Length > MaxFrame)
            throw new InvalidDataException("Game frame exceeds 16 KiB.");

        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }
}
