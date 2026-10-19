from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
FORM = (ROOT / "src" / "MonitorForm.cs").read_text(encoding="utf-8")
BUILD = (ROOT / "BUILD-PREVIEW-V5.1.ps1").read_text(encoding="utf-8-sig")
STORE = (ROOT / "src" / "LocalStore.cs").read_text(encoding="utf-8")

class WindowIconV51Audit(unittest.TestCase):
    def test_taskbar_form_uses_embedded_shino_icon_not_winforms_default(self):
        self.assertIn("originalTrayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);", FORM)
        self.assertIn("Icon = originalTrayIcon ?? SystemIcons.Application;", FORM)
        self.assertIn("tray = new NotifyIcon { Icon = originalTrayIcon", FORM)
        self.assertLess(FORM.index("Icon = originalTrayIcon ?? SystemIcons.Application;"),
                        FORM.index("tray = new NotifyIcon"))
        self.assertIn("('/win32icon:' + (Join-Path $root 'SHINO-CODEX.ico'))", BUILD)

    def test_taskbar_visibility_remains_explicit_user_preference(self):
        self.assertIn("ShowInTaskbar = settings.InTaskbar && settings.Pinned;", FORM)
        self.assertIn("ShowInTaskbar = settings.Pinned && settings.InTaskbar;", FORM)
        self.assertIn('Menu(window, "Afficher dans la barre des tâches"', FORM)
        self.assertIn('preferences.InTaskbar = Json.Bool(Json.Get(dict, "InTaskbar"))', STORE)
        self.assertNotIn('ShowInTaskbar = true;', FORM)

if __name__ == "__main__":
    unittest.main()
