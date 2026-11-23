"""Source-level UI wiring regression checks; not a GUI automation test."""
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]

class AuthUiContract(unittest.TestCase):
    def test_signin_uses_async_transport_not_database(self):
        source = (ROOT / 'Design/LoginForm.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('MySql', source)
        self.assertIn('async void BtnLogin_Click', source)
        self.assertIn('await', source)
        self.assertIn('"signin"', source)
        self.assertNotIn('txtPass.Text.Trim()', source)
        self.assertIn('txtServer.Text', source)

    def test_signup_uses_same_transport_and_preserves_password(self):
        source = (ROOT / 'Design/RegisterForm.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('MySql', source)
        self.assertIn('async void BtnRegister_Click', source)
        self.assertIn('"signup"', source)
        self.assertNotIn('txtPass.Text.Trim()', source)
        self.assertIn('pass.Length < 8', source)

    def test_auth_credentials_first_and_accessible(self):
        for form in ('LoginForm', 'RegisterForm'):
            source = (ROOT / f'Design/{form}.cs').read_text(encoding='utf-8-sig')
            designer = (ROOT / f'Design/{form}.Designer.cs').read_text(encoding='utf-8-sig')
            self.assertNotIn('chkRemember', source + designer)
            self.assertNotRegex(designer, r'this\.txt(?:User|Pass|Confirm)\.Text\s*=')
            self.assertIn('this.AcceptButton = this.', designer)
            self.assertIn('this.ActiveControl = this.txtUser;', designer)
            self.assertIn('this.txtPass.UseSystemPasswordChar = true;', designer)
            self.assertIn('this.chkShowPassword.CheckedChanged +=', designer)
            self.assertIn('txtPass.UseSystemPasswordChar = !chkShowPassword.Checked;', source)
            self.assertIn('SetBusy(true)', source)
            self.assertIn('SetBusy(false)', source)
            self.assertIn('txtUser.Enabled = txtPass.Enabled', source)
            self.assertIn('lifetime.Token', source)
            self.assertIn('lifetime.Cancel()', source)
            self.assertIn('ThemeColors.ToggleTheme()', source)
            self.assertIn('this.card.Size = new System.Drawing.Size(560,', designer)
            self.assertIn('this.AutoScroll = true;', designer)
            self.assertIn('Math.Max(16,', source)
            self.assertIn('this.lblError.Size = new System.Drawing.Size(480, 44);', designer)
            self.assertIn('System.Drawing.ContentAlignment.MiddleLeft', designer)
            self.assertNotRegex(designer.split('private void InitializeComponent()', 1)[1], r'=>|\b(?:if|foreach|await)\s*\(')

    def test_optional_display_and_nonmodal_registration_success(self):
        source = (ROOT / 'Design/RegisterForm.cs').read_text(encoding='utf-8-sig')
        self.assertIn('if (string.IsNullOrWhiteSpace(display)) display = user;', source)
        for bound in ('user.Length > 50', 'display.Length > 100', 'pass.Length > 128'):
            self.assertIn(bound, source)
        self.assertNotIn('MessageBox.Show', source)
        self.assertIn('new LoginForm(user,', source)
        self.assertIn('txtConfirm.UseSystemPasswordChar = !chkShowPassword.Checked;', source)

    def test_connection_configuration_is_designer_managed_and_collapsed(self):
        source = (ROOT / 'Design/LoginForm.cs').read_text(encoding='utf-8-sig')
        designer = (ROOT / 'Design/LoginForm.Designer.cs').read_text(encoding='utf-8-sig')
        self.assertIn('this.btnConnection = new System.Windows.Forms.Button();', designer)
        self.assertIn('this.btnConnection.Text = "Cấu hình kết nối";', designer)
        self.assertIn('this.pnlConnection.Visible = false;', designer)
        self.assertIn('this.pnlConnection.Controls.Add(this.txtServer);', designer)
        self.assertIn('this.btnConnection.Click += new System.EventHandler(this.BtnConnection_Click);', designer)
        self.assertIn('pnlConnection.Visible = !pnlConnection.Visible;', source)
        self.assertIn('txtServer.Text = AuthConnection.Endpoint;', source)
        self.assertIn('btnConnection.Enabled = txtServer.Enabled = !busy;', source)

    def test_keyboard_order_keeps_credentials_first(self):
        for form, controls in (
            ('LoginForm', ['txtUser', 'txtPass', 'chkShowPassword', 'btnLogin', 'btnRegister', 'btnConnection']),
            ('RegisterForm', ['txtUser', 'txtPass', 'txtConfirm', 'chkShowPassword', 'txtDisplay', 'btnRegister', 'btnBack']),
        ):
            designer = (ROOT / f'Design/{form}.Designer.cs').read_text(encoding='utf-8-sig')
            indexes = [int(re.search(rf'this\.{control}\.TabIndex = (\d+);', designer).group(1)) for control in controls]
            self.assertEqual(indexes, sorted(set(indexes)))

    def test_default_auth_is_username_password_without_preset_credentials(self):
        login = (ROOT / 'Design/LoginForm.Designer.cs').read_text(encoding='utf-8-sig')
        register = (ROOT / 'Design/RegisterForm.cs').read_text(encoding='utf-8-sig')
        self.assertIn('this.pnlConnection.Visible = false', login)
        self.assertIn('this.AcceptButton = this.btnLogin', login)
        self.assertNotIn('this.txtUser.Text =', login)
        self.assertNotIn('this.txtPass.Text =', login)
        self.assertIn('if (string.IsNullOrWhiteSpace(display)) display = user;', register)

    def test_no_password_remembering(self):
        source = (ROOT / 'Design/AuthConnection.cs').read_text(encoding='utf-8-sig')
        self.assertIn('GatewayClient', source)
        self.assertNotIn('CHESS_CERT_PIN', source)
        self.assertNotIn('Password', source)

    def test_game_client_has_no_database_credentials_or_direct_mysql_access(self):
        sources = '\n'.join(
            path.read_text(encoding='utf-8-sig')
            for path in (ROOT / 'Design').glob('*.cs')
        )
        project = (ROOT / 'Design/OnlineChessGame.csproj').read_text(encoding='utf-8-sig')
        self.assertNotIn('MySql', sources)
        self.assertNotIn('Pwd=', sources)
        self.assertNotIn('MySql.Data', project)

    def test_matchmaking_ui_uses_game_server_without_database_writes(self):
        dashboard = (ROOT / 'Design/DashboardForm.cs').read_text(encoding='utf-8-sig')
        self.assertIn('GameClient.Instance.ConnectAsync', dashboard)
        self.assertIn('PacketType.FIND_MATCH', dashboard)
        self.assertIn('PacketType.CANCEL_MATCH', dashboard)
        self.assertIn('GameClient.Instance.OnMatchFound', dashboard)
        chess = (ROOT / 'Design/ChessBoardForm.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('SaveGameResultToDatabase', chess)

if __name__ == '__main__':
    unittest.main()
