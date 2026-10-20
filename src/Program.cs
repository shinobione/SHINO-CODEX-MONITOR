using System;
using System.Threading;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--target-video")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);TargetTests.Video();return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--target-captures")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);TargetTests.Capture();return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--target-self-test")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                string report=TargetTests.Run();System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TARGET_TEST_REPORT.txt"),report);
                Environment.ExitCode=report.Contains("FAIL ")?1:0;return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--parity-performance")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PARITY_PERFORMANCE_REPORT.txt"),InstrumentTests.Performance(true));return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--instrument-performance")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"INSTRUMENT_PERFORMANCE_REPORT.txt"),InstrumentTests.Performance());
                return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--instrument-self-test")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                string report=InstrumentTests.Run();
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"INSTRUMENT_TEST_REPORT.txt"),report);
                Environment.ExitCode=report.Contains("FAIL ") ? 1 : 0; return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--instrument-captures")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                InstrumentTests.CaptureAll("after");
                return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--p0-performance")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"P0_PERFORMANCE_REPORT.txt"),P0Tests.Performance());
                return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--p0-self-test")
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                string report = P0Tests.Run();
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"P0_TEST_REPORT.txt"),report);
                Environment.ExitCode = report.Contains("FAIL ") ? 1 : 0;
                return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 && Environment.GetCommandLineArgs()[1] == "--premium-performance")
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PREMIUM_PERFORMANCE_REPORT.txt"),PremiumTests.RunPerformance());
                return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 &&
                Environment.GetCommandLineArgs()[1] == "--premium-self-test")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                string report = PremiumTests.Run();
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "PREMIUM_TEST_REPORT.txt"), report);
                Environment.ExitCode = report.Contains("FAIL ") ? 1 : 0;
                return;
            }
            // Explicit CI-only live download smoke test, without opening the widget
            // or installing anything. Does not depend on an authenticated Codex CLI.
            if (Environment.GetCommandLineArgs().Length > 1 &&
                Environment.GetCommandLineArgs()[1] == "--update-download-smoke")
            {
                Environment.ExitCode = UpdateDownloadSmoke.Run();
                return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 &&
                Environment.GetCommandLineArgs()[1] == "--live-task-self-test")
            {
                string report = LiveTaskTests.Run();
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "V5_TASK_TEST_REPORT.txt"), report); }
                catch { }
                Environment.ExitCode = report.IndexOf("FAIL ", StringComparison.Ordinal) >= 0 ? 1 : 0;
                return;
            }
            if (Environment.GetCommandLineArgs().Length > 1 &&
                (Environment.GetCommandLineArgs()[1] == "--self-test" || Environment.GetCommandLineArgs()[1] == "--self-test-silent"))
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                string report = SelfTests.Run();
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "V4_TEST_REPORT.txt"), report); }
                catch { }
                if (Environment.GetCommandLineArgs()[1] != "--self-test-silent")
                    MessageBox.Show(report, "SHINO // CODEX — Autotests", MessageBoxButtons.OK,
                        report.IndexOf("FAIL ", StringComparison.Ordinal) >= 0 ? MessageBoxIcon.Error : MessageBoxIcon.Information);
                Environment.ExitCode = report.IndexOf("FAIL ", StringComparison.Ordinal) >= 0 ? 1 : 0;
                return;
            }
            bool isNew = false;
            using (Mutex mutex = new Mutex(true, @"Local\SHINO_CODEX_MONITOR_NATIVE_V4", out isNew))
            {
                if (!isNew) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { Application.Run(new MonitorForm()); }
                catch (Exception)
                {
                    MessageBox.Show("Could not start CODEX PERISCOPE " + typeof(Program).Assembly.GetName().Version.ToString(3) + ". The application must be checked before another launch.",
                        "SHINO // CODEX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            }
        }
    }
}
