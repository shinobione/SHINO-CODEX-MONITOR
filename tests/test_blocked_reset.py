from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
FORM = (ROOT / "src" / "MonitorForm.cs").read_text(encoding="utf-8")
SELF = (ROOT / "src" / "SelfTests.cs").read_text(encoding="utf-8")


class BlockedResetRegressionTests(unittest.TestCase):
    def test_blocked_uses_fresh_reset_instead_of_blocked_placeholder(self):
        self.assertIn("settings.Layout, live || blocked, UiNow", FORM)
        self.assertNotIn('meter.Reset.Text = blocked ? "Codex usage blocked"', FORM)
        self.assertIn("if (!sourceCurrent) return \"Stale reading · reset unverified\";", FORM)

    def test_block_state_remains_visible_outside_reset_label(self):
        self.assertIn('status.Text="USAGE BLOCKED / LAST KNOWN"', FORM)
        self.assertIn('if(blocked)', FORM)
        self.assertIn('status.ForeColor=InstrumentStyle.Red', FORM)

    def test_absent_or_expired_reset_not_fabricated(self):
        self.assertIn('if (!utc.HasValue) return "Reset not provided";', FORM)
        self.assertIn('if (left <= TimeSpan.Zero) return "Reset due · checking…";', FORM)
        self.assertIn('if (style == "hidden") return "";', FORM)

    def test_all_four_menu_options_still_repaint(self):
        for style in ("both", "absolute", "relative", "hidden"):
            self.assertIn(f'"{style}", delegate(string value) {{ settings.ResetStyle = value; PaintSnapshot(); Save(); }}', FORM)
        self.assertIn('if (layout == "Circular" && style == "both")', FORM)

    def test_tooltip_recognizes_fresh_blocked_timestamp(self):
        self.assertIn('(live || blocked) && data.ResetUtc.HasValue', FORM)
        self.assertIn('"Reset unavailable or unverified"', FORM)

    def test_windows_unit_tests_cover_regression(self):
        for title in ('Blocage et reset communiqué conservés ensemble',
                      'Reset bloqué 5 h : décompte conservé',
                      'Reset bloqué hebdo : date conservée',
                      'Reset masqué partout sur demande',
                      'Reset ancien non affiché comme actuel'):
            self.assertIn(title, SELF)


if __name__ == "__main__":
    unittest.main()
