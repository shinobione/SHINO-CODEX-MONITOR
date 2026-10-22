from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
READER = (ROOT / "src" / "LiveTaskMonitor.cs").read_text(encoding="utf-8")
VIEW = (ROOT / "src" / "ActivityGlyph.cs").read_text(encoding="utf-8")
FORM = (ROOT / "src" / "MonitorForm.cs").read_text(encoding="utf-8")
TESTS = (ROOT / "src" / "LiveTaskTests.cs").read_text(encoding="utf-8")
BUILD = (ROOT / "BUILD-PERISCOPE-V5.2.ps1").read_text(encoding="utf-8-sig")
CI = (ROOT / ".github/workflows/build-windows.yml").read_text(encoding="utf-8")
PREFS = (ROOT / "src" / "Models.cs").read_text(encoding="utf-8")


class LiveTaskV51Audit(unittest.TestCase):
    def test_bounded_readonly_session_observer(self):
        self.assertIn("CODEX_HOME", READER)
        self.assertIn('"sessions"', READER)
        self.assertIn("FileAccess.Read", READER)
        self.assertIn("TailBytes = 524288", READER)
        self.assertIn("MaxFiles = 12", READER)
        self.assertNotIn("auth.json", READER)
        self.assertNotIn("HttpClient", READER)
        self.assertNotIn("File.WriteAll", READER)
        self.assertNotIn("Process.Start", READER)

    def test_freshness_and_explicit_terminal_states(self):
        for value in ("task_started", "task_complete", "turn_aborted",
                      "LiveTaskPhase.Unconfirmed", "ActivityMaxAgeSeconds"):
            self.assertIn(value, READER)
        self.assertIn("v.State == RadarState.Active", FORM)
        self.assertIn('old.Phase == LiveTaskPhase.Active', READER)

    def test_graphics_lightweight_and_only_visible_animation(self):
        self.assertIn("Graphics", VIEW)
        self.assertIn("g.DrawLine(stroke, cx, cy", VIEW)
        self.assertIn("taskMotion.Enabled = Visible", FORM)
        self.assertIn("taskDock.Bounds=Units(DesktopGeometry.ActivityBounds(", FORM)
        self.assertNotIn("WebView2", VIEW)
        self.assertNotIn("Electron", VIEW)
        self.assertIn("taskMotion.Enabled = Visible", FORM)
        self.assertNotIn("LiveTaskWindow", FORM)
        self.assertIn("taskDock.Controls.Add(taskDeck);", FORM)

    def test_notification_requires_observed_transition(self):
        self.assertIn("taskReport != null && settings.Alerts && settings.TaskAlerts", FORM)
        self.assertIn("TaskNotificationRules.NewTerminals(taskReport, fresh)", FORM)
        self.assertIn("Test Windows notification", FORM)
        self.assertIn("ShowBalloonTip(6500", FORM)
        self.assertIn("SessionKey", READER)
        self.assertIn("string.IsNullOrEmpty(a.Id)", READER)
        self.assertIn("briefTurn", READER)
        self.assertIn("IsRootConfirmed", READER)
        self.assertIn("duration_ms", READER)
        self.assertIn("ReadFirstMetadataLine", READER)
        self.assertIn("RedactedDiagnostics", READER)
        self.assertIn("SafeDiagnosticSource", READER)
        self.assertIn("guardian_review", READER)
        self.assertIn("ChildSessionsLinked", READER)
        self.assertIn("RecoveryLimitedByPartialRead", READER)
        self.assertIn("Global mission selection:", READER)
        self.assertIn("Last primary turns", READER)
        self.assertIn("Last child turns", READER)
        self.assertIn("Copy privacy-safe task diagnostics", FORM)
        self.assertIn("tray.ShowBalloonTip(5500", FORM)

    def test_options_and_preserved_quota_collector(self):
        for value in ("TaskAnimations", "TaskAlerts"):
            self.assertIn(value, PREFS)
            self.assertIn(value, FORM)
        self.assertIn("CodexReader.Read(detected, 20000)", FORM)
        self.assertIn("UpdateQuotaTrayIcon", FORM)

    def test_executable_tests_include_source_and_are_ci_gated(self):
        for value in ("'LiveTaskMonitor.cs'", "'ActivityGlyph.cs'",
                      "'LiveTaskTests.cs'"):
            self.assertIn(value, BUILD)
        self.assertEqual(TESTS.count('Check("'), 69)
        self.assertIn("--live-task-self-test", CI)
        self.assertIn("69/69", CI)


if __name__ == "__main__":
    unittest.main()

