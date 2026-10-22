from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCES = (
    'MonitorForm.cs','SourceHealth.cs','HistoryWindow.cs','LiveTaskMonitor.cs',
    'ReleasePackage.cs','ReleaseUpdater.cs','CodexReader.cs','Models.cs',
    'RuntimeMetrics.cs','Program.cs','UpdateHandoff.cs','UpdateDownloadSmoke.cs',
    'HistoryData.cs','ActivityGlyph.cs','PremiumTests.cs','MissionPresentation.cs','PeriscopeWindow.cs','P0Tests.cs','InstrumentStyle.cs','InstrumentTests.cs'
)

class CodexPeriscopeV52EnglishAudit(unittest.TestCase):
    def test_user_facing_sources_contain_no_french_accented_words(self):
        for filename in SOURCES:
            text = (ROOT / 'src' / filename).read_text(encoding='utf-8')
            # Multiplication and close symbols are icon glyphs, not French.
            text = text.replace('×', '')
            self.assertNotRegex(text, '[À-ÖØ-öø-ÿ]', filename)

    def test_no_known_legacy_french_ui_literals(self):
        combined = "\n".join((ROOT / 'src' / f).read_text(encoding='utf-8')
                             for f in SOURCES)
        for text in ("Taille circulaire", "Binaire CLI", "Aimantation",
                     "Collecte suspendue", "Reprises / invalidations",
                     "% semaine", "Connexion interrompue",
                     "INDISPONIBLE", "min agoute", "Historique local",
                     "Session Codex locale", "Mise en veille Windows",
                     "Interruption de l'horloge", " autres", "INTERROMPU",
                     "✓ FIN ", " · source : local JSONL"):
            self.assertNotIn(text, combined)

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

    def test_radar_pixel_validation_is_part_of_release_build(self):
        build = (ROOT / 'BUILD-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        icon_qa = (ROOT / 'TEST-PERISCOPE-RADAR.ps1').read_text(encoding='utf-8')
        self.assertIn("TEST-PERISCOPE-RADAR.ps1", build)
        self.assertIn("Bitmap", icon_qa)
        self.assertIn("GetPixel", icon_qa)
        self.assertIn("@(16, 32, 48, 256)", icon_qa)
        self.assertIn("visible", icon_qa)
        self.assertIn("teal", icon_qa)

    def test_notification_gates_ignore_old_terminal_timestamps(self):
        reader = (ROOT / 'src/LiveTaskMonitor.cs').read_text(encoding='utf-8')
        tests = (ROOT / 'src/LiveTaskTests.cs').read_text(encoding='utf-8')
        form = (ROOT / 'src/MonitorForm.cs').read_text(encoding='utf-8')
        self.assertIn("task.FinishedUtc.Value < previous.CheckedUtc.AddSeconds(-5)", reader)
        self.assertIn("task.FinishedUtc.Value > current.CheckedUtc.AddSeconds(5)", reader)
        self.assertIn('rollout-stale.jsonl', tests)
        self.assertIn('rollout-future.jsonl', tests)
        self.assertIn("settings.Alerts && settings.TaskAlerts", form)
        self.assertIn('Test Windows notification', form)
        deck = (ROOT / 'src/MissionPresentation.cs').read_text(encoding='utf-8')
        self.assertIn('InstrumentStyle.Teal', deck)
        self.assertIn('#32E0C4', (ROOT / 'src/InstrumentStyle.cs').read_text(encoding='utf-8'))

    def test_history_and_update_handoff_use_english_public_brand(self):
        history = (ROOT / 'src/HistoryWindow.cs').read_text(encoding='utf-8')
        handoff = (ROOT / 'src/UpdateHandoff.cs').read_text(encoding='utf-8')
        self.assertIn('Quota History — CODEX PERISCOPE', history)
        for label in ('"24H"', '"3D"', '"7D"', '"30D"', '"ALL"'):
            self.assertIn(label, history)
        self.assertNotIn('Historique local', history)
        self.assertIn('CONFIRM-INSTALLATION.ps1', handoff)
        self.assertIn('Installing CODEX PERISCOPE', handoff)
        package = (ROOT / 'src/ReleasePackage.cs').read_text(encoding='utf-8')
        self.assertNotIn('Chemin ZIP', package)
        self.assertIn('Unsafe ZIP entry path.', package)
        for old in ('CONFIRMER-INSTALLATION', 'Fichiers de mise a jour',
                    'Empreinte ZIP differente', 'Installation confirmee de SHINO',
                    'Chemin de mise a jour invalide'):
            self.assertNotIn(old, handoff)

    def test_legacy_publisher_no_longer_runs_on_main_push(self):
        legacy = (ROOT / '.github/workflows/publish-v5.1.yml').read_text(encoding='utf-8')
        self.assertIn('workflow_dispatch:', legacy)
        self.assertNotIn('    branches: [main]', legacy)

    def test_installer_batch_uses_process_only_execution_policy(self):
        launcher = (ROOT / 'INSTALL-PERISCOPE-V5.2.bat').read_text(encoding='utf-8')
        self.assertIn('-NoProfile -ExecutionPolicy Bypass -File', launcher)
        self.assertIn('%~dp0INSTALL-PERISCOPE-V5.2.ps1', launcher)
        self.assertIn('%*', launcher)
        self.assertIn('exit /b %result%', launcher)
        for unsafe in ('Set-ExecutionPolicy', 'reg add', 'Set-MpPreference',
                       'powershell.exe -Command Set-ExecutionPolicy'):
            self.assertNotIn(unsafe, launcher)

    def test_installer_and_uninstaller_are_english_only(self):
        for filename in ('INSTALL-PERISCOPE-V5.2.ps1',
                         'UNINSTALL-PERISCOPE-V5.2.ps1','README-EN.txt'):
            text = (ROOT / filename).read_text(encoding='utf-8-sig')
            self.assertNotRegex(text, '[À-ÖØ-öø-ÿ]', filename)
        installer = (ROOT/'INSTALL-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        self.assertIn('ValidateOnly', installer)
        self.assertIn('98/98', installer)
        self.assertIn('59/59', installer)
        self.assertIn('Codex Periscope.lnk', installer)
        self.assertNotIn('Set-ItemProperty -Path $runKey', installer)

    def test_uninstaller_removes_english_shortcuts_and_task_report_is_english(self):
        script = (ROOT / 'UNINSTALL-PERISCOPE-V5.2.ps1').read_text(encoding='utf-8-sig')
        tasks = (ROOT / 'src/LiveTaskTests.cs').read_text(encoding='utf-8')
        self.assertIn("'Uninstall Codex Periscope.lnk'", script)
        self.assertNotIn("'Desinstaller Codex Periscope.lnk'", script)
        self.assertIn("simulated tests passed", tasks)
        self.assertNotIn("tests simulés réussis", tasks)

    def test_no_silent_autostart_migration(self):
        form = (ROOT / 'src/MonitorForm.cs').read_text(encoding='utf-8')
        self.assertIn('LegacyRunName', form)
        self.assertIn('MessageBoxButtons.YesNo', form)
        self.assertIn('IsKnownLegacyEntry', form)
        self.assertIn('No registry values were changed.', form)
        self.assertIn('key.DeleteValue(LegacyRunName, false)', form)


if __name__ == '__main__':
    unittest.main()
