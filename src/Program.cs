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
            bool isNew = false;
            using (Mutex mutex = new Mutex(true, @"Local\SHINO_CODEX_MONITOR_NATIVE_V4", out isNew))
            {
                if (!isNew) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (Environment.GetCommandLineArgs().Length > 1 &&
                    (Environment.GetCommandLineArgs()[1] == "--self-test" || Environment.GetCommandLineArgs()[1] == "--self-test-silent"))
                {
                    string report = SelfTests.Run();
                    try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "V4_TEST_REPORT.txt"), report); }
                    catch { }
                    if (Environment.GetCommandLineArgs()[1] != "--self-test-silent")
                        MessageBox.Show(report, "CODEX PERISCOPE — Autotests", MessageBoxButtons.OK,
                            report.IndexOf("FAIL ", StringComparison.Ordinal) >= 0 ? MessageBoxIcon.Error : MessageBoxIcon.Information);
                    Environment.ExitCode = report.IndexOf("FAIL ", StringComparison.Ordinal) >= 0 ? 1 : 0;
                    return;
                }
                try { Application.Run(new MonitorForm()); }
                catch (Exception)
                {
                    MessageBox.Show("Unable to launch CODEX PERISCOPE " + typeof(Program).Assembly.GetName().Version.ToString(3) + ". Le programme doit être examiné avant un nouveau démarrage.",
                        "CODEX PERISCOPE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            }
        }
    }
}
