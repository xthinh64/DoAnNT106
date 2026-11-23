using System.Globalization;
using OnlineChessGame.Caro;
using OnlineChessGame.Chess;
using OnlineChessGame.Network;

namespace OnlineChessGame.Server;

internal static class MatchRules
{
    public static MatchState Apply(MatchState state, string mode, int side, GamePacket packet)
    {
        long now = state.ObservedAt;
        if (state.Result is not null) throw new InvalidOperationException("Trận đã kết thúc.");
        long first = state.Remaining(1, now), second = state.Remaining(2, now);
        var next = state with
        {
            Revision = state.Revision + 1,
            FirstMilliseconds = first,
            SecondMilliseconds = second,
            TurnStartedAt = now
        };
        if (first == 0 || second == 0)
            return next with { Result = first == 0 ? "SECOND" : "FIRST", Reason = "Hết giờ." };
        if (packet.Type == PacketType.CLOCK_EXPIRED) return state;
        if (packet.ExpectedRevision != state.Revision)
            throw new InvalidOperationException("Trạng thái đã thay đổi. Hãy thử lại.");

        switch (packet.Type)
        {
            case PacketType.SURRENDER:
                return next with { Result = side == 1 ? "SECOND" : "FIRST", Reason = "Đầu hàng." };
            case PacketType.DRAW_REQUEST:
                if (state.DrawOfferedBy != 0) throw new InvalidOperationException("Đã có đề nghị hòa.");
                return next with { DrawOfferedBy = side };
            case PacketType.DRAW_RESPONSE:
                if (state.DrawOfferedBy == 0 || state.DrawOfferedBy == side || packet.Payload is not ("ACCEPT" or "DECLINE"))
                    throw new InvalidOperationException("Không có đề nghị hòa hợp lệ.");
                return packet.Payload == "ACCEPT"
                    ? next with { DrawOfferedBy = 0, Result = "DRAW", Reason = "Hai bên đồng ý hòa." }
                    : next with { DrawOfferedBy = 0 };
            case PacketType.MOVE:
                if (side != state.Turn) throw new InvalidOperationException("Chưa đến lượt của bạn.");
                return Move(next, mode, side, packet.Payload);
            default:
                throw new InvalidOperationException("Lệnh không hợp lệ.");
        }
    }

    private static MatchState Move(MatchState state, string mode, int side, string move)
    {
        string[] history = state.Moves.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? result = null, reason = null;
        if (mode == "CHESS")
        {
            var board = new ChessGameState();
            foreach (string previous in history)
                if (!board.TryApplyMove(previous, out _)) throw new InvalidDataException("Invalid stored chess history.");
            move = move.Trim().ToLowerInvariant();
            if (move.Length is < 4 or > 5 || board.IsEndGame || !board.TryApplyMove(move, out _))
                throw new InvalidOperationException("Nước cờ vua không hợp lệ.");
            if (board.IsEndGame)
            {
                result = board.WhiteWon switch { true => "FIRST", false => "SECOND", _ => "DRAW" };
                reason = board.EndgameType?.ToString();
            }
        }
        else
        {
            var board = new int[15, 15];
            for (int i = 0; i < history.Length; i++)
            {
                if (!TryCaroMove(history[i], out int r, out int c) || board[r, c] != 0)
                    throw new InvalidDataException("Invalid stored Caro history.");
                board[r, c] = i % 2 + 1;
            }
            if (!TryCaroMove(move, out int row, out int col) || board[row, col] != 0)
                throw new InvalidOperationException("Ô Caro không hợp lệ hoặc đã được đánh.");
            move = FormattableString.Invariant($"{row},{col}");
            board[row, col] = side;
            if (CaroEngine.HasFive(board, row, col, side))
            {
                result = side == 1 ? "FIRST" : "SECOND";
                reason = "Năm quân liên tiếp.";
            }
            else if (history.Length == 224) { result = "DRAW"; reason = "Bàn cờ đã kín."; }
        }
        if (result is null && history.Length + 1 >= MatchState.MaxMoves)
        {
            result = "DRAW";
            reason = "Đạt giới hạn số nước của trận online.";
        }
        return state with { Moves = history.Length == 0 ? move : $"{state.Moves} {move}",
            Turn = 3 - side, Result = result, Reason = reason };
    }

    public static bool TryCaroMove(string move, out int row, out int column)
    {
        row = column = -1;
        if (move.Length > 5) return false;
        var parts = move.Split(',');
        return parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out row) && row is >= 0 and < 15 &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out column) && column is >= 0 and < 15;
    }
}
