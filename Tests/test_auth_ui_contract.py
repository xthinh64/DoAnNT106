"""Source-level UI wiring regression checks; not a GUI automation test."""
import pathlib
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

    def test_no_password_remembering(self):
        source = (ROOT / 'Design/AuthConnection.cs').read_text(encoding='utf-8-sig')
        self.assertIn('CHESS_CERT_PIN', source)
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

    def test_ranked_ui_is_honest_until_gameplay_server_exists(self):
        dashboard = (ROOT / 'Design/DashboardForm.cs').read_text(encoding='utf-8-sig')
        self.assertIn('Đấu Rank chưa được kết nối với server gameplay', dashboard)
        self.assertNotIn('MatchmakingQueue', dashboard)
        chess = (ROOT / 'Design/ChessBoardForm.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('SaveGameResultToDatabase', chess)

if __name__ == '__main__':
    unittest.main()
