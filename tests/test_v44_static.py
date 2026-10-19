from pathlib import Path
import hashlib
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
HASHES = __import__('json').loads((ROOT/'tests'/'core-sha256.json').read_text(encoding='utf-8'))

class ReleaseStaticTests(unittest.TestCase):
    def test_core_unchanged(self):
        # Unchanged core math stays hash-locked; translated diagnostics are separately guarded by V5.2 tests.
        for name in ('WakeGuard.cs','AlertRules.cs'):
            a = (ROOT/'src'/name).read_bytes()
            self.assertEqual(hashlib.sha256(a).hexdigest(), HASHES[name], name)
    def test_version(self):
        self.assertIn('5.2.0.0', (ROOT/'src'/'AssemblyInfo.cs').read_text(encoding='utf-8'))
        self.assertIn('V5.2.0', (ROOT/'src'/'MonitorForm.cs').read_text(encoding='utf-8'))
        self.assertIn('CODEX PERISCOPE V5.2.0 —', (ROOT/'src'/'SelfTests.cs').read_text(encoding='utf-8'))
    def test_self_tests_count(self):
        s=(ROOT/'src'/'SelfTests.cs').read_text(encoding='utf-8')
        self.assertEqual(len(re.findall(r'\bCheck\(', s)) - 1,98)
    def test_winexe(self):
        s=(ROOT/'BUILD-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        self.assertIn('/target:winexe',s)
        self.assertIn('Windows self-tests incomplete.',s)
        self.assertIn("'98/98'",s)
    def test_integrity_before_install(self):
        s=(ROOT/'INSTALL-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        self.assertLess(s.index('Get-FileHash'), s.index('New-Item -ItemType Directory -Path $destination'))
        self.assertLess(s.index('98/98'), s.index('New-Item -ItemType Directory -Path $destination'))
        self.assertIn('File]::Replace',s)
        self.assertIn('$ValidateOnly',s)
    def test_no_auto_run_creation(self):
        s=(ROOT/'INSTALLER-V5.0.ps1').read_text(encoding='utf-8-sig')
        self.assertNotIn('Set-ItemProperty -Path $runKey',s)
        self.assertNotIn('New-ItemProperty -Path $runKey',s)
    def test_user_level_install(self):
        s=(ROOT/'INSTALLER-V5.0.ps1').read_text(encoding='utf-8-sig')
        self.assertIn('$env:LOCALAPPDATA',s)
        self.assertIn('HKCU:',s)
        self.assertNotIn('HKLM:',s)
    def test_local_data_untouched(self):
        s=(ROOT/'INSTALLER-V5.0.ps1').read_text(encoding='utf-8-sig')
        self.assertNotIn('history.jsonl',s)
        self.assertNotIn('settings.json',s)
    def test_windows_ci(self):
        s=(ROOT/'.github'/'workflows'/'build-windows.yml').read_text(encoding='utf-8')
        self.assertIn('windows-2022',s)
        self.assertIn('actions/upload-artifact@v4',s)
        self.assertIn('workflow_dispatch',s)
    def test_absence_of_binary_and_personal_data(self):
        for f in ROOT.rglob('*'):
            if f.is_file():
                self.assertNotIn(f.name.lower(),('history.jsonl','settings.json','auth.json','token.json'))
                self.assertNotEqual(f.suffix.lower(),'.exe')
    def test_uninstall_requires_confirmation(self):
        s=(ROOT/'UNINSTALL-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        self.assertIn("-cne 'YES'",s)
        self.assertNotIn('Remove-Item -LiteralPath $env:LOCALAPPDATA',s)
    def test_compiler_sources_present(self):
        s=(ROOT/'BUILD-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        for p in ROOT.joinpath('src').glob('*.cs'):
            self.assertIn(p.name,s)

if __name__ == '__main__': unittest.main()
