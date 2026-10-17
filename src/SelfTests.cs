using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ShinoCodexMonitor
{
    internal static class SelfTests
    {
        private static int total, passed;
        private static readonly StringBuilder report = new StringBuilder();
        private static void Check(string title, Action test)
        {
            total++;
            try { test(); passed++; report.AppendLine("PASS " + title); }
            catch (Exception ex) { report.AppendLine("FAIL " + title + " : " + ex.GetType().Name + " " + ex.Message); }
        }
        private static void Assert(bool ok) { if (!ok) throw new Exception("Assertion non satisfaite"); }
        private static Snapshot Parse(string json)
        {
            object obj = Json.Serializer.DeserializeObject(json);
            return QuotaParser.Parse(obj);
        }
        private static void MustReject(string json)
        {
            bool rejected = false;
            try { Parse(json); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected);
        }
        internal static string Run()
        {
            total = 0; passed = 0; report.Length = 0;
            Check("5 h + 7 jours, used -> remaining", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"limitId\":\"codex\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":59,\"resetsAt\":1792110000},\"secondary\":{\"windowDurationMins\":10080,\"usedPercent\":13}}}");
                Assert(s.Five != null && s.Five.Remaining == 41 && s.Week != null && s.Week.Remaining == 87);
            });
            Check("Fenêtres inversées reconnues par durée", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":10080,\"usedPercent\":3},\"secondary\":{\"windowDurationMins\":300,\"usedPercent\":90}}}");
                Assert(s.Week.Remaining == 97 && s.Five.Remaining == 10);
            });
            Check("Fenêtres contradictoires bloquées", delegate {
                MustReject("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":10},\"secondary\":{\"windowDurationMins\":300,\"usedPercent\":60}}}");
            });
            Check("Fenêtre 5 h seule", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":25}}}");
                Assert(s.Five.Remaining == 75 && s.Week == null);
            });
            Check("ID codex prioritaire face aux modèles", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"limitId\":\"model\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":99}},\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":10}}}}");
                Assert(s.Five.Remaining == 90);
            });
            Check("Pas de confusions modèle vs global", delegate {
                MustReject("{\"rateLimits\":{\"limitId\":\"codex_spark\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":25}}}");
            });
            Check("Fenêtres inconnues non inventées", delegate {
                MustReject("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":60,\"usedPercent\":5}}}");
            });
            Check("Pourcentage absent rejeté", delegate {
                MustReject("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300}}}");
            });
            Check("Pourcentage hors bornes rejeté", delegate {
                MustReject("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":101}}}");
            });
            Check("Reset invalide ne devient pas une date", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":0,\"resetsAt\":\"999999999999999999\"}}}");
                Assert(s.Five.ResetUtc == null);
            });
            Check("Compte réinitialisations explicitement communiqué", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":50}},\"rateLimitResetCredits\":{\"availableCount\":2}}");
                Assert(s.ResetsAvailable.HasValue && s.ResetsAvailable.Value == 2);
            });
            Check("Compte absent distinct de zéro", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":50}}}");
                Assert(!s.ResetsAvailable.HasValue);
            });
            Check("Droit d'usage bloqué conservé", delegate {
                Snapshot s = Parse("{\"ordinaryUsageAllowed\":false,\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":0}}}");
                Assert(s.UsageAllowed == false);
            });
            Check("États jamais inventés avant une mesure", delegate {
                Assert(QuotaHealth.State(null, 0, 60, DateTimeOffset.UtcNow) == "Indisponible");
            });
            Check("Mesure récente directement affichée", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(QuotaHealth.State(now.AddSeconds(-30), 0, 60, now) == "Direct");
            });
            Check("Échec ponctuel : à vérifier", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(QuotaHealth.State(now.AddSeconds(-30), 1, 60, now) == "À vérifier");
            });
            Check("Mesure périmée passe en ancien", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(QuotaHealth.State(now.AddSeconds(-120), 0, 60, now) == "Ancienne donnée");
            });
            Check("Horloge incohérente signale une ancienne donnée", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(QuotaHealth.State(now.AddMinutes(3), 0, 60, now) == "Ancienne donnée");
            });
            Check("Backoff exponentiel borné", delegate {
                Assert(QuotaHealth.Backoff(1,30)==30 && QuotaHealth.Backoff(2,30)==60 && QuotaHealth.Backoff(8,60)==300);
            });
            Check("Préférences invalides sécurisées", delegate {
                Preferences p = Preferences.Defaults(); p.Layout = "Unknown"; p.RefreshSeconds = -1; p.Opacity = 5; p.ColorHigh = "purple"; p.Sanitize();
                Assert(p.Layout == "Vertical" && p.RefreshSeconds == 60 && p.Opacity == .96 && p.ColorHigh == "#51D7AF");
            });
            Check("Icône dynamique Windows valide", delegate {
                using (System.Drawing.Icon icon = QuotaTrayIcon.Create(75,
                    System.Drawing.Color.FromArgb(81, 215, 175), true, "Percent"))
                    Assert(icon != null && icon.Handle != IntPtr.Zero);
            });
            Check("Icône indisponible grise valide", delegate {
                using (System.Drawing.Icon icon = QuotaTrayIcon.Create(null,
                    System.Drawing.Color.Red, false, "Percent"))
                    Assert(icon != null && icon.Handle != IntPtr.Zero);
            });
            Check("Mode tray anneau seul crée une icône", delegate {
                using (System.Drawing.Icon icon = QuotaTrayIcon.Create(50,
                    System.Drawing.Color.Gold, true, "Ring"))
                    Assert(icon != null && icon.Handle != IntPtr.Zero);
            });
            Check("Mode tray pourcentage crée une icône", delegate {
                using (System.Drawing.Icon icon = QuotaTrayIcon.Create(50,
                    System.Drawing.Color.Gold, true, "Percent"))
                    Assert(icon != null && icon.Handle != IntPtr.Zero);
            });
            Check("Préférence tray inconnue revient au symbole pourcentage", delegate {
                Preferences p = Preferences.Defaults();
                Assert(p.TrayStyle == "Percent");
                p.TrayStyle = "invalide"; p.Sanitize();
                Assert(p.TrayStyle == "Percent");
                p.TrayStyle = "Ring"; p.Sanitize();
                Assert(p.TrayStyle == "Ring");
            });
            Check("Préférences V4.2 par défaut compatibles widget V4.1", delegate {
                Preferences p = Preferences.Defaults();
                Assert(p.Pinned && p.Alerts && p.Alert25 && p.Alert10 && !p.Alert50 && !p.AlertResets);
            });
            Check("Seuil 25 % détecté avec même reset", delegate {
                DateTimeOffset reset = DateTimeOffset.UtcNow.AddHours(2);
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 26, ResetUtc = reset };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 24, ResetUtc = reset };
                Assert(AlertRules.CrossedThreshold(a, b, false, true, true) == 25);
            });
            Check("Alerte 50 % désactivée", delegate {
                DateTimeOffset reset = DateTimeOffset.UtcNow.AddHours(2);
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 55, ResetUtc = reset };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 48, ResetUtc = reset };
                Assert(!AlertRules.CrossedThreshold(a, b, false, true, true).HasValue);
            });
            Check("Une seule alerte quand plusieurs paliers traversés", delegate {
                DateTimeOffset reset = DateTimeOffset.UtcNow.AddHours(2);
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 60, ResetUtc = reset };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 8, ResetUtc = reset };
                Assert(AlertRules.CrossedThreshold(a, b, true, true, true) == 10);
            });
            Check("Alerte seuil interdite entre fenêtres différentes", delegate {
                DateTimeOffset reset = DateTimeOffset.UtcNow.AddHours(2);
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 55, ResetUtc = reset };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 3, ResetUtc = reset.AddHours(5) };
                Assert(!AlertRules.CrossedThreshold(a, b, true, true, true).HasValue);
            });
            Check("Réinitialisation réellement observée", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 3, ResetUtc = now.AddMinutes(-1) };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 95, ResetUtc = now.AddHours(5) };
                Assert(AlertRules.ConfirmedReset(a, b, now));
            });
            Check("Pas de fausse réinitialisation avec compteur seul", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 3, ResetUtc = now.AddMinutes(-1) };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 3, ResetUtc = now.AddHours(5) };
                Assert(!AlertRules.ConfirmedReset(a, b, now));
            });
            Check("Pas de reset si absence d'horodatages", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 1 };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 99 };
                Assert(!AlertRules.ConfirmedReset(a, b, now));
            });
            Check("Garde reprise : 1 seconde normale", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(!WakeGuard.Gap(now.AddSeconds(-1), now));
            });
            Check("Garde reprise : longue veille invalide le cache", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(WakeGuard.Gap(now.AddMinutes(-12), now));
            });
            Check("Garde reprise : décalage horloge arrière", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(WakeGuard.Gap(now.AddSeconds(8), now));
            });
            Check("Garde reprise : lectures antérieures rejetées", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(!WakeGuard.Accept(1, 2, now, now.AddSeconds(-1)));
                Assert(!WakeGuard.Accept(2, 2, now.AddSeconds(-5), now));
                Assert(WakeGuard.Accept(2, 2, now, now));
            });
            Check("Télémétrie locale sans dépendance Codex", delegate {
                RuntimeMetrics perf = new RuntimeMetrics();
                perf.Sample(DateTimeOffset.UtcNow);
                Assert(perf.Summary().Contains("RAM courante"));
            });
            Check("GitHub Release stable parsée", delegate {
                PublicRelease r = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.2\",\"draft\":false,\"prerelease\":false}");
                Assert(r.Number.Equals(new Version(4, 5, 2)) && r.PageUrl == ReleaseUpdater.RepoUrl + "/releases/tag/v4.5.2");
            });
            Check("Nouvelle version détectée", delegate {
                PublicRelease r = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.1\"}");
                Assert(ReleaseUpdater.IsNewer(r, new Version(4, 5, 0, 0)));
            });
            Check("Version identique non proposée", delegate {
                PublicRelease r = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.0\"}");
                Assert(!ReleaseUpdater.IsNewer(r, new Version(4, 5, 0, 0)));
            });
            Check("Version publique précédente non proposée", delegate {
                PublicRelease r = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.4.0\"}");
                Assert(!ReleaseUpdater.IsNewer(r, new Version(4, 5, 0, 0)));
            });
            Check("Tag trompeur interdit", delegate {
                Assert(ReleaseUpdater.ParseTag("v4.5.1-beta") == null && ReleaseUpdater.ParseTag("../../other") == null);
            });
            Check("Release brouillon rejetée", delegate {
                bool failed = false;
                try { ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.3\",\"draft\":true}"); }
                catch (InvalidDataException) { failed = true; }
                Assert(failed);
            });
            Check("Release prerelease rejetée", delegate {
                bool failed = false;
                try { ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.3\",\"prerelease\":true}"); }
                catch (InvalidDataException) { failed = true; }
                Assert(failed);
            });
            report.Insert(0, "SHINO // CODEX V4.5.1 — " + passed + "/" + total + " autotests réussis\r\n\r\n");
            return report.ToString();
        }
    }
}
