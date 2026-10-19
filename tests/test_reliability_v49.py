from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
FORM = (ROOT / 'src/MonitorForm.cs').read_text(encoding='utf-8')
SELF = (ROOT / 'src/SelfTests.cs').read_text(encoding='utf-8')
ASSEMBLY = (ROOT / 'src/AssemblyInfo.cs').read_text(encoding='utf-8')


class ReliabilityV49Audit(unittest.TestCase):
    def test_offscreen_saved_location_is_clamped_to_connected_screen(self):
        self.assertIn('Screen.FromRectangle(proposed).WorkingArea', FORM)
        self.assertIn('DesktopGeometry.ClampToScreen(proposed,', FORM)
        self.assertNotIn('if (screen.WorkingArea.IntersectsWith(proposed))', FORM)

    def test_saved_position_only_applies_when_both_coordinates_known(self):
        self.assertIn('if (settings.X.HasValue && settings.Y.HasValue)', FORM)
        self.assertIn('Rectangle proposed = new Rectangle(settings.X.Value, settings.Y.Value, Width, Height);', FORM)

    def test_current_tray_version_is_derived_from_assembly(self):
        self.assertIn('typeof(MonitorForm).Assembly.GetName().Version.ToString(3)', FORM)
        self.assertNotIn('Text = "SHINO // CODEX MONITOR V4.7.2"', FORM)

    def test_multiscreen_geometry_regressions_run_in_csharp(self):
        for title in ('ancienne position hors écran récupérée',
                      'secondaire négatif reste sur son écran',
                      'widget vertical trop bas ramené à l\'écran',
                      'géométrie récupérée sans modifier le quota'):
            self.assertIn(title, SELF)

    def test_version_and_existing_guardrails(self):
        self.assertIn('5.1.0.0', ASSEMBLY)
        self.assertIn('SHINO // CODEX MONITOR V5.1.0 PREVIEW', FORM)
        self.assertIn('if (forcedStale) return', FORM)
        self.assertIn('settings.Layout, live || blocked, DateTimeOffset.UtcNow', FORM)

if __name__ == '__main__':
    unittest.main()
