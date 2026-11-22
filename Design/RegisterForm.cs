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
            ActiveControl = txtUser;
        }

        private void CenterCard()
        {
            card.Location = new Point(Math.Max(16, (ClientSize.Width - card.Width) / 2), Math.Max(16, (ClientSize.Height - card.Height) / 2));
            card.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, card.Width, card.Height), 16));
            btnRegister.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnRegister.Width, btnRegister.Height), 10));
            btnThemeToggle.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnThemeToggle.Width, btnThemeToggle.Height), 8));
        }

        private void RegisterForm_Resize(object? sender, EventArgs e)
        {
            CenterCard();
        }

        private void ChkShowPassword_CheckedChanged(object? sender, EventArgs e)
        {
            txtPass.UseSystemPasswordChar = !chkShowPassword.Checked;
            txtConfirm.UseSystemPasswordChar = !chkShowPassword.Checked;
        }

        private void SetBusy(bool busy)
        {
            txtUser.Enabled = txtPass.Enabled = chkShowPassword.Enabled = !busy;
            txtConfirm.Enabled = txtDisplay.Enabled = !busy;
            btnRegister.Enabled = btnBack.Enabled = !busy;
            btnRegister.Text = busy ? "Đang tạo tài khoản…" : "Tạo tài khoản";
            UseWaitCursor = busy;
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

            chkShowPassword.ForeColor = ThemeColors.TextSecondary;
            lblError.ForeColor = ThemeColors.Danger;

            btnRegister.BackColor = ThemeColors.Primary;
            btnRegister.ForeColor = ThemeColors.TextOnPrimary;

            btnBack.ForeColor = ThemeColors.Primary;

            btnThemeToggle.Text = ThemeColors.CurrentTheme == AppTheme.Light ? "Giao diện sáng" : "Giao diện tối";
            btnThemeToggle.BackColor = ThemeColors.PrimaryLight;
            btnThemeToggle.ForeColor = ThemeColors.Primary;
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
            if (string.IsNullOrWhiteSpace(display)) display = user;
            string pass = txtPass.Text;
            if (string.IsNullOrWhiteSpace(user) || user.Length > 50 || display.Length > 100)
            {
                lblError.Text = "Nhập tên đăng nhập (tối đa 50 ký tự); tên hiển thị tối đa 100 ký tự.";
                (string.IsNullOrWhiteSpace(user) || user.Length > 50 ? txtUser : txtDisplay).Focus();
                return;
            }
            if (pass.Length < 8 || pass.Length > 128)
            {
                lblError.Text = "Mật khẩu phải có từ 8 đến 128 ký tự.";
                txtPass.Focus();
                return;
            }
            if (pass != txtConfirm.Text)
            {
                lblError.Text = "Mật khẩu xác nhận không trùng khớp.";
                txtConfirm.Focus();
                return;
            }
            SetBusy(true);
            lblError.Text = "Đang tạo tài khoản qua kết nối bảo mật…";
            try
            {
                var result = await AuthConnection.CreateClient().SendAsync(
                    new AuthRequest("signup", user, pass, display), lifetime.Token);
                if (IsDisposed) return;
                txtPass.Clear();
                txtConfirm.Clear();
                if (result.Success)
                {
                    Program.Context.SwitchTo(new LoginForm(user, "Tạo tài khoản thành công. Nhập mật khẩu để đăng nhập."));
                    return;
                }
                lblError.Text = result.Code switch
                {
                    "username_taken" => "Tên đăng nhập đã được sử dụng. Vui lòng chọn tên khác.",
                    "invalid_request" => "Thông tin đăng ký không hợp lệ. Kiểm tra các trường bên trên.",
                    "service_unavailable" => "Dịch vụ đăng ký tạm thời không khả dụng. Vui lòng thử lại.",
                    _ => "Không thể tạo tài khoản. Vui lòng thử lại."
                };
            }
            catch (OperationCanceledException) { if (!IsDisposed) lblError.Text = "Kết nối đã hủy hoặc hết thời gian chờ."; }
            catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException or ArgumentException or InvalidOperationException)
            {
                if (!IsDisposed) lblError.Text = "Không thể kết nối TLS. Kiểm tra máy chủ và mã ghim chứng chỉ.";
            }
            finally
            {
                if (!IsDisposed) SetBusy(false);
            }
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            Program.Context.SwitchTo(new LoginForm());
        }
    }
}
