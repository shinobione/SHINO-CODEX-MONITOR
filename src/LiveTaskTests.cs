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
                Assert(x.Model == "Non communiqué" && x.Effort == "Non communiqué");
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
            report.Insert(0, "SHINO // LIVE TASKS — " + passed + "/" + total +
                " tests simulés réussis\r\n");
            return report.ToString();
        }
    }
}
