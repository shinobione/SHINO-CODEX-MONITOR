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
            "Show / Hide", "Refresh quotas",
            "Codex Activity (local)", "Quotas & Data", "Appearance", "Window",
            "Notifications", "History", "Application",
            "Exit completely"
        ]
        items = re.findall(r'Menu\(context, "([^"]+)"', MENU)
        self.assertEqual(items, expected)
        self.assertEqual(MENU.count("context.Items.Add(new ToolStripSeparator())"), 3)

    def test_appearance_contains_layout_palette_and_tray(self):
        for label in ("Layout", "Gauge size", "Opacity",
                      "Background color", "Gauge colors", "Tray Icon"):
            self.assertIn(f'Menu(appearance, "{label}"', MENU)
        self.assertIn("Animate Codex activity radar", MENU)
        self.assertNotIn('Menu(context, "Layout"', MENU)
        self.assertNotIn('Menu(context, "Gauge colors"', MENU)
        self.assertNotIn('Menu(context, "Icône de notification"', MENU)

    def test_window_controls_are_grouped_by_behavior(self):
        for label in ("Pin widget", "Snap to screen edges",
                      "Always on top", "Show in taskbar"):
            self.assertIn(f'Menu(window, "{label}', MENU)
        self.assertIn("magnet.Enabled = settings.Pinned;", MENU)
        self.assertIn("top.Enabled = settings.Pinned;", MENU)
        self.assertIn("taskbar.Enabled = settings.Pinned;", MENU)

    def test_quota_organization_preserves_reset_settings(self):
        self.assertIn('Menu(quotas, "Official Codex dashboard"', MENU)
        self.assertIn('Menu(quotas, "Reset display"', MENU)
        self.assertIn('Menu(quotas, "Refresh interval"', MENU)
        for label, value in (("Date/time + countdown","both"),
                             ("Date/time only","absolute"),
                             ("Countdown only","relative"),
                             ("Hidden","hidden")):
            self.assertIn(f'AddOption(resets, "{label}", "{value}"', MENU)
        self.assertIn("In circular mode: countdown for 5 hours and date for weekly quota.", MENU)

    def test_dynamic_checks_instead_of_stale_selection(self):
        for submenu in ("layouts", "refresh", "opacity",
                        "circularSize", "backgrounds", "window",
                        "alerts", "historyMenu", "application"):
            self.assertIn(f"{submenu}.DropDownOpening += delegate", MENU)
        self.assertIn("Current size: ", MENU)
        self.assertIn("SyncBackgroundMenu(backgrounds);", MENU)
        self.assertIn('Menu(appearance, "Reset background and opacity"', MENU)
        self.assertNotIn('Rétablir l\'apparence', MENU)

    def test_history_and_application_actions_preserved(self):
        for label in ("Show history", "Record local history",
                      "Open data folder"):
            self.assertIn(label, MENU)
        for label in ("Check for updates", "Releases GitHub",
                      "Start with Windows", "Diagnostic", "Copy diagnostics"):
            self.assertIn(f'"{label}"', MENU)
        self.assertIn('updateMenuItem = Menu(application, "Check for updates"', MENU)
        self.assertNotIn("Start V4 with Windows", MENU)

    def test_notifications_remain_opt_in_and_source_checked(self):
        self.assertIn('Menu(alerts, "Enable notifications"', MENU)
        self.assertIn('Menu(alerts, "Test Windows notification"', MENU)
        self.assertIn('AddAlertToggle(alerts, "Codex task completed"', MENU)
        self.assertIn("Menu(tasksMenu, \"Refresh local activity\"", MENU)
        self.assertIn('Menu(tasksMenu, "Copy privacy-safe task diagnostics', MENU)
        self.assertIn("settings.Alerts = ((ToolStripMenuItem)sender).Checked;", MENU)
        self.assertIn("child.Enabled = settings.Alerts;", MENU)
        for text in ("5-hour window", "Weekly window", "Threshold 50 %",
                     "Threshold 25 %", "Threshold 10 %", "Reset observed"):
            self.assertIn(f'AddAlertToggle(alerts, "{text}"', MENU)

    def test_menu_helpers_do_not_change_codex_collector(self):
        self.assertIn("private static ToolStripMenuItem Menu(ToolStripMenuItem parent", UI)
        self.assertIn("private static ToolStripMenuItem Menu(ContextMenuStrip menu", UI)
        self.assertIn("settings.TrayStyle = value; Save(); UpdateQuotaTrayIcon(", MENU)
        self.assertNotIn("CodexReader", MENU)
        self.assertNotIn("File.ReadAllText(", MENU)


if __name__ == "__main__":
    unittest.main()
