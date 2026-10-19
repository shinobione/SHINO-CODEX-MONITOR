from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class P0Architecture(unittest.TestCase):
    def test_live_ui_consumes_mission_collection(self):
        code = (ROOT / 'src/MonitorForm.cs').read_text(encoding='utf-8')
        self.assertIn('taskDeck.UpdateReport(taskReport,now)', code)
        self.assertNotIn('display.Primary', code)
        self.assertIn('taskDeck.ConfirmCompletion(terminal)', code)

    def test_cards_share_clock_and_have_no_private_windows_timer(self):
        code = (ROOT / 'src/MissionPresentation.cs').read_text(encoding='utf-8')
        self.assertEqual(code.count('new Stopwatch()'), 1)
        self.assertNotIn('new System.Windows.Forms.Timer', code)
        self.assertIn('SetSharedMotion', code)

    def test_bounded_reader_keeps_only_sanitized_turn_cache(self):
        code = (ROOT / 'src/LiveTaskMonitor.cs').read_text(encoding='utf-8')
        self.assertIn('MaxTurnsPerSession = 64', code)
        self.assertIn('expansionBudget = ActiveRootFallbackBytes', code)
        self.assertIn('Turns = turns.Select(t => t.Copy())', code)
        for unsafe in ('auth.json', 'HttpClient', 'File.WriteAll', 'Process.Start'):
            self.assertNotIn(unsafe, code)

    def test_native_icon_is_reapplied_without_using_quota_tray_image(self):
        code = (ROOT / 'src/PeriscopeWindow.cs').read_text(encoding='utf-8')
        self.assertIn('OnHandleCreated', code)
        self.assertIn('0x02E0', code)
        self.assertIn('0x0080', code)
        self.assertIn('DestroyIcon', code)
        self.assertNotIn('QuotaTrayIcon.Create', code)
        for file in ('MonitorForm.cs', 'HistoryWindow.cs'):
            self.assertIn(': PeriscopeWindow', (ROOT / 'src' / file).read_text(encoding='utf-8'))


if __name__ == '__main__':
    unittest.main()
