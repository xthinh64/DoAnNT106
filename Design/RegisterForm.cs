namespace OnlineChessGame
{
    public partial class RegisterForm : Form
    {
        // === EVENT: Network Hook sẵn sàng nối Socket TCP ===
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

            if (OnRegisterRequested != null)
            {
                OnRegisterRequested.Invoke(user, display, pass);
                return;
            }

            MessageBox.Show("Tao tai khoan thanh cong! Ban co the dang nhap ngay.", "Thanh cong",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Program.Context.SwitchTo(new LoginForm());
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new LoginForm());
        }
    }
}
