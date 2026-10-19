using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShinoCodexMonitor
{
    internal static class CodexLocator
    {
        internal static string Find()
        {
            string custom = Environment.GetEnvironmentVariable("SHINO_CODEX_EXE");
            if (IsExecutable(custom)) return custom;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                try
                {
                    if (directory.Trim().Length == 0) continue;
                    string exe = Path.Combine(directory.Trim().Trim('"'), "codex.exe");
                    if (IsExecutable(exe)) return exe;
                }
                catch { }
            }
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] candidates = {
                Path.Combine(roaming, @"npm\node_modules\@openai\codex\node_modules\@openai\codex-win32-x64\vendor\x86_64-pc-windows-msvc\bin\codex.exe"),
                Path.Combine(roaming, @"npm\node_modules\@openai\codex\bin\codex.exe"),
                Path.Combine(local, @"Programs\Codex\codex.exe"),
                Path.Combine(profile, @".codex\bin\codex.exe")
            };
            foreach (string pathCandidate in candidates) if (IsExecutable(pathCandidate)) return pathCandidate;
            // Bounded search in one known npm package, to accommodate package layout changes.
            string package = Path.Combine(roaming, @"npm\node_modules\@openai\codex");
            string found = SearchPackage(package, 0);
            if (found != null) return found;
            // VS Code extensions are optional and vary by release; search only openai extensions.
            string vs = Path.Combine(profile, @".vscode\extensions");
            try
            {
                if (Directory.Exists(vs))
                    foreach (string ext in Directory.GetDirectories(vs, "openai.*"))
                    {
                        found = SearchPackage(ext, 0);
                        if (found != null) return found;
                    }
            }
            catch { }
            return null;
        }
        private static string SearchPackage(string dir, int depth)
        {
            if (depth > 8 || !Directory.Exists(dir)) return null;
            try
            {
                foreach (string exe in Directory.GetFiles(dir, "codex.exe")) if (IsExecutable(exe)) return exe;
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    string found = SearchPackage(sub, depth + 1);
                    if (found != null) return found;
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            return null;
        }
        private static bool IsExecutable(string file)
        {
            try { return !string.IsNullOrEmpty(file) && file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(file); }
            catch { return false; }
        }
    }

    internal static class CodexReader
    {
        private static readonly object Gate = new object();
        private static Process activeProcess;
        internal static void Cancel()
        {
            lock (Gate)
            {
                if (activeProcess != null)
                    try { if (!activeProcess.HasExited) activeProcess.Kill(); } catch { }
            }
        }
        // Strictly read-only. The child process inherits Codex's normal login; no auth files read.
        internal static Snapshot Read(string binary, int timeoutMs)
        {
            Process process = null;
            int timedOut = 0;
            Timer deadline = null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(binary, "app-server --listen stdio://");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                info.RedirectStandardInput = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = false;
                info.StandardOutputEncoding = new UTF8Encoding(false);
                process = new Process();
                process.StartInfo = info;
                if (!process.Start()) throw new IOException("Failed to start Codex server.");
                lock (Gate) activeProcess = process;
                Process child = process;
                deadline = new Timer(delegate(object state) {
                    Interlocked.Exchange(ref timedOut, 1);
                    try { if (!child.HasExited) child.Kill(); } catch { }
                }, null, timeoutMs, Timeout.Infinite);
                StreamReader stdout = child.StandardOutput;
                StreamWriter stdin = child.StandardInput;
                stdin.WriteLine("{\"method\":\"initialize\",\"id\":1,\"params\":{\"clientInfo\":{\"name\":\"codex_periscope\",\"title\":\"SHINO CODEX MONITOR\",\"version\":\"4.0.0\"}}}");
                stdin.Flush();
                Dictionary<string, object> initialized = WaitForId(stdout, 1, 30);
                CheckRpcError(initialized, "Initialisation");
                stdin.WriteLine("{\"method\":\"initialized\",\"params\":{}}");
                stdin.WriteLine("{\"method\":\"account/rateLimits/read\",\"id\":2}");
                stdin.Flush();
                Dictionary<string, object> response = WaitForId(stdout, 2, 60);
                CheckRpcError(response, "Lecture des quotas");
                return QuotaParser.Parse(Json.Get(response, "result"));
            }
            catch (Exception error)
            {
                if (Interlocked.CompareExchange(ref timedOut, 0, 0) == 1)
                    throw new TimeoutException("Codex response timeout (20 s).");
                throw new InvalidOperationException(SafeMessage(error), error);
            }
            finally
            {
                if (deadline != null) deadline.Dispose();
                lock (Gate) { if (object.ReferenceEquals(activeProcess, process)) activeProcess = null; }
                if (process != null)
                {
                    try { if (!process.HasExited) process.Kill(); } catch { }
                    process.Dispose();
                }
            }
        }
        private static string SafeMessage(Exception e)
        {
            string m = e.Message ?? "Erreur inconnue";
            // Never record CLI output, RPC bodies or secrets in logs/UI.
            if (m.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Codex access denied (401/403).";
            return m.Length > 150 ? m.Substring(0, 150) : m;
        }
        private static Dictionary<string, object> WaitForId(StreamReader stdout, int id, int maxLines)
        {
            for (int i = 0; i < maxLines; i++)
            {
                string line = stdout.ReadLine();
                if (line == null) throw new IOException("Connexion interruptede par Codex.");
                if (line.Length > 1048576) throw new InvalidDataException("Codex response too large.");
                Dictionary<string, object> obj;
                try { obj = Json.Object(Json.Serializer.DeserializeObject(line)); }
                catch (ArgumentException) { continue; }
                if (obj != null && Json.Int(Json.Get(obj, "id")) == id) return obj;
            }
            throw new InvalidDataException("Expected Codex response missing.");
        }
        private static void CheckRpcError(Dictionary<string, object> response, string step)
        {
            if (Json.Get(response, "error") != null)
            {
                Dictionary<string, object> detail = Json.Object(Json.Get(response, "error"));
                string code = Json.String(Json.Get(detail, "code")) ?? "";
                string privateMessage = Json.String(Json.Get(detail, "message")) ?? "";
                // Inspect error code/message only, never print the response body.
                if (code == "401" || code == "403" || privateMessage.Contains("401") || privateMessage.Contains("403"))
                    throw new UnauthorizedAccessException("Codex access denied (401/403).");
                throw new InvalidOperationException(step + " refused by Codex" + (code.Length <= 8 ? " (" + code + ")" : "") + ".");
            }
        }
    }
}
