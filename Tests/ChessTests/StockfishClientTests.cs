using OnlineChessGame.Chess;
using Xunit;

namespace ChessTests;

public sealed class StockfishClientTests
{
    [Fact]
    public async Task StockfishReturnsALegalMoveFromInitialPosition()
    {
        string enginePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "Engines", "Stockfish", "stockfish.exe"));

        // CI không bắt buộc tải binary 80 MB; máy phát triển chạy ca này sau setup.
        if (!File.Exists(enginePath))
            return;

        var game = new ChessGameState();
        using var stockfish = new StockfishClient(enginePath, ChessDifficulty.Easy);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await stockfish.InitializeAsync(timeout.Token);
        string move = await stockfish.GetBestMoveAsync(game.Fen, timeout.Token);

        Assert.Contains(move, game.GetAllLegalMoves());
    }
}
