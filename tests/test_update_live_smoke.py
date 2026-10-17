from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
PROGRAM = (ROOT / 'src' / 'Program.cs').read_text(encoding='utf-8')
SMOKE = (ROOT / 'src' / 'UpdateDownloadSmoke.cs').read_text(encoding='utf-8')
WORKFLOW = (ROOT / '.github' / 'workflows' / 'build-windows.yml').read_text(encoding='utf-8')


class LiveDownloadSmokeAudit(unittest.TestCase):
    def test_explicit_flag_no_auto_trigger(self):
        self.assertIn('"--update-download-smoke"', PROGRAM)
        self.assertIn('UpdateDownloadSmoke.Run()', PROGRAM)

    def test_real_release_download_uses_existing_verifier(self):
        for fragment in ('ReleaseUpdater.CheckAsync()', 'ReleasePackage.SelectAsset(',
                         'ReleasePackage.StageAsync(', 'ReleasePackage.VerifyAssetFile('):
            self.assertIn(fragment, SMOKE)

    def test_never_launches_installer(self):
        for bad in ('UpdateHandoff.Start(', 'Process.Start(', 'Start-Process',
                    'INSTALLER-V4.9.ps1', 'Unblock-File'):
            self.assertNotIn(bad, SMOKE)
        self.assertIn('Directory.Delete(staged.Directory, true)', SMOKE)
        self.assertIn('Installer: NOT EXECUTED', SMOKE)

    def test_ci_requires_success_and_report(self):
        self.assertIn('Smoke test actual GitHub Release download (NO INSTALL)', WORKFLOW)
        self.assertIn("'--update-download-smoke'", WORKFLOW)
        self.assertIn('$proc.ExitCode -ne 0', WORKFLOW)
        self.assertIn('Installer: NOT EXECUTED', WORKFLOW)


if __name__ == '__main__':
    unittest.main()
