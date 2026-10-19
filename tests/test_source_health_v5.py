from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
HEALTH = (ROOT / 'src' / 'SourceHealth.cs').read_text(encoding='utf-8')
FORM = (ROOT / 'src' / 'MonitorForm.cs').read_text(encoding='utf-8')
SELF = (ROOT / 'src' / 'SelfTests.cs').read_text(encoding='utf-8')
BUILD = (ROOT / 'BUILD-RELEASE-V5.1.ps1').read_text(encoding='utf-8-sig')


class SourceHealthV5Audit(unittest.TestCase):
    def test_report_is_readonly_and_local(self):
        self.assertNotIn('CodexReader.Read(', HEALTH)
        self.assertNotIn('HttpWebRequest', HEALTH)
        self.assertNotIn('auth.json', HEALTH)
        self.assertNotIn('GetCookies(', HEALTH)

    def test_first_party_source_and_states_are_explicit(self):
        self.assertIn('Codex CLI / account/rateLimits/read', HEALTH)
        self.assertIn('Statut : ', HEALTH)
        self.assertIn('Dernier relevé valide : ', HEALTH)
        self.assertIn('Âge du relevé : ', HEALTH)
        self.assertIn('Échecs consécutifs : ', HEALTH)
        self.assertIn('Prochain essai : ', HEALTH)

    def test_missing_stale_and_expired_windows(self):
        self.assertIn('Fenêtre non communiquée', HEALTH)
        self.assertIn('ancienne donnée · reset non vérifié', HEALTH)
        self.assertIn('Reset non communiqué', HEALTH)
        self.assertIn('Reset attendu · vérification...', HEALTH)

    def test_menu_location_and_no_changes_to_collector(self):
        self.assertIn('Menu(quotas, "État des données Codex"', FORM)
        self.assertIn('SourceHealth.Build(snapshot, State(), lastSuccess, failures,', FORM)
        self.assertIn('private void ShowSourceHealth()', FORM)
        self.assertNotIn('File.ReadAllText("auth.json")', FORM)

    def test_compiler_and_csharp_tests_include_module(self):
        self.assertIn("'SourceHealth.cs'", BUILD)
        for label in ('V5 : fenêtre Codex absente explicitement annoncée',
                      'V5 : fenêtre ancienne jamais présentée comme actuelle',
                      'V5 : reset futur direct explicitement daté',
                      'V5 : absence ou échéance du reset clairement distinguées',
                      'V5 : état bloqué conservé indépendamment des deux fenêtres',
                      'V5 : origine et statut indisponible sans valeur inventée'):
            self.assertIn(label, SELF)

    def test_v5_semantics_and_no_simulated_quota(self):
        self.assertIn('FENÊTRES CODEX — QUOTA RESTANT', HEALTH)
        self.assertIn('Usage ordinaire signalé bloqué par Codex', HEALTH)
        self.assertIn('Les valeurs marquées anciennes ne sont pas des quotas actuels.', HEALTH)
        self.assertNotIn('mock', HEALTH.lower())


if __name__ == '__main__':
    unittest.main()
