from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
ASSEMBLY = (ROOT / 'src/AssemblyInfo.cs').read_text(encoding='utf-8')
FORM = (ROOT / 'src/MonitorForm.cs').read_text(encoding='utf-8')
TESTS = (ROOT / 'src/SelfTests.cs').read_text(encoding='utf-8')
BUILDER = (ROOT / 'BUILD-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
INSTALL = (ROOT / 'INSTALL-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
PACKAGE = (ROOT / 'src/ReleasePackage.cs').read_text(encoding='utf-8')
RELEASE = (ROOT / 'src/ReleaseUpdater.cs').read_text(encoding='utf-8')
CI = (ROOT / '.github/workflows/build-windows.yml').read_text(encoding='utf-8')


class PeriscopeV52ReleaseIdentityTests(unittest.TestCase):
    def test_unique_preview_version(self):
        self.assertIn('AssemblyVersion("5.2.0.0")', ASSEMBLY)
        self.assertIn('AssemblyFileVersion("5.2.0.0")', ASSEMBLY)
        self.assertIn('5.2.0-preview.1', ASSEMBLY)
        self.assertIn('Codex Periscope', ASSEMBLY)

    def test_brand_on_every_main_surface(self):
        self.assertIn('CODEX PERISCOPE', FORM)
        self.assertIn('PERISCOPE / V5.2.0', FORM)
        self.assertNotIn('SHINO // CODEX', FORM)
        self.assertIn('CODEX PERISCOPE V5.2.0', TESTS)

    def test_historical_release_paths_remain_supported(self):
        self.assertIn('SHINO_CODEX_MONITOR_V{0}_{1}_WINDOWS.zip', PACKAGE)
        self.assertIn('CODEX_PERISCOPE_V{0}_{1}_WINDOWS.zip', PACKAGE)
        self.assertIn('INSTALL-PERISCOPE-V{0}.{1}.ps1', PACKAGE)
        self.assertIn('INSTALLER-V{0}.{1}.ps1', PACKAGE)

    def test_safe_preview_installation_gate(self):
        self.assertIn('CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip', BUILDER)
        self.assertIn('GENERATE-PERISCOPE-ICON.ps1', BUILDER)
        self.assertIn('CODEX-PERISCOPE.ico', BUILDER)
        self.assertIn('INSTALL-PERISCOPE-V5.2.ps1', BUILDER)
        self.assertIn('INSTALL-PERISCOPE-V5.2.ps1', INSTALL)
        self.assertIn('98/98', INSTALL)
        self.assertIn('46/46', INSTALL)
        self.assertIn('ValidateOnly', BUILDER)
        self.assertNotIn('gh release create', CI)

    def test_user_data_and_startup_are_preserved(self):
        self.assertIn("Programs\\SHINO Codex Monitor", INSTALL)
        self.assertIn('Settings and history preserved', INSTALL)
        self.assertIn("CODEX_PERISCOPE", FORM)
        self.assertIn('LegacyRunName', FORM)
        self.assertIn('MessageBoxButtons.YesNo', FORM)

    def test_updater_still_uses_github_release_validation(self):
        self.assertIn('typeof(ReleaseUpdater).Assembly.GetName().Version.ToString(3)', RELEASE)
        self.assertIn('typeof(ReleasePackage).Assembly.GetName().Version.ToString(3)', PACKAGE)
        self.assertIn('sha256:', PACKAGE)

    def test_quota_test_count_is_not_reduced(self):
        self.assertEqual(len(re.findall(r'\bCheck\(', TESTS)) - 1, 98)


if __name__ == '__main__':
    unittest.main()
