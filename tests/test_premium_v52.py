"""Architecture guards; behavioral, drawing and timing tests run in native C#."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class PremiumGuards(unittest.TestCase):
    def test_history_is_read_only_and_reports_invalid_samples(self):
        path = ROOT / 'src/HistoryData.cs'
        self.assertTrue(path.exists(), 'History normalization is not implemented')
        code = path.read_text(encoding='utf-8')
        self.assertNotIn('File.Write', code)
        self.assertNotIn('File.Delete', code)
        self.assertIn('InvalidRows', code)
        self.assertIn('TimeSpan.FromMinutes(30)', code)

    def test_observations_are_never_pruned_by_recording(self):
        code = (ROOT / 'src/LocalStore.cs').read_text(encoding='utf-8')
        self.assertNotIn('Prune();', code)

    def test_animation_uses_monotonic_time_and_stops_when_hidden(self):
        code = (ROOT / 'src/ActivityGlyph.cs').read_text(encoding='utf-8')
        self.assertIn('Stopwatch', code)
        self.assertIn('Stale', code)
        self.assertIn('Unavailable', code)
        self.assertNotIn('angle + 13', code)

    def test_native_qa_is_required_by_preview_build(self):
        build = (ROOT / 'BUILD-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        self.assertIn('--premium-self-test', build)
        self.assertIn('PREMIUM_TEST_REPORT', build)

    def test_history_exposes_required_ranges_and_accessible_data(self):
        code = (ROOT / 'src/HistoryWindow.cs').read_text(encoding='utf-8')
        for text in ('24H', '3D', '7D', '30D', 'ALL', 'BOTH', 'DataGridView',
                     'Quota remaining (%)', 'Recorded Codex quota samples'):
            self.assertIn(text, code)


if __name__ == '__main__':
    unittest.main()
