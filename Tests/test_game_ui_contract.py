"""Focused wiring checks; runtime controls are exercised by UiSmoke."""
import pathlib
import unittest
ROOT = pathlib.Path(__file__).resolve().parents[1]

class GameUiContract(unittest.TestCase):
    def test_lobby_does_not_invent_history(self):
        source = (ROOT / 'Design/DashboardForm.cs').read_text(encoding='utf-8-sig')
        self.assertNotIn('GrandMaster_VN', source)
        self.assertIn('AuthConnection.Player = null', source)

    def test_caro_uses_engine_and_owned_delay(self):
        source = (ROOT / 'Design/CaroBoardForm.cs').read_text(encoding='utf-8-sig')
        self.assertIn('CaroEngine.FindBestMove', source)
        self.assertIn('botDelay?.Dispose()', source)
        self.assertNotIn('new Random()', source)
        self.assertNotIn('MessageBox.Show(message', source)

if __name__ == '__main__':
    unittest.main()
