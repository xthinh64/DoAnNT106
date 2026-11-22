using OnlineChessGame.Caro;
using OnlineChessGame.Network;

namespace OnlineChessGame
{
    public partial class CaroBoardForm : Form
    {
        // === Hằng số kích thước bàn cờ ===
        private const int BOARD_SIZE = 15;
        private const int CELL = 40;
        private const int MARGIN = 32;
        private const int BOARD_PX = (BOARD_SIZE - 1) * CELL + MARGIN * 2; // 560 + 64 = 624 px

        // === Trạng thái ván cờ ===
        // 0=trống, 1=Player 1 (X), 2=Player 2 (O)
        private int[,] board = new int[BOARD_SIZE, BOARD_SIZE];
        private (int r, int c)? lastMove = null;
        private bool isPlayerTurn = true;
        private bool gameOver = false;
        private bool isSolo = true;
        private bool isFirstPlayer = true;
        private bool onlineDetached;

        // Thông tin người chơi
        private string p1Name = "Kỳ thủ VN";
        private string p2Name = "Caro Engine";
        private int p1Elo = 1200;
        private int p2Elo = 1200;

        // Đồng hồ đếm ngược
        private int p1Time = 600;
        private int p2Time = 600;
        private System.Windows.Forms.Timer? gameTimer;
        private System.Windows.Forms.Timer? botDelay;
        private string? outcome;
        private (int Row, int Column) selectedCell = (7, 7);

        // === EVENT: Network Hooks sẵn sàng nối Socket TCP ===
        public event Action<int, int>? OnCaroMoveMade;
        public event Action<string>? OnChatSent;
        public event Action? OnSurrender;
        public event Action? OnDrawRequest;

        /// <summary>
        /// Constructor không tham số bắt buộc cho Visual Studio Designer
        /// </summary>
        public CaroBoardForm()
        {
            InitializeComponent();
            EnableDoubleBuffering(boardPanel);
            ApplyCurrentTheme();
            Disposed += (_, _) =>
            {
                _ = LeaveOnlineGameAsync();
                gameTimer?.Dispose();
                botDelay?.Dispose();
            };
        }

        public CaroBoardForm(string p1, int elo1, string p2, int elo2, bool isSolo, bool isFirstPlayer = true) : this()
        {
            this.p1Name = p1;
            this.p1Elo = elo1;
            this.p2Name = p2;
            this.p2Elo = elo2;
            this.isSolo = isSolo;
            this.isFirstPlayer = isFirstPlayer;
            isPlayerTurn = isFirstPlayer;

            UpdatePlayerHeaders();
            if (isSolo)
            {
                StartGameTimer();
                if (!isFirstPlayer) ScheduleBotMove();
            }
            else
            {
                lblTimerPlayer.Text = lblTimerOpp.Text = "--:--";
                AttachOnlineGame();
            }
        }

        private void CaroBoardForm_Load(object? sender, EventArgs e)
        {
            UpdatePlayerHeaders();
            ApplyCardRegions();
            btnDraw.Text = isSolo ? "Ván mới" : "Cầu hòa";
            txtChat.Enabled = btnSend.Enabled = !isSolo;
            lblChatTitle.Text = isSolo ? "Nhật ký ván cờ" : "Trò chuyện";
            AddChatMessage("He thong", "Nối từ 5 quân ngang, dọc hoặc chéo để thắng.");
            if (isSolo) AddChatMessage("He thong", "Engine chạy cục bộ: ưu tiên thắng, chặn và tạo thế cờ.");
        }

        private static void EnableDoubleBuffering(Control c)
        {
            typeof(Control).GetMethod("SetStyle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(c, new object[] { ControlStyles.Selectable, true });
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        private void UpdatePlayerHeaders()
        {
            string init1 = string.IsNullOrEmpty(p1Name) ? "P" : p1Name[..1].ToUpper();
            string init2 = string.IsNullOrEmpty(p2Name) ? "C" : p2Name[..1].ToUpper();

            lblAvatarPlayer.Text = init1;
            lblNamePlayer.Text = $"{p1Name} · {(isFirstPlayer ? "X" : "O")}";
            lblEloPlayer.Text = $"Elo: {p1Elo}";

            lblAvatarOpp.Text = init2;
            lblNameOpp.Text = $"{p2Name} · {(isFirstPlayer ? "O" : "X")}";
            lblEloOpp.Text = isSolo ? "Engine cục bộ" : $"Elo: {p2Elo}";

            UpdateTurnBanner();
        }

        private void ApplyCardRegions()
        {
            boardPanel.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, boardPanel.Width, boardPanel.Height), 12));
            cardOpp.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, cardOpp.Width, cardOpp.Height), 10));
            cardPlayer.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, cardPlayer.Width, cardPlayer.Height), 10));
            lblTurn.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, lblTurn.Width, lblTurn.Height), 8));

            lblAvatarOpp.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, lblAvatarOpp.Width, lblAvatarOpp.Height), 22));
            lblAvatarPlayer.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, lblAvatarPlayer.Width, lblAvatarPlayer.Height), 22));

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
            boardPanel.BackColor = ThemeColors.CaroBoardBg;

            cardOpp.BackColor = ThemeColors.Surface;
            lblNameOpp.ForeColor = ThemeColors.TextPrimary;
            lblEloOpp.ForeColor = ThemeColors.TextSecondary;
            pnlTimerOpp.BackColor = ThemeColors.SurfaceAlt;
            lblTimerOpp.ForeColor = ThemeColors.TextPrimary;

            cardPlayer.BackColor = ThemeColors.Surface;
            lblNamePlayer.ForeColor = ThemeColors.TextPrimary;
            lblEloPlayer.ForeColor = ThemeColors.TextSecondary;
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

            btnThemeToggle.Text = ThemeColors.CurrentTheme == AppTheme.Light ? "Giao diện sáng" : "Giao diện tối";
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

                if (isPlayerTurn)
                {
                    if (p1Time > 0) p1Time--;
                    lblTimerPlayer.Text = $"{p1Time / 60:D2}:{p1Time % 60:D2}";
                    if (p1Time <= 0) EndGame("Het gio! Ban da thua cuoc.");
                }
                else
                {
                    if (p2Time > 0) p2Time--;
                    lblTimerOpp.Text = $"{p2Time / 60:D2}:{p2Time % 60:D2}";
                    if (p2Time <= 0) EndGame("Doi thu het gio! Ban thang cuoc.");
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

            // Tọa độ Cột A..O và Dòng 1..15
            using var coordBrush = new SolidBrush(ThemeColors.CaroCoordText);
            for (int i = 0; i < BOARD_SIZE; i++)
            {
                char colChar = (char)('A' + i);
                int cx = MARGIN + i * CELL;
                var sizeC = g.MeasureString(colChar.ToString(), ThemeColors.FontBoardCoord);
                g.DrawString(colChar.ToString(), ThemeColors.FontBoardCoord, coordBrush, cx - sizeC.Width / 2, 10);
                g.DrawString(colChar.ToString(), ThemeColors.FontBoardCoord, coordBrush, cx - sizeC.Width / 2, BOARD_PX - 22);

                string rowStr = (i + 1).ToString();
                int cy = MARGIN + i * CELL;
                var sizeR = g.MeasureString(rowStr, ThemeColors.FontBoardCoord);
                g.DrawString(rowStr, ThemeColors.FontBoardCoord, coordBrush, 12 - sizeR.Width / 2, cy - sizeR.Height / 2);
                g.DrawString(rowStr, ThemeColors.FontBoardCoord, coordBrush, BOARD_PX - 20, cy - sizeR.Height / 2);
            }

            // Lưới bàn cờ 15x15
            using var gridPen = new Pen(ThemeColors.CaroGrid, 1f);
            for (int i = 0; i < BOARD_SIZE; i++)
            {
                int pos = MARGIN + i * CELL;
                g.DrawLine(gridPen, pos, MARGIN, pos, MARGIN + (BOARD_SIZE - 1) * CELL);
                g.DrawLine(gridPen, MARGIN, pos, MARGIN + (BOARD_SIZE - 1) * CELL, pos);
            }

            // 5 Điểm sao (Hoshi)
            int[] hoshiCoords = { 3, 7, 11 };
            using var hoshiBrush = new SolidBrush(ThemeColors.CaroHoshi);
            foreach (int hr in hoshiCoords)
                foreach (int hc in hoshiCoords)
                {
                    int hx = MARGIN + hc * CELL;
                    int hy = MARGIN + hr * CELL;
                    g.FillEllipse(hoshiBrush, hx - 4, hy - 4, 8, 8);
                }

            if (boardPanel.Focused)
            {
                using var focusPen = new Pen(ThemeColors.Primary, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawRectangle(focusPen, MARGIN + selectedCell.Column * CELL - 18, MARGIN + selectedCell.Row * CELL - 18, 36, 36);
            }

            // Vẽ các quân cờ đã đánh
            int radius = CELL / 2 - 4;
            for (int r = 0; r < BOARD_SIZE; r++)
            {
                for (int c = 0; c < BOARD_SIZE; c++)
                {
                    int piece = board[r, c];
                    if (piece == 0) continue;

                    int px = MARGIN + c * CELL;
                    int py = MARGIN + r * CELL;

                    Color pieceColor = piece == 1 ? ThemeColors.CaroPieceX : ThemeColors.CaroPieceO;

                    using var piecePen = new Pen(pieceColor, 3.5f);
                    if (piece == 1)
                    {
                        g.DrawLine(piecePen, px - radius + 3, py - radius + 3, px + radius - 3, py + radius - 3);
                        g.DrawLine(piecePen, px - radius + 3, py + radius - 3, px + radius - 3, py - radius + 3);
                    }
                    else g.DrawEllipse(piecePen, px - radius + 2, py - radius + 2, radius * 2 - 4, radius * 2 - 4);

                    if (lastMove.HasValue && lastMove.Value.r == r && lastMove.Value.c == c)
                    {
                        using var ringPen = new Pen(ThemeColors.TextSecondary, 1f);
                        g.DrawEllipse(ringPen, px - radius - 2, py - radius - 2, (radius + 2) * 2, (radius + 2) * 2);
                    }
                }
            }
        }

        private void BoardPanel_Click(object? sender, MouseEventArgs e)
        {
            boardPanel.Focus();
            if (gameOver || !isPlayerTurn) return;

            int col = (int)Math.Round((double)(e.X - MARGIN) / CELL);
            int row = (int)Math.Round((double)(e.Y - MARGIN) / CELL);

            if (row < 0 || row >= BOARD_SIZE || col < 0 || col >= BOARD_SIZE) return;
            if (board[row, col] != 0) return;

            selectedCell = (row, col);
            int myPiece = isFirstPlayer ? 1 : 2;
            PlaceMove(row, col, myPiece);
            OnCaroMoveMade?.Invoke(row, col);

            if (CheckWin(row, col, myPiece))
            {
                EndGame($"{p1Name} thắng — 5 quân liên tiếp!");
                return;
            }

            if (FinishDraw()) return;
            isPlayerTurn = false;
            UpdateTurnBanner();
            if (isSolo) ScheduleBotMove();
        }

        private void ScheduleBotMove()
        {
            botDelay?.Dispose();
            botDelay = new System.Windows.Forms.Timer { Interval = 300 };
            botDelay.Tick += (_, _) =>
            {
                botDelay.Stop();
                if (!IsDisposed && !gameOver) BotCaroMove();
            };
            botDelay.Start();
        }

        private void BotCaroMove()
        {
            int piece = isFirstPlayer ? 2 : 1;
            var move = CaroEngine.FindBestMove(board, piece);
            if (move is null)
            {
                EndGame("Hòa — bàn cờ đã kín.");
                return;
            }
            var (row, col) = move.Value;
            PlaceMove(row, col, piece);
            if (CheckWin(row, col, piece))
            {
                EndGame($"{p2Name} thắng!");
                return;
            }
            if (FinishDraw()) return;
            isPlayerTurn = true;
            UpdateTurnBanner();
        }

        private bool FinishDraw()
        {
            foreach (int piece in board)
                if (piece == 0) return false;
            EndGame("Hòa — bàn cờ đã kín.");
            return true;
        }

        private void BoardPanel_KeyDown(object? sender, KeyEventArgs e)
        {
            var (row, col) = selectedCell;
            switch (e.KeyCode)
            {
                case Keys.Left: col = Math.Max(0, col - 1); break;
                case Keys.Right: col = Math.Min(BOARD_SIZE - 1, col + 1); break;
                case Keys.Up: row = Math.Max(0, row - 1); break;
                case Keys.Down: row = Math.Min(BOARD_SIZE - 1, row + 1); break;
                case Keys.Enter:
                case Keys.Space:
                    BoardPanel_Click(sender, new MouseEventArgs(MouseButtons.Left, 1, MARGIN + col * CELL, MARGIN + row * CELL, 0));
                    break;
                default: return;
            }
            selectedCell = (row, col);
            boardPanel.Invalidate();
            e.SuppressKeyPress = true;
        }

        private void BoardPanel_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down) e.IsInputKey = true;
        }

        private void PlaceMove(int r, int c, int p)
        {
            board[r, c] = p;
            lastMove = (r, c);
            boardPanel.Invalidate();
        }

        private bool CheckWin(int r, int c, int p) => CaroEngine.HasFive(board, r, c, p);

        private void UpdateTurnBanner()
        {
            if (gameOver)
            {
                lblTurn.Text = outcome;
                lblTurn.BackColor = ThemeColors.PrimaryLight;
                lblTurn.ForeColor = ThemeColors.Primary;
                return;
            }
            if (isPlayerTurn)
            {
                lblTurn.Text = $"Lượt của bạn · {(isFirstPlayer ? "X" : "O")}";
                lblTurn.BackColor = ThemeColors.PrimaryLight;
                lblTurn.ForeColor = ThemeColors.Primary;
            }
            else
            {
                lblTurn.Text = isSolo ? "Máy đang suy nghĩ…" : $"Đợi đối thủ · {(isFirstPlayer ? "O" : "X")}";
                lblTurn.BackColor = ThemeColors.SurfaceAlt;
                lblTurn.ForeColor = ThemeColors.TextSecondary;
            }
        }

        private void EndGame(string message)
        {
            if (gameOver) return;
            gameOver = true;
            outcome = message;
            gameTimer?.Stop();
            botDelay?.Stop();
            btnSurr.Enabled = false;
            UpdateTurnBanner();
            AddChatMessage("He thong", message);
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
            if (isSolo)
            {
                botDelay?.Stop();
                board = new int[BOARD_SIZE, BOARD_SIZE];
                lastMove = null;
                gameOver = false;
                outcome = null;
                isPlayerTurn = isFirstPlayer;
                p1Time = p2Time = 600;
                lblTimerPlayer.Text = lblTimerOpp.Text = "10:00";
                btnSurr.Enabled = true;
                gameTimer?.Start();
                UpdateTurnBanner();
                boardPanel.Invalidate();
                if (!isFirstPlayer) ScheduleBotMove();
                return;
            }
            if (gameOver) return;
            OnDrawRequest?.Invoke();
            AddChatMessage("He thong", "Ban da gui loi de nghi cau hoa.");
        }

        private void BtnSurr_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show("Ban co chac chan muon dau hang van co Caro nay?", "Xac nhan",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                OnSurrender?.Invoke();
                EndGame("Ban da dau hang! Doi thu chien thang.");
            }
        }

        private async void BtnLeave_Click(object? sender, EventArgs e)
        {
            btnLeave.Enabled = false;
            botDelay?.Stop();
            gameTimer?.Stop();
            await LeaveOnlineGameAsync();
            if (IsDisposed) return;
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

        public void ReceiveOpponentMove(int r, int c)
        {
            if (gameOver || isPlayerTurn || IsDisposed) return;
            if (r < 0 || r >= BOARD_SIZE || c < 0 || c >= BOARD_SIZE || board[r, c] != 0) return;

            int opponentPiece = isFirstPlayer ? 2 : 1;
            PlaceMove(r, c, opponentPiece);
            if (CheckWin(r, c, opponentPiece))
            {
                EndGame($"{p2Name} da chien thang!");
                return;
            }

            if (FinishDraw()) return;
            isPlayerTurn = true;
            UpdateTurnBanner();
        }

        private void AttachOnlineGame()
        {
            OnCaroMoveMade += (row, col) => _ = SendOnlineAsync(PacketType.MOVE, $"{row},{col}");
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
        }

        private async Task SendOnlineAsync(PacketType type, string payload = "")
        {
            try { await GameClient.Instance.SendAsync(new GamePacket(type, GameMode: "CARO", Payload: payload)); }
            catch (IOException) { RunOnUi(() => AddChatMessage("He thong", "Mất kết nối game server.")); }
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() => { if (!IsDisposed) action(); });
        }

        private void OnlineMoveReceived(string move) => RunOnUi(() =>
        {
            var parts = move.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int row) && int.TryParse(parts[1], out int col))
                ReceiveOpponentMove(row, col);
        });
        private void OnlineChatReceived(string sender, string message) => RunOnUi(() => AddChatMessage(sender, message));
        private void OnlineSurrenderReceived(string _) => RunOnUi(() => EndGame($"{p2Name} đã đầu hàng. Bạn chiến thắng!"));
        private void OnlineDrawRequestReceived(string requester) => RunOnUi(() =>
        {
            bool accept = MessageBox.Show($"{p2Name} đề nghị hòa. Bạn đồng ý?", "Đề nghị hòa",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            _ = SendOnlineAsync(PacketType.DRAW_RESPONSE, accept ? "ACCEPT" : "DECLINE");
            if (accept) EndGame("Hai bên đã đồng ý hòa.");
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
            base.OnFormClosed(e);
        }
    }
}
