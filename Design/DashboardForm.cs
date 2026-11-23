using OnlineChessGame.Chess;
using OnlineChessGame.Network;

namespace OnlineChessGame
{
    public partial class DashboardForm : Form
    {
        private string playerName = "Kỳ thủ VN";
        private int elo = 1200;
        private int caroElo = 1200;
        private string? queuedMode;
        private readonly System.Windows.Forms.Timer queueTimer = new() { Interval = 1000 };
        private DateTime queueStarted;
        // === EVENT: Network Hooks sẵn sàng nối Socket TCP ===
        public event Action<string>? OnRankedQueueRequested;
        public event Action? OnCancelQueueRequested;
        public event Action? OnLogoutRequested;

        /// <summary>
        /// Constructor không tham số bắt buộc cho Visual Studio Designer
        /// </summary>
        public DashboardForm()
        {
            InitializeComponent();
            queueTimer.Tick += (_, _) => lblQueueTimer.Text = $"Thời gian chờ: {(DateTime.UtcNow - queueStarted):mm\\:ss}";
            GameClient.Instance.OnMatchFound += MatchFound;
            GameClient.Instance.OnDisconnected += GameDisconnected;
            GameClient.Instance.OnMatchUnavailable += MatchUnavailable;
            Disposed += (_, _) => DetachNetwork();
            ApplyCurrentTheme();
        }

        public DashboardForm(string playerName, int elo) : this()
        {
            this.playerName = playerName;
            this.elo = AuthConnection.Player?.ChessElo ?? elo;
            this.caroElo = AuthConnection.Player?.CaroElo ?? elo;
            UpdatePlayerInfo();
        }

        private void DashboardForm_Load(object? sender, EventArgs e)
        {
            UpdatePlayerInfo();
            ApplyCardRegions();
            queuePanel.Visible = false; // Ẩn bảng chờ lúc đầu
        }

        private void UpdatePlayerInfo()
        {
            string initial = string.IsNullOrEmpty(playerName) ? "P" : playerName[..1].ToUpper();
            lblAvatar.Text = initial;
            lblName.Text = playerName;
            lblElo.Text = $"Elo · Cờ vua {elo} / Caro {caroElo}";
        }

        private void ApplyCardRegions()
        {
            lblAvatar.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, lblAvatar.Width, lblAvatar.Height), 23));
            pnlElo.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, pnlElo.Width, pnlElo.Height), 6));
            cardChess.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, cardChess.Width, cardChess.Height), 12));
            pnlTagChess.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, pnlTagChess.Width, pnlTagChess.Height), 6));
            cardCaro.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, cardCaro.Width, cardCaro.Height), 12));
            pnlTagCaro.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, pnlTagCaro.Width, pnlTagCaro.Height), 6));
            queuePanel.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, queuePanel.Width, queuePanel.Height), 12));
            cardGuide.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, cardGuide.Width, cardGuide.Height), 12));

            btnSoloChess.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnSoloChess.Width, btnSoloChess.Height), 8));
            btnRankedChess.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnRankedChess.Width, btnRankedChess.Height), 8));
            btnSoloCaro.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnSoloCaro.Width, btnSoloCaro.Height), 8));
            btnRankedCaro.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnRankedCaro.Width, btnRankedCaro.Height), 8));
            btnCancelQueue.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnCancelQueue.Width, btnCancelQueue.Height), 6));
            btnLogout.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnLogout.Width, btnLogout.Height), 8));
            btnThemeToggle.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnThemeToggle.Width, btnThemeToggle.Height), 8));
        }

        private void ApplyCurrentTheme()
        {
            BackColor = ThemeColors.Background;
            header.BackColor = ThemeColors.Surface;
            lblName.ForeColor = ThemeColors.TextPrimary;
            lblElo.ForeColor = ThemeColors.TextSecondary;
            lblAvatar.BackColor = ThemeColors.PrimaryLight;
            pnlElo.BackColor = ThemeColors.SurfaceAlt;
            lblGuideTitle.ForeColor = ThemeColors.TextPrimary;
            lblGuideBody.ForeColor = ThemeColors.TextSecondary;
            lblAvatar.ForeColor = ThemeColors.Primary;

            lblModeHeader.ForeColor = ThemeColors.TextSecondary;
            lblGuideHeader.ForeColor = ThemeColors.TextSecondary;

            cardChess.BackColor = ThemeColors.Surface;
            lblTitleChess.ForeColor = ThemeColors.TextPrimary;
            lblDescChess.ForeColor = ThemeColors.TextSecondary;
            pnlTagChess.BackColor = ThemeColors.Primary;
            btnSoloChess.BackColor = ThemeColors.Primary;
            btnSoloChess.ForeColor = ThemeColors.TextOnPrimary;
            btnRankedChess.BackColor = ThemeColors.PrimaryLight;
            btnRankedChess.ForeColor = ThemeColors.Primary;
            lblTagChess.ForeColor = ThemeColors.TextOnPrimary;

            cardCaro.BackColor = ThemeColors.Surface;
            lblTitleCaro.ForeColor = ThemeColors.TextPrimary;
            lblDescCaro.ForeColor = ThemeColors.TextSecondary;
            pnlTagCaro.BackColor = ThemeColors.Accent;
            btnSoloCaro.BackColor = ThemeColors.Primary;
            btnSoloCaro.ForeColor = ThemeColors.TextOnPrimary;
            btnRankedCaro.BackColor = ThemeColors.PrimaryLight;
            btnRankedCaro.ForeColor = ThemeColors.Primary;
            lblTagCaro.ForeColor = ThemeColors.TextOnPrimary;

            queuePanel.BackColor = ThemeColors.Surface;
            lblQueueStatus.ForeColor = ThemeColors.TextPrimary;
            lblQueueTimer.ForeColor = ThemeColors.TextSecondary;
            btnCancelQueue.BackColor = ThemeColors.Danger;
            btnLogout.BackColor = ThemeColors.SurfaceAlt;
            btnLogout.ForeColor = ThemeColors.TextPrimary;

            cardGuide.BackColor = ThemeColors.Surface;

            btnThemeToggle.Text = ThemeColors.CurrentTheme == AppTheme.Light ? "Giao diện sáng" : "Giao diện tối";
            btnThemeToggle.BackColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(224, 238, 255) : Color.FromArgb(6, 78, 59);
            btnThemeToggle.ForeColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(37, 99, 235) : Color.FromArgb(52, 211, 153);
        }

        private void BtnThemeToggle_Click(object? sender, EventArgs e)
        {
            ThemeColors.ToggleTheme();
            ApplyCurrentTheme();
        }

        private void BtnLogout_Click(object? sender, EventArgs e)
        {
            _ = CancelMatchAsync();
            GameClient.Instance.Disconnect();
            AuthConnection.SessionToken = null;
            AuthConnection.Player = null;
            OnLogoutRequested?.Invoke();
            Program.Context.SwitchTo(new LoginForm());
        }

        private void BtnSoloChess_Click(object? sender, EventArgs e)
        {
            using var dialog = new ChessDifficultyDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedDifficulty is null)
                return;

            ChessDifficulty difficulty = dialog.SelectedDifficulty;
            Program.Context.SwitchTo(new ChessBoardForm(
                playerName,
                elo,
                $"Máy - {difficulty.DisplayName}",
                difficulty.DisplayElo,
                isSolo: true,
                difficulty));
        }

        private void BtnRankedChess_Click(object? sender, EventArgs e)
        {
            _ = FindMatchAsync("CHESS");
        }

        private void BtnSoloCaro_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new CaroBoardForm(playerName, caroElo, "Caro Engine", 1200, isSolo: true));
        }

        private void BtnRankedCaro_Click(object? sender, EventArgs e)
        {
            _ = FindMatchAsync("CARO");
        }

        private async Task FindMatchAsync(string gameType)
        {
            if (queuedMode is not null) return;
            queuedMode = gameType;
            cardGuide.Visible = false;
            btnSoloChess.Enabled = btnSoloCaro.Enabled = false;
            btnRankedChess.Enabled = btnRankedCaro.Enabled = false;
            lblQueueStatus.Text = gameType == "CHESS" ? "Đang tìm đối thủ Cờ Vua..." : "Đang tìm đối thủ Cờ Caro...";
            lblQueueTimer.Text = "Thời gian chờ: 00:00";
            queueStarted = DateTime.UtcNow;
            queuePanel.Visible = true;
            queueTimer.Start();
            try
            {
                await GameClient.Instance.ConnectAsync(AuthConnection.Player?.Username ?? playerName);
                if (queuedMode != gameType) return;
                await GameClient.Instance.SendAsync(new GamePacket(PacketType.FIND_MATCH, GameMode: gameType,
                    Payload: (gameType == "CARO" ? caroElo : elo).ToString(System.Globalization.CultureInfo.InvariantCulture)));
                OnRankedQueueRequested?.Invoke(gameType);
            }
            catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or InvalidOperationException or ArgumentException or TimeoutException or System.Security.Authentication.AuthenticationException or OperationCanceledException)
            {
                if (IsDisposed) return;
                ResetQueue();
                MessageBox.Show($"Không kết nối được gateway {AuthConnection.Endpoint}: {ex.Message}",
                    "Lỗi tìm trận", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task CancelMatchAsync()
        {
            if (queuedMode is null) return;
            ResetQueue();
            try { await GameClient.Instance.SendAsync(new GamePacket(PacketType.CANCEL_MATCH)); }
            catch (IOException) { GameClient.Instance.Disconnect(); }
            OnCancelQueueRequested?.Invoke();
        }

        private void BtnCancelQueue_Click(object? sender, EventArgs e) => _ = CancelMatchAsync();

        private void ResetQueue()
        {
            queuedMode = null;
            queueTimer.Stop();
            queuePanel.Visible = false;
            cardGuide.Visible = true;
            btnSoloChess.Enabled = btnSoloCaro.Enabled = true;
            btnRankedChess.Enabled = btnRankedCaro.Enabled = true;
        }

        private void MatchFound(string roomId, string opponent, int opponentElo, bool isFirst, string gameMode)
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke(() =>
            {
                if (IsDisposed) return;
                if (queuedMode != gameMode)
                {
                    _ = LeaveUnexpectedMatchAsync(roomId);
                    return;
                }
                ResetQueue();
                int myElo = gameMode == "CARO" ? caroElo : elo;
                Form board = gameMode == "CHESS"
                    ? new ChessBoardForm(playerName, myElo, opponent, opponentElo, isSolo: false, isFirstPlayer: isFirst)
                    : new CaroBoardForm(playerName, myElo, opponent, opponentElo, isSolo: false, isFirstPlayer: isFirst);
                Program.Context.SwitchTo(board);
            });
        }

        private static async Task LeaveUnexpectedMatchAsync(string roomId)
        {
            try { await GameClient.Instance.SendAsync(new GamePacket(PacketType.LEAVE_ROOM, RoomId: roomId)); }
            catch (IOException) { GameClient.Instance.Disconnect(); }
            GameClient.Instance.CurrentRoomId = string.Empty;
        }

        private void GameDisconnected()
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke(() =>
            {
                if (IsDisposed || queuedMode is null) return;
                ResetQueue();
                MessageBox.Show("Mất kết nối game server. Vui lòng tìm trận lại.", "Mất kết nối",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });
        }

        private void MatchUnavailable(string message)
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke(() =>
            {
                if (IsDisposed || queuedMode is null) return;
                ResetQueue();
                MessageBox.Show(message, "Chưa tìm được trận", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            DetachNetwork();
            base.OnFormClosed(e);
        }

        private void DetachNetwork()
        {
            GameClient.Instance.OnMatchFound -= MatchFound;
            GameClient.Instance.OnDisconnected -= GameDisconnected;
            GameClient.Instance.OnMatchUnavailable -= MatchUnavailable;
            queueTimer.Dispose();
        }
    }
}
