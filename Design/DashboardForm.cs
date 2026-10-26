using MySql.Data.MySqlClient;

namespace OnlineChessGame
{
    public partial class DashboardForm : Form
    {
        private string playerName = "Kỳ thủ VN";
        private int elo = 1200;
        private System.Windows.Forms.Timer? queueTimer;
        private int queueSeconds = 0;
        private bool isQueuing = false;
        private string activeQueueGame = "Co Vua"; // "Co Vua" hoặc "Co Caro"

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
            ApplyCurrentTheme();
            BuildHistoryRows();
        }

        public DashboardForm(string playerName, int elo) : this()
        {
            this.playerName = playerName;
            this.elo = elo;
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
            lblElo.Text = $"Elo: {elo}";
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
            cardHistory.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, cardHistory.Width, cardHistory.Height), 12));

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
            header.BackColor = ThemeColors.HeaderBg;
            lblAvatar.ForeColor = ThemeColors.Primary;

            lblModeHeader.ForeColor = ThemeColors.TextSecondary;
            lblHistHeader.ForeColor = ThemeColors.TextSecondary;

            cardChess.BackColor = ThemeColors.Surface;
            lblTitleChess.ForeColor = ThemeColors.TextPrimary;
            lblDescChess.ForeColor = ThemeColors.TextSecondary;
            pnlTagChess.BackColor = ThemeColors.Primary;
            btnSoloChess.BackColor = ThemeColors.Primary;
            btnSoloChess.ForeColor = ThemeColors.TextOnPrimary;
            btnRankedChess.BackColor = ThemeColors.Accent;

            cardCaro.BackColor = ThemeColors.Surface;
            lblTitleCaro.ForeColor = ThemeColors.TextPrimary;
            lblDescCaro.ForeColor = ThemeColors.TextSecondary;
            pnlTagCaro.BackColor = ThemeColors.Accent;
            btnSoloCaro.BackColor = ThemeColors.Primary;
            btnSoloCaro.ForeColor = ThemeColors.TextOnPrimary;
            btnRankedCaro.BackColor = ThemeColors.Accent;

            queuePanel.BackColor = ThemeColors.Surface;
            lblQueueStatus.ForeColor = ThemeColors.TextPrimary;
            lblQueueTimer.ForeColor = ThemeColors.TextSecondary;
            btnCancelQueue.BackColor = ThemeColors.Danger;

            cardHistory.BackColor = ThemeColors.Surface;

            btnThemeToggle.Text = ThemeColors.CurrentTheme == AppTheme.Light ? "Theme: Sang" : "Theme: Den & Xanh";
            btnThemeToggle.BackColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(224, 238, 255) : Color.FromArgb(6, 78, 59);
            btnThemeToggle.ForeColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(37, 99, 235) : Color.FromArgb(52, 211, 153);

            BuildHistoryRows();
        }

        private void BuildHistoryRows()
        {
            cardHistory.Controls.Clear();

            string[,] matchHistory = {
                { "Co Vua",  "Thang", "GrandMaster_VN", "+18", "10 phut truoc" },
                { "Co Caro", "Thua",  "Caro_King99",    "-14", "45 phut truoc" },
                { "Co Vua",  "Thang", "Tactician_07",   "+16", "2 gio truoc" },
                { "Co Caro", "Hoa",   "PeaceMaker",     "+0",  "Hom qua" },
                { "Co Vua",  "Thang", "Beginner_Pro",   "+15", "2 ngay truoc" }
            };

            int startY = 20;
            int rowH = 74;
            int rightW = cardHistory.Width;

            for (int i = 0; i < matchHistory.GetLength(0); i++)
            {
                int curY = startY + i * (rowH + 12);
                var row = new Panel
                {
                    Location = new Point(20, curY),
                    Size = new Size(rightW - 40, rowH),
                    BackColor = ThemeColors.SurfaceAlt
                };
                row.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, rightW - 40, rowH), 8));
                cardHistory.Controls.Add(row);

                string gameType = matchHistory[i, 0];
                string result = matchHistory[i, 1];
                string opp = matchHistory[i, 2];
                string eloDiff = matchHistory[i, 3];
                string time = matchHistory[i, 4];

                Color badgeBg = result == "Thang" ? ThemeColors.Success : (result == "Hoa" ? ThemeColors.Warning : ThemeColors.Danger);

                var pnlBadge = new Panel
                {
                    Location = new Point(14, 18),
                    Size = new Size(68, 38),
                    BackColor = badgeBg
                };
                pnlBadge.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, 68, 38), 6));
                var lblBadge = new Label
                {
                    Text = result,
                    Font = ThemeColors.FontButton,
                    ForeColor = Color.White,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                pnlBadge.Controls.Add(lblBadge);
                row.Controls.Add(pnlBadge);

                var lblOpp = new Label
                {
                    Text = $"[{gameType}] vs {opp}",
                    Font = ThemeColors.FontHeading,
                    ForeColor = ThemeColors.TextPrimary,
                    AutoSize = true,
                    Location = new Point(94, 16)
                };
                row.Controls.Add(lblOpp);

                var lblTime = new Label
                {
                    Text = time,
                    Font = ThemeColors.FontSmall,
                    ForeColor = ThemeColors.TextSecondary,
                    AutoSize = true,
                    Location = new Point(94, 40)
                };
                row.Controls.Add(lblTime);

                var lblDiff = new Label
                {
                    Text = eloDiff,
                    Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                    ForeColor = badgeBg,
                    AutoSize = true,
                    Location = new Point(rightW - 40 - 75, 24)
                };
                row.Controls.Add(lblDiff);
            }
        }

        private void BtnThemeToggle_Click(object? sender, EventArgs e)
        {
            ThemeColors.ToggleTheme();
            ApplyCurrentTheme();
        }

        private void BtnLogout_Click(object? sender, EventArgs e)
        {
            OnLogoutRequested?.Invoke();
            Program.Context.SwitchTo(new LoginForm());
        }

        private void BtnSoloChess_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new ChessBoardForm(playerName, elo, "Computer Chess", 1250, isSolo: true));
        }

        private void BtnRankedChess_Click(object? sender, EventArgs e)
        {
            StartQueue("Co Vua");
        }

        private void BtnSoloCaro_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new CaroBoardForm(playerName, elo, "Computer Caro", 1200, isSolo: true));
        }

        private void BtnRankedCaro_Click(object? sender, EventArgs e)
        {
            StartQueue("Co Caro");
        }

        // === TÍCH HỢP HÀNG CHỜ GHÉP TRẬN QUA CSDL (ELO +- 50) ===
        private void StartQueue(string gameType)
        {
            if (isQueuing) return;
            isQueuing = true;
            activeQueueGame = gameType;
            queueSeconds = 0;
            queuePanel.Visible = true;
            lblQueueStatus.Text = $"Đang tìm đối thủ {gameType} (Elo: {elo} ± 50)...";

            string dbGameType = (gameType == "Co Vua") ? "CHESS" : "CARO";

            // 1. Đưa người chơi vào hàng chờ trên Database
            string connString = "Server=localhost;Database=onlinechessdb;Uid=root;Pwd=Taitn2007;";
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    // Xóa trước nếu lỡ tồn tại hàng chờ cũ
                    string cleanOld = "DELETE FROM MatchmakingQueue WHERE username = @user;";
                    using (MySqlCommand cmdClean = new MySqlCommand(cleanOld, conn))
                    {
                        cmdClean.Parameters.AddWithValue("@user", playerName);
                        cmdClean.ExecuteNonQuery();
                    }

                    // Thêm mới vào queue
                    string insertQueue = "INSERT INTO MatchmakingQueue (username, game_type, elo) VALUES (@user, @gameType, @elo);";
                    using (MySqlCommand cmdIns = new MySqlCommand(insertQueue, conn))
                    {
                        cmdIns.Parameters.AddWithValue("@user", playerName);
                        cmdIns.Parameters.AddWithValue("@gameType", dbGameType);
                        cmdIns.Parameters.AddWithValue("@elo", elo);
                        cmdIns.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối hàng chờ: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopQueue();
                return;
            }
            int nearbyCount = GetNearbyPlayersCount(dbGameType);
            lblQueueStatus.Text = $"Đang tìm đối thủ {gameType} (Elo: {elo} ± 50)... | Có {nearbyCount} người đang trong tầm Elo";      
            // 2. Bật Timer quét liên tục mỗi 2 giây để tìm đối thủ phù hợp
            queueTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            queueTimer.Tick += (s, e) =>
            {
                queueSeconds += 2;
                int min = (queueSeconds / 2) / 60; // hoặc đếm theo giây thực tế
                int sec = (queueSeconds / 2) % 60;
                lblQueueTimer.Text = $"Thời gian chờ: {queueSeconds / 2:D2}s";
                int currentNearby = GetNearbyPlayersCount(dbGameType);
                // Gọi hàm quét đối thủ trong database
                CheckDatabaseMatchmaking(dbGameType);
            };
            queueTimer.Start();
        }

        private int GetNearbyPlayersCount(string dbGameType)
        {
            string connString = "Server=localhost;Database=onlinechessdb;Uid=root;Pwd=Taitn2007;";
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string countQuery = "SELECT COUNT(*) FROM MatchmakingQueue WHERE username != @user AND game_type = @gameType AND elo BETWEEN @minElo AND @maxElo;";
                    using (MySqlCommand cmd = new MySqlCommand(countQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", playerName);
                        cmd.Parameters.AddWithValue("@gameType", dbGameType);
                        cmd.Parameters.AddWithValue("@minElo", elo - 50);
                        cmd.Parameters.AddWithValue("@maxElo", elo + 50);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                     }
                }
            }
           catch
           {
          return 0;
           }   }
        private void CheckDatabaseMatchmaking(string dbGameType)
        {
            string connString = "Server=localhost;Database=onlinechessdb;Uid=root;Pwd=Taitn2007;";
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Tìm kiếm đối thủ khác trong queue có cùng loại game và chênh lệch Elo <= 50
                    string findOpponent = "SELECT username, elo FROM MatchmakingQueue WHERE username != @user AND game_type = @gameType AND elo BETWEEN @minElo AND @maxElo ORDER BY joined_at ASC LIMIT 1;";
                    
                    using (MySqlCommand cmd = new MySqlCommand(findOpponent, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", playerName);
                        cmd.Parameters.AddWithValue("@gameType", dbGameType);
                        cmd.Parameters.AddWithValue("@minElo", elo - 50);
                        cmd.Parameters.AddWithValue("@maxElo", elo + 50);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string oppName = reader["username"].ToString();
                                int oppElo = Convert.ToInt32(reader["elo"]);
                                reader.Close();

                                // THÀNH CÔNG: Dừng timer tìm trận
                                StopQueueWithoutDB();

                                // Xóa cả 2 người ra khỏi bảng MatchmakingQueue để tránh ghép trùng
                                string removeBoth = "DELETE FROM MatchmakingQueue WHERE username IN (@user1, @user2);";
                                using (MySqlCommand cmdDel = new MySqlCommand(removeBoth, conn))
                                {
                                    cmdDel.Parameters.AddWithValue("@user1", playerName);
                                    cmdDel.Parameters.AddWithValue("@user2", oppName);
                                    cmdDel.ExecuteNonQuery();
                                }

                                // Mở màn hình chơi tương ứng
                                MessageBox.Show($"Đã tìm thấy đối thủ: {oppName} (Elo: {oppElo})!", "Ghép trận thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                
                                this.Invoke((MethodInvoker)delegate {
                                    if (activeQueueGame == "Co Vua")
                                    {
                                        Program.Context.SwitchTo(new ChessBoardForm(playerName, elo, oppName, oppElo, isSolo: false));
                                    }
                                    else
                                    {
                                        Program.Context.SwitchTo(new CaroBoardForm(playerName, elo, oppName, oppElo, isSolo: false));
                                    }
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Bỏ qua lỗi ngầm trong vòng lặp timer để tránh gián đoạn
            }
        }

        private void BtnCancelQueue_Click(object? sender, EventArgs e)
        {
            StopQueue();
            MessageBox.Show("Đã hủy tìm trận.", "Thông báo");
        }

        // Dừng hàng chờ và xóa tên khỏi Database
        private void StopQueue()
        {
            StopQueueWithoutDB();

            string connString = "Server=localhost;Database=onlinechessdb;Uid=root;Pwd=Taitn2007;";
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();
                    string deleteQueue = "DELETE FROM MatchmakingQueue WHERE username = @user;";
                    using (MySqlCommand cmd = new MySqlCommand(deleteQueue, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", playerName);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception) { }

            OnCancelQueueRequested?.Invoke();
        }

        // Chỉ dừng Timer và ẩn giao diện chờ (dùng nội bộ khi đã khớp trận)
        private void StopQueueWithoutDB()
        {
            isQueuing = false;
            queueTimer?.Stop();
            queueTimer?.Dispose();
            queueTimer = null;
            queuePanel.Visible = false;
        }
    }
}