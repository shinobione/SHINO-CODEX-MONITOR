from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCES = (
    'MonitorForm.cs','SourceHealth.cs','HistoryWindow.cs','LiveTaskMonitor.cs',
    'ReleasePackage.cs','ReleaseUpdater.cs','CodexReader.cs','Models.cs',
    'RuntimeMetrics.cs','Program.cs','UpdateHandoff.cs','UpdateDownloadSmoke.cs'
)

class CodexPeriscopeV52EnglishAudit(unittest.TestCase):
    def test_user_facing_sources_contain_no_french_accented_words(self):
        for filename in SOURCES:
            text = (ROOT / 'src' / filename).read_text(encoding='utf-8')
            # Multiplication and close symbols are icon glyphs, not French.
            text = text.replace('×', '')
            self.assertNotRegex(text, '[À-ÖØ-öø-ÿ]', filename)

    def test_public_brand_and_disclaimer(self):
        form = (ROOT / 'src/MonitorForm.cs').read_text(encoding='utf-8')
        assembly = (ROOT / 'src/AssemblyInfo.cs').read_text(encoding='utf-8')
        self.assertIn('CODEX PERISCOPE', form)
        self.assertIn('About Codex Periscope', form)
        self.assertIn('not affiliated with OpenAI', form)
        self.assertIn('AssemblyVersion("5.2.0.0")', assembly)
        self.assertNotIn('SHINO // CODEX', form)

    def test_radar_icon_and_animation_are_native(self):
        icon = (ROOT / 'GENERATE-PERISCOPE-ICON.ps1').read_text(encoding='utf-8')
        build = (ROOT / 'BUILD-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        glyph = (ROOT / 'src/ActivityGlyph.cs').read_text(encoding='utf-8')
        for size in ('16, 32, 48, 256',):
            self.assertIn(size, icon)
        self.assertIn('CODEX-PERISCOPE.ico', build)
        self.assertIn('g.DrawEllipse(track, inner)', glyph)
        self.assertIn('g.DrawLine(stroke, cx, cy', glyph)
        self.assertNotIn('Electron', glyph)

    def test_installer_and_uninstaller_are_english_only(self):
        for filename in ('INSTALL-PERISCOPE-V5.2.ps1',
                         'UNINSTALL-PERISCOPE-V5.2.ps1','README-EN.txt'):
            text = (ROOT / filename).read_text(encoding='utf-8-sig')
            self.assertNotRegex(text, '[À-ÖØ-öø-ÿ]', filename)
        installer = (ROOT/'INSTALL-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        self.assertIn('ValidateOnly', installer)
        self.assertIn('98/98', installer)
        self.assertIn('46/46', installer)
        self.assertIn('Codex Periscope.lnk', installer)
        self.assertNotIn('Set-ItemProperty -Path $runKey', installer)

    def test_no_silent_autostart_migration(self):
        form = (ROOT / 'src/MonitorForm.cs').read_text(encoding='utf-8')
        self.assertIn('LegacyRunName', form)
        self.assertIn('MessageBoxButtons.YesNo', form)
        self.assertIn('IsKnownLegacyEntry', form)
        self.assertIn('No registry values were changed.', form)
        self.assertIn('key.DeleteValue(LegacyRunName, false)', form)


if __name__ == '__main__':
    unittest.main()
