from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = (ROOT / 'INSTALLER-V4.8.ps1').read_text(encoding='utf-8-sig')


class TargetedMotwTests(unittest.TestCase):
    def test_only_installed_exe_is_unblocked(self):
        self.assertEqual(SCRIPT.count('Unblock-File -LiteralPath $installedExe -ErrorAction Stop'), 1)
        self.assertNotIn('Unblock-File -LiteralPath $sourceExe', SCRIPT)
        self.assertNotIn('Unblock-File -LiteralPath $pendingExe', SCRIPT)

    def test_not_before_validation(self):
        self.assertLess(SCRIPT.index('$expected -ne $observed'), SCRIPT.index('Unblock-File -LiteralPath'))
        self.assertLess(SCRIPT.index('88/88'), SCRIPT.index('Unblock-File -LiteralPath'))
        self.assertLess(SCRIPT.index('if ($ValidateOnly)'), SCRIPT.index('Unblock-File -LiteralPath'))

    def test_installed_binary_verified_before_unblock(self):
        self.assertLess(SCRIPT.index('[System.IO.File]::Replace('), SCRIPT.index('$installedHash = '))
        self.assertLess(SCRIPT.index('if ($installedHash -ne $expected)'), SCRIPT.index('Unblock-File -LiteralPath'))
        self.assertIn("throw 'Verification du binaire installe echouee : retour arriere.'", SCRIPT)

    def test_failure_is_explained(self):
        self.assertIn("Write-Warning ('Marquage Internet non retire : '", SCRIPT)
        self.assertIn('Un avertissement Windows peut encore apparaitre', SCRIPT)

    def test_no_system_wide_security_bypass(self):
        for forbidden in ('Set-MpPreference', 'DisableSmartScreen',
                          'SaveZoneInformation', 'HKLM:', 'Set-ExecutionPolicy',
                          'Remove-Item -Stream', 'Remove-ItemProperty -Path $runKey'):
            self.assertNotIn(forbidden, SCRIPT)

    def test_version_display(self):
        self.assertIn("-Name DisplayVersion -Value '4.8.0'", SCRIPT)

if __name__ == '__main__':
    unittest.main()
