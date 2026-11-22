using Chess;
using OnlineChessGame.Chess;
using Xunit;

namespace ChessTests;

public sealed class ChessGameStateTests
{
    [Theory]
    [InlineData("easy")]
    [InlineData("medium")]
    [InlineData("hard")]
    public void LocalEngineCanReplyWithoutExecutable(string level)
    {
        var game = new ChessGameState();
        Apply(game, "f2f3", "e7e5", "g2g4");
        var difficulty = ChessDifficulty.All.Single(d => d.Id == level);
        string reply = LocalChessEngine.ChooseMove(game.Fen, difficulty);
        Assert.Contains(reply, game.GetAllLegalMoves());
        if (difficulty != ChessDifficulty.Easy) Assert.Equal("d8h4", reply);
    }

    [Fact]
    public void InitialPositionHasTwentyLegalMoves()
    {
        var game = new ChessGameState();

        Assert.True(game.WhiteToMove);
        Assert.Equal(20, game.GetAllLegalMoves().Count);
        Assert.Contains("e2e4", game.GetAllLegalMoves());
    }

    [Fact]
    public void FoolsMateIsDetectedAsBlackCheckmate()
    {
        var game = new ChessGameState();

        Apply(game, "f2f3", "e7e5", "g2g4", "d8h4");

        Assert.True(game.IsEndGame);
        Assert.Equal(EndgameType.Checkmate, game.EndgameType);
        Assert.False(game.WhiteWon);
    }

    [Fact]
    public void CastlingMovesKingAndRookAndUpdatesFen()
    {
        var game = new ChessGameState();

        Apply(game, "e2e4", "e7e5", "g1f3", "b8c6", "f1e2", "g8f6", "e1g1");

        Assert.StartsWith("r1bqkb1r/pppp1ppp/2n2n2/4p3/4P3/5N2/PPPPBPPP/RNBQ1RK1", game.Fen);
    }

    [Fact]
    public void EnPassantRemovesCapturedPawn()
    {
        var game = new ChessGameState();

        Apply(game, "e2e4", "a7a6", "e4e5", "d7d5", "e5d6");

        Assert.StartsWith("rnbqkbnr/1pp1pppp/p2P4/8/8/8/PPPP1PPP/RNBQKBNR", game.Fen);
    }

    [Theory]
    [InlineData("a7a8q", "Q3k3")]
    [InlineData("a7a8n", "N3k3")]
    public void PawnCanPromoteToSelectedPiece(string move, string expectedFirstRank)
    {
        var game = new ChessGameState("4k3/P7/8/8/8/8/8/4K3 w - - 0 1");

        Assert.True(game.TryApplyMove(move, out string error), error);

        Assert.StartsWith(expectedFirstRank, game.Fen);
    }

    private static void Apply(ChessGameState game, params string[] moves)
    {
        foreach (string move in moves)
            Assert.True(game.TryApplyMove(move, out string error), error);
    }
}
