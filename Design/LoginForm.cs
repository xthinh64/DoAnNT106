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
            chkRemember.Checked = false;
            chkRemember.Enabled = false;
            chkRemember.Text = "Không lưu mật khẩu trên máy";
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

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            string user = txtUser.Text.Trim();
            string pass = txtPass.Text;
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(pass))
            {
                lblError.Text = "Vui lòng nhập tài khoản và mật khẩu.";
                return;
            }
            btnLogin.Enabled = btnRegister.Enabled = false;
            lblError.Text = "Đang kết nối bảo mật...";
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
                    Program.Context.SwitchTo(new DashboardForm(player.Username, player.ChessElo));
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
                if (!IsDisposed) btnLogin.Enabled = btnRegister.Enabled = true;
            }
        }

        private void BtnRegister_Click(object? sender, EventArgs e)
        {
            AuthConnection.Endpoint = txtServer.Text.Trim();
            Program.Context.SwitchTo(new RegisterForm());
        }
    }
}