using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShinoCodexMonitor
{
    // Pure synthetic fixtures: never opens a real Codex account or session.
    internal static class LiveTaskTests
    {
        private static int passed, total;
        private static readonly StringBuilder report = new StringBuilder();
        private static readonly DateTimeOffset Now =
            DateTimeOffset.Parse("2026-10-19T10:00:00Z", CultureInfo.InvariantCulture);
        private static void Check(string label, Action test)
        {
            total++;
            try { test(); passed++; report.AppendLine("PASS " + label); }
            catch (Exception e) { report.AppendLine("FAIL " + label + " : " + e.GetType().Name); }
        }
        private static void Assert(bool condition)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed.");
        }
        private static string Event(string name, string id, int secondsAgo, string extras = "")
        {
            return "{\"timestamp\":\"" + Now.AddSeconds(-secondsAgo).ToString("o") +
                "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"" + name +
                "\",\"turn_id\":\"" + id + "\"" + extras + "}}";
        }
        private static string Context(string id, string model, string effort)
        {
            return "{\"timestamp\":\"" + Now.AddSeconds(-15).ToString("o") +
                "\",\"type\":\"turn_context\",\"payload\":{\"turn_id\":\"" + id +
                "\",\"model\":\"" + model + "\",\"effort\":\"" + effort + "\"}}";
        }
        private static LiveTask Parse(DateTimeOffset write, params string[] lines)
        {
            return LiveTaskReader.ParseLines(lines, write, Now);
        }
        private static LiveTaskReport Report(DateTimeOffset when, params LiveTask[] tasks)
        {
            LiveTaskReport result = new LiveTaskReport { CheckedUtc = when };
            result.Tasks.AddRange(tasks);
            return result;
        }
        internal static string Run()
        {
            passed = 0; total = 0; report.Length = 0;
            Check("Task lifecycle check 01", delegate {
                LiveTask x = Parse(Now.AddSeconds(-1), Event("task_started", "a1", 30));
                Assert(x != null && x.Phase == LiveTaskPhase.Active);
            });
            Check("Task lifecycle check 02", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 74));
                Assert(x.Duration(Now) == "01:14");
            });
            Check("Task lifecycle check 03", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 20),
                    Context("a1", "gpt-6", "high"));
                Assert(x.Model == "gpt-6" && x.Effort == "high");
            });
            Check("Task lifecycle check 04", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 20));
                Assert(x.Model == "Not provided" && x.Effort == "Not provided");
            });
            Check("Task lifecycle check 05", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 40),
                    Event("task_complete", "a1", 10));
                Assert(x.Phase == LiveTaskPhase.Completed && x.Duration(Now) == "00:30");
            });
            Check("Task lifecycle check 06", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 20),
                    Event("task_complete", "a1", 2, ",\"error\":{\"message\":\"redacted\"}"));
                Assert(x.Phase == LiveTaskPhase.Failed);
            });
            Check("Task lifecycle check 07", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 20),
                    Event("turn_aborted", "a1", 1));
                Assert(x.Phase == LiveTaskPhase.Interrupted);
            });
            Check("Task lifecycle check 08", delegate {
                LiveTask x = Parse(Now.AddSeconds(-91), Event("task_started", "a1", 300));
                Assert(x.Phase == LiveTaskPhase.Unconfirmed && !x.FinishedUtc.HasValue);
            });
            Check("Task lifecycle check 09", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 30),
                    Event("task_complete", "OTHER", 10));
                Assert(x.Phase == LiveTaskPhase.Active);
            });
            Check("Task lifecycle check 10", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 70),
                    Event("task_complete", "a1", 50),
                    Event("task_started", "b2", 20));
                Assert(x.Id == "b2" && x.Phase == LiveTaskPhase.Active);
            });
            Check("Task lifecycle check 11", delegate {
                LiveTask x = Parse(Now, "{malformed", "{\"type\":\"response_item\",\"payload\":{\"content\":\"SECRET\"}}");
                Assert(x == null);
            });
            Check("Task lifecycle check 12", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", -600));
                Assert(x == null);
            });
            Check("Task lifecycle check 13", delegate {
                LiveTask x = Parse(Now.AddMinutes(4), Event("task_started", "a1", 20));
                Assert(x.Phase == LiveTaskPhase.Unconfirmed);
            });
            Check("Task lifecycle check 14", delegate {
                foreach (int width in new int[] { 470, 540, 620, 720 })
                {
                    int quotasHeight = DesktopGeometry.HeightForWidth(width);
                    System.Drawing.Rectangle box = DesktopGeometry.ActivityBounds(width, quotasHeight, "Circular");
                    int fullHeight = DesktopGeometry.CombinedHeight(width, quotasHeight, "Circular");
                    Assert(box.Left >= 0 && box.Top > quotasHeight && box.Right < width &&
                        box.Bottom < fullHeight && box.Height > 60);
                }
            });
            Check("Task lifecycle check 15", delegate {
                foreach (string layout in new string[] { "Vertical", "Compact" })
                {
                    int width = layout == "Vertical" ? 355 : 670;
                    int baseHeight = layout == "Vertical" ? 257 : 177;
                    System.Drawing.Rectangle box = DesktopGeometry.ActivityBounds(width, baseHeight, layout);
                    Assert(box.Top > baseHeight && box.Width > width - 40 &&
                        box.Bottom < DesktopGeometry.CombinedHeight(width, baseHeight, layout));
                }
            });
            Check("Task lifecycle check 16", delegate {
                System.Drawing.Rectangle box = DesktopGeometry.ActivityBounds(484, 122, "Micro");
                Assert(box.Top >= 129 && box.Height >= 92 &&
                    box.Bottom < DesktopGeometry.CombinedHeight(484, 122, "Micro"));
            });
            Check("Task lifecycle check 17", delegate {
                LiveTask old = Parse(Now.AddSeconds(-20), Event("task_started", "", 40));
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                old.SessionKey = done.SessionKey = "rollout-0001.jsonl";
                LiveTaskReport before = Report(Now.AddSeconds(-20), old);
                LiveTaskReport after = Report(Now, done);
                Assert(old.Id == "" && done.Id == "" &&
                    TaskNotificationRules.NewTerminals(before, after).Count == 1);
            });
            Check("Task lifecycle check 18", delegate {
                LiveTask old = Parse(Now, Event("task_started", "", 40));
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                old.SessionKey = "rollout-A.jsonl"; done.SessionKey = "rollout-B.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-20), old),
                    Report(Now, done)).Count == 0);
            });
            Check("Task lifecycle check 19", delegate {
                LiveTask old = Parse(Now, Event("task_started", "", 60));
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                old.SessionKey = done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-20), old),
                    Report(Now, done)).Count == 0);
            });
            Check("Task lifecycle check 20", delegate {
                LiveTask old = Parse(Now, Event("task_started", "turn-A", 40));
                LiveTask done = Parse(Now, Event("task_started", "turn-B", 40),
                    Event("task_complete", "turn-B", 10));
                old.SessionKey = done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-20), old),
                    Report(Now, done)).Count == 0);
            });
            Check("Task lifecycle check 21", delegate {
                LiveTask done = Parse(Now, Event("task_started", "", 6),
                    Event("task_complete", "", 2));
                done.SessionKey = "rollout-short.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-10)),
                    Report(Now, done)).Count == 1);
            });
            Check("Task lifecycle check 22", delegate {
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-5), done),
                    Report(Now, done)).Count == 0);
            });
            Check("Task lifecycle check 23", delegate {
                LiveTask done = Parse(Now, Event("task_started", "", 5),
                    Event("task_complete", "", 2));
                done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(null, Report(Now, done)).Count == 0);
            });
            Check("Task lifecycle check 24", delegate {
                LiveTask old = Parse(Now, Event("task_started", "", 80));
                LiveTask done = Parse(Now, Event("task_started", "", 80),
                    Event("task_complete", "", 4));
                old.SessionKey = done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-70), old),
                    Report(Now, done)).Count == 0);
            });
            Check("Task lifecycle check 25", delegate {
                LiveTask old = Parse(Now.AddSeconds(-120), Event("task_started", "", 140));
                LiveTask done = Parse(Now, Event("task_started", "", 140),
                    Event("task_complete", "", 2));
                old.SessionKey = done.SessionKey = "rollout-A.jsonl";
                Assert(old.Phase == LiveTaskPhase.Unconfirmed &&
                    TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-10), old),
                        Report(Now, done)).Count == 0);
            });
            Check("Task lifecycle check 26", delegate {
                LiveTask done = Parse(Now.AddSeconds(-120), Event("task_started", "old", 130),
                    Event("task_complete", "old", 120));
                done.SessionKey = "rollout-old.jsonl";
                TaskDisplay d = Report(Now, done).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null &&
                    d.LastTerminal == done);
            });
            Check("Task lifecycle check 27", delegate {
                LiveTask done = Parse(Now, Event("task_started", "a", 20),
                    Event("task_complete", "a", 8));
                TaskDisplay d = Report(Now, done).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.RecentTerminal && d.Primary == done);
            });
            Check("Task lifecycle check 28", delegate {
                LiveTask done = Parse(Now, Event("task_started", "a", 20),
                    Event("task_complete", "a", 8));
                TaskDisplay d = Report(Now, done).DisplayAt(Now.AddSeconds(31));
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null);
            });
            Check("Task lifecycle check 29", delegate {
                LiveTask done = Parse(Now, Event("task_started", "old", 25),
                    Event("task_complete", "old", 5));
                LiveTask active = Parse(Now.AddSeconds(-2), Event("task_started", "new", 15));
                TaskDisplay d = Report(Now, done, active).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.ObservedActive && d.Primary == active &&
                    d.LastTerminal == done);
            });
            Check("Task lifecycle check 30", delegate {
                LiveTask done = Parse(Now, Event("task_started", "old", 20),
                    Event("task_complete", "old", 5));
                LiveTask unconfirmed = Parse(Now.AddSeconds(-100),
                    Event("task_started", "new", 180));
                TaskDisplay d = Report(Now, done, unconfirmed).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.Uncertain &&
                    d.Primary == unconfirmed);
            });
            Check("Task lifecycle check 31", delegate {
                LiveTask active = Parse(Now, Event("task_started", "run", 10));
                TaskDisplay d = Report(Now.AddSeconds(-30), active).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null);
            });
            Check("Task lifecycle check 32", delegate {
                LiveTask run = Parse(Now.AddSeconds(5),
                    Event("task_started", "run", 10));
                run.LastWriteUtc = Now.AddSeconds(8);
                TaskDisplay d = Report(Now, run).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null);
            });
            Check("Task lifecycle check 33", delegate {
                TaskDisplay d = Report(Now).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null &&
                    d.LastTerminal == null);
            });
            Check("Task lifecycle check 34", delegate {
                LiveTask old = Parse(Now, Event("task_started", "old", 500),
                    Event("task_complete", "old", 450));
                LiveTask newer = Parse(Now, Event("task_started", "new", 300),
                    Event("task_complete", "new", 180));
                TaskDisplay d = Report(Now, old, newer).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null &&
                    d.LastTerminal == newer);
            });
            Check("Task lifecycle check 35", delegate {
                LiveTask t = Parse(Now,
                    "{\"timestamp\":\"2026-10-19T09:59:00Z\",\"type\":\"session_meta\",\"payload\":{\"originator\":\"Codex Desktop\",\"thread_source\":\"user\",\"cwd\":\"C:/repo\"}}",
                    Event("task_started", "root", 35));
                Assert(t.IsRootConfirmed && !t.IsSubagent &&
                    t.ThreadSource == "user" && t.Originator == "Codex Desktop");
            });
            Check("Task lifecycle check 36", delegate {
                LiveTask t = Parse(Now,
                    "{\"timestamp\":\"2026-10-19T09:59:00Z\",\"type\":\"session_meta\",\"payload\":{\"originator\":\"Codex Desktop\",\"thread_source\":\"subagent\"}}",
                    Event("task_started", "child", 4), Event("task_complete", "child", 2));
                Assert(t.IsSubagent && !t.IsRootConfirmed && t.Phase == LiveTaskPhase.Completed);
            });
            Check("Task lifecycle check 37", delegate {
                LiveTask t = Parse(Now,
                    "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\",\"parent_thread_id\":\"root-private\"}}",
                    Event("task_started", "child", 4));
                Assert(t.IsSubagent && !t.IsRootConfirmed);
            });
            Check("Task lifecycle check 38", delegate {
                LiveTask t = Parse(Now, Event("task_started", "unknown", 4));
                Assert(!t.IsRootConfirmed && !t.IsSubagent &&
                    t.ThreadSource == "Inconnue");
            });
            Check("Task lifecycle check 39", delegate {
                LiveTask t = Parse(Now,
                    "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\"}}",
                    Event("task_started", "root", 650),
                    Event("task_complete", "root", 12, ",\"duration_ms\":638000"));
                Assert(t.IsRootConfirmed && t.RecordedDurationMs == 638000L &&
                    t.Duration(Now) == "10:38");
            });
            Check("Task lifecycle check 40", delegate {
                LiveTask t = Parse(Now,
                    Event("task_started", "root", 60),
                    Event("task_complete", "root", 0, ",\"duration_ms\":638000"));
                Assert(!t.RecordedDurationMs.HasValue && t.Duration(Now) == "01:00");
            });
            Check("Task lifecycle check 41", delegate {
                LiveTask t = Parse(Now, Event("task_started", "a", 70),
                    Event("task_complete", "a", 15));
                Assert(t.Duration(Now) == "00:55");
            });
            Check("Task lifecycle check 42", delegate {
                LiveTask t = Parse(Now, Event("task_started", "root", 40),
                    "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\"}}");
                Assert(t.IsRootConfirmed && !t.IsSubagent);
            });
            Check("Task lifecycle check 43", delegate {
                string parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "shino-fixture-" + Guid.NewGuid().ToString("N"));
                string previous = Environment.GetEnvironmentVariable("CODEX_HOME");
                try
                {
                    Environment.SetEnvironmentVariable("CODEX_HOME", parent);
                    string dir = System.IO.Path.Combine(parent, "sessions", "2026", "10", "19");
                    System.IO.Directory.CreateDirectory(dir);
                    string rootFile = System.IO.Path.Combine(dir, "rollout-fixture-user.jsonl");
                    string childFile = System.IO.Path.Combine(dir, "rollout-fixture-child.jsonl");
                    string userMeta = "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\",\"originator\":\"Codex Desktop\"}}";
                    string childMeta = "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"subagent\",\"originator\":\"Codex Desktop\"}}";
                    System.IO.File.WriteAllLines(rootFile, new string[] { userMeta,
                        Event("task_started", "root", 650), Event("task_complete", "root", 12, ",\"duration_ms\":638000") });
                    System.IO.File.WriteAllLines(childFile, new string[] { childMeta,
                        Event("task_started", "child", 5), Event("task_complete", "child", 3) });
                    System.IO.File.SetLastWriteTimeUtc(rootFile, Now.AddSeconds(-5).UtcDateTime);
                    System.IO.File.SetLastWriteTimeUtc(childFile, Now.AddSeconds(-2).UtcDateTime);
                    LiveTaskReport data = LiveTaskReader.Read(Now);
                    Assert(data.Tasks.Count == 1 && data.Tasks[0].Id == "root" &&
                        data.Tasks[0].Duration(Now) == "10:38" && data.Tasks[0].IsRootConfirmed);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("CODEX_HOME", previous);
                    if (System.IO.Directory.Exists(parent))
                        System.IO.Directory.Delete(parent, true);
                }
            });
            Check("Task lifecycle check 44", delegate {
                string parent = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "shino-fixture-" + Guid.NewGuid().ToString("N"));
                string previous = Environment.GetEnvironmentVariable("CODEX_HOME");
                try
                {
                    Environment.SetEnvironmentVariable("CODEX_HOME", parent);
                    string dir = System.IO.Path.Combine(parent, "sessions", "2026", "10", "19");
                    System.IO.Directory.CreateDirectory(dir);
                    string path = System.IO.Path.Combine(dir, "rollout-long-root.jsonl");
                    string meta = "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\",\"originator\":\"Codex Desktop\"}}";
                    string unrelated = "{\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"content\":\"ignored\"}}";
                    using (System.IO.StreamWriter writer = new System.IO.StreamWriter(path))
                    {
                        writer.WriteLine(meta);
                        writer.WriteLine(Event("task_started", "root", 650));
                        for (int i = 0; i < 9000; i++) writer.WriteLine(unrelated);
                        writer.WriteLine(Event("task_complete", "root", 12, ",\"duration_ms\":638000"));
                    }
                    System.IO.File.SetLastWriteTimeUtc(path, Now.AddSeconds(-2).UtcDateTime);
                    LiveTaskReport data = LiveTaskReader.Read(Now);
                    Assert(data.Tasks.Count == 1 && data.Tasks[0].IsRootConfirmed &&
                        data.Tasks[0].Duration(Now) == "10:38");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("CODEX_HOME", previous);
                    if (System.IO.Directory.Exists(parent))
                        System.IO.Directory.Delete(parent, true);
                }
            });
            Check("Task lifecycle check 45", delegate {
                LiveTask t = Parse(Now,
                    "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\",\"originator\":\"Codex Desktop\",\"cwd\":\"C:/secret/project\"}}",
                    Event("task_started", "PRIVATE_TURN_ID", 30),
                    Event("task_complete", "PRIVATE_TURN_ID", 5, ",\"duration_ms\":25000"));
                t.SessionKey = "PRIVATE_FILE.jsonl";
                LiveTaskReport r = Report(Now, t);
                r.ExaminedRoots = 1;
                string sanitized = r.RedactedDiagnostics(Now);
                Assert(sanitized.Contains("duration_ms=25000") &&
                    !sanitized.Contains("PRIVATE_TURN_ID") &&
                    !sanitized.Contains("PRIVATE_FILE") &&
                    !sanitized.Contains("C:/secret") &&
                    !sanitized.Contains("project"));
            });
            Check("Task lifecycle check 46", delegate {
                LiveTaskReport r = Report(Now);
                string diagnostic = r.RedactedDiagnostics(Now);
                Assert(diagnostic.Contains("NotVerified") &&
                    diagnostic.Contains("No observable root turns."));
            });
            report.Insert(0, "SHINO // LIVE TASKS — " + passed + "/" + total +
                " simulated tests passed\r\n");
            return report.ToString();
        }
    }
}
