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
        }

        public ChessBoardForm(string p1, int elo1, string p2, int elo2, bool isSolo) : this()
        {
            this.p1Name = p1;
            this.p1Elo = elo1;
            this.p2Name = p2;
            this.p2Elo = elo2;
            this.isSolo = isSolo;

            UpdatePlayerHeaders();
            StartGameTimer();
        }

        private void ChessBoardForm_Load(object? sender, EventArgs e)
        {
            UpdatePlayerHeaders();
            ApplyCardRegions();
            AddChatMessage("He thong", "Tran dau Co Vua da bat dau. Chuc 2 ky thu thi dau tot!");
        }

        private static void EnableDoubleBuffering(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        private void SetupInitialBoard()
        {
            for (int r = 0; r < BOARD_SIZE; r++)
                for (int c = 0; c < BOARD_SIZE; c++)
                    board[r, c] = 0;

            // Đen (Hàng 0 và hàng 1)
            board[0, 0] = -4; board[0, 1] = -2; board[0, 2] = -3; board[0, 3] = -5;
            board[0, 4] = -6; board[0, 5] = -3; board[0, 6] = -2; board[0, 7] = -4;
            for (int c = 0; c < BOARD_SIZE; c++) board[1, c] = -1;

            // Trắng (Hàng 6 và hàng 7)
            for (int c = 0; c < BOARD_SIZE; c++) board[6, c] = 1;
            board[7, 0] = 4; board[7, 1] = 2; board[7, 2] = 3; board[7, 3] = 5;
            board[7, 4] = 6; board[7, 5] = 3; board[7, 6] = 2; board[7, 7] = 4;

            isWhiteTurn = true;
            gameOver = false;
        }

        private void UpdatePlayerHeaders()
        {
            string init1 = string.IsNullOrEmpty(p1Name) ? "P" : p1Name[..1].ToUpper();
            string init2 = string.IsNullOrEmpty(p2Name) ? "C" : p2Name[..1].ToUpper();

            lblAvatarPlayer.Text = init1;
            lblNamePlayer.Text = $"{p1Name} [Quan Trang]";
            lblEloPlayer.Text = $"Elo: {p1Elo}";

            lblAvatarOpp.Text = init2;
            lblNameOpp.Text = $"{p2Name} [Quan Den]";
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
                    if (p1Time <= 0) EndGame("Het gio! Quan Trang da thua.");
                }
                else
                {
                    if (p2Time > 0) p2Time--;
                    lblTimerOpp.Text = $"{p2Time / 60:D2}:{p2Time % 60:D2}";
                    if (p2Time <= 0) EndGame("Het gio! Quan Den da thua.");
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
            if (gameOver) return;
            if (!isWhiteTurn && isSolo) return;

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
                validMoves = ComputeBasicMoves(row, col);
                boardPanel.Invalidate();
            }
            else
            {
                selectedSquare = null;
                validMoves.Clear();
                boardPanel.Invalidate();
            }
        }

        private List<(int r, int c)> ComputeBasicMoves(int r, int c)
        {
            var moves = new List<(int r, int c)>();
            int piece = board[r, c];
            if (piece == 0) return moves;

            bool isWhite = piece > 0;
            int type = Math.Abs(piece);

            switch (type)
            {
                case 1: // Tốt
                    int dir = isWhite ? -1 : 1;
                    int startRow = isWhite ? 6 : 1;

                    if (IsInside(r + dir, c) && board[r + dir, c] == 0)
                    {
                        moves.Add((r + dir, c));
                        if (r == startRow && board[r + 2 * dir, c] == 0)
                            moves.Add((r + 2 * dir, c));
                    }
                    if (IsInside(r + dir, c - 1) && IsEnemy(isWhite, r + dir, c - 1))
                        moves.Add((r + dir, c - 1));
                    if (IsInside(r + dir, c + 1) && IsEnemy(isWhite, r + dir, c + 1))
                        moves.Add((r + dir, c + 1));
                    break;

                case 2: // Mã
                    int[][] knightD = {
                        new[] {-2, -1}, new[] {-2, 1}, new[] {-1, -2}, new[] {-1, 2},
                        new[] {1, -2}, new[] {1, 2}, new[] {2, -1}, new[] {2, 1}
                    };
                    foreach (var d in knightD)
                    {
                        int nr = r + d[0], nc = c + d[1];
                        if (IsInside(nr, nc) && CanLand(isWhite, nr, nc))
                            moves.Add((nr, nc));
                    }
                    break;

                case 3: // Tượng
                    AddLineMoves(moves, isWhite, r, c, new[] { -1, -1 });
                    AddLineMoves(moves, isWhite, r, c, new[] { -1, 1 });
                    AddLineMoves(moves, isWhite, r, c, new[] { 1, -1 });
                    AddLineMoves(moves, isWhite, r, c, new[] { 1, 1 });
                    break;

                case 4: // Xe
                    AddLineMoves(moves, isWhite, r, c, new[] { -1, 0 });
                    AddLineMoves(moves, isWhite, r, c, new[] { 1, 0 });
                    AddLineMoves(moves, isWhite, r, c, new[] { 0, -1 });
                    AddLineMoves(moves, isWhite, r, c, new[] { 0, 1 });
                    break;

                case 5: // Hậu
                    int[][] queenD = {
                        new[] {-1, 0}, new[] {1, 0}, new[] {0, -1}, new[] {0, 1},
                        new[] {-1, -1}, new[] {-1, 1}, new[] {1, -1}, new[] {1, 1}
                    };
                    foreach (var d in queenD) AddLineMoves(moves, isWhite, r, c, d);
                    break;

                case 6: // Vua
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            if (dr == 0 && dc == 0) continue;
                            int nr = r + dr, nc = c + dc;
                            if (IsInside(nr, nc) && CanLand(isWhite, nr, nc))
                                moves.Add((nr, nc));
                        }
                    break;
            }

            return moves;
        }

        private void AddLineMoves(List<(int r, int c)> moves, bool isWhite, int r, int c, int[] dir)
        {
            int nr = r + dir[0], nc = c + dir[1];
            while (IsInside(nr, nc))
            {
                if (board[nr, nc] == 0)
                {
                    moves.Add((nr, nc));
                }
                else
                {
                    if (IsEnemy(isWhite, nr, nc)) moves.Add((nr, nc));
                    break;
                }
                nr += dir[0];
                nc += dir[1];
            }
        }

        private bool IsInside(int r, int c) => r >= 0 && r < BOARD_SIZE && c >= 0 && c < BOARD_SIZE;
        private bool IsEnemy(bool isWhite, int r, int c) => isWhite ? board[r, c] < 0 : board[r, c] > 0;
        private bool CanLand(bool isWhite, int r, int c) => board[r, c] == 0 || IsEnemy(isWhite, r, c);

        private void ExecuteMove(int fr, int fc, int tr, int tc)
        {
            int movingPiece = board[fr, fc];
            int targetPiece = board[tr, tc];

            if (targetPiece != 0)
            {
                if (targetPiece < 0) p1Captured.Add(targetPiece);
                else p2Captured.Add(targetPiece);
                UpdateCapturedDisplays();

                if (Math.Abs(targetPiece) == 6)
                {
                    board[tr, tc] = movingPiece;
                    board[fr, fc] = 0;
                    lastMoveFrom = (fr, fc);
                    lastMoveTo = (tr, tc);
                    selectedSquare = null;
                    validMoves.Clear();
                    boardPanel.Invalidate();

                    string winner = movingPiece > 0 ? p1Name : p2Name;
                    EndGame($"Chieu bi! {winner} da bat duoc Vua va gianh chien thang!");
                    return;
                }
            }

            if (movingPiece == 1 && tr == 0) movingPiece = 5;
            else if (movingPiece == -1 && tr == 7) movingPiece = -5;

            board[tr, tc] = movingPiece;
            board[fr, fc] = 0;

            lastMoveFrom = (fr, fc);
            lastMoveTo = (tr, tc);
            selectedSquare = null;
            validMoves.Clear();

            OnChessMoveMade?.Invoke(fr, fc, tr, tc);

            isWhiteTurn = !isWhiteTurn;
            UpdateTurnBanner();
            boardPanel.Invalidate();

            if (isSolo && !isWhiteTurn && !gameOver)
            {
                var botTimer = new System.Windows.Forms.Timer { Interval = 400 };
                botTimer.Tick += (s, e) =>
                {
                    botTimer.Stop();
                    botTimer.Dispose();
                    if (!gameOver) BotChessMove();
                };
                botTimer.Start();
            }
        }

        private void BotChessMove()
        {
            var rng = new Random();
            var allBlackMoves = new List<(int fr, int fc, int tr, int tc)>();

            for (int r = 0; r < BOARD_SIZE; r++)
                for (int c = 0; c < BOARD_SIZE; c++)
                {
                    if (board[r, c] < 0)
                    {
                        var moves = ComputeBasicMoves(r, c);
                        foreach (var m in moves)
                            allBlackMoves.Add((r, c, m.r, m.c));
                    }
                }

            if (allBlackMoves.Count == 0)
            {
                EndGame("Quan Den het nuoc di hop le! Hoa co.");
                return;
            }

            var captureMoves = allBlackMoves.Where(m => board[m.tr, m.tc] > 0).ToList();
            var chosen = captureMoves.Count > 0
                ? captureMoves[rng.Next(captureMoves.Count)]
                : allBlackMoves[rng.Next(allBlackMoves.Count)];

            ExecuteMove(chosen.fr, chosen.fc, chosen.tr, chosen.tc);
        }

        private void UpdateTurnBanner()
        {
            if (isWhiteTurn)
            {
                lblTurn.Text = "LUOT CUA TRANG (BAN)";
                lblTurn.BackColor = ThemeColors.PrimaryLight;
                lblTurn.ForeColor = ThemeColors.Primary;
            }
            else
            {
                lblTurn.Text = $"LUOT CUA DEN ({p2Name.ToUpper()})";
                lblTurn.BackColor = ThemeColors.SurfaceAlt;
                lblTurn.ForeColor = ThemeColors.TextSecondary;
            }
        }

        private void UpdateCapturedDisplays()
        {
            string p1Text = string.Join(" ", p1Captured.Select(GetPieceSymbol));
            lblCapturedPlayer.Text = "Bat: " + (string.IsNullOrEmpty(p1Text) ? "(chua co)" : p1Text);

            string p2Text = string.Join(" ", p2Captured.Select(GetPieceSymbol));
            lblCapturedOpp.Text = "Bat: " + (string.IsNullOrEmpty(p2Text) ? "(chua co)" : p2Text);
        }

        private void EndGame(string message)
        {
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
                EndGame("Ban da dau hang! Doi thu chien thang.");
            }
        }

        private void BtnLeave_Click(object? sender, EventArgs e)
        {
            gameTimer?.Stop();
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
            if (gameOver || isWhiteTurn) return;
            ExecuteMove(fr, fc, tr, tc);
        }
    }
}
