import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]

class ServerEntrypointContract(unittest.TestCase):
    def test_launcher_requires_external_secrets_and_starts_cluster(self):
        path = ROOT / 'Server/Program.cs'
        self.assertTrue(path.exists(), 'Missing executable server entry point')
        source = path.read_text(encoding='utf-8-sig')
        for value in ['CHESS_DB_CONNECTION', 'CHESS_CERT_PATH', 'CHESS_CERT_PASSWORD', 'new AuthCluster', 'StartAsync', 'CancelKeyPress']:
            self.assertIn(value, source)
        self.assertNotIn('Pwd=', source)

    def test_cluster_uses_shared_auth_admission(self):
        source = (ROOT / 'Server/Program.cs').read_text(encoding='utf-8-sig')
        self.assertIn('new AuthAdmissionService(new MySqlAuthService(', source)

    def test_legacy_migration_requires_explicit_opt_in(self):
        source = (ROOT / 'Server/Program.cs').read_text(encoding='utf-8-sig')
        self.assertIn('AUTH_ALLOW_LEGACY_MIGRATION', source)
        self.assertIn('allowLegacyMigration: allowLegacy', source)

if __name__ == '__main__':
    unittest.main()
