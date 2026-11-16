using OnlineChessGame.Network;

namespace OnlineChessGame
{
    public partial class RegisterForm : Form
    {
        private readonly CancellationTokenSource lifetime = new();
        public event Action<string, string, string>? OnRegisterRequested;

        public RegisterForm()
        {
            InitializeComponent();
            ApplyCurrentTheme();
            Disposed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
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

        private async void BtnRegister_Click(object? sender, EventArgs e)
        {
            string user = txtUser.Text.Trim();
            string display = txtDisplay.Text.Trim();
            string pass = txtPass.Text;
            if (string.IsNullOrWhiteSpace(user) || user.Length > 50 || string.IsNullOrWhiteSpace(display) || display.Length > 100)
            {
                lblError.Text = "Tài khoản tối đa 50, tên hiển thị tối đa 100 ký tự.";
                return;
            }
            if (pass.Length < 8 || pass.Length > 128)
            {
                lblError.Text = "Mật khẩu phải có từ 8 đến 128 ký tự.";
                return;
            }
            if (pass != txtConfirm.Text)
            {
                lblError.Text = "Mật khẩu xác nhận không trùng khớp.";
                return;
            }
            btnRegister.Enabled = btnBack.Enabled = false;
            lblError.Text = "Đang đăng ký qua TLS...";
            try
            {
                var result = await AuthConnection.CreateClient().SendAsync(
                    new AuthRequest("signup", user, pass, display), lifetime.Token);
                if (IsDisposed) return;
                txtPass.Clear();
                txtConfirm.Clear();
                if (result.Success)
                {
                    MessageBox.Show("Tạo tài khoản thành công. Hãy đăng nhập.", "Thông báo");
                    Program.Context.SwitchTo(new LoginForm());
                    return;
                }
                lblError.Text = result.Message;
            }
            catch (OperationCanceledException) { if (!IsDisposed) lblError.Text = "Kết nối đã hủy hoặc hết thời gian chờ."; }
            catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException or ArgumentException or InvalidOperationException)
            {
                if (!IsDisposed) lblError.Text = "Không thể kết nối TLS. Kiểm tra server và pin chứng chỉ.";
            }
            finally
            {
                if (!IsDisposed) btnRegister.Enabled = btnBack.Enabled = true;
            }
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new LoginForm());
        }
    }
}