from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
UI = (ROOT / "src" / "MonitorForm.cs").read_text(encoding="utf-8")
BEG = UI.index("        private void MakeMenu()")
END = UI.index("        private static void AddAlertToggle(", BEG)
MENU = UI[BEG:END]


class ContextMenuRefactorTests(unittest.TestCase):
    def test_main_menu_is_short_and_ordered(self):
        expected = [
            "Afficher / masquer", "Actualiser maintenant",
            "Tâches Codex · LIVE", "Quotas & données", "Apparence", "Fenêtre",
            "Notifications", "Historique", "Application",
            "Quitter complètement"
        ]
        items = re.findall(r'Menu\(context, "([^"]+)"', MENU)
        self.assertEqual(items, expected)
        self.assertEqual(MENU.count("context.Items.Add(new ToolStripSeparator())"), 3)

    def test_appearance_contains_layout_palette_and_tray(self):
        for label in ("Disposition", "Taille des roues", "Opacité",
                      "Couleur du fond", "Couleurs des jauges", "Tray Icon"):
            self.assertIn(f'Menu(appearance, "{label}"', MENU)
        self.assertNotIn('Menu(context, "Disposition"', MENU)
        self.assertNotIn('Menu(context, "Couleurs des jauges"', MENU)
        self.assertNotIn('Menu(context, "Icône de notification"', MENU)

    def test_window_controls_are_grouped_by_behavior(self):
        for label in ("Épingler le widget", "Aimantation aux bords des écrans",
                      "Toujours au-dessus", "Afficher dans la barre des tâches"):
            self.assertIn(f'Menu(window, "{label}', MENU)
        self.assertIn("magnet.Enabled = settings.Pinned;", MENU)
        self.assertIn("top.Enabled = settings.Pinned;", MENU)
        self.assertIn("taskbar.Enabled = settings.Pinned;", MENU)

    def test_quota_organization_preserves_reset_settings(self):
        self.assertIn('Menu(quotas, "Tableau officiel Codex"', MENU)
        self.assertIn('Menu(quotas, "Réinitialisations"', MENU)
        self.assertIn('Menu(quotas, "Fréquence d\'actualisation"', MENU)
        for label, value in (("Date/heure + décompte","both"),
                             ("Date/heure uniquement","absolute"),
                             ("Décompte uniquement","relative"),
                             ("Masquées","hidden")):
            self.assertIn(f'AddOption(resets, "{label}", "{value}"', MENU)
        self.assertIn("En mode Circulaire : décompte pour 5 h, date pour 7 jours.", MENU)

    def test_dynamic_checks_instead_of_stale_selection(self):
        for submenu in ("layouts", "refresh", "opacity",
                        "circularSize", "backgrounds", "window",
                        "alerts", "historyMenu", "application"):
            self.assertIn(f"{submenu}.DropDownOpening += delegate", MENU)
        self.assertIn("Taille actuelle : ", MENU)
        self.assertIn("SyncBackgroundMenu(backgrounds);", MENU)
        self.assertIn('Menu(appearance, "Réinitialiser fond et opacité"', MENU)
        self.assertNotIn('Rétablir l\'apparence', MENU)

    def test_history_and_application_actions_preserved(self):
        for label in ("Afficher l'historique", "Enregistrer l'historique local",
                      "Ouvrir le dossier de données"):
            self.assertIn(label, MENU)
        for label in ("Vérifier les mises à jour", "Releases GitHub",
                      "Démarrer avec Windows", "Diagnostic", "Copier le diagnostic"):
            self.assertIn(f'"{label}"', MENU)
        self.assertIn('updateMenuItem = Menu(application, "Vérifier les mises à jour"', MENU)
        self.assertNotIn("Démarrer V4 avec Windows", MENU)

    def test_notifications_remain_opt_in_and_source_checked(self):
        self.assertIn('Menu(alerts, "Activer les notifications"', MENU)
        self.assertIn("settings.Alerts = ((ToolStripMenuItem)sender).Checked;", MENU)
        self.assertIn("child.Enabled = settings.Alerts;", MENU)
        for text in ("Fenêtre 5 heures", "Fenêtre 7 jours", "Seuil 50 %",
                     "Seuil 25 %", "Seuil 10 %", "Réinitialisation constatée"):
            self.assertIn(f'AddAlertToggle(alerts, "{text}"', MENU)

    def test_menu_helpers_do_not_change_codex_collector(self):
        self.assertIn("private static ToolStripMenuItem Menu(ToolStripMenuItem parent", UI)
        self.assertIn("private static ToolStripMenuItem Menu(ContextMenuStrip menu", UI)
        self.assertIn("settings.TrayStyle = value; Save(); UpdateQuotaTrayIcon(", MENU)
        self.assertNotIn("CodexReader", MENU)
        self.assertNotIn("File.ReadAllText(", MENU)


if __name__ == "__main__":
    unittest.main()
