namespace OnlineChessGame.Caro;

public static class CaroEngine
{
    private static readonly (int Row, int Column)[] Directions =
        [(0, 1), (1, 0), (1, 1), (1, -1)];

    public static (int Row, int Column)? FindBestMove(int[,] board, int piece)
    {
        Validate(board, piece);
        (int Row, int Column)? best = null;
        int bestPriority = -1;
        int bestScore = -1;
        long bestDistance = long.MaxValue;
        for (int row = 0; row < board.GetLength(0); row++)
        for (int col = 0; col < board.GetLength(1); col++)
        {
            if (board[row, col] != 0) continue;
            int priority = WouldWin(board, row, col, piece) ? 2
                : WouldWin(board, row, col, 3 - piece) ? 1 : 0;
            int score = 2 * PatternScore(board, row, col, piece)
                + PatternScore(board, row, col, 3 - piece);
            long dr = row - board.GetLength(0) / 2;
            long dc = col - board.GetLength(1) / 2;
            long distance = dr * dr + dc * dc;
            if (priority > bestPriority || priority == bestPriority &&
                (score > bestScore || score == bestScore && distance < bestDistance))
            {
                best = (row, col);
                bestPriority = priority;
                bestScore = score;
                bestDistance = distance;
            }
        }
        return best;
    }

    public static bool HasFive(int[,] board, int row, int col, int piece)
    {
        Validate(board, piece);
        if (row < 0 || row >= board.GetLength(0)) throw new ArgumentOutOfRangeException(nameof(row));
        if (col < 0 || col >= board.GetLength(1)) throw new ArgumentOutOfRangeException(nameof(col));
        return board[row, col] == piece && WouldWin(board, row, col, piece);
    }

    private static void Validate(int[,] board, int piece)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (piece is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(piece));
        if (board.Length == 0) throw new ArgumentException("Board must have at least one cell.", nameof(board));
        foreach (int cell in board)
            if (cell is < 0 or > 2)
                throw new ArgumentException("Cells must be 0 (empty), 1 or 2.", nameof(board));
    }

    private static bool WouldWin(int[,] board, int row, int col, int piece)
    {
        foreach (var (dr, dc) in Directions)
            if (1 + Count(board, row, col, dr, dc, piece)
                + Count(board, row, col, -dr, -dc, piece) >= 5)
                return true;
        return false;
    }

    private static int Count(int[,] board, int row, int col, int dr, int dc, int piece)
    {
        int count = 0;
        row += dr;
        col += dc;
        while (Inside(board, row, col) && board[row, col] == piece)
        {
            count++;
            row += dr;
            col += dc;
        }
        return count;
    }

    // shortcut: one-ply patterns cannot foresee forks; add search only if stronger play is needed.
    private static int PatternScore(int[,] board, int row, int col, int piece)
    {
        int score = 0;
        foreach (var (dr, dc) in Directions)
        {
            int forward = Count(board, row, col, dr, dc, piece);
            int backward = Count(board, row, col, -dr, -dc, piece);
            int openEnds = IsEmpty(board, row + dr * (forward + 1), col + dc * (forward + 1)) ? 1 : 0;
            if (IsEmpty(board, row - dr * (backward + 1), col - dc * (backward + 1))) openEnds++;
            int weight = (1 + forward + backward) switch
            {
                1 => 0,
                2 => 10,
                3 => 100,
                _ => 1000
            };
            if (openEnds > 0) score += weight * (openEnds == 2 ? 10 : 1);
        }
        return score;
    }

    private static bool IsEmpty(int[,] board, int row, int col) =>
        Inside(board, row, col) && board[row, col] == 0;

    private static bool Inside(int[,] board, int row, int col) =>
        row >= 0 && row < board.GetLength(0) && col >= 0 && col < board.GetLength(1);
}
