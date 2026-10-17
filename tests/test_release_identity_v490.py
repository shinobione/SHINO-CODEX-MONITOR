from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
ASSEMBLY = (ROOT / 'src' / 'AssemblyInfo.cs').read_text(encoding='utf-8')
FORM = (ROOT / 'src' / 'MonitorForm.cs').read_text(encoding='utf-8')
SELF = (ROOT / 'src' / 'SelfTests.cs').read_text(encoding='utf-8')
INSTALL = (ROOT / 'INSTALLER-V4.9.ps1').read_text(encoding='utf-8-sig')
BUILD = (ROOT / 'BUILD-RELEASE-V4.9.ps1').read_text(encoding='utf-8-sig')
RELEASE = (ROOT / 'src' / 'ReleaseUpdater.cs').read_text(encoding='utf-8')
PACKAGE = (ROOT / 'src' / 'ReleasePackage.cs').read_text(encoding='utf-8')
CI = (ROOT / '.github' / 'workflows' / 'build-windows.yml').read_text(encoding='utf-8')


class V490ReleaseIdentityTests(unittest.TestCase):
    def test_assembly_has_unique_new_version(self):
        self.assertIn('AssemblyVersion("4.9.0.0")', ASSEMBLY)
        self.assertIn('AssemblyFileVersion("4.9.0.0")', ASSEMBLY)

    def test_ui_and_installer_match_new_version(self):
        self.assertIn('SHINO // CODEX MONITOR V4.9.0', FORM)
        self.assertIn('MONITOR / V4.9.0', FORM)
        self.assertIn("-Name DisplayVersion -Value '4.9.0'", INSTALL)

    def test_new_version_is_strictly_greater_in_runtime_checks(self):
        for label in ('V4.9.0 : proposée à un utilisateur V4.7.2',
                      'V4.7.2 : non proposée à l\'utilisateur V4.9.0',
                      'V4.9.0 : version identique non reproposée'):
            self.assertIn(label, SELF)
        self.assertIn('ReleaseUpdater.IsNewer(', SELF)

    def test_package_keeps_minor_version_naming(self):
        self.assertIn('SHINO_CODEX_MONITOR_V4_9_WINDOWS.zip', BUILD)
        self.assertIn('INSTALLER-V4.9.ps1', INSTALL)
        self.assertIn('V4.9.0 : nom ZIP et installateur conformes', SELF)

    def test_http_user_agents_derive_running_assembly_version(self):
        self.assertIn('typeof(ReleaseUpdater).Assembly.GetName().Version.ToString(3)', RELEASE)
        self.assertIn('typeof(ReleasePackage).Assembly.GetName().Version.ToString(3)', PACKAGE)
        self.assertNotIn('SHINO-CODEX-MONITOR/4.6', RELEASE + PACKAGE)

    def test_build_guards_and_no_automatic_public_release(self):
        self.assertIn('92/92', INSTALL)
        self.assertIn('92/92', BUILD)
        self.assertIn('92 C# self-tests', CI)
        self.assertNotIn('gh release create', CI)
        self.assertEqual(len(re.findall(r'\bCheck\(', SELF)) - 1, 92)


if __name__ == '__main__':
    unittest.main()
