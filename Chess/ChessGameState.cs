using Chess;

namespace OnlineChessGame.Chess;

/// <summary>
/// Nguồn sự thật cho luật cờ vua. Gera.Chess sinh nước hợp lệ và duy trì toàn
/// bộ trạng thái đặc biệt như nhập thành, bắt tốt qua đường, phong cấp và hòa.
/// </summary>
public sealed class ChessGameState
{
    private readonly ChessBoard board;

    public ChessGameState(string? fen = null)
    {
        board = string.IsNullOrWhiteSpace(fen)
            ? new ChessBoard { AutoEndgameRules = AutoEndgameRules.All }
            : ChessBoard.LoadFromFen(fen, AutoEndgameRules.All);
        board.StandardiseCastlingPositions = true;
    }

    public string Fen => board.ToFen();
    public bool WhiteToMove => board.Turn == PieceColor.White;
    public bool InCheck => WhiteToMove ? board.WhiteKingChecked : board.BlackKingChecked;
    public bool IsEndGame => board.IsEndGame;
    public EndgameType? EndgameType => board.EndGame?.EndgameType;
    public bool? WhiteWon => board.EndGame?.WonSide is null
        ? null
        : board.EndGame.WonSide == PieceColor.White;
    public IReadOnlyList<char> CapturedWhitePieces =>
        board.CapturedWhite.Select(piece => piece.Type.AsChar).ToArray();
    public IReadOnlyList<char> CapturedBlackPieces =>
        board.CapturedBlack.Select(piece => piece.Type.AsChar).ToArray();

    public IReadOnlyList<string> GetAllLegalMoves() =>
        board.Moves(generateSan: false).Select(ToUci).ToArray();

    public IReadOnlyList<string> GetLegalMovesFrom(int row, int column)
    {
        var position = new Position(CoordinatesToSquare(row, column));
        if (board[position] is null)
            return Array.Empty<string>();

        return board.Moves(position, generateSan: false)
            .Select(ToUci)
            .ToArray();
    }

    public bool TryApplyMove(string uciMove, out string error)
    {
        string normalized = uciMove.Trim().ToLowerInvariant();
        Move? move = board.Moves(generateSan: false)
            .FirstOrDefault(candidate => ToUci(candidate) == normalized);

        if (move is null)
        {
            error = $"Nước đi '{uciMove}' không hợp lệ trong thế cờ hiện tại.";
            return false;
        }

        try
        {
            board.Move(move);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static string CoordinatesToSquare(int row, int column) =>
        $"{(char)('a' + column)}{8 - row}";

    public static (int Row, int Column) SquareToCoordinates(ReadOnlySpan<char> square) =>
        (8 - (square[1] - '0'), square[0] - 'a');

    private static string ToUci(Move move)
    {
        string value = $"{move.OriginalPosition}{move.NewPosition}".ToLowerInvariant();
        if (!move.IsPromotion || move.Promotion is null)
            return value;

        char promotion = move.Promotion.Type == PieceType.Rook ? 'r'
            : move.Promotion.Type == PieceType.Bishop ? 'b'
            : move.Promotion.Type == PieceType.Knight ? 'n'
            : 'q';
        return value + promotion;
    }
}
