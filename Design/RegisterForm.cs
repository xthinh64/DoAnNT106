using MySql.Data.MySqlClient;

namespace OnlineChessGame
{
    public partial class RegisterForm : Form
    {
        public event Action<string, string, string>? OnRegisterRequested;

        public RegisterForm()
        {
            InitializeComponent();
            ApplyCurrentTheme();
        }

        private void RegisterForm_Load(object? sender, EventArgs e)
        {
            CenterCard();
        }

        private void CenterCard()
        {
            card.Location = new Point((ClientSize.Width - card.Width) / 2, (ClientSize.Height - card.Height) / 2);
            card.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, card.Width, card.Height), 16));
            btnRegister.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnRegister.Width, btnRegister.Height), 10));
            btnThemeToggle.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnThemeToggle.Width, btnThemeToggle.Height), 8));
        }

        private void ApplyCurrentTheme()
        {
            BackColor = ThemeColors.Background;
            card.BackColor = ThemeColors.Surface;
            lblTitle.ForeColor = ThemeColors.Primary;
            lblSub.ForeColor = ThemeColors.TextSecondary;

            lblUser.ForeColor = ThemeColors.TextPrimary;
            txtUser.BackColor = ThemeColors.InputBg;
            txtUser.ForeColor = ThemeColors.InputFg;

            lblDisplay.ForeColor = ThemeColors.TextPrimary;
            txtDisplay.BackColor = ThemeColors.InputBg;
            txtDisplay.ForeColor = ThemeColors.InputFg;

            lblPass.ForeColor = ThemeColors.TextPrimary;
            txtPass.BackColor = ThemeColors.InputBg;
            txtPass.ForeColor = ThemeColors.InputFg;

            lblConfirm.ForeColor = ThemeColors.TextPrimary;
            txtConfirm.BackColor = ThemeColors.InputBg;
            txtConfirm.ForeColor = ThemeColors.InputFg;

            lblError.ForeColor = ThemeColors.Danger;

            btnRegister.BackColor = ThemeColors.Primary;
            btnRegister.ForeColor = ThemeColors.TextOnPrimary;

            btnBack.ForeColor = ThemeColors.Primary;

            btnThemeToggle.Text = ThemeColors.CurrentTheme == AppTheme.Light ? "Theme: Sang" : "Theme: Den & Xanh";
            btnThemeToggle.BackColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(224, 238, 255) : Color.FromArgb(6, 78, 59);
            btnThemeToggle.ForeColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(37, 99, 235) : Color.FromArgb(52, 211, 153);
        }

        private void BtnThemeToggle_Click(object? sender, EventArgs e)
        {
            ThemeColors.ToggleTheme();
            ApplyCurrentTheme();
        }

        private void BtnRegister_Click(object? sender, EventArgs e)
        {
            string user = txtUser.Text.Trim();
            string display = txtDisplay.Text.Trim();
            string pass = txtPass.Text.Trim();
            string confirm = txtConfirm.Text.Trim();

            if (string.IsNullOrEmpty(user))
            {
                lblError.Text = "Vui long nhap ten tai khoan!";
                return;
            }
            if (string.IsNullOrEmpty(display))
            {
                lblError.Text = "Vui long nhap ten hien thi!";
                return;
            }
            if (pass.Length < 4)
            {
                lblError.Text = "Mat khau phai co toi thieu 4 ky tu!";
                return;
            }
            if (pass != confirm)
            {
                lblError.Text = "Mat khau xac nhan khong trung khop!";
                return;
            }

            lblError.Text = "";

            string connString = "Server=localhost;Database=onlinechessdb;Uid=root;Pwd=Taitn2007;";

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    // Kiểm tra xem tên đăng ký đã tồn tại chưa
                    string checkExist = "SELECT COUNT(*) FROM Users WHERE username = @user";
                    using (MySqlCommand cmdCheck = new MySqlCommand(checkExist, conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@user", user);
                        int count = Convert.ToInt32(cmdCheck.ExecuteScalar());
                        if (count > 0)
                        {
                            lblError.Text = "Tên tài khoản này đã tồn tại!";
                            return;
                        }
                    }

                    // Thêm tài khoản mới vào bảng Users
                    string insertUser = "INSERT INTO Users (username, password_hash, display_name) VALUES (@user, @pass, @display); SELECT LAST_INSERT_ID();";
                    long newUserId = 0;
                    using (MySqlCommand cmdInsert = new MySqlCommand(insertUser, conn))
                    {
                        cmdInsert.Parameters.AddWithValue("@user", user);
                        cmdInsert.Parameters.AddWithValue("@pass", pass);
                        cmdInsert.Parameters.AddWithValue("@display", display);

                        newUserId = Convert.ToInt64(cmdInsert.ExecuteScalar());
                    }

                    // Khởi tạo Elo mặc định cho Cờ Vua và Cờ Caro trong UserGameStats
                    string insertStats = "INSERT INTO UserGameStats (user_id, game_type, elo) VALUES (@userId, 'CHESS', 1200), (@userId, 'CARO', 1200);";
                    using (MySqlCommand cmdStats = new MySqlCommand(insertStats, conn))
                    {
                        cmdStats.Parameters.AddWithValue("@userId", newUserId);
                        cmdStats.ExecuteNonQuery();
                    }

                    MessageBox.Show("Tạo tài khoản thành công! Bạn có thể đăng nhập ngay.", "Thành công",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Program.Context.SwitchTo(new LoginForm());
                }
            }
            catch (Exception ex)
            {
                lblError.Text = "Lỗi kết nối CSDL!";
                MessageBox.Show("Chi tiết lỗi: " + ex.Message, "Lỗi Database", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new LoginForm());
        }
    }
}