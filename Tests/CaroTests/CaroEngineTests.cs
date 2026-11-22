using OnlineChessGame.Caro;
using Xunit;

namespace CaroTests;

public sealed class CaroEngineTests
{
    [Theory]
    [InlineData(15, 15, 7, 7)]
    [InlineData(6, 10, 3, 5)]
    [InlineData(1, 1, 0, 0)]
    public void EmptyBoardOpensAtCenter(int rows, int columns, int row, int column)
    {
        Assert.Equal(((int Row, int Column)?)(row, column),
            CaroEngine.FindBestMove(new int[rows, columns], 1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, -1)]
    public void FiveAtEdgeWinsInEveryDirection(int dr, int dc)
    {
        var board = new int[6, 9];
        int column = dc < 0 ? 8 : 0;
        for (int i = 0; i < 5; i++)
            board[i * dr, column + i * dc] = 2;

        Assert.True(CaroEngine.HasFive(board, 2 * dr, column + 2 * dc, 2));
        board[2 * dr, column + 2 * dc] = 0;
        Assert.False(CaroEngine.HasFive(board, 2 * dr, column + 2 * dc, 2));
    }

    [Fact]
    public void OverlineWinsEvenWithBothEndsBlocked()
    {
        var board = new int[3, 8];
        board[1, 0] = board[1, 7] = 2;
        for (int c = 1; c <= 6; c++) board[1, c] = 1;
        Assert.True(CaroEngine.HasFive(board, 1, 3, 1));
        Assert.False(CaroEngine.HasFive(board, 1, 3, 2));
        Assert.False(CaroEngine.HasFive(new int[,] { { 1, 1, 1, 1 } }, 0, 1, 1));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 1, 1)]
    [InlineData(1, -1, 2)]
    public void TakesImmediateWinInEveryDirection(int dr, int dc, int piece)
    {
        var board = new int[6, 9];
        int column = dc < 0 ? 8 : 0;
        for (int i = 0; i < 4; i++) board[i * dr, column + i * dc] = piece;
        Assert.Equal(((int Row, int Column)?)(4 * dr, column + 4 * dc),
            CaroEngine.FindBestMove(board, piece));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 1, 1)]
    [InlineData(1, -1, 2)]
    public void BlocksImmediateLossInEveryDirection(int dr, int dc, int piece)
    {
        var board = new int[6, 9];
        int column = dc < 0 ? 8 : 0;
        for (int i = 0; i < 4; i++) board[i * dr, column + i * dc] = 3 - piece;
        Assert.Equal(((int Row, int Column)?)(4 * dr, column + 4 * dc),
            CaroEngine.FindBestMove(board, piece));
    }

    [Fact]
    public void TakesWinInsteadOfBlockingOpponent()
    {
        var board = new int[7, 9];
        for (int c = 0; c < 4; c++) board[0, c] = 1;
        for (int c = 0; c < 4; c++) board[6, c] = 2;
        Assert.Equal(((int Row, int Column)?)(6, 4), CaroEngine.FindBestMove(board, 2));
    }

    [Fact]
    public void FillsGapToCompleteFive()
    {
        var board = new int[,] { { 1, 1, 0, 1, 1 } };
        Assert.Equal(((int Row, int Column)?)(0, 2), CaroEngine.FindBestMove(board, 1));
    }

    [Fact]
    public void FullBoardReturnsNull()
    {
        Assert.Null(CaroEngine.FindBestMove(new int[,] { { 1, 2, 1 }, { 2, 1, 2 } }, 2));
    }

    [Fact]
    public void OnlyEmptyCellIsChosenEvenOnThinBoard()
    {
        Assert.Equal(((int Row, int Column)?)(0, 0),
            CaroEngine.FindBestMove(new int[,] { { 0, 1, 2 } }, 1));
    }

    [Fact]
    public void EvaluationIsDeterministicAndLeavesInputUntouched()
    {
        var board = new int[9, 13];
        board[4, 4] = board[4, 5] = 1;
        board[5, 4] = board[5, 5] = 2;
        var before = board.Cast<int>().ToArray();
        var move = CaroEngine.FindBestMove(board, 2);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(move, CaroEngine.FindBestMove(board, 2));
            Assert.False(CaroEngine.HasFive(board, 5, 4, 2));
            Assert.Equal(before, board.Cast<int>().ToArray());
        }
        Assert.NotNull(move);
        Assert.Equal(0, board[move.Value.Row, move.Value.Column]);
    }

    [Fact]
    public void BuildsContiguousThreatInsteadOfPlayingDistantCenter()
    {
        var board = new int[15, 15];
        board[1, 1] = board[1, 2] = board[1, 3] = 2;
        Assert.Equal(((int Row, int Column)?)(1, 4), CaroEngine.FindBestMove(board, 2));
    }

    [Fact]
    public void RespondsToOpponentOpenThreeBeforeItBecomesFour()
    {
        var board = new int[15, 15];
        board[1, 1] = board[1, 2] = board[1, 3] = 1;
        board[7, 7] = 2;
        Assert.Equal(((int Row, int Column)?)(1, 4), CaroEngine.FindBestMove(board, 2));
    }

    [Fact]
    public void PrefersOpenEndedPatternOverCloserBlockedPattern()
    {
        var board = new int[15, 15];
        board[7, 3] = 1;
        board[7, 4] = board[7, 5] = board[7, 6] = 2;
        board[1, 1] = board[1, 2] = board[1, 3] = 2;
        Assert.Equal(((int Row, int Column)?)(1, 4), CaroEngine.FindBestMove(board, 2));
    }

    [Fact]
    public void PlaysNearExistingStoneRatherThanDistantCenter()
    {
        var board = new int[15, 15];
        board[0, 0] = 1;
        var move = CaroEngine.FindBestMove(board, 2)!.Value;
        Assert.InRange(move.Row, 0, 2);
        Assert.InRange(move.Column, 0, 2);
        Assert.Equal(0, board[move.Row, move.Column]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void RejectsInvalidPieces(int piece)
    {
        var board = new int[5, 5];
        Assert.Throws<ArgumentOutOfRangeException>(() => CaroEngine.FindBestMove(board, piece));
        Assert.Throws<ArgumentOutOfRangeException>(() => CaroEngine.HasFive(board, 0, 0, piece));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void RejectsInvalidCellsAnywhereOnBoard(int value)
    {
        var board = new int[5, 5];
        board[4, 4] = value;
        Assert.Throws<ArgumentException>(() => CaroEngine.FindBestMove(board, 1));
        Assert.Throws<ArgumentException>(() => CaroEngine.HasFive(board, 0, 0, 1));
        Assert.Equal(value, board[4, 4]);
    }

    [Fact]
    public void RejectsNullBoards()
    {
        Assert.Throws<ArgumentNullException>(() => CaroEngine.FindBestMove(null!, 1));
        Assert.Throws<ArgumentNullException>(() => CaroEngine.HasFive(null!, 0, 0, 1));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void RejectsZeroSizedBoards(int rows, int columns)
    {
        var board = new int[rows, columns];
        Assert.Throws<ArgumentException>(() => CaroEngine.FindBestMove(board, 1));
        Assert.Throws<ArgumentException>(() => CaroEngine.HasFive(board, 0, 0, 1));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 7)]
    public void RejectsOutOfRangeCoordinates(int row, int col)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CaroEngine.HasFive(new int[3, 7], row, col, 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TacticalEvaluationNeverChangesBoard(int piece)
    {
        var board = new int[,] { { 1, 1, 0, 1, 1 } };
        var before = board.Cast<int>().ToArray();
        Assert.Equal(((int Row, int Column)?)(0, 2), CaroEngine.FindBestMove(board, piece));
        Assert.Equal(before, board.Cast<int>().ToArray());
    }
}
