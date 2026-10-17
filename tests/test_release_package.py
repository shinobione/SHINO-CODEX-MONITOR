from pathlib import Path
import unittest
import re

ROOT=Path(__file__).resolve().parents[1]
PACKAGE=(ROOT/'src/ReleasePackage.cs').read_text(encoding='utf-8')
HANDOFF=(ROOT/'src/UpdateHandoff.cs').read_text(encoding='utf-8')
UI=(ROOT/'src/MonitorForm.cs').read_text(encoding='utf-8')

class ReleasePackageAudit(unittest.TestCase):
    def test_github_asset_digest_required(self):
        self.assertIn('"^sha256:([0-9a-fA-F]{64})$"', PACKAGE)
        self.assertIn('SHA-256 GitHub absent', PACKAGE)
        self.assertIn('VerifyAssetFile(asset, zip);', PACKAGE)

    def test_no_remote_url_from_json(self):
        self.assertIn('browser_download_url supplied in remote JSON', PACKAGE)
        self.assertIn('ReleaseUpdater.RepoUrl + "/releases/download/"', PACKAGE)

    def test_secure_redirect_policy(self):
        self.assertIn('uri.Scheme != Uri.UriSchemeHttps', PACKAGE)
        self.assertIn('uri.Host.Equals("github.com"', PACKAGE)
        self.assertIn('uri.Host.Equals("release-assets.githubusercontent.com"', PACKAGE)
        self.assertIn('request.AllowAutoRedirect = false;', PACKAGE)

    def test_bounded_download_and_unzip(self):
        for item in ['MaxZipBytes', 'MaxExpandedBytes', 'MaxEntries',
                     'request.ReadWriteTimeout = 12000', 'time.Elapsed.TotalSeconds > 90',
                     'copied > e.Length']:
            self.assertIn(item, PACKAGE)

    def test_zip_slip_duplicates_and_links_rejected(self):
        for item in ['ValidateEntry(e, asset.Release.Number)', 'seen.Add(name)',
                     'entry.ExternalAttributes >> 16', 'FileMode.CreateNew',
                     'Path.GetFileNameWithoutExtension(AssetFile(version))']:
            self.assertIn(item, PACKAGE)

    def test_installation_explicit_second_confirmation(self):
        self.assertIn('await DownloadAndConfirm(latest)', UI)
        self.assertIn('if (yes != DialogResult.Yes) return;', UI)
        self.assertLess(UI.index('if (yes != DialogResult.Yes) return;'),UI.index('UpdateHandoff.Start(staged);'))
        self.assertLess(UI.index('UpdateHandoff.Start(staged);'),UI.index('exiting = true;', UI.index('UpdateHandoff.Start(staged);')))

    def test_handoff_checks_digest_again(self):
        self.assertIn('ReleasePackage.VerifyAssetFile(update.Asset, update.Archive)', HANDOFF)
        self.assertIn('Get-FileHash -LiteralPath $zip -Algorithm SHA256', HANDOFF)
        self.assertIn('Wait-Process -Id $ParentPid', HANDOFF)
        self.assertIn('if (Get-Process -Id $ParentPid', HANDOFF)

    def test_preserve_previous_install_safeguards(self):
        install=(ROOT/'INSTALLER-V4.9.ps1').read_text(encoding='utf-8-sig')
        self.assertIn('[System.IO.File]::Replace(', install)
        self.assertIn('$backupExe',install)
        self.assertIn('Get-FileHash', install)
        self.assertNotIn('history.jsonl',install)
        self.assertNotIn('settings.json',install)

    def test_absence_of_auto_installation(self):
        self.assertNotIn('UpdateHandoff.Start(', PACKAGE)
        self.assertNotIn('GetCookies',PACKAGE+HANDOFF)
        self.assertNotIn('auth.json',PACKAGE+HANDOFF)
        self.assertNotIn('Set-MpPreference', PACKAGE+HANDOFF)

if __name__ == '__main__': unittest.main()
