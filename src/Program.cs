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
                        MessageBox.Show(report, "SHINO // CODEX — Autotests", MessageBoxButtons.OK,
                            report.IndexOf("FAIL ", StringComparison.Ordinal) >= 0 ? MessageBoxIcon.Error : MessageBoxIcon.Information);
                    Environment.ExitCode = report.IndexOf("FAIL ", StringComparison.Ordinal) >= 0 ? 1 : 0;
                    return;
                }
                try { Application.Run(new MonitorForm()); }
                catch (Exception)
                {
                    MessageBox.Show("Impossible de lancer SHINO // CODEX MONITOR V4.4. Ouvre le diagnostic dans le menu de l'application ou conserve la V3.0.2.",
                        "SHINO // CODEX", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            }
        }
    }
}
