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
        private static void Assert(bool ok) { if (!ok) throw new Exception("Assertion failed"); }
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
            Check("Quota and update check 01", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"limitId\":\"codex\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":59,\"resetsAt\":1792110000},\"secondary\":{\"windowDurationMins\":10080,\"usedPercent\":13}}}");
                Assert(s.Five != null && s.Five.Remaining == 41 && s.Week != null && s.Week.Remaining == 87);
            });
            Check("Quota and update check 02", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":10080,\"usedPercent\":3},\"secondary\":{\"windowDurationMins\":300,\"usedPercent\":90}}}");
                Assert(s.Week.Remaining == 97 && s.Five.Remaining == 10);
            });
            Check("Quota and update check 03", delegate {
                MustReject("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":10},\"secondary\":{\"windowDurationMins\":300,\"usedPercent\":60}}}");
            });
            Check("Quota and update check 04", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":25}}}");
                Assert(s.Five.Remaining == 75 && s.Week == null);
            });
            Check("Quota and update check 05", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"limitId\":\"model\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":99}},\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":10}}}}");
                Assert(s.Five.Remaining == 90);
            });
            Check("Quota and update check 06", delegate {
                MustReject("{\"rateLimits\":{\"limitId\":\"codex_spark\",\"primary\":{\"windowDurationMins\":300,\"usedPercent\":25}}}");
            });
            Check("Quota and update check 07", delegate {
                MustReject("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":60,\"usedPercent\":5}}}");
            });
            Check("Quota and update check 08", delegate {
                MustReject("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300}}}");
            });
            Check("Quota and update check 09", delegate {
                MustReject("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":101}}}");
            });
            Check("Quota and update check 10", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":0,\"resetsAt\":\"999999999999999999\"}}}");
                Assert(s.Five.ResetUtc == null);
            });
            Check("Quota and update check 11", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":50}},\"rateLimitResetCredits\":{\"availableCount\":2}}");
                Assert(s.ResetsAvailable.HasValue && s.ResetsAvailable.Value == 2);
            });
            Check("Quota and update check 12", delegate {
                Snapshot s = Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":50}}}");
                Assert(!s.ResetsAvailable.HasValue);
            });
            Check("Quota and update check 13", delegate {
                Snapshot s = Parse("{\"ordinaryUsageAllowed\":false,\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":0}}}");
                Assert(s.UsageAllowed == false);
            });
            Check("Quota and update check 14", delegate {
                Assert(QuotaHealth.State(null, 0, 60, DateTimeOffset.UtcNow) == "Unavailable");
            });
            Check("Quota and update check 15", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(QuotaHealth.State(now.AddSeconds(-30), 0, 60, now) == "Direct");
            });
            Check("Quota and update check 16", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(QuotaHealth.State(now.AddSeconds(-30), 1, 60, now) == "UNVERIFIED");
            });
            Check("Quota and update check 17", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(QuotaHealth.State(now.AddSeconds(-120), 0, 60, now) == "Stale data");
            });
            Check("Quota and update check 18", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(QuotaHealth.State(now.AddMinutes(3), 0, 60, now) == "Stale data");
            });
            Check("Quota and update check 19", delegate {
                Assert(QuotaHealth.Backoff(1,30)==30 && QuotaHealth.Backoff(2,30)==60 && QuotaHealth.Backoff(8,60)==300);
            });
            Check("Quota and update check 20", delegate {
                Preferences p = Preferences.Defaults(); p.Layout = "Unknown"; p.RefreshSeconds = -1; p.Opacity = 5; p.ColorHigh = "purple"; p.Sanitize();
                Assert(p.Layout == "Vertical" && p.RefreshSeconds == 60 && p.Opacity == .96 && p.ColorHigh == "#51D7AF");
            });
            Check("Quota and update check 21", delegate {
                using (System.Drawing.Icon icon = QuotaTrayIcon.Create(75,
                    System.Drawing.Color.FromArgb(81, 215, 175), true, "Percent"))
                    Assert(icon != null && icon.Handle != IntPtr.Zero);
            });
            Check("Quota and update check 22", delegate {
                using (System.Drawing.Icon icon = QuotaTrayIcon.Create(null,
                    System.Drawing.Color.Red, false, "Percent"))
                    Assert(icon != null && icon.Handle != IntPtr.Zero);
            });
            Check("Quota and update check 23", delegate {
                using (System.Drawing.Icon icon = QuotaTrayIcon.Create(50,
                    System.Drawing.Color.Gold, true, "Ring"))
                    Assert(icon != null && icon.Handle != IntPtr.Zero);
            });
            Check("Quota and update check 24", delegate {
                using (System.Drawing.Icon icon = QuotaTrayIcon.Create(50,
                    System.Drawing.Color.Gold, true, "Percent"))
                    Assert(icon != null && icon.Handle != IntPtr.Zero);
            });
            Check("Quota and update check 25", delegate {
                Preferences p = Preferences.Defaults();
                Assert(p.TrayStyle == "Percent");
                p.TrayStyle = "invalide"; p.Sanitize();
                Assert(p.TrayStyle == "Percent");
                p.TrayStyle = "Ring"; p.Sanitize();
                Assert(p.TrayStyle == "Ring");
            });
            Check("Quota and update check 26", delegate {
                Preferences p = Preferences.Defaults();
                Assert(p.Pinned && p.Alerts && p.Alert25 && p.Alert10 && !p.Alert50 && !p.AlertResets);
            });
            Check("Quota and update check 27", delegate {
                DateTimeOffset reset = DateTimeOffset.UtcNow.AddHours(2);
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 26, ResetUtc = reset };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 24, ResetUtc = reset };
                Assert(AlertRules.CrossedThreshold(a, b, false, true, true) == 25);
            });
            Check("Quota and update check 28", delegate {
                DateTimeOffset reset = DateTimeOffset.UtcNow.AddHours(2);
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 55, ResetUtc = reset };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 48, ResetUtc = reset };
                Assert(!AlertRules.CrossedThreshold(a, b, false, true, true).HasValue);
            });
            Check("Quota and update check 29", delegate {
                DateTimeOffset reset = DateTimeOffset.UtcNow.AddHours(2);
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 60, ResetUtc = reset };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 8, ResetUtc = reset };
                Assert(AlertRules.CrossedThreshold(a, b, true, true, true) == 10);
            });
            Check("Quota and update check 30", delegate {
                DateTimeOffset reset = DateTimeOffset.UtcNow.AddHours(2);
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 55, ResetUtc = reset };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 3, ResetUtc = reset.AddHours(5) };
                Assert(!AlertRules.CrossedThreshold(a, b, true, true, true).HasValue);
            });
            Check("Quota and update check 31", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 3, ResetUtc = now.AddMinutes(-1) };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 95, ResetUtc = now.AddHours(5) };
                Assert(AlertRules.ConfirmedReset(a, b, now));
            });
            Check("Quota and update check 32", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 3, ResetUtc = now.AddMinutes(-1) };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 3, ResetUtc = now.AddHours(5) };
                Assert(!AlertRules.ConfirmedReset(a, b, now));
            });
            Check("Quota and update check 33", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow a = new QuotaWindow { Minutes = 300, Remaining = 1 };
                QuotaWindow b = new QuotaWindow { Minutes = 300, Remaining = 99 };
                Assert(!AlertRules.ConfirmedReset(a, b, now));
            });
            Check("Quota and update check 34", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(!WakeGuard.Gap(now.AddSeconds(-1), now));
            });
            Check("Quota and update check 35", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(WakeGuard.Gap(now.AddMinutes(-12), now));
            });
            Check("Quota and update check 36", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(WakeGuard.Gap(now.AddSeconds(8), now));
            });
            Check("Quota and update check 37", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(!WakeGuard.Accept(1, 2, now, now.AddSeconds(-1)));
                Assert(!WakeGuard.Accept(2, 2, now.AddSeconds(-5), now));
                Assert(WakeGuard.Accept(2, 2, now, now));
            });
            Check("Quota and update check 38", delegate {
                RuntimeMetrics perf = new RuntimeMetrics();
                perf.Sample(DateTimeOffset.UtcNow);
                Assert(perf.Summary().Contains("RAM courante"));
            });
            Check("Quota and update check 39", delegate {
                PublicRelease r = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.2\",\"draft\":false,\"prerelease\":false}");
                Assert(r.Number.Equals(new Version(4, 5, 2)) && r.PageUrl == ReleaseUpdater.RepoUrl + "/releases/tag/v4.5.2");
            });
            Check("Quota and update check 40", delegate {
                PublicRelease r = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.1\"}");
                Assert(ReleaseUpdater.IsNewer(r, new Version(4, 5, 0, 0)));
            });
            Check("Quota and update check 41", delegate {
                PublicRelease r = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.0\"}");
                Assert(!ReleaseUpdater.IsNewer(r, new Version(4, 5, 0, 0)));
            });
            Check("Quota and update check 42", delegate {
                PublicRelease r = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.4.0\"}");
                Assert(!ReleaseUpdater.IsNewer(r, new Version(4, 5, 0, 0)));
            });
            Check("Quota and update check 43", delegate {
                Assert(ReleaseUpdater.ParseTag("v4.5.1-beta") == null && ReleaseUpdater.ParseTag("../../other") == null);
            });
            Check("Quota and update check 44", delegate {
                bool failed = false;
                try { ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.3\",\"draft\":true}"); }
                catch (InvalidDataException) { failed = true; }
                Assert(failed);
            });
            Check("Quota and update check 45", delegate {
                bool failed = false;
                try { ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.5.3\",\"prerelease\":true}"); }
                catch (InvalidDataException) { failed = true; }
                Assert(failed);
            });
            Check("Quota and update check 46", delegate {
                PublicRelease rel = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.6.0\"}");
                string digest = new string('a', 64);
                ReleaseAsset a = ReleasePackage.SelectAsset(rel, "{\"assets\":[{\"name\":\"SHINO_CODEX_MONITOR_V4_6_WINDOWS.zip\",\"size\":1234,\"digest\":\"sha256:" + digest + "\"}]}");
                Assert(a.Name == "SHINO_CODEX_MONITOR_V4_6_WINDOWS.zip" && a.Size == 1234 && a.Sha256 == digest && a.Url.StartsWith(ReleaseUpdater.RepoUrl));
            });
            Check("Quota and update check 47", delegate {
                bool rejected = false;
                try { ReleasePackage.SelectAsset(ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.6.0\"}"), "{\"assets\":[{\"name\":\"SHINO_CODEX_MONITOR_V4_6_WINDOWS.zip\",\"size\":1234}]}"); }
                catch (InvalidDataException) { rejected = true; }
                Assert(rejected);
            });
            Check("Quota and update check 48", delegate {
                bool rejected = false;
                string a = "{\"name\":\"SHINO_CODEX_MONITOR_V4_6_WINDOWS.zip\",\"size\":123,\"digest\":\"sha256:" + new string('a',64) + "\"}";
                try { ReleasePackage.SelectAsset(ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.6.0\"}"), "{\"assets\":[" + a + "," + a + "]}"); }
                catch (InvalidDataException) { rejected = true; }
                Assert(rejected);
            });
            Check("Quota and update check 49", delegate {
                bool rejected = false;
                try { ReleasePackage.SelectAsset(ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.6.0\"}"),
                   "{\"assets\":[{\"name\":\"SHINO_CODEX_MONITOR_V4_6_WINDOWS.zip\",\"size\":9999999999,\"digest\":\"sha256:" + new string('a',64) + "\"}]}"); }
                catch (InvalidDataException) { rejected = true; }
                Assert(rejected);
            });
            Check("Quota and update check 50", delegate {
                bool blocked1 = false, blocked2 = false;
                try { ReleasePackage.ValidateRedirect("http://github.com/a"); } catch (InvalidDataException) { blocked1 = true; }
                try { ReleasePackage.ValidateRedirect("https://evil.example.com/payload"); } catch (InvalidDataException) { blocked2 = true; }
                Assert(blocked1 && blocked2);
            });
            Check("Quota and update check 51", delegate {
                Assert(ReleasePackage.ValidateRedirect("https://release-assets.githubusercontent.com/file") == "https://release-assets.githubusercontent.com/file");
            });
            Check("Quota and update check 52", delegate {
                Assert(ReleasePackage.AssetFile(new Version(4,6,0)) == "SHINO_CODEX_MONITOR_V4_6_WINDOWS.zip" &&
                    ReleasePackage.InstallerFile(new Version(4,6,0)) == "INSTALLER-V4.6.ps1");
            });
            Check("Quota and update check 53", delegate {
                string tmp = Path.GetTempFileName();
                try {
                    File.WriteAllText(tmp, "bad");
                    bool badLength = false, badHash = false;
                    ReleaseAsset a = new ReleaseAsset { Size = 8, Sha256 = new string('0',64) };
                    try { ReleasePackage.VerifyAssetFile(a, tmp); } catch (InvalidDataException) { badLength = true; }
                    a.Size = 3;
                    try { ReleasePackage.VerifyAssetFile(a, tmp); } catch (InvalidDataException) { badHash = true; }
                    Assert(badLength && badHash);
                } finally { File.Delete(tmp); }
            });
            Check("Quota and update check 54", delegate {
                string temp = Path.Combine(Path.GetTempPath(), "SHINO-UPDATE-TEST-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                try {
                    string zip = Path.Combine(temp, "test.zip");
                    using (System.IO.Compression.ZipArchive a = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) {
                        using (StreamWriter writer = new StreamWriter(a.CreateEntry("../SHINO-CODEX-MONITOR.exe").Open())) writer.Write("exe");
                        using (StreamWriter writer = new StreamWriter(a.CreateEntry("SHA256SUMS.txt").Open())) writer.Write("none");
                        using (StreamWriter writer = new StreamWriter(a.CreateEntry("INSTALLER-V4.6.ps1").Open())) writer.Write("none");
                        using (StreamWriter writer = new StreamWriter(a.CreateEntry("INSTALLER-V4.6.bat").Open())) writer.Write("none");
                        using (StreamWriter writer = new StreamWriter(a.CreateEntry("README-FR.txt").Open())) writer.Write("none");
                    }
                    ReleaseAsset file = new ReleaseAsset { Size = new FileInfo(zip).Length, Sha256 = ReleasePackage.HashFile(zip), Release = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.6.0\"}") };
                    bool blocked = false;
                    try { ReleasePackage.VerifyZip(file, zip, Path.Combine(temp,"extracted")); } catch (InvalidDataException) { blocked = true; }
                    Assert(blocked && !File.Exists(Path.Combine(temp, "SHINO-CODEX-MONITOR.exe")));
                } finally { Directory.Delete(temp, true); }
            });
            Check("Quota and update check 55", delegate {
                string temp = Path.Combine(Path.GetTempPath(), "SHINO-UPDATE-TEST-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                try {
                    string zip = Path.Combine(temp, "test.zip");
                    using (System.IO.Compression.ZipArchive a = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) {
                        foreach(string n in new [] { "SHINO-CODEX-MONITOR.exe", "SHA256SUMS.txt", "README-FR.txt", "SHINO-CODEX.ico", "DESINSTALLER-V4.6.ps1" })
                            using (StreamWriter writer = new StreamWriter(a.CreateEntry(n).Open())) writer.Write("bad");
                    }
                    ReleaseAsset file = new ReleaseAsset { Size = new FileInfo(zip).Length, Sha256 = ReleasePackage.HashFile(zip), Release = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.6.0\"}") };
                    bool blocked = false;
                    try { ReleasePackage.VerifyZip(file, zip, Path.Combine(temp,"extracted")); } catch (InvalidDataException) { blocked = true; }
                    Assert(blocked);
                } finally { Directory.Delete(temp, true); }
            });
            Check("Quota and update check 56", delegate {
                Preferences p = Preferences.Defaults();
                Assert(QuotaPalette.Hex(100, p) == p.Color76 && QuotaPalette.Hex(76, p) == p.Color76);
            });
            Check("Quota and update check 57", delegate {
                Preferences p = Preferences.Defaults();
                Assert(QuotaPalette.Hex(75, p) == p.Color51 && QuotaPalette.Hex(51, p) == p.Color51);
            });
            Check("Quota and update check 58", delegate {
                Preferences p = Preferences.Defaults();
                Assert(QuotaPalette.Hex(50, p) == p.Color26 && QuotaPalette.Hex(26, p) == p.Color26);
            });
            Check("Quota and update check 59", delegate {
                Preferences p = Preferences.Defaults();
                Assert(QuotaPalette.Hex(25, p) == p.Color11 && QuotaPalette.Hex(11, p) == p.Color11);
            });
            Check("Quota and update check 60", delegate {
                Preferences p = Preferences.Defaults();
                Assert(QuotaPalette.Hex(10, p) == p.Color0 && QuotaPalette.Hex(0, p) == p.Color0);
            });
            Check("Quota and update check 61", delegate {
                Preferences p = Preferences.Defaults();
                p.Color76 = null; p.Color0 = "invalid"; p.Sanitize();
                Assert(QuotaPalette.Hex(109, p) == p.Color76 && QuotaPalette.Hex(-5, p) == p.Color0);
            });
            Check("Quota and update check 62", delegate {
                Preferences p = Preferences.Defaults(); p.Layout = "Circular"; p.Sanitize();
                Assert(p.Layout == "Circular");
                p.Layout = "Micro"; p.Sanitize(); Assert(p.Layout == "Micro");
            });
            Check("Quota and update check 63", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(MonitorForm.FreshnessLabel(null, now) == "No sample yet");
                Assert(MonitorForm.FreshnessLabel(now.AddMinutes(-2), now).Contains("2 min"));
            });
            Check("Quota and update check 64", delegate {
                Assert(Preferences.IsDarkBackground("#111926") && Preferences.IsDarkBackground("#090C13") &&
                    Preferences.IsDarkBackground("#22272E") && Preferences.IsDarkBackground("#102C36"));
            });
            Check("Quota and update check 65", delegate {
                Assert(!Preferences.IsDarkBackground("#FFFFFF") && !Preferences.IsDarkBackground("#FFCC88") &&
                    !Preferences.IsDarkBackground("#invalid"));
            });
            Check("Quota and update check 66", delegate {
                Preferences p = Preferences.Defaults();
                p.BackgroundColor = null; p.Sanitize();
                Assert(p.BackgroundColor == "#111926");
            });
            Check("Quota and update check 67", delegate {
                Preferences p = Preferences.Defaults();
                p.BackgroundColor = "#152B35"; p.Sanitize();
                Assert(p.BackgroundColor == "#152B35");
            });
            Check("Quota and update check 68", delegate {
                Preferences p = Preferences.Defaults();
                p.Opacity = .70; p.Sanitize(); Assert(Math.Abs(p.Opacity - .70) < .001);
                p.Opacity = 2; p.Sanitize(); Assert(Math.Abs(p.Opacity - .96) < .001);
                Assert(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) == "5.2.0");
            });
            Check("Quota and update check 69", delegate {
                Snapshot snap = Parse("{\"ordinaryUsageAllowed\":false,\"rateLimits\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":100,\"resetsAt\":1792250000},\"secondary\":{\"windowDurationMins\":10080,\"usedPercent\":62,\"resetsAt\":1792500000}}}");
                Assert(snap.UsageAllowed == false && snap.Five.Remaining == 0 &&
                    snap.Week.Remaining == 38 && snap.Five.ResetUtc.HasValue && snap.Week.ResetUtc.HasValue);
            });
            Check("Quota and update check 70", delegate {
                DateTimeOffset now = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
                Assert(MonitorForm.ResetLabel(now.AddMinutes(94), "five", "relative", "Circular", true, now) ==
                    "Reset in 1h 34min");
            });
            Check("Quota and update check 71", delegate {
                DateTimeOffset now = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
                string label = MonitorForm.ResetLabel(now.AddDays(5), "week", "absolute", "Circular", true, now);
                Assert(label.StartsWith("Reset on Oct 22 ") && !label.Contains("blocked"));
            });
            Check("Quota and update check 72", delegate {
                DateTimeOffset now = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
                Assert(MonitorForm.ResetLabel(now.AddMinutes(90), "five", "both", "Circular", true, now).StartsWith("Reset in "));
                Assert(MonitorForm.ResetLabel(now.AddDays(5), "week", "both", "Circular", true, now).StartsWith("Reset on "));
            });
            Check("Quota and update check 73", delegate {
                DateTimeOffset now = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
                DateTimeOffset reset = now.AddMinutes(75);
                Assert(MonitorForm.ResetLabel(reset, "five", "absolute", "Micro", true, now).StartsWith("Reset at "));
                Assert(MonitorForm.ResetLabel(reset, "five", "relative", "Micro", true, now) == "Reset in 1h 15min");
            });
            Check("Quota and update check 74", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(MonitorForm.ResetLabel(now.AddHours(4), "five", "hidden", "Circular", true, now) == "");
                Assert(MonitorForm.ResetLabel(null, "week", "hidden", "Micro", false, now) == "");
            });
            Check("Quota and update check 75", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(MonitorForm.ResetLabel(null, "five", "relative", "Circular", true, now) == "Reset not provided");
                Assert(MonitorForm.ResetLabel(now.AddSeconds(-1), "five", "relative", "Circular", true, now) ==
                    "Reset expected · verifying...");
            });
            Check("Quota and update check 76", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Assert(MonitorForm.ResetLabel(now.AddHours(2), "five", "absolute", "Circular", false, now) ==
                    "Old sample · reset unverified");
                Assert(MonitorForm.ResetLabel(now.AddDays(3), "week", "both", "Vertical", true, now).Contains(" · in "));
            });
            Check("Quota and update check 77", delegate {
                PublicRelease latest = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v5.0.0\"}");
                Assert(ReleaseUpdater.IsNewer(latest, new Version(4, 7, 2, 0)));
            });
            Check("Quota and update check 78", delegate {
                PublicRelease latest = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v4.7.2\"}");
                Assert(!ReleaseUpdater.IsNewer(latest, new Version(5, 0, 0, 0)));
            });
            Check("Quota and update check 79", delegate {
                PublicRelease latest = ReleaseUpdater.ParseLatest("{\"tag_name\":\"v5.0.0\"}");
                Assert(!ReleaseUpdater.IsNewer(latest, new Version(5, 0, 0, 0)));
            });
            Check("Quota and update check 80", delegate {
                Assert(ReleasePackage.AssetFile(new Version(5, 0, 0)) == "SHINO_CODEX_MONITOR_V5_0_WINDOWS.zip");
                Assert(ReleasePackage.InstallerFile(new Version(5, 0, 0)) == "INSTALLER-V5.0.ps1");
            });
            Check("Quota and update check 81", delegate {
                Assert(DesktopGeometry.ClampWidth(200) == 470 &&
                    DesktopGeometry.ClampWidth(800) == 720 &&
                    DesktopGeometry.ClampWidth(540) == 540);
            });
            Check("Quota and update check 82", delegate {
                Assert(DesktopGeometry.HeightForWidth(540) == 278);
                Assert(DesktopGeometry.HeightForWidth(620) > 278);
                Assert(DesktopGeometry.Scale(130, 540) == 130);
            });
            Check("Quota and update check 83", delegate {
                System.Drawing.Rectangle r = DesktopGeometry.ScaleRect(85, 78, 130, 130, 620);
                Assert(r.Width > 130 && r.Height > 130 && r.X > 85);
            });
            Check("Quota and update check 84", delegate {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 1920, 1040);
                System.Drawing.Rectangle w = DesktopGeometry.Snap(
                    new System.Drawing.Rectangle(14, 13, 540, 278), work, 20);
                Assert(w.Left == 0 && w.Top == 0);
            });
            Check("Quota and update check 85", delegate {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 1920, 1040);
                System.Drawing.Rectangle w = DesktopGeometry.Snap(
                    new System.Drawing.Rectangle(1394, 752, 540, 278), work, 20);
                Assert(w.Right == 1920 && w.Bottom == 1040);
            });
            Check("Quota and update check 86", delegate {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(-1920, 0, 1920, 1080);
                System.Drawing.Rectangle w = DesktopGeometry.Snap(
                    new System.Drawing.Rectangle(-1910, 150, 540, 278), work, 20);
                Assert(w.Left == -1920 && w.Top == 150);
            });
            Check("Quota and update check 87", delegate {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 1920, 1080);
                System.Drawing.Rectangle r = new System.Drawing.Rectangle(410, 260, 540, 278);
                Assert(DesktopGeometry.Snap(r, work, 20) == r);
            });
            Check("Quota and update check 88", delegate {
                Preferences p = Preferences.Defaults();
                Assert(p.CircularWidth == 540 && !p.SnapToEdges && p.AlertFive && p.AlertWeek);
                p.CircularWidth = -42; p.Sanitize();
                Assert(p.CircularWidth == 540);
                p.CircularWidth = 700; p.AlertWeek = false; p.SnapToEdges = true;
                p.Sanitize();
                Assert(p.CircularWidth == 700 && !p.AlertWeek && p.SnapToEdges);
            });
            Check("Quota and update check 89", delegate {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 1920, 1040);
                System.Drawing.Rectangle old = new System.Drawing.Rectangle(3100, 400, 540, 278);
                System.Drawing.Rectangle result = DesktopGeometry.ClampToScreen(old, work);
                Assert(result.Right == work.Right && result.Top == 400);
            });
            Check("Quota and update check 90", delegate {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(-1920, 0, 1920, 1040);
                System.Drawing.Rectangle old = new System.Drawing.Rectangle(-1800, 80, 540, 278);
                Assert(DesktopGeometry.ClampToScreen(old, work).Location == old.Location);
            });
            Check("Quota and update check 91", delegate {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(1920, -200, 1200, 930);
                System.Drawing.Rectangle old = new System.Drawing.Rectangle(2200, 900, 355, 257);
                System.Drawing.Rectangle result = DesktopGeometry.ClampToScreen(old, work);
                Assert(result.Bottom == work.Bottom && result.Left == 2200);
            });
            Check("Quota and update check 92", delegate {
                System.Drawing.Rectangle work = new System.Drawing.Rectangle(0, 0, 900, 600);
                System.Drawing.Rectangle old = new System.Drawing.Rectangle(-5000, -3000, 540, 278);
                System.Drawing.Rectangle result = DesktopGeometry.ClampToScreen(old, work);
                Assert(work.Contains(result) && result.Width == 540 && result.Height == 278);
            });
            Check("Quota and update check 93", delegate {
                Assert(SourceHealth.WindowLine("5 heures", null, true, DateTimeOffset.UtcNow) ==
                    "5 heures : Fenêtre non communiquée");
            });
            Check("Quota and update check 94", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow w = new QuotaWindow { Minutes = 300, Remaining = 37, ResetUtc = now.AddHours(2) };
                string text = SourceHealth.WindowLine("5 heures", w, false, now);
                Assert(text.Contains("ancienne donnée") && text.Contains("reset non vérifié") &&
                    !text.Contains("dd/MM"));
            });
            Check("Quota and update check 95", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow w = new QuotaWindow { Minutes = 300, Remaining = 25, ResetUtc = now.AddHours(2) };
                string text = SourceHealth.WindowLine("5 heures", w, true, now);
                Assert(text.Contains("25 % restants") && text.Contains("Reset ") &&
                    !text.Contains("non vérifié"));
            });
            Check("Quota and update check 96", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                QuotaWindow w = new QuotaWindow { Minutes = 300, Remaining = 0 };
                Assert(SourceHealth.WindowLine("5 heures", w, true, now).Contains("Reset not provided"));
                w.ResetUtc = now.AddMinutes(-1);
                Assert(SourceHealth.WindowLine("5 heures", w, true, now).Contains("Reset attendu"));
            });
            Check("Quota and update check 97", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                Snapshot snap = new Snapshot { CapturedUtc = now, UsageAllowed = false,
                    Five = new QuotaWindow { Minutes = 300, Remaining = 0, ResetUtc = now.AddHours(1) } };
                string text = SourceHealth.Build(snap, "Direct", now, 0, "", now.AddMinutes(1), now);
                Assert(text.Contains("Usage ordinaire signalé blocked") && text.Contains("0 % restants") &&
                    text.Contains("7 jours : Fenêtre non communiquée"));
            });
            Check("Quota and update check 98", delegate {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                string text = SourceHealth.Build(null, "Unavailable", null, 1,
                    "Accès refusé (401/403)", now.AddMinutes(2), now);
                Assert(text.Contains("Codex CLI / account/rateLimits/read") &&
                    text.Contains("Statut : Unavailable") && text.Contains("Accès refusé") &&
                    text.Contains("Dernier relevé valide : Aucun") &&
                    !text.Contains("% restants"));
            });
            report.Insert(0, "SHINO // CODEX V5.2.0 — " + passed + "/" + total + " autotests réussis\r\n\r\n");
            return report.ToString();
        }
    }
}
