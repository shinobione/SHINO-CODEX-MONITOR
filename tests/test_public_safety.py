from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
# A deliberately narrow allowlist: a file accidentally copied into the project must fail CI.
TOP_LEVEL = {
    '.gitignore','.gitattributes','README.md','README-FR.txt','SECURITY.md',
    'BUILD-RELEASE-V4.4.bat','BUILD-RELEASE-V4.4.ps1',
    'INSTALLER-V4.4.bat','INSTALLER-V4.4.ps1',
    'DESINSTALLER-V4.4.ps1','PREPARER-ET-INSTALLER-V4.4.bat',
    'SHINO-CODEX.ico','app.manifest','PUBLIER-GITHUB.bat','PUBLIER-GITHUB.ps1'
}
ALLOW_DIRECTORIES = {'.github/workflows','src','tests'}
ALLOW_EXT = {'.cs','.py','.yml','.json'}
BANNED_NAMES = {'settings.json','history.jsonl','auth.json','token.json', '.env','credentials.json'}
SECRET_PATTERNS = [
    re.compile(r'gh[pousr]_[A-Za-z0-9_]{25,}'),
    re.compile(r'github_pat_[A-Za-z0-9_]{20,}'),
    re.compile(r'sk-(?:proj-)?[A-Za-z0-9_-]{16,}'),
    re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
    re.compile(r'(?i)Authorization\s*:\s*Bearer\s+[A-Za-z0-9._-]{12,}'),
]

def approved_files():
    files = []
    for file in ROOT.rglob('*'):
        if not file.is_file() or '.git' in file.parts or '__pycache__' in file.parts or file.suffix == '.pyc':
            continue
        files.append(file)
    return files

class PublicSafety(unittest.TestCase):
    def test_exact_public_file_allowlist(self):
        for p in approved_files():
            relative = p.relative_to(ROOT)
            self.assertNotIn(p.name.lower(), BANNED_NAMES, str(relative))
            if len(relative.parts) == 1:
                self.assertIn(p.name, TOP_LEVEL, str(relative))
            else:
                self.assertIn(relative.parent.as_posix(), ALLOW_DIRECTORIES, str(relative))
                self.assertIn(p.suffix.lower(), ALLOW_EXT, str(relative))
    def test_no_credentials_embedded(self):
        for p in approved_files():
            if p.suffix.lower() == '.ico':
                continue
            content = p.read_text(encoding='utf-8-sig')
            for pattern in SECRET_PATTERNS:
                self.assertIsNone(pattern.search(content), f'Potential secret detected in {p.relative_to(ROOT)}')
    def test_no_account_files_bundled(self):
        for p in approved_files():
            rel = p.relative_to(ROOT).as_posix().lower()
            self.assertNotIn('/.codex/', '/'+rel)
            self.assertFalse(rel.endswith(('.exe','.pem','.pfx','.key','.log','.jsonl')))
    def test_ignore_patterns(self):
        rules=(ROOT/'.gitignore').read_text()
        for name in ['settings.json','history.jsonl','auth.json','.codex/','bin/','dist/']:
            self.assertIn(name,rules)
    def test_session_used_without_reading_auth_file(self):
        code=(ROOT/'src/CodexReader.cs').read_text()
        self.assertIn('account/rateLimits/read',code)
        self.assertNotIn('File.ReadAllText(auth',code)
        self.assertNotIn('GetCookies',code)
    def test_ci_scope_and_no_secret_dependency(self):
        workflow=(ROOT/'.github/workflows/build-windows.yml').read_text()
        self.assertIn('contents: read',workflow)
        self.assertIn('pull-requests: read',workflow)
        self.assertIn('timeout-minutes:',workflow)
        self.assertIn('unittest discover -s tests',workflow)

if __name__ == '__main__': unittest.main()
