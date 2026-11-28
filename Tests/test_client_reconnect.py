"""Source integration guards; runtime WinForms/TLS needs Windows/LAN acceptance."""
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1]

class ClientReconnectTests(unittest.TestCase):
    def test_lobby_opens_authenticated_connection_without_matchmaking(self):
        source = (ROOT / 'Design/DashboardForm.cs').read_text()
        self.assertIn('ResumeSessionAsync', source)
        self.assertIn('Shown +=', source)
        self.assertIn('GameClient.Instance.CurrentRoomId != roomId', source)

    def test_retry_is_bounded_serialized_and_cancellable(self):
        source = (ROOT / 'Design/GameClient.cs').read_text()
        for guard in ['TimeSpan.FromSeconds(90)', '_connectLock', 'CreateLinkedTokenSource',
                      'RequestTicketAsync', 'ReconnectAsync', 'ct.ThrowIfCancellationRequested()',
                      'if (!ReferenceEquals(_tcp, tcp)) break;']:
            self.assertIn(guard, source)

    def test_boards_do_not_end_game_on_transient_disconnect(self):
        for board in ['Chess', 'Caro']:
            source = (ROOT / f'Design/{board}BoardForm.cs').read_text()
            self.assertNotIn('OnlineDisconnected() => RunOnUi(() => EndGame', source)
            self.assertIn('GameClient.Instance.LeaveRoomAsync()', source)
            self.assertIn('connectionVersion', source)

    def test_cancelled_queue_uses_explicit_resumption_and_attempt_epoch(self):
        dashboard = (ROOT / 'Design/DashboardForm.cs').read_text()
        client = (ROOT / 'Design/GameClient.cs').read_text()
        self.assertIn('parts[2] == "RESUME"', client)
        self.assertIn('bool resumed = GameClient.Instance.IsResumedMatch', dashboard)
        self.assertIn('!resumed && (queuedMode != gameMode || attempt != queueEpoch ||', dashboard)
        self.assertIn('matchedAttemptId != queueAttemptId', dashboard)
        self.assertIn('QueueAttemptId: requestId', dashboard)
        self.assertIn('_ = GameClient.Instance.LeaveRoomAsync()', dashboard)

    def test_queued_error_callbacks_guard_connection_attempt_and_lifetime(self):
        source = (ROOT / 'Design/DashboardForm.cs').read_text()
        for method in ['GameDisconnected()', 'MatchUnavailable(string message)']:
            body = source.split('private void ' + method, 1)[1].split('\n        private ', 1)[0]
            for guard in ['connectionVersion != GameClient.Instance.ConnectionVersion',
                          'attempt != queueEpoch', 'lifetime.IsCancellationRequested']:
                self.assertIn(guard, body)

    def test_windows_smoke_gates_publish_and_retains_failure_log(self):
        source = (ROOT / '.github/workflows/build-release.yml').read_text()
        self.assertIn('Tests/ClientRecoverySmoke/ClientRecoverySmoke.csproj', source)
        self.assertLess(source.index('Tests/ClientRecoverySmoke/ClientRecoverySmoke.csproj'),
                        source.index('dotnet publish Design/OnlineChessGame.csproj'))
        self.assertIn('client-recovery-smoke.log', source)
        self.assertIn('if ($LASTEXITCODE -ne 0)', source)
        self.assertIn('name: client-recovery-smoke-${{ github.sha }}', source)

if __name__ == '__main__':
    unittest.main()
