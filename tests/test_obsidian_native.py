from pathlib import Path
import hashlib
import json
import unittest

ROOT = Path(__file__).resolve().parents[1]
FORM = (ROOT / 'src/MonitorForm.cs').read_text(encoding='utf-8')
DECK = (ROOT / 'src/MissionPresentation.cs').read_text(encoding='utf-8')
HARNESS = (ROOT / 'src/InstrumentTests.cs').read_text(encoding='utf-8')


class ObsidianNative(unittest.TestCase):
    def test_slice_preserves_collectors_identity_settings_history_and_alerts(self):
        expected = json.loads((ROOT / 'tests/obsidian-core-sha256.json').read_text())
        for name, digest in expected.items():
            source=(ROOT / 'src' / name).read_text(encoding='utf-8')
            # Only additive appearance fields/loader lines may differ from the frozen core.
            additions=[line for line in source.splitlines() if '// PARITY_APPEARANCE_ONLY' in line]
            expected_additions={'Models.cs':3,'LocalStore.cs':2}.get(name,0)
            self.assertEqual(len(additions),expected_additions,name)
            source=''.join(line for line in source.splitlines(keepends=True) if '// PARITY_APPEARANCE_ONLY' not in line)
            self.assertEqual(hashlib.sha256(source.encode('utf-8')).hexdigest(), digest, name)

    def test_entire_production_window_has_isolated_render_entry(self):
        self.assertIn('new MonitorForm(p)', HARNESS)
        self.assertIn('window.DrawToBitmap', HARNESS)
        self.assertIn('internal MonitorForm(Preferences appearance) : this(null, appearance)', FORM)
        self.assertIn('if (store != null) { tick.Start(); Heartbeat(); }', FORM)
        for call in ('new LocalStore(', 'CodexReader.Read(', 'LiveTaskReader.Read('):
            self.assertNotIn(call, HARNESS)

    def test_native_bezels_and_fonts_need_no_web_runtime(self):
        style = (ROOT / 'src/InstrumentStyle.cs').read_text(encoding='utf-8')
        for value in ('Bitmap', 'DrawImageUnscaled', 'Segoe UI', 'Dispose', '#32E0C4'):
            self.assertIn(value, style)
        for value in ('WebView', 'HttpClient', 'https://', 'Download', 'Tailwind'):
            self.assertNotIn(value, style)
        self.assertIn('"Obsidian / Sonar", "#080D14"', FORM)

    def test_overflow_is_explicit_vertical_only(self):
        self.assertIn('AutoScroll=false', DECK)
        self.assertIn('viewport.AutoScrollMinSize=Size.Empty', DECK)
        self.assertIn('cards.Count>2 && extent>height', DECK)
        self.assertIn('if(arranging', DECK)
        self.assertIn('Math.Max(1,(viewport.Width-(columns-1)*gap)/columns)', DECK)

    def test_cached_usage_never_suppresses_independent_task_source(self):
        for value in ('LAST KNOWN', 'USAGE BLOCKED', 'dd MMM HH:mm:ss', 'Task availability is independent'):
            self.assertIn(value, FORM)
        self.assertIn('Blocked quotas do not change observed JSONL missions', HARNESS)
        self.assertIn('Production motion timer stops hidden/minimized/suspended', HARNESS)

    def test_full_window_evidence_and_performance_are_ci_gates(self):
        build = (ROOT / 'BUILD-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        ci = (ROOT / '.github/workflows/build-windows.yml').read_text(encoding='utf-8')
        self.assertIn('--instrument-self-test', build)
        self.assertIn('All instrument checks passed', build)
        self.assertIn('--instrument-performance', ci)
        self.assertIn('bin/instrument-qa/after/*.png', ci)
        for value in ('100,125,150,200', 'A completion keeps B identity', 'BoundsCheck', 'GetGuiResources'):
            self.assertIn(value, HARNESS)


if __name__ == '__main__':
    unittest.main()
