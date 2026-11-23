using Chess;
using OnlineChessGame.Chess;
using OnlineChessGame.Network;

namespace OnlineChessGame
{
    public partial class ChessBoardForm : Form
    {
        // === Hằng số kích thước bàn cờ ===
        private const int BOARD_SIZE = 8;
        private const int CELL = 68;
        private const int MARGIN = 36;
        private const int BOARD_PX = BOARD_SIZE * CELL + MARGIN * 2; // 616 px

        // === Trạng thái ván cờ ===
        // 0: trống
        // 1..6: Trắng (1=Tốt, 2=Mã, 3=Tượng, 4=Xe, 5=Hậu, 6=Vua)
        // -1..-6: Đen (-1=Tốt, -2=Mã, -3=Tượng, -4=Xe, -5=Hậu, -6=Vua)
        private int[,] board = new int[BOARD_SIZE, BOARD_SIZE];
        private (int r, int c)? selectedSquare = null;
        private List<(int r, int c)> validMoves = new();
        private (int r, int c)? lastMoveFrom = null;
        private (int r, int c)? lastMoveTo = null;

        private bool isWhiteTurn = true; // Trắng đi trước
        private bool gameOver = false;
        private bool isSolo = true;
        private bool isFirstPlayer = true;
        private bool onlineDetached;
        private bool botThinking;
        private bool engineWarningShown;
        private ChessGameState chessState = new();
        private MatchState? onlineState;
        private long clockSampleAt;
        private ChessDifficulty difficulty = ChessDifficulty.Medium;
        private StockfishClient? stockfish;
        private bool useLocalEngine;
        private readonly CancellationTokenSource lifetimeCancellation = new();
        private bool engineResourcesDisposed;
        private IReadOnlyList<string> selectedLegalMoves = Array.Empty<string>();

        // Thông tin người chơi
        private string p1Name = "Kỳ thủ VN";
        private string p2Name = "Computer Chess";
        private int p1Elo = 1200;
        private int p2Elo = 1250;
        private List<int> p1Captured = new();
        private List<int> p2Captured = new();

        // Đồng hồ đếm ngược
        private int p1Time = 600;
        private int p2Time = 600;
        private System.Windows.Forms.Timer? gameTimer;

        // === EVENT: Network Hooks sẵn sàng nối Socket TCP ===
        public event Action<int, int, int, int>? OnChessMoveMade;
        public event Action<string>? OnChessUciMoveMade;
        public event Action<string>? OnChatSent;
        public event Action? OnSurrender;
        public event Action? OnDrawRequest;

        /// <summary>
        /// Constructor không tham số bắt buộc cho Visual Studio Designer
        /// </summary>
        public ChessBoardForm()
        {
            InitializeComponent();
            EnableDoubleBuffering(boardPanel);
            SetupInitialBoard();
            ApplyCurrentTheme();
            Disposed += (_, _) =>
            {
                _ = LeaveOnlineGameAsync();
                gameTimer?.Dispose();
                DisposeEngineResources();
            };
        }

        public ChessBoardForm(
            string p1,
            int elo1,
            string p2,
            int elo2,
            bool isSolo,
            ChessDifficulty? difficulty = null,
            bool isFirstPlayer = true) : this()
        {
            this.p1Name = p1;
            this.p1Elo = elo1;
            this.p2Name = p2;
            this.p2Elo = elo2;
            this.isSolo = isSolo;
            this.isFirstPlayer = isFirstPlayer;
            this.difficulty = difficulty ?? ChessDifficulty.Medium;

            UpdatePlayerHeaders();
            if (isSolo) StartGameTimer();
            else
            {
                lblTimerPlayer.Text = lblTimerOpp.Text = "--:--";
                AttachOnlineGame();
            }
        }

        private async void ChessBoardForm_Load(object? sender, EventArgs e)
        {
            UpdatePlayerHeaders();
            ApplyCardRegions();
            AddChatMessage("He thong", "Tran dau Co Vua da bat dau. Chuc 2 ky thu thi dau tot!");
            if (isSolo)
                await EnsureStockfishReadyAsync(showError: true);
            else if (GameClient.Instance.CurrentMatchState is { } state)
                ApplyOnlineState(state, "");
        }

        private static void EnableDoubleBuffering(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        private void SetupInitialBoard()
        {
            SyncBoardFromFen();
            gameOver = false;
        }

        private void UpdatePlayerHeaders()
        {
            string init1 = string.IsNullOrEmpty(p1Name) ? "P" : p1Name[..1].ToUpper();
            string init2 = string.IsNullOrEmpty(p2Name) ? "C" : p2Name[..1].ToUpper();

            lblAvatarPlayer.Text = init1;
            lblNamePlayer.Text = $"{p1Name} [{(isFirstPlayer ? "Quan Trang" : "Quan Den")}]";
            lblEloPlayer.Text = $"Elo: {p1Elo}";

            lblAvatarOpp.Text = init2;
            lblNameOpp.Text = $"{p2Name} [{(isFirstPlayer ? "Quan Den" : "Quan Trang")}]";
            lblEloOpp.Text = $"Elo: {p2Elo}";

            UpdateCapturedDisplays();
            UpdateTurnBanner();
        }

        private void ApplyCardRegions()
        {
            boardPanel.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, boardPanel.Width, boardPanel.Height), 12));
            cardOpp.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, cardOpp.Width, cardOpp.Height), 10));
            cardPlayer.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, cardPlayer.Width, cardPlayer.Height), 10));
            lblTurn.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, lblTurn.Width, lblTurn.Height), 8));

            lblAvatarOpp.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, lblAvatarOpp.Width, lblAvatarOpp.Height), 23));
            lblAvatarPlayer.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, lblAvatarPlayer.Width, lblAvatarPlayer.Height), 23));

            pnlTimerOpp.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, pnlTimerOpp.Width, pnlTimerOpp.Height), 6));
            pnlTimerPlayer.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, pnlTimerPlayer.Width, pnlTimerPlayer.Height), 6));

            btnDraw.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnDraw.Width, btnDraw.Height), 8));
            btnSurr.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnSurr.Width, btnSurr.Height), 8));
            btnLeave.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnLeave.Width, btnLeave.Height), 8));
            btnThemeToggle.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnThemeToggle.Width, btnThemeToggle.Height), 8));
            btnSend.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnSend.Width, btnSend.Height), 6));
        }

        private void ApplyCurrentTheme()
        {
            BackColor = ThemeColors.Background;
            boardPanel.BackColor = ThemeColors.Surface;

            cardOpp.BackColor = ThemeColors.Surface;
            lblNameOpp.ForeColor = ThemeColors.TextPrimary;
            lblEloOpp.ForeColor = ThemeColors.TextSecondary;
            lblCapturedOpp.ForeColor = ThemeColors.TextSecondary;
            pnlTimerOpp.BackColor = ThemeColors.SurfaceAlt;
            lblTimerOpp.ForeColor = ThemeColors.TextPrimary;

            cardPlayer.BackColor = ThemeColors.Surface;
            lblNamePlayer.ForeColor = ThemeColors.TextPrimary;
            lblEloPlayer.ForeColor = ThemeColors.TextSecondary;
            lblCapturedPlayer.ForeColor = ThemeColors.TextSecondary;
            pnlTimerPlayer.BackColor = ThemeColors.SurfaceAlt;
            lblTimerPlayer.ForeColor = ThemeColors.TextPrimary;

            lblAvatarPlayer.BackColor = ThemeColors.Primary;
            lblAvatarPlayer.ForeColor = ThemeColors.TextOnPrimary;

            lblChatTitle.ForeColor = ThemeColors.TextPrimary;
            rtbChat.BackColor = ThemeColors.SurfaceAlt;
            rtbChat.ForeColor = ThemeColors.TextPrimary;

            txtChat.BackColor = ThemeColors.InputBg;
            txtChat.ForeColor = ThemeColors.InputFg;

            btnLeave.BackColor = ThemeColors.Primary;
            btnLeave.ForeColor = ThemeColors.TextOnPrimary;
            btnSend.BackColor = ThemeColors.Primary;
            btnSend.ForeColor = ThemeColors.TextOnPrimary;

            btnThemeToggle.Text = ThemeColors.CurrentTheme == AppTheme.Light ? "Theme: Sang" : "Theme: Den & Xanh";
            btnThemeToggle.BackColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(224, 238, 255) : Color.FromArgb(6, 78, 59);
            btnThemeToggle.ForeColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(37, 99, 235) : Color.FromArgb(52, 211, 153);

            UpdateTurnBanner();
            boardPanel.Invalidate();
        }

        private void StartGameTimer()
        {
            gameTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            gameTimer.Tick += (s, e) =>
            {
                if (gameOver) return;

                if (isWhiteTurn)
                {
                    if (p1Time > 0) p1Time--;
                    lblTimerPlayer.Text = $"{p1Time / 60:D2}:{p1Time % 60:D2}";
                    if (p1Time <= 0) EndGame("Hết giờ! Quân Đen đã thắng.");
                }
                else
                {
                    if (p2Time > 0) p2Time--;
                    lblTimerOpp.Text = $"{p2Time / 60:D2}:{p2Time % 60:D2}";
                    if (p2Time <= 0) EndGame("Hết giờ! Quân Trắng đã thắng.");
                }
            };
            gameTimer.Start();
        }

        private void BoardPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var borderPen = new Pen(ThemeColors.Border, 1.5f);
            g.DrawPath(borderPen, UIHelper.RoundedRect(new Rectangle(0, 0, BOARD_PX - 1, BOARD_PX - 1), 12));

            // Tọa độ File a..h và Rank 1..8
            using var coordBrush = new SolidBrush(ThemeColors.ChessCoordText);
            for (int i = 0; i < BOARD_SIZE; i++)
            {
                char fileChar = (char)('a' + i);
                int cx = MARGIN + i * CELL + CELL / 2;
                var sizeF = g.MeasureString(fileChar.ToString(), ThemeColors.FontBoardCoord);
                g.DrawString(fileChar.ToString(), ThemeColors.FontBoardCoord, coordBrush, cx - sizeF.Width / 2, 10);
                g.DrawString(fileChar.ToString(), ThemeColors.FontBoardCoord, coordBrush, cx - sizeF.Width / 2, BOARD_PX - 24);

                string rankStr = (8 - i).ToString();
                int cy = MARGIN + i * CELL + CELL / 2;
                var sizeR = g.MeasureString(rankStr, ThemeColors.FontBoardCoord);
                g.DrawString(rankStr, ThemeColors.FontBoardCoord, coordBrush, 12 - sizeR.Width / 2, cy - sizeR.Height / 2);
                g.DrawString(rankStr, ThemeColors.FontBoardCoord, coordBrush, BOARD_PX - 22, cy - sizeR.Height / 2);
            }

            // Vẽ các ô cờ so le 8x8
            using var brushLight = new SolidBrush(ThemeColors.ChessSquareLight);
            using var brushDark = new SolidBrush(ThemeColors.ChessSquareDark);
            using var brushHighlight = new SolidBrush(Color.FromArgb(160, ThemeColors.ChessHighlight));
            using var brushLastMove = new SolidBrush(Color.FromArgb(90, ThemeColors.Accent));

            for (int r = 0; r < BOARD_SIZE; r++)
            {
                for (int c = 0; c < BOARD_SIZE; c++)
                {
                    int x = MARGIN + c * CELL;
                    int y = MARGIN + r * CELL;
                    var squareRect = new Rectangle(x, y, CELL, CELL);

                    bool isLight = (r + c) % 2 == 0;
                    g.FillRectangle(isLight ? brushLight : brushDark, squareRect);

                    if ((lastMoveFrom.HasValue && lastMoveFrom.Value.r == r && lastMoveFrom.Value.c == c) ||
                        (lastMoveTo.HasValue && lastMoveTo.Value.r == r && lastMoveTo.Value.c == c))
                    {
                        g.FillRectangle(brushLastMove, squareRect);
                    }

                    if (selectedSquare.HasValue && selectedSquare.Value.r == r && selectedSquare.Value.c == c)
                    {
                        g.FillRectangle(brushHighlight, squareRect);
                        using var penSel = new Pen(ThemeColors.ChessHighlight, 3f);
                        g.DrawRectangle(penSel, x + 1, y + 1, CELL - 2, CELL - 2);
                    }

                    if (validMoves.Contains((r, c)))
                    {
                        int dotSize = board[r, c] != 0 ? CELL - 12 : 20;
                        int dx = x + (CELL - dotSize) / 2;
                        int dy = y + (CELL - dotSize) / 2;

                        if (board[r, c] != 0)
                        {
                            using var penTarget = new Pen(ThemeColors.ChessTargetDot, 3.5f);
                            g.DrawEllipse(penTarget, dx, dy, dotSize, dotSize);
                        }
                        else
                        {
                            using var brushTarget = new SolidBrush(ThemeColors.ChessTargetDot);
                            g.FillEllipse(brushTarget, dx, dy, dotSize, dotSize);
                        }
                    }

                    int piece = board[r, c];
                    if (piece != 0)
                    {
                        DrawChessPiece(g, piece, squareRect);
                    }
                }
            }
        }

        private void DrawChessPiece(Graphics g, int piece, Rectangle squareRect)
        {
            string symbol = GetPieceSymbol(piece);
            bool isWhite = piece > 0;

            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            using var shadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
            var shadowRect = new Rectangle(squareRect.X + 2, squareRect.Y + 3, squareRect.Width, squareRect.Height);
            g.DrawString(symbol, ThemeColors.FontChessPiece, shadowBrush, shadowRect, sf);

            Color pieceColor = isWhite ? ThemeColors.ChessPieceWhiteFill : ThemeColors.ChessPieceBlackFill;
            using var pieceBrush = new SolidBrush(pieceColor);
            g.DrawString(symbol, ThemeColors.FontChessPiece, pieceBrush, squareRect, sf);

            Color outlineColor = isWhite ? ThemeColors.ChessPieceWhiteStroke : ThemeColors.ChessPieceBlackStroke;
            using var outlinePen = new Pen(outlineColor, 1.2f);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddString(symbol, ThemeColors.FontChessPiece.FontFamily, (int)FontStyle.Bold, 38F, squareRect, sf);
            g.DrawPath(outlinePen, path);
        }

        private string GetPieceSymbol(int piece)
        {
            return Math.Abs(piece) switch
            {
                6 => "♚",
                5 => "♛",
                4 => "♜",
                3 => "♝",
                2 => "♞",
                1 => "♟",
                _ => ""
            };
        }

        private void BoardPanel_Click(object? sender, MouseEventArgs e)
        {
            if (gameOver || botThinking || !isSolo && (onlineState is null || GameClient.Instance.IsRecovering)) return;
            if (isSolo ? !isWhiteTurn : isWhiteTurn != isFirstPlayer) return;

            int col = (e.X - MARGIN) / CELL;
            int row = (e.Y - MARGIN) / CELL;

            if (row < 0 || row >= BOARD_SIZE || col < 0 || col >= BOARD_SIZE) return;

            if (selectedSquare.HasValue && validMoves.Contains((row, col)))
            {
                ExecuteMove(selectedSquare.Value.r, selectedSquare.Value.c, row, col);
                return;
            }

            int piece = board[row, col];
            bool isMyPiece = isWhiteTurn ? (piece > 0) : (piece < 0);

            if (isMyPiece)
            {
                selectedSquare = (row, col);
                selectedLegalMoves = chessState.GetLegalMovesFrom(row, col);
                validMoves = selectedLegalMoves
                    .Select(move => ChessGameState.SquareToCoordinates(move.AsSpan(2, 2)))
                    .Select(position => (position.Row, position.Column))
                    .Distinct()
                    .ToList();
                boardPanel.Invalidate();
            }
            else
            {
                selectedSquare = null;
                selectedLegalMoves = Array.Empty<string>();
                validMoves.Clear();
                boardPanel.Invalidate();
            }
        }

        private void ExecuteMove(int fr, int fc, int tr, int tc)
        {
            string destination = ChessGameState.CoordinatesToSquare(tr, tc);
            var candidates = selectedLegalMoves
                .Where(move => move.AsSpan(2, 2).SequenceEqual(destination))
                .ToArray();

            if (candidates.Length == 0)
                candidates = chessState.GetLegalMovesFrom(fr, fc)
                    .Where(move => move.AsSpan(2, 2).SequenceEqual(destination))
                    .ToArray();

            string? uciMove = candidates.FirstOrDefault();
            if (candidates.Length > 1 && candidates.Any(move => move.Length == 5))
            {
                using var dialog = new PromotionDialog();
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedPiece is null)
                    return;
                uciMove = candidates.FirstOrDefault(move => move.Length == 5 && move[4] == dialog.SelectedPiece);
            }

            if (uciMove is null) return;
            if (isSolo) ApplyUciMove(uciMove, notifyNetwork: true);
            else _ = SendOnlineAsync(PacketType.MOVE, uciMove);
        }

        private async Task BotChessMoveAsync()
        {
            if (gameOver || isWhiteTurn || botThinking)
                return;

            botThinking = true;
            UpdateTurnBanner();
            try
            {
                if (!await EnsureStockfishReadyAsync(showError: true))
                    return;

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                string fen = chessState.Fen;
                string bestMove = useLocalEngine
                    ? await Task.Run(() => LocalChessEngine.ChooseMove(fen, difficulty, timeout.Token), timeout.Token)
                    : await stockfish!.GetBestMoveAsync(fen, timeout.Token);
                if (gameOver || lifetimeCancellation.IsCancellationRequested) return;
                ApplyUciMove(bestMove, notifyNetwork: false);
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                // Form đang đóng hoặc người dùng đã rời ván.
            }
            catch (OperationCanceledException)
            {
                ShowEngineError("Stockfish đã suy nghĩ quá thời gian cho phép.");
            }
            catch (Exception exception)
            {
                ShowEngineError($"Stockfish không thể tính nước đi: {exception.Message}");
            }
            finally
            {
                botThinking = false;
                if (!gameOver && !IsDisposed && !Disposing)
                    UpdateTurnBanner();
            }
        }

        private void ApplyUciMove(string uciMove, bool notifyNetwork)
        {
            if (!chessState.TryApplyMove(uciMove, out string error))
            {
                MessageBox.Show(error, "Nước đi không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var from = ChessGameState.SquareToCoordinates(uciMove.AsSpan(0, 2));
            var to = ChessGameState.SquareToCoordinates(uciMove.AsSpan(2, 2));
            lastMoveFrom = (from.Row, from.Column);
            lastMoveTo = (to.Row, to.Column);
            selectedSquare = null;
            selectedLegalMoves = Array.Empty<string>();
            validMoves.Clear();

            SyncBoardFromFen();
            boardPanel.Invalidate();

            if (notifyNetwork)
            {
                OnChessMoveMade?.Invoke(from.Row, from.Column, to.Row, to.Column);
                OnChessUciMoveMade?.Invoke(uciMove);
            }

            if (HandleChessEndGame())
                return;

            UpdateTurnBanner();
            if (isSolo && !isWhiteTurn)
                _ = BotChessMoveAsync();
        }

        private void SyncBoardFromFen()
        {
            Array.Clear(board);
            string[] fields = chessState.Fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] ranks = fields[0].Split('/');

            for (int row = 0; row < BOARD_SIZE; row++)
            {
                int column = 0;
                foreach (char symbol in ranks[row])
                {
                    if (char.IsDigit(symbol))
                    {
                        column += symbol - '0';
                        continue;
                    }

                    int piece = char.ToLowerInvariant(symbol) switch
                    {
                        'p' => 1,
                        'n' => 2,
                        'b' => 3,
                        'r' => 4,
                        'q' => 5,
                        'k' => 6,
                        _ => 0
                    };
                    board[row, column++] = char.IsUpper(symbol) ? piece : -piece;
                }
            }

            isWhiteTurn = chessState.WhiteToMove;
            RebuildCapturedPieces();
        }

        private void RebuildCapturedPieces()
        {
            p1Captured.Clear();
            p2Captured.Clear();
            p1Captured.AddRange(chessState.CapturedBlackPieces.Select(piece => -FenPieceToValue(piece)));
            p2Captured.AddRange(chessState.CapturedWhitePieces.Select(FenPieceToValue));
            UpdateCapturedDisplays();
        }

        private static int FenPieceToValue(char piece) => char.ToLowerInvariant(piece) switch
        {
            'p' => 1,
            'n' => 2,
            'b' => 3,
            'r' => 4,
            'q' => 5,
            'k' => 6,
            _ => 0
        };

        private bool HandleChessEndGame()
        {
            if (!chessState.IsEndGame || chessState.EndgameType is null)
                return false;

            string message = chessState.EndgameType.Value switch
            {
                EndgameType.Checkmate => chessState.WhiteWon == isFirstPlayer
                    ? $"Chiếu bí! {p1Name} chiến thắng."
                    : $"Chiếu bí! {p2Name} chiến thắng.",
                EndgameType.Stalemate => "Hòa cờ do bên đến lượt không còn nước đi hợp lệ.",
                EndgameType.InsufficientMaterial => "Hòa cờ do không đủ lực lượng để chiếu bí.",
                EndgameType.FiftyMoveRule => "Hòa cờ theo luật 50 nước.",
                EndgameType.Repetition => "Hòa cờ do thế cờ lặp lại ba lần.",
                EndgameType.DrawDeclared => "Hai bên đã đồng ý hòa.",
                _ => "Ván cờ đã kết thúc."
            };
            EndGame(message);
            return true;
        }

        private async Task<bool> EnsureStockfishReadyAsync(bool showError)
        {
            if (!isSolo)
                return false;

            if (useLocalEngine || stockfish is not null)
                return true;

            string? executable = StockfishClient.FindExecutable();
            if (executable is null)
            {
                useLocalEngine = true;
                AddChatMessage("He thong", $"Máy chơi cục bộ ở mức {difficulty.DisplayName}.");
                return true;
            }

            try
            {
                stockfish = new StockfishClient(executable, difficulty);
                await stockfish.InitializeAsync(lifetimeCancellation.Token);
                AddChatMessage("He thong", $"Stockfish đã sẵn sàng ở mức {difficulty.DisplayName}.");
                return true;
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                stockfish?.Dispose();
                stockfish = null;
                return false;
            }
            catch (Exception exception)
            {
                stockfish?.Dispose();
                stockfish = null;
                if (showError)
                    ShowEngineError($"Không thể khởi động Stockfish: {exception.Message}");
                return false;
            }
        }

        private void ShowEngineError(string message)
        {
            if (engineWarningShown)
                return;
            engineWarningShown = true;
            AddChatMessage("He thong", message);
            MessageBox.Show(message, "Lỗi Stockfish", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void UpdateTurnBanner()
        {
            if (botThinking)
            {
                lblTurn.Text = $"STOCKFISH ({difficulty.DisplayName.ToUpperInvariant()}) ĐANG SUY NGHĨ...";
                lblTurn.BackColor = ThemeColors.SurfaceAlt;
                lblTurn.ForeColor = ThemeColors.Accent;
                return;
            }

            if (isWhiteTurn)
            {
                lblTurn.Text = chessState.InCheck ? "LƯỢT CỦA TRẮNG — ĐANG BỊ CHIẾU" : $"LƯỢT CỦA TRẮNG ({(isFirstPlayer ? "BẠN" : p2Name.ToUpperInvariant())})";
                lblTurn.BackColor = ThemeColors.PrimaryLight;
                lblTurn.ForeColor = ThemeColors.Primary;
            }
            else
            {
                lblTurn.Text = chessState.InCheck
                    ? $"LƯỢT CỦA ĐEN ({(isFirstPlayer ? p2Name.ToUpperInvariant() : "BẠN")}) — ĐANG BỊ CHIẾU"
                    : $"LƯỢT CỦA ĐEN ({(isFirstPlayer ? p2Name.ToUpperInvariant() : "BẠN")})";
                lblTurn.BackColor = ThemeColors.SurfaceAlt;
                lblTurn.ForeColor = ThemeColors.TextSecondary;
            }
        }

        private void UpdateCapturedDisplays()
        {
            string p1Text = string.Join(" ", p1Captured.Select(GetPieceSymbol));
            string p2Text = string.Join(" ", p2Captured.Select(GetPieceSymbol));
            if (!isFirstPlayer) (p1Text, p2Text) = (p2Text, p1Text);
            lblCapturedPlayer.Text = "Bat: " + (string.IsNullOrEmpty(p1Text) ? "(chua co)" : p1Text);
            lblCapturedOpp.Text = "Bat: " + (string.IsNullOrEmpty(p2Text) ? "(chua co)" : p2Text);
        }

        private void EndGame(string message)
        {
            if (gameOver) return;
            gameOver = true;
            gameTimer?.Stop();

            lblTurn.Text = message;
            lblTurn.BackColor = ThemeColors.Success;
            lblTurn.ForeColor = Color.White;

            AddChatMessage("He thong", message);
            MessageBox.Show(message, "Ket thuc van co Vua", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void SendChat()
        {
            string msg = txtChat.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            AddChatMessage(p1Name, msg);
            OnChatSent?.Invoke(msg);
            txtChat.Clear();
        }

        public void AddChatMessage(string sender, string message)
        {
            rtbChat.SelectionFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            rtbChat.SelectionColor = sender == "He thong" ? ThemeColors.Accent : ThemeColors.Primary;
            rtbChat.AppendText($"{sender}: ");

            rtbChat.SelectionFont = ThemeColors.FontChat;
            rtbChat.SelectionColor = ThemeColors.TextPrimary;
            rtbChat.AppendText($"{message}\n");
            rtbChat.ScrollToCaret();
        }

        private void BtnThemeToggle_Click(object? sender, EventArgs e)
        {
            ThemeColors.ToggleTheme();
            ApplyCurrentTheme();
        }

        private void BtnDraw_Click(object? sender, EventArgs e)
        {
            if (isSolo || gameOver) return;
            OnDrawRequest?.Invoke();
            AddChatMessage("He thong", "Ban da gui loi de nghi cau hoa.");
        }

        private void BtnSurr_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show("Ban co chac chan muon dau hang van dau nay?", "Xac nhan",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                
                OnSurrender?.Invoke();
                if (isSolo) EndGame("Bạn đã đầu hàng! Đối thủ chiến thắng.");
            }
        }

        private async void BtnLeave_Click(object? sender, EventArgs e)
        {
            await LeaveOnlineGameAsync();
            gameTimer?.Stop();
            DisposeEngineResources();
            Program.Context.SwitchTo(new DashboardForm(p1Name, p1Elo));
        }

        private void BtnSend_Click(object? sender, EventArgs e)
        {
            SendChat();
        }

        private void TxtChat_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SendChat();
                e.SuppressKeyPress = true;
            }
        }

        public void ReceiveOpponentMove(int fr, int fc, int tr, int tc)
        {
            if (gameOver || (isSolo ? isWhiteTurn : isWhiteTurn == isFirstPlayer)) return;
            string destination = ChessGameState.CoordinatesToSquare(tr, tc);
            string? uciMove = chessState.GetLegalMovesFrom(fr, fc)
                .Where(move => move.AsSpan(2, 2).SequenceEqual(destination))
                .OrderByDescending(move => move.EndsWith('q'))
                .FirstOrDefault();
            if (uciMove is not null)
                ApplyUciMove(uciMove, notifyNetwork: false);
        }

        public void ReceiveOpponentMove(string uciMove)
        {
            if (gameOver || (isSolo ? isWhiteTurn : isWhiteTurn == isFirstPlayer)) return;
            ApplyUciMove(uciMove, notifyNetwork: false);
        }

        private void AttachOnlineGame()
        {
            OnChessUciMoveMade += move => _ = SendOnlineAsync(PacketType.MOVE, move);
            OnChatSent += message => _ = SendOnlineAsync(PacketType.CHAT, message);
            OnSurrender += () => _ = SendOnlineAsync(PacketType.SURRENDER);
            OnDrawRequest += () => _ = SendOnlineAsync(PacketType.DRAW_REQUEST);
            GameClient.Instance.OnMoveReceived += OnlineMoveReceived;
            GameClient.Instance.OnChatReceived += OnlineChatReceived;
            GameClient.Instance.OnSurrenderReceived += OnlineSurrenderReceived;
            GameClient.Instance.OnDrawRequestReceived += OnlineDrawRequestReceived;
            GameClient.Instance.OnDrawResponseReceived += OnlineDrawResponseReceived;
            GameClient.Instance.OnOpponentLeft += OnlineOpponentLeft;
            GameClient.Instance.OnDisconnected += OnlineDisconnected;
            GameClient.Instance.OnMatchState += OnlineStateReceived;
            GameClient.Instance.OnRecovering += OnlineRecovering;
            gameTimer = new System.Windows.Forms.Timer { Interval = 250 };
            gameTimer.Tick += (_, _) => UpdateOnlineClocks();
            gameTimer.Start();
        }

        private async Task SendOnlineAsync(PacketType type, string payload = "")
        {
            try { await GameClient.Instance.SendAsync(new GamePacket(type, GameMode: "CHESS", Payload: payload,
                ExpectedRevision: onlineState?.Revision)); }
            catch (IOException) { RunOnUi(() => AddChatMessage("He thong", "Mất kết nối game server.")); }
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() => { if (!IsDisposed) action(); });
        }

        private void OnlineMoveReceived(string move) => RunOnUi(() => ReceiveOpponentMove(move));
        private void OnlineStateReceived(MatchState state, string message) => RunOnUi(() => ApplyOnlineState(state, message));
        private void OnlineRecovering() => RunOnUi(() => lblTurn.Text = "Đang chuyển worker…");

        private void ApplyOnlineState(MatchState state, string message)
        {
            if (onlineState is { } old && (state.Generation < old.Generation || state.Revision < old.Revision)) return;
            if (onlineState?.Moves != state.Moves)
            {
                var restored = new ChessGameState();
                foreach (var move in state.Moves.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!restored.TryApplyMove(move, out _)) { EndGame("Không thể đồng bộ bàn cờ."); return; }
                    lastMoveFrom = ChessGameState.SquareToCoordinates(move.AsSpan(0, 2));
                    lastMoveTo = ChessGameState.SquareToCoordinates(move.AsSpan(2, 2));
                }
                chessState = restored;
                selectedSquare = null;
                selectedLegalMoves = Array.Empty<string>();
                validMoves.Clear();
                SyncBoardFromFen();
                boardPanel.Invalidate();
            }
            onlineState = state;
            clockSampleAt = System.Diagnostics.Stopwatch.GetTimestamp();
            UpdateOnlineClocks();
            if (message.Length > 0) AddChatMessage("Hệ thống", message);
            if (state.Result is { } result)
                EndGame($"{(result == "DRAW" ? "Hòa" : result == "ABORTED" ? "Trận đã hủy" : result == (isFirstPlayer ? "FIRST" : "SECOND") ? "Bạn thắng" : "Bạn thua")} · {state.Reason}");
            else if (GameClient.Instance.IsRecovering) lblTurn.Text = "Đang chuyển worker…";
            else UpdateTurnBanner();
        }

        private void UpdateOnlineClocks()
        {
            if (onlineState is not { } state) return;
            long now = state.ObservedAt + (long)System.Diagnostics.Stopwatch.GetElapsedTime(clockSampleAt).TotalMilliseconds;
            lblTimerPlayer.Text = TimeSpan.FromSeconds((state.Remaining(isFirstPlayer ? 1 : 2, now) + 999) / 1000).ToString(@"mm\:ss");
            lblTimerOpp.Text = TimeSpan.FromSeconds((state.Remaining(isFirstPlayer ? 2 : 1, now) + 999) / 1000).ToString(@"mm\:ss");
        }
        private void OnlineChatReceived(string sender, string message) => RunOnUi(() => AddChatMessage(sender, message));
        private void OnlineSurrenderReceived(string _) => RunOnUi(() => EndGame($"{p2Name} đã đầu hàng. Bạn chiến thắng!"));
        private void OnlineDrawRequestReceived(string requester) => RunOnUi(() =>
        {
            bool accept = MessageBox.Show($"{p2Name} đề nghị hòa. Bạn đồng ý?", "Đề nghị hòa",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            _ = SendOnlineAsync(PacketType.DRAW_RESPONSE, accept ? "ACCEPT" : "DECLINE");
        });
        private void OnlineDrawResponseReceived(bool accept) => RunOnUi(() =>
        {
            if (accept) EndGame("Hai bên đã đồng ý hòa.");
            else AddChatMessage("He thong", "Đối thủ từ chối hòa.");
        });
        private void OnlineOpponentLeft(string message) => RunOnUi(() => EndGame(message));
        private void OnlineDisconnected() => RunOnUi(() => EndGame("Mất kết nối game server."));

        private async Task LeaveOnlineGameAsync()
        {
            if (isSolo || onlineDetached) return;
            onlineDetached = true;
            GameClient.Instance.OnMoveReceived -= OnlineMoveReceived;
            GameClient.Instance.OnChatReceived -= OnlineChatReceived;
            GameClient.Instance.OnSurrenderReceived -= OnlineSurrenderReceived;
            GameClient.Instance.OnDrawRequestReceived -= OnlineDrawRequestReceived;
            GameClient.Instance.OnDrawResponseReceived -= OnlineDrawResponseReceived;
            GameClient.Instance.OnOpponentLeft -= OnlineOpponentLeft;
            GameClient.Instance.OnDisconnected -= OnlineDisconnected;
            GameClient.Instance.OnMatchState -= OnlineStateReceived;
            GameClient.Instance.OnRecovering -= OnlineRecovering;
            if (!string.IsNullOrEmpty(GameClient.Instance.CurrentRoomId))
            {
                try { await SendOnlineAsync(PacketType.LEAVE_ROOM).WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (TimeoutException) { GameClient.Instance.Disconnect(); }
                finally { GameClient.Instance.CurrentRoomId = string.Empty; }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _ = LeaveOnlineGameAsync();
            DisposeEngineResources();
            base.OnFormClosed(e);
        }

        private void DisposeEngineResources()
        {
            if (engineResourcesDisposed)
                return;
            engineResourcesDisposed = true;
            lifetimeCancellation.Cancel();
            stockfish?.Dispose();
            lifetimeCancellation.Dispose();
        }
    }
}
