namespace OnlineChessGame
{
    public partial class LoginForm : Form
    {
        // === EVENT: Network Hook sẵn sàng nối Socket TCP ===
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
            string server = txtServer.Text.Trim();

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

            // Nơi gắn Socket Client gửi gói tin Đăng nhập tới Server
            if (OnLoginRequested != null)
            {
                OnLoginRequested.Invoke(user, pass, server);
                return;
            }

            // Chế độ demo giao diện: mở sảnh Dashboard
            Program.Context.SwitchTo(new DashboardForm(user, 1200));
        }

        private void BtnRegister_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new RegisterForm());
        }
    }
}
