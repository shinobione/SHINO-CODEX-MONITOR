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
            if (!condition) throw new InvalidOperationException("Échec d'assertion.");
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
            Check("Start récent identifié comme observation", delegate {
                LiveTask x = Parse(Now.AddSeconds(-1), Event("task_started", "a1", 30));
                Assert(x != null && x.Phase == LiveTaskPhase.Active);
            });
            Check("Temps depuis vrai début et non depuis dernier poll", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 74));
                Assert(x.Duration(Now) == "01:14");
            });
            Check("Modèle et effort de turn_context", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 20),
                    Context("a1", "gpt-6", "high"));
                Assert(x.Model == "gpt-6" && x.Effort == "high");
            });
            Check("Modèle non communiqué distinct de modèle inventé", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 20));
                Assert(x.Model == "Not provided" && x.Effort == "Not provided");
            });
            Check("Terminal confirmé : succès et durée figée", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 40),
                    Event("task_complete", "a1", 10));
                Assert(x.Phase == LiveTaskPhase.Completed && x.Duration(Now) == "00:30");
            });
            Check("Terminal avec erreur explicite : échec", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 20),
                    Event("task_complete", "a1", 2, ",\"error\":{\"message\":\"redacted\"}"));
                Assert(x.Phase == LiveTaskPhase.Failed);
            });
            Check("Interruption distincte du succès", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 20),
                    Event("turn_aborted", "a1", 1));
                Assert(x.Phase == LiveTaskPhase.Interrupted);
            });
            Check("Démarrage orphelin / fichier périmé ne reste pas LIVE", delegate {
                LiveTask x = Parse(Now.AddSeconds(-91), Event("task_started", "a1", 300));
                Assert(x.Phase == LiveTaskPhase.Unconfirmed && !x.FinishedUtc.HasValue);
            });
            Check("Fin d'un autre tour ne termine pas le tour courant", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 30),
                    Event("task_complete", "OTHER", 10));
                Assert(x.Phase == LiveTaskPhase.Active);
            });
            Check("Plusieurs tours : le dernier prend le dessus", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", 70),
                    Event("task_complete", "a1", 50),
                    Event("task_started", "b2", 20));
                Assert(x.Id == "b2" && x.Phase == LiveTaskPhase.Active);
            });
            Check("Journaux incomplets ou mal formés : pas de faux succès", delegate {
                LiveTask x = Parse(Now, "{malformed", "{\"type\":\"response_item\",\"payload\":{\"content\":\"SECRET\"}}");
                Assert(x == null);
            });
            Check("Horodatage incohérent rejeté", delegate {
                LiveTask x = Parse(Now, Event("task_started", "a1", -600));
                Assert(x == null);
            });
            Check("Donnée future système : état non vérifiable", delegate {
                LiveTask x = Parse(Now.AddMinutes(4), Event("task_started", "a1", 20));
                Assert(x.Phase == LiveTaskPhase.Unconfirmed);
            });
            Check("Widget circulaire tout-en-un 470 à 720 px", delegate {
                foreach (int width in new int[] { 470, 540, 620, 720 })
                {
                    int quotasHeight = DesktopGeometry.HeightForWidth(width);
                    System.Drawing.Rectangle box = DesktopGeometry.ActivityBounds(width, quotasHeight, "Circular");
                    int fullHeight = DesktopGeometry.CombinedHeight(width, quotasHeight, "Circular");
                    Assert(box.Left >= 0 && box.Top > quotasHeight && box.Right < width &&
                        box.Bottom < fullHeight && box.Height > 60);
                }
            });
            Check("Widget vertical et compact conservent les quotas", delegate {
                foreach (string layout in new string[] { "Vertical", "Compact" })
                {
                    int width = layout == "Vertical" ? 355 : 670;
                    int baseHeight = layout == "Vertical" ? 257 : 177;
                    System.Drawing.Rectangle box = DesktopGeometry.ActivityBounds(width, baseHeight, layout);
                    Assert(box.Top > baseHeight && box.Width > width - 40 &&
                        box.Bottom < DesktopGeometry.CombinedHeight(width, baseHeight, layout));
                }
            });
            Check("Micro : indicateur intégré sans recouvrir les resets", delegate {
                System.Drawing.Rectangle box = DesktopGeometry.ActivityBounds(484, 122, "Micro");
                Assert(box.Top >= 129 && box.Height >= 92 &&
                    box.Bottom < DesktopGeometry.CombinedHeight(484, 122, "Micro"));
            });
            Check("Notification : tour sans ID mais même session et début", delegate {
                LiveTask old = Parse(Now.AddSeconds(-20), Event("task_started", "", 40));
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                old.SessionKey = done.SessionKey = "rollout-0001.jsonl";
                LiveTaskReport before = Report(Now.AddSeconds(-20), old);
                LiveTaskReport after = Report(Now, done);
                Assert(old.Id == "" && done.Id == "" &&
                    TaskNotificationRules.NewTerminals(before, after).Count == 1);
            });
            Check("Notification : sessions distinctes ne se confondent pas", delegate {
                LiveTask old = Parse(Now, Event("task_started", "", 40));
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                old.SessionKey = "rollout-A.jsonl"; done.SessionKey = "rollout-B.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-20), old),
                    Report(Now, done)).Count == 0);
            });
            Check("Notification : autre tour même fichier pas assimilé", delegate {
                LiveTask old = Parse(Now, Event("task_started", "", 60));
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                old.SessionKey = done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-20), old),
                    Report(Now, done)).Count == 0);
            });
            Check("Notification : turn_id contradictoire reste distinct", delegate {
                LiveTask old = Parse(Now, Event("task_started", "turn-A", 40));
                LiveTask done = Parse(Now, Event("task_started", "turn-B", 40),
                    Event("task_complete", "turn-B", 10));
                old.SessionKey = done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-20), old),
                    Report(Now, done)).Count == 0);
            });
            Check("Notification : tâche complète entièrement entre deux polls", delegate {
                LiveTask done = Parse(Now, Event("task_started", "", 6),
                    Event("task_complete", "", 2));
                done.SessionKey = "rollout-short.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-10)),
                    Report(Now, done)).Count == 1);
            });
            Check("Notification : aucun doublon sur fin déjà vue", delegate {
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-5), done),
                    Report(Now, done)).Count == 0);
            });
            Check("Notification : jamais lors du tout premier scan", delegate {
                LiveTask done = Parse(Now, Event("task_started", "", 5),
                    Event("task_complete", "", 2));
                done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(null, Report(Now, done)).Count == 0);
            });
            Check("Notification : pas de replay après veille / grand intervalle", delegate {
                LiveTask old = Parse(Now, Event("task_started", "", 80));
                LiveTask done = Parse(Now, Event("task_started", "", 80),
                    Event("task_complete", "", 4));
                old.SessionKey = done.SessionKey = "rollout-A.jsonl";
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-70), old),
                    Report(Now, done)).Count == 0);
            });
            Check("Notification : aucun faux positif après activité non confirmée", delegate {
                LiveTask old = Parse(Now.AddSeconds(-120), Event("task_started", "", 140));
                LiveTask done = Parse(Now, Event("task_started", "", 140),
                    Event("task_complete", "", 2));
                old.SessionKey = done.SessionKey = "rollout-A.jsonl";
                Assert(old.Phase == LiveTaskPhase.Unconfirmed &&
                    TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-10), old),
                        Report(Now, done)).Count == 0);
            });
            Check("Capture réelle : ancien tour FIN ne vaut pas tâche actuelle", delegate {
                LiveTask done = Parse(Now.AddSeconds(-120), Event("task_started", "old", 130),
                    Event("task_complete", "old", 120));
                done.SessionKey = "rollout-old.jsonl";
                TaskDisplay d = Report(Now, done).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null &&
                    d.LastTerminal == done);
            });
            Check("Fin locale récente seulement 30 s, explicitement historique", delegate {
                LiveTask done = Parse(Now, Event("task_started", "a", 20),
                    Event("task_complete", "a", 8));
                TaskDisplay d = Report(Now, done).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.RecentTerminal && d.Primary == done);
            });
            Check("Ancienne coche verte doit expirer sans nouveau relevé", delegate {
                LiveTask done = Parse(Now, Event("task_started", "a", 20),
                    Event("task_complete", "a", 8));
                TaskDisplay d = Report(Now, done).DisplayAt(Now.AddSeconds(31));
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null);
            });
            Check("Session active récente prioritaire sur autre session terminée", delegate {
                LiveTask done = Parse(Now, Event("task_started", "old", 25),
                    Event("task_complete", "old", 5));
                LiveTask active = Parse(Now.AddSeconds(-2), Event("task_started", "new", 15));
                TaskDisplay d = Report(Now, done, active).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.ObservedActive && d.Primary == active &&
                    d.LastTerminal == done);
            });
            Check("Début sans fin fiable : incertain prioritaire sur ancien succès", delegate {
                LiveTask done = Parse(Now, Event("task_started", "old", 20),
                    Event("task_complete", "old", 5));
                LiveTask unconfirmed = Parse(Now.AddSeconds(-100),
                    Event("task_started", "new", 180));
                TaskDisplay d = Report(Now, done, unconfirmed).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.Uncertain &&
                    d.Primary == unconfirmed);
            });
            Check("Relevé sans refresh plus de 25 s : plus de statut LIVE", delegate {
                LiveTask active = Parse(Now, Event("task_started", "run", 10));
                TaskDisplay d = Report(Now.AddSeconds(-30), active).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null);
            });
            Check("Horodatage futur du fichier : aucune activité actuelle prétendue", delegate {
                LiveTask run = Parse(Now.AddSeconds(5),
                    Event("task_started", "run", 10));
                run.LastWriteUtc = Now.AddSeconds(8);
                TaskDisplay d = Report(Now, run).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null);
            });
            Check("Aucun tour présent ne signifie pas Codex inactif", delegate {
                TaskDisplay d = Report(Now).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null &&
                    d.LastTerminal == null);
            });
            Check("Deux tours terminés anciens : dernière fin n'est pas statut global", delegate {
                LiveTask old = Parse(Now, Event("task_started", "old", 500),
                    Event("task_complete", "old", 450));
                LiveTask newer = Parse(Now, Event("task_started", "new", 300),
                    Event("task_complete", "new", 180));
                TaskDisplay d = Report(Now, old, newer).DisplayAt(Now);
                Assert(d.Kind == TaskDisplayKind.NotVerified && d.Primary == null &&
                    d.LastTerminal == newer);
            });
            Check("Session racine Desktop : thread_source=user", delegate {
                LiveTask t = Parse(Now,
                    "{\"timestamp\":\"2026-10-19T09:59:00Z\",\"type\":\"session_meta\",\"payload\":{\"originator\":\"Codex Desktop\",\"thread_source\":\"user\",\"cwd\":\"C:/repo\"}}",
                    Event("task_started", "root", 35));
                Assert(t.IsRootConfirmed && !t.IsSubagent &&
                    t.ThreadSource == "user" && t.Originator == "Codex Desktop");
            });
            Check("Sous-agent : thread_source=subagent exclu du statut utilisateur", delegate {
                LiveTask t = Parse(Now,
                    "{\"timestamp\":\"2026-10-19T09:59:00Z\",\"type\":\"session_meta\",\"payload\":{\"originator\":\"Codex Desktop\",\"thread_source\":\"subagent\"}}",
                    Event("task_started", "child", 4), Event("task_complete", "child", 2));
                Assert(t.IsSubagent && !t.IsRootConfirmed && t.Phase == LiveTaskPhase.Completed);
            });
            Check("Sous-agent : parent_thread_id implique un thread enfant", delegate {
                LiveTask t = Parse(Now,
                    "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\",\"parent_thread_id\":\"root-private\"}}",
                    Event("task_started", "child", 4));
                Assert(t.IsSubagent && !t.IsRootConfirmed);
            });
            Check("Thread_source absent ne devient jamais racine confirmée", delegate {
                LiveTask t = Parse(Now, Event("task_started", "unknown", 4));
                Assert(!t.IsRootConfirmed && !t.IsSubagent &&
                    t.ThreadSource == "Unknown");
            });
            Check("Turn complet 10 min 38 s : duration_ms fiable", delegate {
                LiveTask t = Parse(Now,
                    "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\"}}",
                    Event("task_started", "root", 650),
                    Event("task_complete", "root", 12, ",\"duration_ms\":638000"));
                Assert(t.IsRootConfirmed && t.RecordedDurationMs == 638000L &&
                    t.Duration(Now) == "10:38");
            });
            Check("duration_ms contradictoire : conserver les horodatages", delegate {
                LiveTask t = Parse(Now,
                    Event("task_started", "root", 60),
                    Event("task_complete", "root", 0, ",\"duration_ms\":638000"));
                Assert(!t.RecordedDurationMs.HasValue && t.Duration(Now) == "01:00");
            });
            Check("duration_ms absent : utiliser les timestamps du tour", delegate {
                LiveTask t = Parse(Now, Event("task_started", "a", 70),
                    Event("task_complete", "a", 15));
                Assert(t.Duration(Now) == "00:55");
            });
            Check("Session_META après début conserve son statut racine", delegate {
                LiveTask t = Parse(Now, Event("task_started", "root", 40),
                    "{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\"}}");
                Assert(t.IsRootConfirmed && !t.IsSubagent);
            });
            Check("Read() : priorité au tour utilisateur, sous-agent exclu", delegate {
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
            Check("Session racine retrouvée quand en-tête hors de la fin du fichier", delegate {
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
            Check("Diagnostic : aucune donnée privée ni ID de thread", delegate {
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
            Check("Diagnostic : aucune session ne signifie pas terminé", delegate {
                LiveTaskReport r = Report(Now);
                string diagnostic = r.RedactedDiagnostics(Now);
                Assert(diagnostic.Contains("NotVerified") &&
                    diagnostic.Contains("No primary turn observed."));
            });
            report.Insert(0, "CODEX PERISCOPE TASKS — " + passed + "/" + total +
                " tests simulés réussis\r\n");
            return report.ToString();
        }
    }
}
