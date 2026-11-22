namespace OnlineChessGame.Chess;

public static class LocalChessEngine
{
    public static string ChooseMove(string fen, ChessDifficulty difficulty, CancellationToken token = default)
    {
        var position = new ChessGameState(fen);
        var moves = position.GetAllLegalMoves();
        token.ThrowIfCancellationRequested();
        if (moves.Count == 0)
            throw new InvalidOperationException("Không còn nước đi hợp lệ.");
        if (difficulty == ChessDifficulty.Easy)
            return moves[Random.Shared.Next(moves.Count)];

        int bestScore = int.MinValue;
        string best = moves[0];
        foreach (string move in moves)
        {
            token.ThrowIfCancellationRequested();
            var next = After(fen, move);
            int score = -Evaluate(next);
            if (difficulty != ChessDifficulty.Medium && !next.IsEndGame)
            {
                string replyFen = next.Fen;
                score = int.MaxValue;
                foreach (string reply in next.GetAllLegalMoves())
                {
                    token.ThrowIfCancellationRequested();
                    int replyScore = Evaluate(After(replyFen, reply));
                    score = Math.Min(score, replyScore);
                    if (score <= bestScore) break;
                }
            }
            if (score > bestScore)
                (bestScore, best) = (score, move);
        }
        return best;
    }

    private static ChessGameState After(string fen, string move)
    {
        var state = new ChessGameState(fen);
        if (!state.TryApplyMove(move, out string error))
            throw new InvalidOperationException(error);
        return state;
    }

    private static int Evaluate(ChessGameState state)
    {
        if (state.IsEndGame)
            return state.WhiteWon is bool whiteWon ? (whiteWon == state.WhiteToMove ? 100000 : -100000) : 0;

        int score = 0, row = 0, column = 0;
        foreach (char piece in state.Fen)
        {
            if (piece == ' ') break;
            if (piece == '/') { row++; column = 0; continue; }
            if (char.IsDigit(piece)) { column += piece - '0'; continue; }
            int center = 14 - Math.Abs(2 * column - 7) - Math.Abs(2 * row - 7);
            int value = char.ToLowerInvariant(piece) switch
            {
                'p' => 100 + (char.IsUpper(piece) ? 6 - row : row - 1) * 3,
                'n' => 320 + center,
                'b' => 330 + center,
                'r' => 500,
                'q' => 900,
                _ => 0
            };
            score += char.IsUpper(piece) ? value : -value;
            column++;
        }
        return state.WhiteToMove ? score : -score;
    }
}
