using System.Diagnostics;

namespace OnlineChessGame.Chess;

public sealed class StockfishClient : IDisposable
{
    private readonly string executablePath;
    private readonly ChessDifficulty difficulty;
    private readonly SemaphoreSlim commandLock = new(1, 1);
    private Process? process;
    private bool initialized;

    public StockfishClient(string executablePath, ChessDifficulty difficulty)
    {
        this.executablePath = executablePath;
        this.difficulty = difficulty;
    }

    public static string? FindExecutable(string? baseDirectory = null)
    {
        string? configuredPath = Environment.GetEnvironmentVariable("STOCKFISH_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return Path.GetFullPath(configuredPath);

        string root = baseDirectory ?? AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(root, "Engines", "Stockfish", "stockfish.exe"),
            Path.Combine(root, "stockfish.exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized && process is { HasExited: false })
            return;

        if (!File.Exists(executablePath))
            throw new FileNotFoundException("Không tìm thấy Stockfish.", executablePath);

        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        if (!process.Start())
            throw new InvalidOperationException("Không thể khởi động Stockfish.");

        process.BeginErrorReadLine();
        await SendAsync("uci", cancellationToken);
        await ReadUntilAsync(line => line == "uciok", cancellationToken);
        await SendAsync($"setoption name Skill Level value {difficulty.SkillLevel}", cancellationToken);
        await SendAsync("setoption name Threads value 1", cancellationToken);
        await SendAsync("setoption name Hash value 64", cancellationToken);
        await SendAsync("isready", cancellationToken);
        await ReadUntilAsync(line => line == "readyok", cancellationToken);
        initialized = true;
    }

    public async Task<string> GetBestMoveAsync(string fen, CancellationToken cancellationToken = default)
    {
        await commandLock.WaitAsync(cancellationToken);
        try
        {
            await InitializeAsync(cancellationToken);
            await SendAsync($"position fen {fen}", cancellationToken);
            await SendAsync($"go movetime {difficulty.MoveTimeMilliseconds}", cancellationToken);

            string line = await ReadUntilAsync(
                value => value.StartsWith("bestmove ", StringComparison.Ordinal),
                cancellationToken);

            string move = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1)
                ?? throw new InvalidOperationException("Stockfish không trả về nước đi.");

            if (move is "(none)" or "0000")
                throw new InvalidOperationException("Stockfish báo không còn nước đi hợp lệ.");

            return move.ToLowerInvariant();
        }
        finally
        {
            commandLock.Release();
        }
    }

    private async Task SendAsync(string command, CancellationToken cancellationToken)
    {
        if (process is null || process.HasExited)
            throw new InvalidOperationException("Tiến trình Stockfish chưa chạy hoặc đã dừng.");

        cancellationToken.ThrowIfCancellationRequested();
        await process.StandardInput.WriteLineAsync(command.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private async Task<string> ReadUntilAsync(Func<string, bool> predicate, CancellationToken cancellationToken)
    {
        if (process is null)
            throw new InvalidOperationException("Tiến trình Stockfish chưa được khởi tạo.");

        while (true)
        {
            string? line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new InvalidOperationException("Stockfish đã đóng luồng kết quả ngoài dự kiến.");
            if (predicate(line.Trim()))
                return line.Trim();
        }
    }

    public void Dispose()
    {
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.WriteLine("quit");
                    if (!process.WaitForExit(1000))
                        process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Đóng form không được thất bại chỉ vì engine đã tự thoát.
            }
            process.Dispose();
        }
    }
}
