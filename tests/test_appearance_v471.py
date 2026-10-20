from pathlib import Path
import re
import unittest

ROOT=Path(__file__).resolve().parents[1]
FORM=(ROOT/'src/MonitorForm.cs').read_text(encoding='utf-8')
MODELS=(ROOT/'src/Models.cs').read_text(encoding='utf-8')
STORE=(ROOT/'src/LocalStore.cs').read_text(encoding='utf-8')
TESTS=(ROOT/'src/SelfTests.cs').read_text(encoding='utf-8')


class AppearancePolishAudit(unittest.TestCase):
    def test_micro_geometry_and_header_close_are_separate(self):
        self.assertIn('width=micro?484:', FORM)
        self.assertIn('quotaHeight=micro?122:', FORM)
        self.assertIn('PlaceMeter(week,240,31,232,true)', FORM)
        self.assertIn('close.Bounds=Units(new Rectangle(width-34,micro?3:7,25,24))', FORM)
        self.assertIn('m.Face.Bounds=Units(new Rectangle(x,y,width,micro?62:80))', FORM)

    def test_circular_instruments_share_bounded_inset_faces(self):
        self.assertIn('quotaHeight=DesktopGeometry.HeightForWidth(width)', FORM)
        self.assertIn('PlaceCircular(five,14,246)', FORM)
        self.assertIn('PlaceCircular(week,280,246)', FORM)
        self.assertIn('m.Ring.Bounds=Units(DesktopGeometry.ScaleRect((width-118)/2,24,118,118,w))', FORM)

    def test_opacity_control_persisted_and_live(self):
        for value in ('70','80','90','96','100'):
            self.assertIn(value,FORM)
        self.assertIn('Opacity = settings.Opacity;',FORM)
        self.assertIn('public double Opacity',MODELS)
        self.assertIn('preferences.Opacity = Json.Double(',STORE)

    def test_dark_background_presets_and_custom_validation(self):
        for hexcolor in ('#111926','#090C13','#22272E','#102C36'):
            self.assertIn(hexcolor,FORM)
        self.assertIn('Preferences.IsDarkBackground(hex)',FORM)
        self.assertIn('internal static bool IsDarkBackground(string color)',MODELS)
        self.assertIn('preferences.BackgroundColor = Json.String(',STORE)
        self.assertIn('five.Ring.BackColor = Color.Transparent;',FORM)
        self.assertIn('week.Ring.BackColor = Color.Transparent;',FORM)

    def test_diagnostic_uses_assembly_version(self):
        self.assertIn('runningVersion.ToString(3)',FORM)
        self.assertNotIn('return "Version : 4.6.0',FORM)
        self.assertIn('PERISCOPE / V5.2.0',FORM)
        self.assertIn('CODEX PERISCOPE V5.2.0 —',TESTS)

    def test_no_codex_reader_changes_or_global_security_bypass(self):
        self.assertNotIn('CodexReader',MODELS)
        self.assertNotIn('Set-MpPreference',FORM)
        self.assertNotIn('WebClient',FORM)
        self.assertIn('store.Save(settings)',(ROOT/'src/MonitorForm.cs').read_text(encoding='utf-8'))

if __name__=='__main__':
    unittest.main()
