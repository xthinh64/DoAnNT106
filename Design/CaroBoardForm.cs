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

        // Thông tin người chơi
        private string p1Name = "Kỳ thủ VN";
        private string p2Name = "Computer Caro";
        private int p1Elo = 1200;
        private int p2Elo = 1200;

        // Đồng hồ đếm ngược
        private int p1Time = 600;
        private int p2Time = 600;
        private System.Windows.Forms.Timer? gameTimer;

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
        }

        public CaroBoardForm(string p1, int elo1, string p2, int elo2, bool isSolo) : this()
        {
            this.p1Name = p1;
            this.p1Elo = elo1;
            this.p2Name = p2;
            this.p2Elo = elo2;
            this.isSolo = isSolo;

            UpdatePlayerHeaders();
            StartGameTimer();
        }

        private void CaroBoardForm_Load(object? sender, EventArgs e)
        {
            UpdatePlayerHeaders();
            ApplyCardRegions();
            AddChatMessage("He thong", "Tran dau Co Caro da bat dau. Chuc 2 ky thu thi dau tot!");
        }

        private static void EnableDoubleBuffering(Control c)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, c, new object[] { true });
        }

        private void UpdatePlayerHeaders()
        {
            string init1 = string.IsNullOrEmpty(p1Name) ? "P" : p1Name[..1].ToUpper();
            string init2 = string.IsNullOrEmpty(p2Name) ? "C" : p2Name[..1].ToUpper();

            lblAvatarPlayer.Text = init1;
            lblNamePlayer.Text = $"{p1Name} [Quan X]";
            lblEloPlayer.Text = $"Elo: {p1Elo}";

            lblAvatarOpp.Text = init2;
            lblNameOpp.Text = $"{p2Name} [Quan O]";
            lblEloOpp.Text = $"Elo: {p2Elo}";

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

                    using var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
                    g.FillEllipse(shadowBrush, px - radius + 2, py - radius + 3, radius * 2, radius * 2);

                    using var brush = new SolidBrush(pieceColor);
                    g.FillEllipse(brush, px - radius, py - radius, radius * 2, radius * 2);

                    using var hiBrush = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
                    g.FillEllipse(hiBrush, px - radius / 2, py - radius * 3 / 4, radius, radius / 2);

                    if (lastMove.HasValue && lastMove.Value.r == r && lastMove.Value.c == c)
                    {
                        using var ringPen = new Pen(Color.White, 2.5f);
                        g.DrawEllipse(ringPen, px - radius - 2, py - radius - 2, (radius + 2) * 2, (radius + 2) * 2);
                    }
                }
            }
        }

        private void BoardPanel_Click(object? sender, MouseEventArgs e)
        {
            if (gameOver || !isPlayerTurn) return;

            int col = (int)Math.Round((double)(e.X - MARGIN) / CELL);
            int row = (int)Math.Round((double)(e.Y - MARGIN) / CELL);

            if (row < 0 || row >= BOARD_SIZE || col < 0 || col >= BOARD_SIZE) return;
            if (board[row, col] != 0) return;

            PlaceMove(row, col, 1);
            OnCaroMoveMade?.Invoke(row, col);

            if (CheckWin(row, col, 1))
            {
                EndGame($"Chuc mung! {p1Name} da chien thang 5 quan lien tiep!");
                return;
            }

            isPlayerTurn = false;
            UpdateTurnBanner();

            if (isSolo)
            {
                var botDelay = new System.Windows.Forms.Timer { Interval = 400 };
                botDelay.Tick += (s, ev) =>
                {
                    botDelay.Stop();
                    botDelay.Dispose();
                    if (!gameOver) BotCaroMove();
                };
                botDelay.Start();
            }
        }

        private void BotCaroMove()
        {
            var rng = new Random();
            var emptyList = new List<(int r, int c)>();

            for (int r = 0; r < BOARD_SIZE; r++)
                for (int c = 0; c < BOARD_SIZE; c++)
                    if (board[r, c] == 0) emptyList.Add((r, c));

            if (emptyList.Count == 0)
            {
                EndGame("Van co ket thuc: Hoa! Ban co da kin.");
                return;
            }

            var (ar, ac) = emptyList[rng.Next(emptyList.Count)];
            PlaceMove(ar, ac, 2);

            if (CheckWin(ar, ac, 2))
            {
                EndGame($"{p2Name} da chien thang!");
                return;
            }

            isPlayerTurn = true;
            UpdateTurnBanner();
        }

        private void PlaceMove(int r, int c, int p)
        {
            board[r, c] = p;
            lastMove = (r, c);
            boardPanel.Invalidate();
        }

        private bool CheckWin(int r, int c, int p)
        {
            int[][] directions = {
                new[] { 0, 1 },
                new[] { 1, 0 },
                new[] { 1, 1 },
                new[] { 1, -1 }
            };

            foreach (var d in directions)
            {
                int count = 1;
                for (int i = 1; i < 5; i++)
                {
                    int nr = r + d[0] * i, nc = c + d[1] * i;
                    if (nr >= 0 && nr < BOARD_SIZE && nc >= 0 && nc < BOARD_SIZE && board[nr, nc] == p) count++;
                    else break;
                }
                for (int i = 1; i < 5; i++)
                {
                    int nr = r - d[0] * i, nc = c - d[1] * i;
                    if (nr >= 0 && nr < BOARD_SIZE && nc >= 0 && nc < BOARD_SIZE && board[nr, nc] == p) count++;
                    else break;
                }
                if (count >= 5) return true;
            }
            return false;
        }

        private void UpdateTurnBanner()
        {
            if (isPlayerTurn)
            {
                lblTurn.Text = "LUOT CUA BAN (QUAN X)";
                lblTurn.BackColor = ThemeColors.PrimaryLight;
                lblTurn.ForeColor = ThemeColors.Primary;
            }
            else
            {
                lblTurn.Text = $"LUOT CUA {p2Name.ToUpper()} (QUAN O)";
                lblTurn.BackColor = ThemeColors.SurfaceAlt;
                lblTurn.ForeColor = ThemeColors.TextSecondary;
            }
        }

        private void EndGame(string message)
        {
            gameOver = true;
            gameTimer?.Stop();

            lblTurn.Text = message;
            lblTurn.BackColor = ThemeColors.Success;
            lblTurn.ForeColor = Color.White;

            AddChatMessage("He thong", message);
            MessageBox.Show(message, "Ket thuc van co Caro", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            var confirm = MessageBox.Show("Ban co chac chan muon dau hang van co Caro nay?", "Xac nhan",
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

        public void ReceiveOpponentMove(int r, int c)
        {
            if (gameOver || isPlayerTurn) return;

            PlaceMove(r, c, 2);
            if (CheckWin(r, c, 2))
            {
                EndGame($"{p2Name} da chien thang!");
                return;
            }

            isPlayerTurn = true;
            UpdateTurnBanner();
        }
    }
}
