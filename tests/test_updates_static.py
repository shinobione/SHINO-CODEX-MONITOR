from pathlib import Path
import re
import unittest
ROOT=Path(__file__).resolve().parents[1]
class UpdateChecks(unittest.TestCase):
    def test_expected_source(self):
        s=(ROOT/'src'/'ReleaseUpdater.cs').read_text(encoding='utf-8')
        self.assertIn('releases/latest', s)
        self.assertIn('ReadWriteTimeout = 8000', s)
        self.assertIn('AllowAutoRedirect = false', s)
        self.assertIn('public-release-check', s)
        self.assertNotIn('auth.json', s)
        self.assertNotIn('Process.Start', s)
    def test_updates_are_opt_in(self):
        s=(ROOT/'src'/'MonitorForm.cs').read_text(encoding='utf-8')
        self.assertIn('Vérifier les mises à jour', s)
        self.assertIn('deuxième accord explicite', s)
        self.assertIn('if (choice == DialogResult.Yes) await DownloadAndConfirm(latest);', s)
    def test_unchanged_codex(self):
        s=(ROOT/'src'/'CodexReader.cs').read_text(encoding='utf-8')
        self.assertIn('account/rateLimits/read', s)
    def test_windows_compiles_updater(self):
        s=(ROOT/'BUILD-RELEASE-V4.7.ps1').read_text(encoding='utf-8-sig')
        self.assertIn("'ReleaseUpdater.cs'", s)
        self.assertIn('68/68', s)
    def test_assembly_version(self):
        s=(ROOT/'src'/'AssemblyInfo.cs').read_text(encoding='utf-8')
        self.assertIn('4.7.1.0', s)
