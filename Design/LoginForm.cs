using OnlineChessGame.Network;

namespace OnlineChessGame
{
    public partial class LoginForm : Form
    {
        private readonly CancellationTokenSource lifetime = new();
        public event Action<string, string, string>? OnLoginRequested;

        public LoginForm()
        {
            InitializeComponent();
            ApplyCurrentTheme();
            Disposed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
            txtServer.Text = AuthConnection.Endpoint;
        }

        public LoginForm(string username, string status) : this()
        {
            txtUser.Text = username;
            lblError.Text = status;
            ActiveControl = txtPass;
        }

        private void LoginForm_Load(object? sender, EventArgs e)
        {
            CenterCard();
            ActiveControl = txtUser.Text.Length == 0 ? txtUser : txtPass;
        }

        private void CenterCard()
        {
            card.Location = new Point(Math.Max(16, (ClientSize.Width - card.Width) / 2), Math.Max(16, (ClientSize.Height - card.Height) / 2));
            card.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, card.Width, card.Height), 16));
            btnLogin.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnLogin.Width, btnLogin.Height), 10));
            btnThemeToggle.Region = new Region(UIHelper.RoundedRect(new Rectangle(0, 0, btnThemeToggle.Width, btnThemeToggle.Height), 8));
        }

        private void LoginForm_Resize(object? sender, EventArgs e)
        {
            CenterCard();
        }

        private void ChkShowPassword_CheckedChanged(object? sender, EventArgs e)
        {
            txtPass.UseSystemPasswordChar = !chkShowPassword.Checked;
        }

        private void SetBusy(bool busy)
        {
            txtUser.Enabled = txtPass.Enabled = chkShowPassword.Enabled = !busy;
            btnLogin.Enabled = btnRegister.Enabled = !busy;
            btnConnection.Enabled = txtServer.Enabled = !busy;
            btnLogin.Text = busy ? "Đang đăng nhập…" : "Đăng nhập";
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

            lblPass.ForeColor = ThemeColors.TextPrimary;
            txtPass.BackColor = ThemeColors.InputBg;
            txtPass.ForeColor = ThemeColors.InputFg;

            lblServer.ForeColor = ThemeColors.TextPrimary;
            txtServer.BackColor = ThemeColors.InputBg;
            txtServer.ForeColor = ThemeColors.InputFg;

            btnConnection.ForeColor = ThemeColors.TextSecondary;
            chkShowPassword.ForeColor = ThemeColors.TextSecondary;
            lblError.ForeColor = ThemeColors.Danger;

            btnLogin.BackColor = ThemeColors.Primary;
            btnLogin.ForeColor = ThemeColors.TextOnPrimary;

            btnRegister.ForeColor = ThemeColors.Primary;

            btnThemeToggle.Text = ThemeColors.CurrentTheme == AppTheme.Light ? "Giao diện sáng" : "Giao diện tối";
            btnThemeToggle.BackColor = ThemeColors.PrimaryLight;
            btnThemeToggle.ForeColor = ThemeColors.Primary;
        }

        private void BtnThemeToggle_Click(object? sender, EventArgs e)
        {
            ThemeColors.ToggleTheme();
            ApplyCurrentTheme();
        }

        private void BtnConnection_Click(object? sender, EventArgs e)
        {
            pnlConnection.Visible = !pnlConnection.Visible;
            btnConnection.Text = pnlConnection.Visible ? "Ẩn cấu hình kết nối" : "Cấu hình kết nối";
            card.Height = pnlConnection.Visible ? pnlConnection.Bottom + 24 : btnConnection.Bottom + 32;
            CenterCard();
            if (pnlConnection.Visible) txtServer.Focus();
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            string user = txtUser.Text.Trim();
            string pass = txtPass.Text;
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(pass))
            {
                lblError.Text = "Vui lòng nhập tên đăng nhập và mật khẩu.";
                (string.IsNullOrWhiteSpace(user) ? txtUser : txtPass).Focus();
                return;
            }
            SetBusy(true);
            lblError.Text = "Đang xác thực qua kết nối bảo mật…";
            try
            {
                AuthConnection.Endpoint = txtServer.Text.Trim();
                var result = await AuthConnection.CreateClient().SendAsync(
                    new AuthRequest("signin", user, pass), lifetime.Token);
                if (IsDisposed) return;
                txtPass.Clear();
                if (result.Success && result.Player is { } player)
                {
                    AuthConnection.Player = player;
                    AuthConnection.SessionToken = result.SessionToken;
                    Program.Context.SwitchTo(new DashboardForm(player.Username, player.ChessElo));
                    return;
                }
                lblError.Text = result.Code switch
                {
                    "invalid_credentials" => "Tên đăng nhập hoặc mật khẩu không đúng.",
                    "invalid_request" => "Thông tin đăng nhập không hợp lệ.",
                    "reset_required" => "Tài khoản cần đặt lại mật khẩu. Vui lòng liên hệ quản trị viên.",
                    "service_unavailable" => "Dịch vụ đăng nhập tạm thời không khả dụng. Vui lòng thử lại.",
                    _ => "Không thể đăng nhập. Vui lòng thử lại."
                };
            }
            catch (OperationCanceledException) { if (!IsDisposed) lblError.Text = "Kết nối đã hủy hoặc hết thời gian chờ."; }
            catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException or ArgumentException or InvalidOperationException)
            {
                if (!IsDisposed) lblError.Text = "Không thể xác thực chứng chỉ TLS. Kiểm tra địa chỉ máy chủ và CA tin cậy.";
            }
            finally
            {
                if (!IsDisposed) SetBusy(false);
            }
        }

        private void BtnRegister_Click(object? sender, EventArgs e)
        {
            AuthConnection.Endpoint = txtServer.Text.Trim();
            Program.Context.SwitchTo(new RegisterForm());
        }
    }
}
