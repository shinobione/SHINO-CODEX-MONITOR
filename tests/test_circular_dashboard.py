from pathlib import Path
import re
import unittest

ROOT=Path(__file__).resolve().parents[1]
FORM=(ROOT/'src/MonitorForm.cs').read_text(encoding='utf-8')
MODELS=(ROOT/'src/Models.cs').read_text(encoding='utf-8')
PALETTE=(ROOT/'src/CircularGauge.cs').read_text(encoding='utf-8')
STORE=(ROOT/'src/LocalStore.cs').read_text(encoding='utf-8')

class CircularLayoutAudit(unittest.TestCase):
    def test_five_explicit_thresholds(self):
        for bound,field in [(76,'Color76'),(51,'Color51'),(26,'Color26'),(11,'Color11')]:
            self.assertIn(f'value >= {bound} ? p.{field}',PALETTE)
        self.assertIn(': p.Color0;',PALETTE)
        self.assertIn('Color76 = "#A477FF"',MODELS)
        self.assertIn('Color0 = "#EE4B55"',MODELS)

    def test_settings_preserved_and_rounding_consistent(self):
        self.assertIn('Layout != "Circular"',MODELS)
        for key in ('Color76','Color51','Color26','Color11','Color0'):
            self.assertIn(f'public string {key}',MODELS)
            self.assertIn(f'preferences.{key} = Json.String(',STORE)
        self.assertIn('QuotaPalette.ColorFor(p, settings)',FORM)
        self.assertIn('MidpointRounding.AwayFromZero',FORM)

    def test_new_layout_and_old_modes(self):
        for name in ('"Circular"','"Micro"','"Vertical"','"Compact"'):
            self.assertIn(name,FORM)
        self.assertIn('PlaceCircular(five',FORM)
        self.assertIn('PlaceCircular(week',FORM)
        self.assertIn('m.Ring.Visible=circular;',FORM)
        self.assertIn('m.Track.Visible=!circular;',FORM)

    def test_no_stale_quota_looks_fresh(self):
        self.assertIn('meter.Ring.SetReading(percent, live, color,exhausted)',FORM)
        self.assertIn('meter.Ring.SetReading(null, false',FORM)
        self.assertIn('current ? accent : ColorTranslator.FromHtml("#8BA0B1")',PALETTE)
        self.assertIn('statusAge.Visible=true;statusDivider.Visible=false;',FORM)
        self.assertIn('FreshnessLabel(lastSuccess,UiNow)',FORM)

    def test_double_buffer_low_cost(self):
        self.assertIn('ControlStyles.OptimizedDoubleBuffer',PALETTE)
        self.assertIn('if (percentage == clamped && current == isCurrent && accent == color && exhausted==rawExhausted) return;',PALETTE)
        self.assertNotIn('CodexReader',PALETTE)

    def test_menu_has_five_color_options(self):
        for label in ('76–100 %','51–75 %','26–50 %','11–25 %','0–10 %'):
            self.assertIn(label,FORM)
        self.assertIn('SelectTargetLayout(layout)',FORM)
        self.assertIn('new ToolStripMenuItem(mode)',FORM)
        self.assertNotIn('Obsidian visual parity layout',FORM)

if __name__=='__main__':
    unittest.main()
