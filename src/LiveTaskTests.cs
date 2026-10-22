using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            try { test(); passed++; report.AppendLine("PASS Test #" + total); }
            catch (Exception e) { report.AppendLine("FAIL Test #" + total + " [" + label + "] : " + e.GetType().Name); }
        }
        private static void Assert(bool condition)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed.");
        }
        private static void Assert(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException(message);
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
        private static LiveTask Root(string thread,string key,DateTimeOffset started,DateTimeOffset evidence,LiveTaskPhase phase=LiveTaskPhase.Active)
        { return new LiveTask { Id="root",SessionKey=key,ThreadId=thread,IsRootConfirmed=true,StartedUtc=started,LastWriteUtc=evidence,Phase=phase,Workspace="fixture" }; }
        private static LiveTask Child(string thread,string key,DateTimeOffset started,DateTimeOffset evidence,LiveTaskPhase phase=LiveTaskPhase.Active)
        { return new LiveTask { Id="child",SessionKey=key,ThreadId="child-thread",ParentThreadId=thread,IsSubagent=true,StartedUtc=started,LastWriteUtc=evidence,Phase=phase }; }
        private static MissionView Mission(params LiveTask[] tasks)
        { MissionTracker tracker=new MissionTracker();tracker.Update(Report(Now,tasks));return tracker.At(Now).First(); }
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
                    Assert(box.Left >= 0 && box.Top == quotasHeight-DesktopGeometry.Scale(22,width) && box.Right < width &&
                        box.Bottom < fullHeight && box.Height > 90 && fullHeight == quotasHeight+DesktopGeometry.Scale(8,width)+DesktopGeometry.Scale(100,width)+DesktopGeometry.Scale(12,width));
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
                Assert(box.Top == 138 && box.Height == 84 &&
                    DesktopGeometry.CombinedHeight(484,122,"Micro") == 230 &&
                    box.Bottom < DesktopGeometry.CombinedHeight(484, 122, "Micro"));
            });
            Check("Notification : tour sans ID mais même session et début", delegate {
                LiveTask old = Parse(Now.AddSeconds(-20), Event("task_started", "", 40));
                LiveTask done = Parse(Now, Event("task_started", "", 40),
                    Event("task_complete", "", 10));
                old.IsRootConfirmed=done.IsRootConfirmed=true;
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
                done.IsRootConfirmed=true;
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
                // A terminal timestamp in the future is never a confirmed alert.
                LiveTask impossible = new LiveTask { Phase = LiveTaskPhase.Completed,
                    SessionKey = "rollout-future.jsonl", StartedUtc = Now.AddSeconds(-8),
                    FinishedUtc = Now.AddSeconds(10) };
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-10)),
                    Report(Now, impossible)).Count == 0);
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
                // Same-session ACTIVE snapshot is not enough: an old terminal
                // event predating the last scan must not generate a new toast.
                LiveTask recentlySeen = Parse(Now, Event("task_started", "stale", 80));
                LiveTask staleFinish = Parse(Now, Event("task_started", "stale", 80),
                    Event("task_complete", "stale", 40));
                recentlySeen.SessionKey = staleFinish.SessionKey = "rollout-stale.jsonl";
                Assert(TaskNotificationRules.NewTerminals(
                    Report(Now.AddSeconds(-20), recentlySeen),
                    Report(Now, staleFinish)).Count == 0);
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
                done.IsRootConfirmed=active.IsRootConfirmed=true;
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
                    Assert(data.Tasks.Count(t=>t.IsRootConfirmed)==1 && data.Tasks.First(t=>t.IsRootConfirmed).Id == "root" &&
                        data.Tasks.First(t=>t.IsRootConfirmed).Duration(Now) == "10:38" && data.Tasks.Count(t=>t.IsSubagent)==1);
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
            Check("Sous-agent frais lié au seul tour racine ouvert : état délégué, distinct du LIVE parent", delegate {
                LiveTask root = new LiveTask { Id="root-turn", SessionKey="root-file", ThreadId="root-thread",
                    IsRootConfirmed=true, StartedUtc=Now.AddMinutes(-4), LastWriteUtc=Now.AddMinutes(-3),
                    Phase=LiveTaskPhase.Active, Workspace="repo" };
                LiveTask child = new LiveTask { Id="child-turn", SessionKey="child-file", ParentThreadId="root-thread",
                    IsSubagent=true, StartedUtc=Now.AddMinutes(-2), LastWriteUtc=Now.AddSeconds(-4),
                    Phase=LiveTaskPhase.Active };
                MissionTracker tracker = new MissionTracker();
                tracker.Update(Report(Now, root, child));
                MissionView mission = tracker.At(Now).Single();
                Assert(mission.State == RadarState.Delegated && mission.Task.Phase == LiveTaskPhase.Active &&
                    mission.ParentEvidenceUtc == root.LastWriteUtc && mission.ChildEvidenceUtc == child.LastWriteUtc);
            });
            Check("Read() lie les vrais champs id et parent_thread_id et étiquette l'activité comme déléguée", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-fixture-"+Guid.NewGuid().ToString("N"));string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions","2026","10","19");System.IO.Directory.CreateDirectory(dir);
                    string rootPath=System.IO.Path.Combine(dir,"rollout-parent.jsonl"),childPath=System.IO.Path.Combine(dir,"rollout-child.jsonl");
                    System.IO.File.WriteAllLines(rootPath,new[]{"{\"type\":\"session_meta\",\"payload\":{\"id\":\"root-thread\",\"thread_source\":\"user\"}}",Event("task_started","root-turn",240)});
                    System.IO.File.WriteAllLines(childPath,new[]{"{\"type\":\"session_meta\",\"payload\":{\"id\":\"child-thread\",\"parent_thread_id\":\"root-thread\",\"thread_source\":\"subagent\"}}",Event("task_started","child-turn",5)});
                    System.IO.File.SetLastWriteTimeUtc(rootPath,Now.AddSeconds(-180).UtcDateTime);System.IO.File.SetLastWriteTimeUtc(childPath,Now.AddSeconds(-2).UtcDateTime);
                    LiveTaskReader.ClearCacheForTests();LiveTaskReport data=LiveTaskReader.Read(Now);MissionTracker tracker=new MissionTracker();tracker.Update(data);
                    Assert(data.Tasks.Count(t=>t.IsRootConfirmed&&!t.IsSubagent)==1&&data.Tasks.Count(t=>t.IsSubagent)==1&&
                        tracker.At(Now).Single().State==RadarState.Delegated&&data.DisplayAt(Now).Kind==TaskDisplayKind.DelegatedObserved);
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Fin racine confirmée prime sur enfant actif contradictoire", delegate {
                LiveTask root = new LiveTask { Id="root-turn", SessionKey="root-file", ThreadId="root-thread",
                    IsRootConfirmed=true, StartedUtc=Now.AddMinutes(-4), LastWriteUtc=Now.AddSeconds(-2),
                    FinishedUtc=Now.AddSeconds(-2), Phase=LiveTaskPhase.Completed };
                LiveTask child = new LiveTask { Id="child-turn", SessionKey="child-file", ParentThreadId="root-thread",
                    IsSubagent=true, StartedUtc=Now.AddMinutes(-2), LastWriteUtc=Now.AddSeconds(-1),
                    Phase=LiveTaskPhase.Active };
                MissionTracker tracker = new MissionTracker();
                tracker.Update(Report(Now, root, child));
                Assert(tracker.At(Now).Single().State == RadarState.Complete);
            });
            Check("Racine active sans enfant conserve ACTIVE", delegate {
                Assert(Mission(Root("root-thread","root-file",Now.AddMinutes(-1),Now.AddSeconds(-4))).State==RadarState.Active);
            });
            Check("Deux enfants frais du même parent sont observés sans exposer leurs identifiants", delegate {
                MissionView m=Mission(Root("root-thread","root-file",Now.AddMinutes(-4),Now.AddMinutes(-3)),
                    Child("root-thread","child-a",Now.AddMinutes(-2),Now.AddSeconds(-5)),
                    Child("root-thread","child-b",Now.AddMinutes(-1),Now.AddSeconds(-2)));
                Assert(m.State==RadarState.Delegated&&m.ActiveChildCount==2&&m.ChildEvidenceUtc==Now.AddSeconds(-2));
            });
            Check("Deux parents concurrents ne reçoivent que leurs enfants explicites", delegate {
                MissionTracker tracker=new MissionTracker();
                tracker.Update(Report(Now,Root("root-a","a",Now.AddMinutes(-5),Now.AddMinutes(-4)),
                    Root("root-b","b",Now.AddMinutes(-5),Now.AddMinutes(-4)),
                    Child("root-a","ca",Now.AddMinutes(-2),Now.AddSeconds(-3)),
                    Child("root-b","cb",Now.AddMinutes(-1),Now.AddSeconds(-2))));
                List<MissionView> views=tracker.At(Now);
                Assert(views.Count==2&&views.All(v=>v.State==RadarState.Delegated)&&views.All(v=>v.ActiveChildCount==1));
            });
            Check("Enfant terminé : parent silencieux retombe sur UNVERIFIED", delegate {
                MissionView m=Mission(Root("root-thread","root-file",Now.AddMinutes(-4),Now.AddMinutes(-3)),
                    Child("root-thread","child-file",Now.AddMinutes(-2),Now.AddSeconds(-120),LiveTaskPhase.Completed));
                Assert(m.State==RadarState.Unverified&&m.ActiveChildCount==0&&m.DelegatedEvidenceSeen);
            });
            Check("Enfant non lié ou parent thread ambigu ne confirme aucun parent", delegate {
                MissionTracker tracker=new MissionTracker();
                tracker.Update(Report(Now,Root("same-thread","root-a",Now.AddMinutes(-2),Now.AddMinutes(-2)),
                    Root("same-thread","root-b",Now.AddMinutes(-1),Now.AddMinutes(-1)),
                    Child("other-thread","child-file",Now.AddMinutes(-1),Now.AddSeconds(-1)),
                    new LiveTask {IsSubagent=true,SessionKey="unlinked",StartedUtc=Now.AddSeconds(-2),LastWriteUtc=Now.AddSeconds(-1),Phase=LiveTaskPhase.Active}));
                Assert(tracker.At(Now).All(v=>v.State!=RadarState.Delegated));
            });
            Check("Horodatage enfant périmé retombe sur UNVERIFIED", delegate {
                MissionView m=Mission(Root("root-thread","root-file",Now.AddMinutes(-4),Now.AddMinutes(-3)),
                    Child("root-thread","child-file",Now.AddMinutes(-4),Now.AddSeconds(-120)));
                Assert(m.State==RadarState.Unverified&&m.ActiveChildCount==0);
            });
            Check("Tracker prioritizes fresh missions while preserving historical numbers", delegate {
                MissionTracker tracker=new MissionTracker();
                LiveTask old=Root("old-root","old-file",Now.AddMinutes(-8),Now.AddMinutes(-2));
                LiveTask fresh=Root("fresh-root","fresh-file",Now.AddMinutes(-1),Now.AddSeconds(-2));
                tracker.Update(Report(Now,old,fresh));List<MissionView> views=tracker.At(Now);
                Assert(views.Count==2&&views[0].Number==2&&views[0].State==RadarState.Active&&
                    views[1].Number==1&&views[1].State==RadarState.Stale&&
                    views.Single(v=>v.Task.ThreadId=="old-root").Number==1&&
                    views.Single(v=>v.Task.ThreadId=="fresh-root").Number==2);
            });
            Check("Terminal enfant ne produit ni événement ni replay de notification racine", delegate {
                LiveTask child=Child("root-thread","child-file",Now.AddMinutes(-2),Now.AddSeconds(-2),LiveTaskPhase.Completed);
                child.FinishedUtc=Now.AddSeconds(-2);child.IsRootConfirmed=false;
                LiveTaskReport previous=Report(Now.AddSeconds(-10));
                LiveTaskReport current=Report(Now,child);
                Assert(TaskNotificationRules.NewTerminals(previous,current).Count==0&&
                    TaskNotificationRules.NewTerminals(current,Report(Now.AddSeconds(40),child)).Count==0);
            });
            Check("Terminal reconstruit après récupération partielle ne rejoue aucune notification", delegate {
                LiveTask previous=Root("root","session",Now.AddSeconds(-8),Now.AddSeconds(-7));previous.IdentityUncertain=true;previous.Phase=LiveTaskPhase.Unconfirmed;previous.FinishedUtc=null;
                previous.SessionKey="rollout";LiveTask terminal=previous.Copy();terminal.IdentityUncertain=false;terminal.IsRootConfirmed=true;
                terminal.Phase=LiveTaskPhase.Completed;terminal.FinishedUtc=Now.AddSeconds(-1);terminal.LastWriteUtc=Now.AddSeconds(-1);
                LiveTaskReport before=Report(Now.AddSeconds(-2),previous),after=Report(Now,terminal);
                Assert(TaskNotificationRules.NewTerminals(before,after).Count==0);
            });
            Check("Reprise bornée récupère un terminal avant plus de 512 Kio de lignes non pertinentes", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-fixture-"+Guid.NewGuid().ToString("N"));
                string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);
                    string dir=System.IO.Path.Combine(home,"sessions","2026","10","19");System.IO.Directory.CreateDirectory(dir);
                    string path=System.IO.Path.Combine(dir,"rollout-recovery.jsonl");
                    using(System.IO.StreamWriter writer=new System.IO.StreamWriter(path)) {
                        writer.WriteLine("{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-root\",\"thread_source\":\"user\"}}");
                        writer.WriteLine(Event("task_started","large-root",650));
                        writer.WriteLine(Event("task_complete","large-root",12));
                        string filler="{\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"content\":\""+new string('x',900)+"\"}}";
                        for(int i=0;i<11000;i++)writer.WriteLine(filler);
                    }
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-2).UtcDateTime);
                    LiveTaskReader.ClearCacheForTests();LiveTaskReport data=null;long totalBytes=0,totalMs=0;int cycles=0;
                    do {data=LiveTaskReader.Read(Now);cycles++;totalBytes+=data.RecoveryBytesRead;totalMs+=data.RecoveryDurationMs;}
                    while(data.RecoveryLimitedSessions>0&&cycles<8);
                    LiveTask recovered=data.Tasks.FirstOrDefault(t=>t.IsRootConfirmed);
                    Assert(recovered!=null&&recovered.Phase==LiveTaskPhase.Completed&&totalBytes>9*1024*1024&&data.RecoveryLimitedSessions==0);
                    report.AppendLine("Synthetic recovery measurement: cycles="+cycles.ToString(CultureInfo.InvariantCulture)+"; bytes="+totalBytes.ToString(CultureInfo.InvariantCulture)+
                        "; elapsed_ms="+totalMs.ToString(CultureInfo.InvariantCulture)+"; limited="+
                        data.RecoveryLimitedSessions.ToString(CultureInfo.InvariantCulture));
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Reprise progressive atteint un démarrage placé à 18,88 Mio", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-progressive-fixture-"+Guid.NewGuid().ToString("N"));
                string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);
                    string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));
                    System.IO.Directory.CreateDirectory(dir);string path=System.IO.Path.Combine(dir,"rollout-progressive.jsonl");
                    byte[] header=Encoding.UTF8.GetBytes("{\"type\":\"session_meta\",\"payload\":{\"id\":\"private-thread\",\"thread_source\":\"user\"}}\n");
                    byte[] filler=Encoding.UTF8.GetBytes("{\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"content\":\""+new string('x',900)+"\"}}\n");
                    long eventOffset=(long)(18.88*1024*1024);
                    using(System.IO.FileStream stream=new System.IO.FileStream(path,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write)) {
                        stream.Write(header,0,header.Length);
                        while(stream.Position<eventOffset)stream.Write(filler,0,filler.Length);
                        byte[] start=Encoding.UTF8.GetBytes(Event("task_started","private-turn",2)+"\n");stream.Write(start,0,start.Length);
                        byte[] context=Encoding.UTF8.GetBytes(Context("private-turn","fixture-model","high")+"\n");stream.Write(context,0,context.Length);
                    }
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);LiveTaskReader.ClearCacheForTests();
                    LiveTaskReport data=null;long totalBytes=0;for(int cycle=0;cycle<32;cycle++){data=LiveTaskReader.Read(Now);totalBytes+=data.RecoveryBytesRead;}
                    LiveTask recovered=data.Tasks.FirstOrDefault(t=>t.IsRootConfirmed&&!t.IsSubagent&&t.Id=="private-turn");
                    Assert(recovered!=null&&recovered.Phase==LiveTaskPhase.Active&&!recovered.IdentityUncertain&&
                        data.RecoveryLimitedSessions==0&&totalBytes>eventOffset);
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Queue récente confirme un départ vérifié dans une source de 22 Mio avant la fin de l'historique", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-recent-22m-"+Guid.NewGuid().ToString("N"));string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));System.IO.Directory.CreateDirectory(dir);
                    string path=System.IO.Path.Combine(dir,"rollout-recent-22m.jsonl");byte[] header=Encoding.UTF8.GetBytes("{\"type\":\"session_meta\",\"payload\":{\"id\":\"recent-root\",\"thread_source\":\"user\"}}\n");
                    byte[] filler=Encoding.UTF8.GetBytes("{\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"content\":\""+new string('x',900)+"\"}}\n");
                    using(System.IO.FileStream stream=new System.IO.FileStream(path,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write)){stream.Write(header,0,header.Length);while(stream.Position<22L*1024*1024-2048)stream.Write(filler,0,filler.Length);byte[] recent=Encoding.UTF8.GetBytes(Event("task_started","recent-turn",2)+"\n"+Context("recent-turn","fixture","high")+"\n");stream.Write(recent,0,recent.Length);}
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);LiveTaskReader.ClearCacheForTests();LiveTaskReport sample=LiveTaskReader.Read(Now);
                    LiveTask found=sample.Tasks.FirstOrDefault(t=>t.Id=="recent-turn");
                    Assert(sample.RecoveryLimitedSessions>0&&found!=null&&found.Phase==LiveTaskPhase.Active&&!found.IdentityUncertain,"recent activity hidden during incomplete prefix recovery");
                    Assert(sample.RecentTailBytesRead<=524288,"recent tail exceeded source budget");
                    report.AppendLine("Synthetic 22 MiB: history_bytes="+sample.RecoveryBytesRead.ToString(CultureInfo.InvariantCulture)+"; tail_bytes="+sample.RecentTailBytesRead.ToString(CultureInfo.InvariantCulture)+"; elapsed_ms="+sample.RecoveryDurationMs.ToString(CultureInfo.InvariantCulture));
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Queue récente traverse le plafond historique de 64 Mio sans promouvoir la croissance", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-recent-cap-"+Guid.NewGuid().ToString("N"));string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));System.IO.Directory.CreateDirectory(dir);
                    string path=System.IO.Path.Combine(dir,"rollout-recent-cap.jsonl");byte[] header=Encoding.UTF8.GetBytes("{\"type\":\"session_meta\",\"payload\":{\"id\":\"cap-root\",\"thread_source\":\"user\"}}\n");byte[] filler=Encoding.UTF8.GetBytes("{\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"content\":\""+new string('x',900)+"\"}}\n");
                    using(System.IO.FileStream stream=new System.IO.FileStream(path,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write)){stream.Write(header,0,header.Length);while(stream.Position<65L*1024*1024)stream.Write(filler,0,filler.Length);}
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-5).UtcDateTime);LiveTaskReader.ClearCacheForTests();LiveTaskReport sample=null;
                    for(int i=0;i<33;i++)sample=LiveTaskReader.Read(Now);
                    Assert(sample.RecoveryLimitedSessions>0&&sample.RecoveryProgressBytes>=LiveTaskReader.RecoveryMaxBytesPerSource,"did not reach 64 MiB historical cap");
                    using(System.IO.StreamWriter writer=new System.IO.StreamWriter(path,true)){writer.WriteLine(Event("task_started","stale-after-cap",180));}
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);sample=LiveTaskReader.Read(Now);
                    Assert(!sample.Tasks.Any(t=>t.IsRootConfirmed&&!t.IsSubagent&&t.Phase==LiveTaskPhase.Active),"file growth promoted stale evidence");
                    using(System.IO.StreamWriter writer=new System.IO.StreamWriter(path,true)){writer.WriteLine(Event("task_started","fresh-after-cap",2));writer.WriteLine(Context("fresh-after-cap","fixture","high"));}
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);sample=LiveTaskReader.Read(Now);
                    Assert(sample.Tasks.Any(t=>t.Id=="fresh-after-cap"&&t.Phase==LiveTaskPhase.Active&&!t.IdentityUncertain),"new activity starved after source cap");
                    Assert(sample.RecentTailBytesRead<=524288,"recent tail exceeded source budget");
                    using(System.IO.StreamWriter writer=new System.IO.StreamWriter(path,true)){writer.WriteLine(Event("task_complete","fresh-after-cap",1));}
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);sample=LiveTaskReader.Read(Now);
                    Assert(sample.Tasks.Any(t=>t.Id=="fresh-after-cap"&&t.Phase==LiveTaskPhase.Completed&&!t.IdentityUncertain),"fresh parent terminal event was hidden by incomplete history");
                    report.AppendLine("Synthetic >64 MiB: history_progress="+sample.RecoveryProgressBytes.ToString(CultureInfo.InvariantCulture)+"; poll_history_bytes="+sample.RecoveryBytesRead.ToString(CultureInfo.InvariantCulture)+"; tail_bytes="+sample.RecentTailBytesRead.ToString(CultureInfo.InvariantCulture)+"; elapsed_ms="+sample.RecoveryDurationMs.ToString(CultureInfo.InvariantCulture));
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Frontière UTF-8 partielle reprend la ligne et conserve une preuve de queue complète", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-utf8-boundary-"+Guid.NewGuid().ToString("N"));string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));System.IO.Directory.CreateDirectory(dir);
                    string path=System.IO.Path.Combine(dir,"rollout-utf8.jsonl");byte[] header=Encoding.UTF8.GetBytes("{\"type\":\"session_meta\",\"payload\":{\"id\":\"utf8-root\",\"thread_source\":\"user\"}}\n");
                    using(System.IO.FileStream stream=new System.IO.FileStream(path,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write)) {
                        stream.Write(header,0,header.Length);
                        while(stream.Position<700000)stream.WriteByte((byte)' ');
                        stream.WriteByte(10);
                        while(stream.Position<1400000)stream.WriteByte((byte)' ');
                        stream.WriteByte(10);
                        byte[] prefix=Encoding.UTF8.GetBytes("{\"type\":\"response_item\",\"payload\":{\"content\":\"");stream.Write(prefix,0,prefix.Length);
                        while(stream.Position<2097151)stream.WriteByte((byte)'x');
                        byte[] emoji=Encoding.UTF8.GetBytes("🙂\"}}\n");stream.Write(emoji,0,emoji.Length);
                        byte[] start=Encoding.UTF8.GetBytes(Event("task_started","utf8-turn",2)+"\n");stream.Write(start,0,start.Length);
                    }
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);LiveTaskReader.ClearCacheForTests();
                    LiveTaskReport first=LiveTaskReader.Read(Now);LiveTask early=first.Tasks.FirstOrDefault(t=>t.Id=="utf8-turn");
                    Assert(early!=null&&early.Phase==LiveTaskPhase.Active&&!early.IdentityUncertain,"complete recent tail record was hidden by historical scan progress");
                    LiveTaskReport second=LiveTaskReader.Read(Now);LiveTask recovered=second.Tasks.FirstOrDefault(t=>t.Id=="utf8-turn");
                    Assert(recovered!=null&&recovered.Phase==LiveTaskPhase.Active&&!recovered.IdentityUncertain,"UTF-8 record was lost at the slice boundary");
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Remplacement ou troncature invalide le checkpoint et relit le nouvel en-tête", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-replace-checkpoint-"+Guid.NewGuid().ToString("N"));string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));System.IO.Directory.CreateDirectory(dir);
                    string path=System.IO.Path.Combine(dir,"rollout-replaced.jsonl");byte[] header=Encoding.UTF8.GetBytes("{\"type\":\"session_meta\",\"payload\":{\"id\":\"old-root\",\"thread_source\":\"user\"}}\n");
                    using(System.IO.FileStream stream=new System.IO.FileStream(path,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write)) {
                        stream.Write(header,0,header.Length);byte[] filler=Encoding.UTF8.GetBytes("{\"type\":\"response_item\",\"payload\":{\"content\":\""+new string('x',900)+"\"}}\n");
                        while(stream.Position<3*1024*1024)stream.Write(filler,0,filler.Length);
                    }
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);LiveTaskReader.ClearCacheForTests();LiveTaskReader.Read(Now);
                    System.IO.File.Delete(path);System.IO.File.WriteAllLines(path,new[]{"{\"type\":\"session_meta\",\"payload\":{\"id\":\"new-root\",\"thread_source\":\"user\"}}",Event("task_started","replacement-turn",2)});
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);LiveTaskReport replaced=LiveTaskReader.Read(Now);
                    Assert(replaced.Tasks.Any(t=>t.Id=="replacement-turn"&&t.IsRootConfirmed&&t.Phase==LiveTaskPhase.Active),"Replacement inherited the old source checkpoint/header");
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Reprise équitable de deux gros JSONL survit à la croissance", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-fair-fixture-"+Guid.NewGuid().ToString("N"));string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));System.IO.Directory.CreateDirectory(dir);
                    byte[] filler=Encoding.UTF8.GetBytes("{\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"content\":\""+new string('x',900)+"\"}}\n");
                    foreach(string id in new[]{"fair-a","fair-b","fair-c"}) {
                        string path=System.IO.Path.Combine(dir,"rollout-"+id+".jsonl");
                        using(System.IO.FileStream stream=new System.IO.FileStream(path,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write)) {
                            byte[] header=Encoding.UTF8.GetBytes("{\"type\":\"session_meta\",\"payload\":{\"id\":\""+id+"\",\"thread_source\":\"user\"}}\n");stream.Write(header,0,header.Length);
                            while(stream.Position<5*1024*1024)stream.Write(filler,0,filler.Length);
                        }
                        System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);
                    }
                    LiveTaskReader.ClearCacheForTests();LiveTaskReport reportNow=LiveTaskReader.Read(Now);long totalBytes=reportNow.RecoveryBytesRead;
                    Assert(reportNow.Tasks.Count==0,"No start event should be invented");
                    foreach(string id in new[]{"fair-a","fair-b","fair-c"}) {
                        string path=System.IO.Path.Combine(dir,"rollout-"+id+".jsonl");
                        using(System.IO.StreamWriter writer=new System.IO.StreamWriter(path,true)){writer.WriteLine(Event("task_started",id+"-turn",2));writer.WriteLine(Context(id+"-turn","fixture-model","high"));}
                        System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);
                    }
                    string childPath=System.IO.Path.Combine(dir,"rollout-fair-child.jsonl");
                    using(System.IO.StreamWriter writer=new System.IO.StreamWriter(childPath,false)){
                        writer.WriteLine("{\"type\":\"session_meta\",\"payload\":{\"id\":\"fair-child-thread\",\"parent_thread_id\":\"fair-a\",\"thread_source\":\"subagent\"}}");
                        writer.WriteLine(Event("task_started","fair-child-turn",2));writer.WriteLine(Context("fair-child-turn","fixture-model","high"));
                    }
                    System.IO.File.SetLastWriteTimeUtc(childPath,Now.AddSeconds(-1).UtcDateTime);
                    for(int cycle=0;cycle<5;cycle++){reportNow=LiveTaskReader.Read(Now);totalBytes+=reportNow.RecoveryBytesRead;}
                    MissionTracker tracker=new MissionTracker();tracker.Update(reportNow);List<MissionView> views=tracker.At(Now);
                    // A has both an identified fresh root turn AND a linked fresh child.
                    // Match the global ROOT ACTIVE selector: direct parent evidence has
                    // priority; DELEGATED is reserved for a silent/stale parent.
                    MissionView parent=views.SingleOrDefault(v=>v.Task.SessionKey=="rollout-fair-a.jsonl");
                    Assert(reportNow.Tasks.Count(t=>t.IsRootConfirmed&&!t.IsSubagent&&t.Phase==LiveTaskPhase.Active)==3&&
                        views.Count==3&&parent!=null&&parent.State==RadarState.Active&&
                        parent.ActiveChildCount==1&&totalBytes>6*1024*1024,
                        "three live roots, including a fresh root with a linked child, must remain visible as ACTIVE");
                    // The same verified parent/child relationship becomes DELEGATED when
                    // fresh direct root evidence ages out but linked child evidence remains.
                    LiveTask[] onlyChildFresh=reportNow.Tasks.Select(t=>{
                        LiveTask copy=t.Copy();
                        if(copy.IsRootConfirmed&&copy.SessionKey=="rollout-fair-a.jsonl"){
                            copy.LastWriteUtc=Now.AddMinutes(-3);copy.RecentDirectEvidence=false;
                        }
                        return copy;
                    }).ToArray();
                    MissionTracker delegatedTracker=new MissionTracker();
                    delegatedTracker.Update(Report(Now,onlyChildFresh));
                    Assert(delegatedTracker.At(Now).Single(v=>v.Task.SessionKey=="rollout-fair-a.jsonl").State==RadarState.Delegated,
                        "a silent root with fresh linked child must remain DELEGATED");
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Cache vidé puis relu à froid reconstruit l'historique sans notification ancienne", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-fixture-"+Guid.NewGuid().ToString("N"));
                string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions","2026","10","19");System.IO.Directory.CreateDirectory(dir);
                    string path=System.IO.Path.Combine(dir,"rollout-restart.jsonl");System.IO.File.WriteAllLines(path,new[]{"{\"type\":\"session_meta\",\"payload\":{\"id\":\"restart-thread\",\"thread_source\":\"user\"}}",Event("task_started","restart-turn",90),Event("task_complete","restart-turn",40)});
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);LiveTaskReader.ClearCacheForTests();LiveTaskReport first=LiveTaskReader.Read(Now.AddSeconds(-10));
                    LiveTaskReader.ClearCacheForTests();LiveTaskReport recovered=LiveTaskReader.Read(Now);LiveTask terminal=recovered.Tasks.First(t=>t.IsRootConfirmed);
                    Assert(first.Tasks.Any(t=>t.IsRootConfirmed)&&terminal.Phase==LiveTaskPhase.Completed&&TaskNotificationRules.NewTerminals(first,recovered).Count==0);
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Sources manquantes, malformed et verrouillée restent indisponibles sans faux résultat", delegate {
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-fixture-"+Guid.NewGuid().ToString("N"));string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);LiveTaskReader.ClearCacheForTests();
                    Assert(LiveTaskReader.Read(Now).Tasks.Count==0);
                    string dir=System.IO.Path.Combine(home,"sessions","2026","10","19");System.IO.Directory.CreateDirectory(dir);
                    string bad=System.IO.Path.Combine(dir,"rollout-malformed.jsonl");System.IO.File.WriteAllText(bad,"{broken header\n");
                    System.IO.File.SetLastWriteTimeUtc(bad,Now.AddSeconds(-1).UtcDateTime);
                    string lockedPath=System.IO.Path.Combine(dir,"rollout-locked.jsonl");System.IO.File.WriteAllText(lockedPath,"{}");System.IO.File.SetLastWriteTimeUtc(lockedPath,Now.AddSeconds(-1).UtcDateTime);
                    using(System.IO.FileStream held=new System.IO.FileStream(lockedPath,System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite,System.IO.FileShare.None)) {
                        LiveTaskReader.ClearCacheForTests();LiveTaskReport data=LiveTaskReader.Read(Now);Assert(data.Tasks.Count==0&&data.UnknownOrigins>0);
                    }
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
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
                Assert(diagnostic.Contains("UNVERIFIED") &&
                    diagnostic.Contains("Last primary turns (up to 6):") && diagnostic.Contains("  none"));
            });
            Check("Diagnostic : plafond racines exact à 12, 13 et 20 candidates", delegate {
                string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-root-cap-"+Guid.NewGuid().ToString("N"));
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);
                    string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));
                    System.IO.Directory.CreateDirectory(dir);
                    foreach(int count in new[]{12,13,20}) {
                        foreach(string file in System.IO.Directory.GetFiles(dir))System.IO.File.Delete(file);
                        LiveTaskReader.ClearCacheForTests();
                        for(int n=0;n<count;n++) {
                            string path=System.IO.Path.Combine(dir,"rollout-root-"+n.ToString("00",CultureInfo.InvariantCulture)+".jsonl");
                            System.IO.File.WriteAllLines(path,new[]{"{\"type\":\"session_meta\",\"payload\":{\"id\":\"root-"+n+"\",\"thread_source\":\"user\"}}",Event("task_started","root-turn-"+n,3)});
                            System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);
                        }
                        LiveTaskReport data=LiveTaskReader.Read(Now);
                        Assert(data.RootSessionCandidates==count&&data.ExaminedRoots==Math.Min(count,12)&&
                            data.RootSessionsExcludedByCap==Math.Max(0,count-12));
                    }
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Diagnostic : guardian_review lié, enfant non lié, âges et statut ROOT DELEGATED", delegate {
                string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-child-diagnostic-"+Guid.NewGuid().ToString("N"));
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));System.IO.Directory.CreateDirectory(dir);
                    string root=System.IO.Path.Combine(dir,"rollout-root.jsonl");
                    System.IO.File.WriteAllLines(root,new[]{"{\"type\":\"session_meta\",\"payload\":{\"id\":\"root-identifier-private\",\"thread_source\":\"user\"}}",Event("task_started","root-turn",180)});
                    System.IO.File.SetLastWriteTimeUtc(root,Now.AddSeconds(-180).UtcDateTime);
                    string linked=System.IO.Path.Combine(dir,"rollout-linked.jsonl");
                    System.IO.File.WriteAllLines(linked,new[]{"{\"type\":\"session_meta\",\"payload\":{\"id\":\"child-id-private\",\"parent_thread_id\":\"root-identifier-private\",\"thread_source\":\"guardian_review\"}}",Event("task_started","child-turn",2)});
                    System.IO.File.SetLastWriteTimeUtc(linked,Now.AddSeconds(-1).UtcDateTime);
                    string unlinked=System.IO.Path.Combine(dir,"rollout-unlinked.jsonl");
                    System.IO.File.WriteAllLines(unlinked,new[]{"{\"type\":\"session_meta\",\"payload\":{\"id\":\"unlinked-id-private\",\"parent_thread_id\":\"missing-parent-private\",\"thread_source\":\"guardian_review\"}}",Event("task_started","unlinked-turn",2)});
                    System.IO.File.SetLastWriteTimeUtc(unlinked,Now.AddSeconds(-1).UtcDateTime);
                    LiveTaskReader.ClearCacheForTests();LiveTaskReport data=LiveTaskReader.Read(Now);string diagnostic=data.RedactedDiagnostics(Now);
                    Assert(data.ChildSessionCandidates==2&&data.ChildSessionsLinked==1&&data.ChildSessionsUnlinked==1&&data.ActiveChildSessions==2);
                    Assert(diagnostic.Contains("ROOT DELEGATED")&&diagnostic.Contains("guardian_review")&&
                        diagnostic.Contains("Last primary evidence age (s): 180")&&diagnostic.Contains("Last child evidence age (s): 1")&&
                        !diagnostic.Contains("root-identifier-private")&&!diagnostic.Contains("child-id-private")&&
                        !diagnostic.Contains("missing-parent-private"));
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Diagnostic : raisons agrégées d'une récupération limitée restent anonymes", delegate {
                string prior=Environment.GetEnvironmentVariable("CODEX_HOME");
                string home=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"shino-recovery-reasons-"+Guid.NewGuid().ToString("N"));
                try {
                    Environment.SetEnvironmentVariable("CODEX_HOME",home);string dir=System.IO.Path.Combine(home,"sessions",Now.ToString("yyyy"),Now.ToString("MM"),Now.ToString("dd"));System.IO.Directory.CreateDirectory(dir);
                    string path=System.IO.Path.Combine(dir,"rollout-limited-private.jsonl");
                    using(System.IO.FileStream stream=new System.IO.FileStream(path,System.IO.FileMode.CreateNew,System.IO.FileAccess.Write)) {
                        byte[] header=Encoding.UTF8.GetBytes("{\"type\":\"session_meta\",\"payload\":{\"id\":\"private-session-id\",\"thread_source\":\"user\"}}\n");
                        stream.Write(header,0,header.Length);stream.SetLength(LiveTaskReader.RecoveryMaxBytes+1L);
                    }
                    System.IO.File.SetLastWriteTimeUtc(path,Now.AddSeconds(-1).UtcDateTime);LiveTaskReader.ClearCacheForTests();
                    LiveTaskReport data=null;for(int cycle=0;cycle<33;cycle++)data=LiveTaskReader.Read(Now);string diagnostic=data.RedactedDiagnostics(Now);
                    Assert(data.RecoveryLimitedSessions==1&&data.RecoveryLimitedBySize==1&&
                        data.RecoveryLimitedByInvalidLine==1&&data.RecoveryLimitedByPartialRead==1&&
                        diagnostic.Contains("size=1")&&diagnostic.Contains("invalid-line=1")&&diagnostic.Contains("partial-read=1")&&
                        !diagnostic.Contains("private-session-id")&&!diagnostic.Contains("rollout-limited-private"),
                        "recovery reasons="+data.RecoveryLimitedBySize+"/"+data.RecoveryLimitedByInvalidLine+"/"+data.RecoveryLimitedByPartialRead);
                } finally {Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();if(System.IO.Directory.Exists(home))System.IO.Directory.Delete(home,true);}
            });
            Check("Diagnostic : six derniers terminés ne masquent pas la preuve racine active", delegate {
                List<LiveTask> tasks=new List<LiveTask>();
                LiveTask active=Root("root","root-file",Now.AddHours(-1),Now.AddSeconds(-1),LiveTaskPhase.Active);tasks.Add(active);
                for(int n=0;n<6;n++) {
                    DateTimeOffset started=Now.AddMinutes(-10+n);
                    tasks.Add(new LiveTask { Id="terminal-"+n,SessionKey="fixture",ThreadId="root",IsRootConfirmed=true,
                        StartedUtc=started,LastWriteUtc=started,FinishedUtc=started.AddSeconds(5),Phase=LiveTaskPhase.Completed });
                }
                string diagnostic=Report(Now,tasks.ToArray()).RedactedDiagnostics(Now);
                Assert(diagnostic.Contains("Global mission selection: ROOT ACTIVE")&&
                    diagnostic.Contains("the six latest turns are terminal history")&&diagnostic.Contains("Last primary turns (up to 6):"));
            });
            report.Insert(0, "CODEX PERISCOPE TASKS — " + passed + "/" + total +
                " simulated tests passed\r\n");
            return report.ToString();
        }
    }
}

