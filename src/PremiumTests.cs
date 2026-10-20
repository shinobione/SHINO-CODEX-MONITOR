using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    // Explicit synthetic fixtures. Never instantiate LocalStore or a live reader here.
    internal static class PremiumTests
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T21:00:00Z");
        private static int total, passed;
        private static readonly StringBuilder log = new StringBuilder();
        private static void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
        private static void Check(string name, Action action)
        {
            total++;
            try { action(); passed++; log.AppendLine("PASS " + name); }
            catch (Exception e) { log.AppendLine("FAIL " + name + ": " + e.Message); }
        }
        private static string Row(string time, string five, string week)
        { return "{\"utc\":\"" + time + "\",\"five\":" + five + ",\"week\":" + week + "}"; }
        private static HistoryDataset Read(params string[] lines) { return HistoryData.Normalize(lines, Now); }
        private static HistoryView View(HistoryDataset data, int hours, HistorySeries series)
        { return HistoryData.Select(data, Now, hours, series); }
        internal static string Run()
        {
            total = passed = 0; log.Length = 0;
            Check("Empty history has no fabricated points", delegate { Assert(View(Read(), 24, HistorySeries.Both).Points.Count == 0); });
            Check("One reading retains exact percentage and timestamp", delegate {
                HistoryView v = View(Read(Row("2026-10-19T20:00:00Z", "64", "null")), 24, HistorySeries.Five);
                Assert(v.Points.Count == 1 && v.Points[0].Remaining == 64 && v.Start == Now.AddHours(-1)); });
            Check("Irregular samples retain UTC spacing", delegate {
                HistoryView v = View(Read(Row("2026-10-19T19:00:00Z", "80", "50"), Row("2026-10-19T19:01:00Z", "78", "49"), Row("2026-10-19T20:00:00Z", "70", "48")), 24, HistorySeries.Five);
                Assert(v.Points.Count == 3 && !v.Points[2].ConnectPrevious && v.Points[1].ConnectPrevious); });
            Check("Independent series retain absent observations", delegate {
                HistoryView v = View(Read(Row("2026-10-19T20:00:00Z", "60", "null"), Row("2026-10-19T20:05:00Z", "null", "30")), 24, HistorySeries.Both);
                Assert(v.Points.Count == 2 && v.Points[0].Five && !v.Points[1].Five); });
            Check("Unordered timestamps sort by instant", delegate {
                HistoryDataset d = Read(Row("2026-10-19T20:00:00Z", "40", "null"), Row("2026-10-19T19:00:00Z", "50", "null"));
                Assert(d.Rows[0].Five == 50); });
            Check("Exact duplicates collapse", delegate {
                string r = Row("2026-10-19T20:00:00Z", "40", "null"); Assert(Read(r, r).Rows.Count == 1); });
            Check("Contradictory duplicate invalidates only conflicting series", delegate {
                HistoryDataset d = Read(Row("2026-10-19T20:00:00Z", "40", "30"), Row("2026-10-19T20:00:00Z", "50", "30"));
                Assert(d.Rows[0].Five == null && d.Rows[0].Week == 30 && d.InvalidRows > 0); });
            Check("NaN infinity null and out of bounds are not points", delegate {
                HistoryDataset d = Read(Row("2026-10-19T20:00:00Z", "101", "-1"), "broken", Row("2026-10-19T20:01:00Z", "null", "null"));
                Assert(View(d, 24, HistorySeries.Both).Points.Count == 0 && d.InvalidRows == 3); });
            Check("Boundary percentages zero and one hundred are retained", delegate {
                HistoryView v = View(Read(Row("2026-10-19T20:00:00Z", "0", "100")), 24, HistorySeries.Both);
                Assert(v.Points.Min(p => p.Remaining) == 0 && v.Points.Max(p => p.Remaining) == 100); });
            Check("Stale-labelled record excluded without closing a gap", delegate {
                HistoryDataset d = Read("{\"utc\":\"2026-10-19T20:00:00Z\",\"five\":80,\"status\":\"stale\"}");
                Assert(View(d, 24, HistorySeries.Both).Points.Count == 0 && d.InvalidRows == 1); });
            Check("Unavailable source distinguished from empty file", delegate {
                Assert(HistoryData.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "absent"), Now).Unavailable == false);
                Assert(View(new HistoryDataset { Unavailable = true }, 24, HistorySeries.Both).Message == "History unavailable"); });
            Check("Large temporal gap interrupts line", delegate {
                HistoryView v = View(Read(Row("2026-10-19T18:00:00Z", "60", "20"), Row("2026-10-19T20:00:00Z", "50", "19")), 24, HistorySeries.Five);
                Assert(!v.Points[1].ConnectPrevious); });
            Check("Expected reset passing is never an observed reset", delegate {
                HistoryDataset d = Read("{\"utc\":\"2026-10-19T20:00:00Z\",\"five\":20,\"fiveResetUtc\":\"2026-10-19T20:15:00Z\"}");
                Assert(!View(d, 24, HistorySeries.Five).Points[0].ObservedReset); });
            Check("Observed replenishment and advanced reset support reset marker", delegate {
                HistoryDataset d = Read("{\"utc\":\"2026-10-19T20:00:00Z\",\"five\":20,\"fiveResetUtc\":\"2026-10-19T20:05:00Z\"}",
                    "{\"utc\":\"2026-10-19T20:10:00Z\",\"five\":100,\"fiveResetUtc\":\"2026-10-20T01:05:00Z\"}");
                HistoryPoint p = View(d, 24, HistorySeries.Five).Points[1]; Assert(p.ObservedReset && !p.ConnectPrevious); });
            Check("Replenishment without reset metadata remains unconfirmed", delegate {
                HistoryPoint p = View(Read(Row("2026-10-19T20:00:00Z", "1", "null"), Row("2026-10-19T20:05:00Z", "99", "null")), 24, HistorySeries.Five).Points[1];
                Assert(!p.ObservedReset && !p.ConnectPrevious); });
            Check("Different offsets identify same instant", delegate {
                Assert(Read(Row("2026-10-19T22:00:00+02:00", "60", "null"), Row("2026-10-19T20:00:00Z", "60", "null")).Rows.Count == 1); });
            Check("Offset-less time rejected", delegate { Assert(Read(Row("2026-10-19T20:00:00", "50", "null")).InvalidRows == 1); });
            Check("DST repeated wall time retains two distinct UTC instants", delegate {
                HistoryDataset d = HistoryData.Normalize(new [] {Row("2026-10-25T02:30:00+02:00", "80", "null"), Row("2026-10-25T02:30:00+01:00", "70", "null")}, Now.AddDays(10));
                Assert(d.Rows.Count == 2 && (d.Rows[1].Utc - d.Rows[0].Utc).TotalHours == 1); });
            Check("Thirty-day and ALL filters retain old V5.1 records", delegate {
                HistoryDataset d = Read(Row("2026-09-25T20:00:00Z", "64", "30"));
                Assert(View(d, 24, HistorySeries.Both).Points.Count == 0 && View(d, 720, HistorySeries.Both).Points.Count == 2 && View(d, 0, HistorySeries.Both).Points.Count == 2); });
            Check("Summary uses selected real points only", delegate {
                HistoryView v = View(Read(Row("2026-10-19T20:00:00Z", "40", "99"), Row("2026-10-19T20:05:00Z", "30", "98")), 24, HistorySeries.Five);
                Assert(v.Points.Count == 2 && v.Summary.Contains("30%") && v.Summary.Contains("40%") && !v.Summary.Contains("99%")); });
            Check("Future readings rejected", delegate { Assert(Read(Row("2026-10-20T20:00:00Z", "60", "null")).InvalidRows == 1); });
            Check("Invalid middle sample breaks its own series only", delegate {
                HistoryView v = View(Read(Row("2026-10-19T20:00:00Z", "80", "30"), Row("2026-10-19T20:05:00Z", "101", "29"), Row("2026-10-19T20:10:00Z", "70", "28")), 24, HistorySeries.Both);
                Assert(!v.Points.Last(p => p.Five).ConnectPrevious && v.Points.Last(p => !p.Five).ConnectPrevious); });
            Check("Tooltip names source status and expected reset", delegate {
                HistoryPoint p = View(Read(Row("2026-10-19T20:00:00Z", "60", "null")), 24, HistorySeries.Five).Points[0];
                Assert(p.Tooltip.Contains("Source:") && p.Tooltip.Contains("Valid") && p.Tooltip.Contains("19 Oct 2026")); });
            Check("Rapid filters never mutate originals", delegate {
                HistoryDataset d = Fixture(); int n = d.Rows.Count;
                for (int i = 0; i < 100; i++) View(d, i % 2 == 0 ? 24 : 168, (HistorySeries)(i % 3));
                Assert(d.Rows.Count == n && d.Rows[0].Five == 80); });
            Check("Clock is elapsed-time based at three seconds per turn", delegate { Assert(Math.Abs(RadarMotion.AngleAt(1.5) - 180) < .01 && RadarMotion.AngleAt(3) == 0); });
            Check("All radar evidence states map explicitly", delegate {
                Assert(RadarMotion.Map(LiveTaskPhase.Active, true, false) == RadarState.Active);
                Assert(RadarMotion.Map(LiveTaskPhase.Active, false, false) == RadarState.Stale);
                Assert(RadarMotion.Map(LiveTaskPhase.Unconfirmed, true, false) == RadarState.Unverified);
                Assert(RadarMotion.Map(LiveTaskPhase.Unknown, false, true) == RadarState.Unavailable);
                Assert(RadarMotion.Map(LiveTaskPhase.Completed, true, false) == RadarState.Complete); });
            Check("Completion deduplicated and startup never celebrates", delegate {
                using (ActivityGlyph glyph = new ActivityGlyph()) {
                    glyph.SetPhase(LiveTaskPhase.Completed); Assert(!glyph.Completing);
                    glyph.ConfirmCompletion("synthetic-1"); Assert(glyph.Completing);
                    glyph.SetPhase(LiveTaskPhase.Completed); Assert(glyph.Completing);
                    glyph.FinishMotion(); glyph.ConfirmCompletion("synthetic-1"); Assert(!glyph.Completing); } });
            Check("Hidden or disabled radar requests no frames", delegate {
                using (ActivityGlyph glyph = new ActivityGlyph()) {
                    glyph.SetPhase(LiveTaskPhase.Active); glyph.SetMotion(false); Assert(!glyph.MotionRunning);
                    glyph.Visible = false; Assert(!glyph.NeedsFrames); } });
            Check("Resume resets monotonic clock", delegate {
                using (ActivityGlyph glyph = new ActivityGlyph()) { glyph.SetPhase(LiveTaskPhase.Active); glyph.SetMotion(true); glyph.SetMotion(false); glyph.SetMotion(true); Assert(glyph.ElapsedSeconds < .1); } });
            Check("Unlocated corruption never bridges observations", delegate {
                HistoryView v = View(Read(Row("2026-10-19T20:00:00Z", "80", "null"), "broken", Row("2026-10-19T20:05:00Z", "70", "null")),24,HistorySeries.Five);
                Assert(!v.Points[1].ConnectPrevious); });
            Check("Third duplicate cannot resurrect contradictory value", delegate {
                HistoryDataset d = Read(Row("2026-10-19T20:00:00Z", "10", "20"), Row("2026-10-19T20:00:00Z", "11", "20"), Row("2026-10-19T20:00:00Z", "10", "20"));
                Assert(!d.Rows[0].Five.HasValue && d.Rows[0].Week == 20); });
            Check("History read does not rewrite V5.1 bytes", delegate {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"legacy-history-fixture.txt");
                string text = Row("2026-09-25T20:00:00Z","64","30");
                try { File.WriteAllText(path,text); Assert(HistoryData.Load(path,Now).Rows.Count == 1 && File.ReadAllText(path) == text); }
                finally { File.Delete(path); } });
            Check("Unreadable history produces unavailable state", delegate {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"locked-history-fixture.txt");
                try { using (FileStream locked = new FileStream(path,FileMode.Create,FileAccess.ReadWrite,FileShare.None))
                    Assert(HistoryData.Load(path,Now).Unavailable); }
                finally { File.Delete(path); } });
            Check("History controls filter real samples and expose keyboard table", delegate {
                using (HistoryWindow window = new HistoryWindow(Fixture(),Now)) {
                    window.ShowInTaskbar = false; window.StartPosition = FormStartPosition.Manual; window.Location = new Point(-32000,-32000); window.Show();
                    FlowLayoutPanel tools = window.Controls.OfType<FlowLayoutPanel>().Single();
                    tools.Controls.OfType<Button>().Single(b => b.Text == "7D").PerformClick();
                    tools.Controls.OfType<Button>().Single(b => b.Text == "WEEKLY").PerformClick();
                    HistoryCanvas canvas = window.Controls.OfType<HistoryCanvas>().Single();
                    Assert(canvas.View.Points.Count > 1000 && canvas.View.Points.All(p => !p.Five));
                    tools.Controls.OfType<Button>().Single(b => b.Text == "Data table").PerformClick();
                    DataGridView table = window.Controls.OfType<DataGridView>().Single();
                    Assert(table.Visible && table.ReadOnly && table.Rows.Count == canvas.View.Points.Count);
                    window.Size = new Size(620,460); window.PerformLayout();
                    Assert(tools.Bottom <= table.Top && table.Height > 30);
                    tools.Controls.OfType<Button>().Single(b => b.Text == "24H").PerformClick();
                    tools.Controls.OfType<Button>().Single(b => b.Text == "BOTH").PerformClick();
                } });
            Check("Native drawing and resize fixtures", delegate { RenderEvidence(); });
            return "SYNTHETIC TEST DATA — " + passed + "/" + total + " premium tests passed\r\n" + log +
                (passed == total ? "All premium checks passed\r\n" : "FAIL Premium checks incomplete\r\n");
        }
        internal static HistoryDataset Fixture()
        {
            List<string> rows = new List<string>();
            for (int i = 0; i < 1800; i++) {
                DateTimeOffset t = Now.AddDays(-7).AddMinutes(i * 5);
                if (i > 1400 && i < 1470) continue;
                rows.Add(Row(t.ToString("o"), (80 - i % 75).ToString(CultureInfo.InvariantCulture), (90 - i % 60).ToString(CultureInfo.InvariantCulture)));
            }
            return Read(rows.ToArray());
        }
        private static void RenderEvidence()
        {
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "premium-qa");
            Directory.CreateDirectory(output);
            HistoryDataset data = Fixture();
            foreach (int dpi in new [] {100,125,150,200}) using (HistoryWindow window = new HistoryWindow(data, Now)) {
                window.ShowInTaskbar = false;
                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(-32000,-32000);
                window.Show(); Application.DoEvents();
                    if (dpi != 100) { window.Scale(new SizeF(dpi / 100f, dpi / 100f)); ScaleFonts(window,dpi / 100f); }
                    window.PerformLayout();
                    using (Bitmap b = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(b, new Rectangle(Point.Empty,b.Size)); Stamp(b); b.Save(Path.Combine(output, "history-layout-" + dpi + ".png")); }
            }
            using (HistoryCanvas canvas = new HistoryCanvas()) {
                canvas.Size = new Size(1000, 440); canvas.CreateControl();
                foreach (int hours in new [] {24,168}) {
                    canvas.View = View(data, hours, HistorySeries.Both);
                    using (Bitmap b = new Bitmap(canvas.Width, canvas.Height)) { canvas.DrawToBitmap(b, canvas.ClientRectangle); Stamp(b); b.Save(Path.Combine(output, "history-" + hours + "h.png")); }
                }
                canvas.View = View(Read("{\"utc\":\"2026-10-19T20:00:00Z\",\"five\":20,\"fiveResetUtc\":\"2026-10-19T20:05:00Z\",\"source\":\"Codex CLI\"}",
                    "{\"utc\":\"2026-10-19T20:10:00Z\",\"five\":100,\"fiveResetUtc\":\"2026-10-20T01:05:00Z\",\"source\":\"Codex CLI\"}"),24,HistorySeries.Both);
                using (Bitmap b = new Bitmap(canvas.Width,canvas.Height)) { canvas.DrawToBitmap(b,canvas.ClientRectangle); Stamp(b); b.Save(Path.Combine(output,"history-observed-reset.png")); }
                canvas.View = View(data,168,HistorySeries.Both);
                Stopwatch watch = Stopwatch.StartNew();
                using (Bitmap b = new Bitmap(canvas.Width, canvas.Height)) for (int i = 0; i < 20; i++) canvas.DrawToBitmap(b, canvas.ClientRectangle);
                log.AppendLine("MEASURE chart " + canvas.View.Points.Count + " points: " + (watch.Elapsed.TotalMilliseconds / 20).ToString("F2", CultureInfo.InvariantCulture) + " ms/frame (DrawToBitmap)");
            }
            using (ActivityGlyph glyph = new ActivityGlyph()) {
                glyph.Size = new Size(180,180); glyph.CreateControl();
                foreach (LiveTaskPhase p in new [] {LiveTaskPhase.Active, LiveTaskPhase.Completed, LiveTaskPhase.Unconfirmed}) {
                    glyph.SetPhase(p);
                    using (Bitmap b = new Bitmap(260,220)) { using (Graphics g = Graphics.FromImage(b)) g.Clear(UiColors.Background); glyph.DrawToBitmap(b, new Rectangle(40,25,180,180)); Stamp(b); b.Save(Path.Combine(output, "radar-" + p + ".png")); }
                }
                foreach (string layout in new [] {"Circular","Vertical","Compact","Micro"}) {
                    int size = layout == "Circular" ? DesktopGeometry.Scale(30,540) : 30;
                    glyph.Size = new Size(size,size); glyph.SetPhase(LiveTaskPhase.Active);
                    using (Bitmap b = new Bitmap(300,100)) {
                        using (Graphics g = Graphics.FromImage(b)) { g.Clear(UiColors.Background); g.DrawString(layout + " · radar at actual layout size",SystemFonts.DefaultFont,Brushes.White,8,68); }
                        glyph.DrawToBitmap(b,new Rectangle(12,30,size,size)); Stamp(b); b.Save(Path.Combine(output,"radar-size-"+layout+".png"));
                    }
                }
                int before = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                for (int i = 0; i < 100; i++) {
                    glyph.Size = new Size(24 + i % 8 * 8, 24 + i % 8 * 8);
                    glyph.BackColor = i % 2 == 0 ? Color.FromArgb(8,14,23) : UiColors.Background;
                    using (Bitmap b = new Bitmap(100,100)) glyph.DrawToBitmap(b, glyph.ClientRectangle);
                }
                int after = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                log.AppendLine("MEASURE GDI resize/theme 100 cycles: " + before + " -> " + after);
                Assert(after - before <= 4);
            }
            log.AppendLine("ENV " + Environment.OSVersion.VersionString + "; CPU threads=" + Environment.ProcessorCount + "; native GDI+; synthetic layout scaling is NOT physical monitor DPI proof.");
        }
        private static void ScaleFonts(Control root, float scale)
        {
            // Deliberate layout simulation; OS monitor DPI is a separate field gate.
            List<KeyValuePair<Control,Font>> original = new List<KeyValuePair<Control,Font>>();
            CollectFonts(root,original);
            foreach (KeyValuePair<Control,Font> item in original) item.Key.Font = new Font(item.Value.FontFamily,item.Value.Size*scale,item.Value.Style);
        }
        private static void CollectFonts(Control root, List<KeyValuePair<Control,Font>> result)
        { foreach (Control child in root.Controls) { result.Add(new KeyValuePair<Control,Font>(child,child.Font)); CollectFonts(child,result); } }
        internal static string RunPerformance()
        {
            StringBuilder report = new StringBuilder("SYNTHETIC TEST DATA — native instrument benchmark; no collectors or user settings\r\n");
            Process process = Process.GetCurrentProcess();
            using (Form host = new Form())
            using (ActivityGlyph glyph = new ActivityGlyph())
            using (System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer())
            using (System.Windows.Forms.Timer sampler = new System.Windows.Forms.Timer())
            using (Bitmap frame = new Bitmap(30,30)) {
                host.ShowInTaskbar = false; host.StartPosition = FormStartPosition.Manual; host.Location = new Point(-32000,-32000);
                glyph.Size = frame.Size; host.Controls.Add(glyph); glyph.SetPhase(LiveTaskPhase.Active);
                Stopwatch elapsed = new Stopwatch(); TimeSpan cpu = TimeSpan.Zero; TimeSpan hiddenCpu = TimeSpan.Zero;
                int frames = 0, activeFrames = 0; bool hidden = false; double last = 0, maxGap = 0;
                animation.Interval = 34;
                animation.Tick += delegate {
                    double t = elapsed.Elapsed.TotalMilliseconds;
                    if (last > 0) maxGap = Math.Max(maxGap,t-last); last = t;
                    glyph.Advance(); glyph.DrawToBitmap(frame,glyph.ClientRectangle); frames++;
                };
                sampler.Interval = 250;
                sampler.Tick += delegate {
                    if (!hidden && elapsed.Elapsed.TotalSeconds >= 30) {
                        process.Refresh(); TimeSpan used = process.TotalProcessorTime-cpu;
                        report.AppendLine("Active: " + frames + " frames / " + elapsed.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture) + " s; FPS=" + (frames/elapsed.Elapsed.TotalSeconds).ToString("F2",CultureInfo.InvariantCulture) +
                            "; CPU total-machine=" + (used.TotalSeconds/elapsed.Elapsed.TotalSeconds/Environment.ProcessorCount*100).ToString("F3",CultureInfo.InvariantCulture) + "%; single-core=" + (used.TotalSeconds/elapsed.Elapsed.TotalSeconds*100).ToString("F2",CultureInfo.InvariantCulture) +
                            "%; max frame gap=" + maxGap.ToString("F1",CultureInfo.InvariantCulture) + " ms");
                        animation.Stop(); glyph.Visible = false; glyph.SetMotion(false); hidden = true; activeFrames = frames;
                        hiddenCpu = process.TotalProcessorTime; elapsed.Restart();
                    } else if (hidden && elapsed.Elapsed.TotalSeconds >= 5) {
                        process.Refresh(); report.AppendLine("Hidden: graphical timer enabled=" + animation.Enabled + "; additional frames=" + (frames-activeFrames) + "; CPU total-machine=" +
                            ((process.TotalProcessorTime-hiddenCpu).TotalSeconds/elapsed.Elapsed.TotalSeconds/Environment.ProcessorCount*100).ToString("F3",CultureInfo.InvariantCulture) + "% (sampling timer remains)");
                        sampler.Stop(); host.Close();
                    }
                };
                host.Shown += delegate { process.Refresh(); cpu = process.TotalProcessorTime; elapsed.Start(); glyph.SetMotion(true); animation.Start(); sampler.Start(); };
                Application.Run(host);
            }
            HistoryDataset dataset = Fixture(); HistoryCycles(dataset,5);
            for (int batch=1; batch<=3; batch++) {
                GC.Collect(); GC.WaitForPendingFinalizers(); process.Refresh(); long before = process.PrivateMemorySize64; int gdiBefore = GetGuiResources(process.Handle,0);
                HistoryCycles(dataset,25);
                GC.Collect(); GC.WaitForPendingFinalizers(); process.Refresh();
                report.AppendLine("History show/close batch " + batch + " (25 cycles after warmup): private bytes " + before + " -> " + process.PrivateMemorySize64 + "; GDI " + gdiBefore + " -> " + GetGuiResources(process.Handle,0));
            }
            report.AppendLine("Environment: " + Environment.OSVersion.VersionString + "; logical processors=" + Environment.ProcessorCount + "; " + Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"));
            report.AppendLine("Synthetic component benchmark only. Actual widget multi-monitor smoothness, sleep/wake and notification delivery remain field checks.");
            return report.ToString();
        }
        private static void HistoryCycles(HistoryDataset data, int count)
        {
            for (int i=0; i<count; i++) using (HistoryWindow window = new HistoryWindow(data,Now)) {
                window.ShowInTaskbar = false; window.StartPosition = FormStartPosition.Manual; window.Location = new Point(-32000,-32000);
                window.Show(); Application.DoEvents(); window.Close();
            }
        }
        private static void Stamp(Bitmap b)
        { using (Graphics g = Graphics.FromImage(b)) using (Brush bg = new SolidBrush(Color.FromArgb(8,14,23))) using (Brush ink = new SolidBrush(Color.Gold)) { int y = b.Height <= 250 ? 0 : b.Height-22; g.FillRectangle(bg, 0, y, b.Width, 22); g.DrawString("SYNTHETIC TEST DATA", SystemFonts.DefaultFont, ink, 6, y+4); } }
        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);
    }
}
