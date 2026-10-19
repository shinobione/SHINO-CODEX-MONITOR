from pathlib import Path
import re
import struct
import unittest

ROOT = Path(__file__).resolve().parents[1]
def source(name):
    return (ROOT / name).read_text(encoding="utf-8-sig")

FORM = source("src/MonitorForm.cs")
TASK = source("src/LiveTaskMonitor.cs")
MODEL = source("src/Models.cs")
PROGRAM = source("src/Program.cs")
BUILD = source("BUILD-PREVIEW-V5.2.ps1")
INSTALL = source("INSTALLER-V5.2.ps1")
UNINSTALL = source("UNINSTALL-V5.2.ps1")
ASSEMBLY = source("src/AssemblyInfo.cs")
LOCAL = source("src/LocalStore.cs")


class CodexPeriscopePreview(unittest.TestCase):
    def test_product_identity_is_explicit_and_independent(self):
        self.assertIn('AssemblyTitle("Codex Periscope")', ASSEMBLY)
        self.assertIn('AssemblyVersion("5.2.0.0")', ASSEMBLY)
        self.assertIn('5.2.0-preview.1', ASSEMBLY)
        self.assertIn("CODEX PERISCOPE", FORM)
        self.assertIn("Independent", ASSEMBLY)

    def test_english_culture_and_local_timestamps(self):
        self.assertIn('CultureInfo.GetCultureInfo("en-US")', PROGRAM)
        self.assertIn("Thread.CurrentThread.CurrentUICulture = english", PROGRAM)
        self.assertIn("ToLocalTime()", FORM)
        self.assertNotIn('"fr-FR"', PROGRAM + FORM)

    def test_no_old_dialogue_or_french_menu_strings(self):
        self.assertNotIn("V4.5", FORM)
        for phrase in ("Afficher / masquer", "Actualiser les quotas",
                       "Ancienne donnée", "Démarrage Windows", "Activité Codex",
                       "Aucun relevé", "Fenêtre 7 jours", "Non communiqué",
                       "À vérifier", "Indisponible"):
            self.assertNotIn('"' + phrase, FORM + TASK + MODEL)
        self.assertIn('"Codex activity (local)"', FORM)
        self.assertIn('"Appearance"', FORM)
        self.assertIn('"Notifications"', FORM)
        self.assertIn('"Start with Windows"', FORM)

    def test_quota_reader_unchanged_and_live_state_conservative(self):
        self.assertIn('CodexReader.Read(detected, 20000)', FORM)
        self.assertIn('TaskDisplayKind.NotVerified', TASK)
        self.assertIn('ActivityMaxAgeSeconds', TASK)
        self.assertIn('duration_ms', TASK)
        self.assertIn('thread_source', TASK)

    def test_rebrand_uses_legacy_data_and_mutex_compatibility(self):
        self.assertIn('"SHINO-CODEX-MONITOR"', LOCAL)
        self.assertIn('SHINO_CODEX_MONITOR_NATIVE_V4', PROGRAM)
        self.assertIn("LocalApplicationData", LOCAL)
        self.assertNotIn('Path.Combine(home, "auth.json")', TASK)

    def test_startup_migration_requires_user_confirmation(self):
        self.assertIn('private const string RunName = "CODEX_PERISCOPE"', FORM)
        self.assertIn('"SHINO_CODEX_MONITOR_V4"', FORM)
        self.assertIn('"SHINO_CODEX_MONITOR"', FORM)
        self.assertIn('MessageBoxButtons.YesNo', FORM)
        self.assertIn('IsLegacyMonitorCommand', FORM)
        self.assertIn('key.DeleteValue(legacyName, false)', FORM)

    def test_installer_is_versioned_and_local(self):
        self.assertIn("$exeName = 'CODEX-PERISCOPE.exe'", INSTALL)
        self.assertIn("Programs\\SHINO Codex Monitor", INSTALL)
        self.assertIn("SHA256", INSTALL)
        self.assertIn('--self-test-silent', INSTALL)
        self.assertIn('--live-task-self-test', INSTALL)
        self.assertIn('if ($ValidateOnly)', INSTALL)
        self.assertIn("Read-Host 'Move your existing Windows autostart", INSTALL)
        self.assertIn("Existing user settings and quota history were preserved", INSTALL)
        self.assertNotIn("HKLM:", INSTALL)

    def test_uninstaller_is_english_confirmed_and_preserves_history(self):
        self.assertIn("Type YES to uninstall Codex Periscope", UNINSTALL)
        self.assertIn("settings and history remain intact", UNINSTALL)
        self.assertNotIn("Remove-Item -LiteralPath $env:LOCALAPPDATA", UNINSTALL)
        self.assertIn("CODEX_PERISCOPE", UNINSTALL)

    def test_preview_archive_has_no_legacy_installer_or_french_manual(self):
        self.assertIn("CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip", BUILD)
        self.assertIn("CODEX-PERISCOPE.ico", BUILD)
        self.assertIn("INSTALLER-V5.2.ps1", BUILD)
        self.assertIn("UNINSTALL-V5.2.ps1", BUILD)
        self.assertIn("README.txt", BUILD)
        self.assertNotIn("'README-FR.txt'", BUILD)
        self.assertNotIn("'INSTALLER-V5.1.ps1'", BUILD)

    def test_icon_contains_multiple_radar_sized_images(self):
        image = (ROOT / "CODEX-PERISCOPE.ico").read_bytes()
        reserved, image_type, count = struct.unpack_from("<HHH", image)
        self.assertEqual((reserved, image_type), (0, 1))
        self.assertGreaterEqual(count, 5)
        sizes = set()
        for i in range(count):
            width, height = struct.unpack_from("<BB", image, 6 + i * 16)
            sizes.add(width or 256)
            self.assertEqual(width, height)
        self.assertTrue({16, 32, 48, 64, 128}.issubset(sizes))

    def test_all_shipped_csharp_literals_have_no_french_accents(self):
        # Check user-facing messages and menu strings, not comments or identifiers.
        for path in ("src/MonitorForm.cs", "src/HistoryWindow.cs", "src/SourceHealth.cs",
                     "src/LiveTaskMonitor.cs", "src/ReleasePackage.cs",
                     "src/ReleaseUpdater.cs", "src/Models.cs", "src/Program.cs",
                     "src/CodexReader.cs"):
            text = source(path)
            strings = re.findall(r'(?<!@)"(?:\\.|[^"\\])*"', text)
            bad = [line for line in strings if re.search(r'[À-ÿ]', line.replace("×", "").replace("÷", ""))]
            self.assertFalse(bad, f"{path}: {bad[:3]}")

    def test_all_new_installation_messages_are_english(self):
        for path in ("INSTALLER-V5.2.ps1", "UNINSTALL-V5.2.ps1",
                     "BUILD-PREVIEW-V5.2.ps1", "README.txt"):
            self.assertNotRegex(source(path), r'[À-ÿ]', path)


if __name__ == "__main__":
    unittest.main()
