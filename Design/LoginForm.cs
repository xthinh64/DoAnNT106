using MySql.Data.MySqlClient;

namespace OnlineChessGame
{
    public partial class LoginForm : Form
    {
        public event Action<string, string, string>? OnLoginRequested;

        public LoginForm()
        {
            InitializeComponent();
            ApplyCurrentTheme();
        }

        private void LoginForm_Load(object? sender, EventArgs e)
        {
            CenterCard();
        }

        private void CenterCard()
        {
            card.Location = new Point((ClientSize.Width - card.Width) / 2, (ClientSize.Height - card.Height) / 2);
            card.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, card.Width, card.Height), 16));
            btnLogin.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnLogin.Width, btnLogin.Height), 10));
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

            lblPass.ForeColor = ThemeColors.TextPrimary;
            txtPass.BackColor = ThemeColors.InputBg;
            txtPass.ForeColor = ThemeColors.InputFg;

            lblServer.ForeColor = ThemeColors.TextPrimary;
            txtServer.BackColor = ThemeColors.InputBg;
            txtServer.ForeColor = ThemeColors.InputFg;

            chkRemember.ForeColor = ThemeColors.TextSecondary;
            lblError.ForeColor = ThemeColors.Danger;

            btnLogin.BackColor = ThemeColors.Primary;
            btnLogin.ForeColor = ThemeColors.TextOnPrimary;

            btnRegister.ForeColor = ThemeColors.Primary;

            btnThemeToggle.Text = ThemeColors.CurrentTheme == AppTheme.Light ? "Theme: Sang" : "Theme: Den & Xanh";
            btnThemeToggle.BackColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(224, 238, 255) : Color.FromArgb(6, 78, 59);
            btnThemeToggle.ForeColor = ThemeColors.CurrentTheme == AppTheme.Light ? Color.FromArgb(37, 99, 235) : Color.FromArgb(52, 211, 153);
        }

        private void BtnThemeToggle_Click(object? sender, EventArgs e)
        {
            ThemeColors.ToggleTheme();
            ApplyCurrentTheme();
        }

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            string user = txtUser.Text.Trim();
            string pass = txtPass.Text.Trim();

            if (string.IsNullOrEmpty(user))
            {
                lblError.Text = "Vui long nhap ten tai khoan!";
                return;
            }
            if (string.IsNullOrEmpty(pass))
            {
                lblError.Text = "Vui long nhap mat khau!";
                return;
            }

            lblError.Text = "";

            // Thay đổi "Pwd=;" nếu MariaDB của bạn có mật khẩu (ví dụ: "Pwd=123456;")
            string connString = "Server=localhost;Database=onlinechessdb;Uid=root;Pwd=Taitn2007;";

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connString))
                {
                    conn.Open();

                    string checkQuery = "SELECT user_id, password_hash FROM Users WHERE username = @user";
                    using (MySqlCommand cmdCheck = new MySqlCommand(checkQuery, conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@user", user);
                        using (var reader = cmdCheck.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedPass = reader["password_hash"].ToString();
                                
                                if (storedPass == pass)
                                {
                                    MessageBox.Show("Đăng nhập thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    Program.Context.SwitchTo(new DashboardForm(user, 1200));
                                }
                                else
                                {
                                    lblError.Text = "Sai mật khẩu!";
                                }
                            }
                            else
                            {
                                lblError.Text = "Tài khoản không tồn tại!";
                                DialogResult dialogResult = MessageBox.Show(
                                    "Tài khoản chưa tồn tại. Bạn có muốn chuyển sang trang Đăng ký không?",
                                    "Thông báo",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question
                                );

                                if (dialogResult == DialogResult.Yes)
                                {
                                    Program.Context.SwitchTo(new RegisterForm());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lblError.Text = "Lỗi kết nối CSDL!";
                MessageBox.Show("Chi tiết lỗi: " + ex.Message, "Lỗi Database", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRegister_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new RegisterForm());
        }
    }
}