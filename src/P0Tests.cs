using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    // Synthetic fixtures only. No account, installed monitor, or user session access.
    internal static class P0Tests
    {
        internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T12:00:00Z");
        private static int total, passed;
        private static readonly StringBuilder output = new StringBuilder();
        internal static string Event(string type, string id, int ago)
        { return "{\"timestamp\":\"" + Now.AddSeconds(-ago).ToString("o") + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"" + type + "\",\"turn_id\":\"" + id + "\"}}"; }
        internal static string Context(string id, int ago)
        { return "{\"timestamp\":\"" + Now.AddSeconds(-ago).ToString("o") + "\",\"type\":\"turn_context\",\"payload\":{\"turn_id\":\"" + id + "\",\"model\":\"test-model\",\"effort\":\"high\"}}"; }
        internal static List<LiveTask> Parse(params string[] lines)
        {
            return LiveTaskReader.ParseTurns(lines,Now,Now);
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Check(string name, Action action)
        {
            total++; File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"P0_PROGRESS.txt"),"RUN " + name + "\r\n" + output);
            try { action(); passed++; output.AppendLine("PASS " + name); } catch (Exception e) { output.AppendLine("FAIL " + name + ": " + e.Message); }
        }
        internal static string Run()
        {
            total = passed = 0; output.Length = 0;
            Check("Two identified turns in one source both survive",delegate {
                List<LiveTask> tasks = Parse(Event("task_started","A",40),Event("task_started","B",30));
                Assert(tasks.Count == 2 && tasks.All(t => t.Phase == LiveTaskPhase.Active),"Only last turn survived; expected A and B active"); });
            Check("A completion never replaces running B",delegate {
                List<LiveTask> tasks = Parse(Event("task_started","A",40),Event("task_started","B",30),Event("task_complete","A",2));
                Assert(tasks.Any(t => t.Id == "A" && t.Phase == LiveTaskPhase.Completed) && tasks.Any(t => t.Id == "B" && t.Phase == LiveTaskPhase.Active),"A terminal or active B lost"); });
            Check("Per-turn freshness does not borrow B activity for stale A",delegate {
                List<LiveTask> tasks = Parse(Event("task_started","A",200),Event("task_started","B",180),Context("B",2));
                Assert(tasks.Any(t => t.Id == "A" && t.Phase == LiveTaskPhase.Unconfirmed) && tasks.Any(t => t.Id == "B" && t.Phase == LiveTaskPhase.Active),"Fresh B hid stale A"); });
            Check("B completes while A stays active",delegate {
                List<LiveTask> tasks=Parse(Event("task_started","A",40),Event("task_started","B",30),Event("task_complete","B",2));
                Assert(tasks.Single(t => t.Id == "A").Phase == LiveTaskPhase.Active && tasks.Single(t => t.Id == "B").Phase == LiveTaskPhase.Completed,"A overwritten by B"); });
            Check("Two separate root sessions and one workspace retain identities",delegate {
                LiveTask a=Task("A","session-a",40), b=Task("B","session-b",40);
                MissionTracker tracker=new MissionTracker(); tracker.Update(Report(Now,a,b));
                Assert(tracker.At(Now).Count == 2 && a.Identity != b.Identity,"Workspace collapsed identities"); });
            Check("Distinct identifiers at same session timestamp remain independent",delegate {
                List<LiveTask> tasks=Parse(Event("task_started","A",40),Event("task_started","B",40));
                foreach (LiveTask t in tasks) t.SessionKey="one-source";
                Assert(tasks.Count == 2 && tasks[0].Identity != tasks[1].Identity,"Distinct explicit IDs merged"); });
            Check("Both short turns complete between polling intervals",delegate {
                LiveTask a=Task("A","source-a",4), b=Task("B","source-b",3);
                a.Phase=b.Phase=LiveTaskPhase.Completed; a.FinishedUtc=b.FinishedUtc=Now.AddSeconds(-1);
                Assert(TaskNotificationRules.NewTerminals(Report(Now.AddSeconds(-5)),Report(Now,a,b)).Count == 2,"Short completed turn lost"); });
            Check("Repeated completion does not produce a second transition",delegate {
                LiveTask a=Task("A","source-a",30); LiveTask done=a.Copy(); done.Phase=LiveTaskPhase.Completed; done.FinishedUtc=Now;
                LiveTaskReport before=Report(Now.AddSeconds(-5),a), after=Report(Now,done);
                Assert(TaskNotificationRules.NewTerminals(before,after).Count == 1 && TaskNotificationRules.NewTerminals(after,Report(Now.AddSeconds(5),done.Copy())).Count == 0,"Duplicate transition"); });
            Check("One stale source does not stop another fresh source",delegate {
                LiveTask a=Task("A","source-a",200),b=Task("B","source-b",20); a.LastWriteUtc=Now.AddSeconds(-120);
                MissionTracker tracker=new MissionTracker(); tracker.Update(Report(Now,a,b));
                Assert(tracker.At(Now).Single(v => v.Task.Id == "A").State == RadarState.Stale && tracker.At(Now).Single(v => v.Task.Id == "B").State == RadarState.Active,"Freshness leaked between sources"); });
            Check("A missing readable source becomes unverified without a terminal",delegate {
                LiveTask a=Task("A","source-a",30),b=Task("B","source-b",20);
                MissionTracker tracker=new MissionTracker(); tracker.Update(Report(Now,a,b)); tracker.Update(Report(Now.AddSeconds(5),b.Copy()));
                List<MissionView> views=tracker.At(Now.AddSeconds(5));
                Assert(views.Count == 2 && views.Single(v => v.Task.Id == "A").State == RadarState.Unverified && !a.FinishedUtc.HasValue,"Missing source invented completion"); });
            Check("Anonymous overlapping starts do not fabricate a second mission",delegate {
                List<LiveTask> tasks=Parse(Event("task_started","",40),Event("task_started","",30));
                Assert(tasks.Count == 1 && tasks[0].Phase == LiveTaskPhase.Unconfirmed,"Ambiguous anonymous missions invented"); });
            Check("Reused identifier with a contradictory start is unverified",delegate {
                List<LiveTask> tasks=Parse(Event("task_started","A",40),Event("task_started","A",30),Event("task_complete","A",2));
                Assert(tasks.Count == 1 && tasks[0].IdentityUncertain && !tasks[0].FinishedUtc.HasValue,"Contradictory identity confirmed"); });
            Check("Terminal without identity cannot finish two open turns",delegate {
                List<LiveTask> tasks=Parse(Event("task_started","A",40),Event("task_started","B",30),Event("task_complete","",2));
                Assert(tasks.Count == 2 && tasks.All(t => t.Phase == LiveTaskPhase.Unconfirmed && !t.FinishedUtc.HasValue),"Ambiguous terminal attributed"); });
            Check("Duplicate start and terminal records are idempotent",delegate {
                string start=Event("task_started","A",40),done=Event("task_complete","A",2);
                List<LiveTask> tasks=Parse(start,start,done,done); Assert(tasks.Count == 1 && tasks[0].Phase == LiveTaskPhase.Completed,"Duplicate records changed lifecycle"); });
            Check("Long identifiers are never truncated into one identity",delegate {
                string prefix=new string('a',70);
                List<LiveTask> tasks=Parse(Event("task_started",prefix+"A",40),Event("task_started",prefix+"B",30));
                Assert(tasks.Count == 2 && tasks[0].Id != tasks[1].Id,"Truncated identity collision"); });
            Check("A unique anonymous start accepts identified context without losing metadata",delegate {
                List<LiveTask> tasks=Parse(Event("task_started","",40),Context("A",30),Event("task_started","",40),Event("task_complete","",2));
                Assert(tasks.Count == 1 && tasks[0].Id == "A" && tasks[0].Model == "test-model" && tasks[0].Phase == LiveTaskPhase.Completed,"Anonymous/context identity was lost"); });
            Check("Restart and wake do not replay terminal transitions",delegate {
                LiveTask done=Task("A","source-a",40); done.Phase=LiveTaskPhase.Completed; done.FinishedUtc=Now.AddSeconds(-2);
                Assert(TaskNotificationRules.NewTerminals(null,Report(Now,done)).Count == 0 && TaskNotificationRules.NewTerminals(Report(Now.AddMinutes(-2)),Report(Now,done)).Count == 0,"Old completion replayed"); });
            Check("Subagents stay excluded from the mission collection",delegate {
                LiveTask child=Task("child","source-child",20); child.IsSubagent=true; child.IsRootConfirmed=false;
                MissionTracker tracker=new MissionTracker(); tracker.Update(Report(Now,child)); Assert(tracker.At(Now).Count == 0,"Child shown as user mission"); });
            Check("Three independent observed missions remain visible in collection",delegate {
                MissionTracker tracker=new MissionTracker(); tracker.Update(Report(Now,Task("A","a",40),Task("B","b",30),Task("C","c",20)));
                Assert(tracker.At(Now).Count == 3,"Third task dropped"); });
            Check("Sanitized cached starts survive a growing tail",delegate { CheckReaderCache(); });
            Check("Cold concurrent long root sessions share a bounded expansion budget",delegate { CheckColdSources(); });
            Check("Completed history cannot permanently saturate the turn cache",delegate {
                List<string> lines=new List<string>();
                for (int i=0;i<70;i++) { lines.Add(Event("task_started","old"+i,200-i*2)); lines.Add(Event("task_complete","old"+i,199-i*2)); }
                lines.Add(Event("task_started","latest",1));
                List<LiveTask> tasks=Parse(lines.ToArray()); Assert(tasks.Count <= LiveTaskReader.MaxTurnsPerSession && tasks.Any(t => t.Id == "latest" && t.Phase == LiveTaskPhase.Active),"Cache saturated with completed history"); });
            Check("Two cards share one clock and A completion does not reset B",delegate { CheckIndependentAnimation(); });
            Check("Two and three cards render in all four layout widths",delegate { RenderLayouts(); });
            Check("Real HWND big and small icons survive taskbar and handle transitions",delegate { CheckWindowIcons(); });
            return "SYNTHETIC TEST DATA — " + passed + "/" + total + " P0 tests passed\r\n" + output + (passed == total ? "All P0 checks passed\r\n" : "FAIL P0 checks incomplete\r\n");
        }
        internal static LiveTask Task(string id,string source,int ago)
        {
            LiveTask task=Parse(Event("task_started",id,ago))[0];
            task.SessionKey=source; task.IsRootConfirmed=true; task.Workspace="Synthetic project"; task.Model="test-model"; task.Effort="high"; return task;
        }
        internal static LiveTaskReport Report(DateTimeOffset when,params LiveTask[] tasks)
        { LiveTaskReport report=new LiveTaskReport { CheckedUtc=when }; report.Tasks.AddRange(tasks); return report; }
        private static void CheckReaderCache()
        {
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"p0-fixture-"+Guid.NewGuid().ToString("N"));
            string previous=Environment.GetEnvironmentVariable("CODEX_HOME");
            string folder=Path.Combine(root,"sessions","2026","10","19"); Directory.CreateDirectory(folder);
            try {
                Environment.SetEnvironmentVariable("CODEX_HOME",root);
                string path=Path.Combine(folder,"rollout-synthetic.jsonl");
                string meta="{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\",\"cwd\":\"C:/Synthetic project\"}}";
                File.WriteAllLines(path,new [] {meta,Event("task_started","A",40),Event("task_started","B",30)}); File.SetLastWriteTimeUtc(path,Now.UtcDateTime);
                Assert(LiveTaskReader.Read(Now).Tasks.Count == 2,"Initial read lost a turn");
                using (StreamWriter writer=new StreamWriter(path,true)) { for (int i=0;i<2500;i++) writer.WriteLine("{\"type\":\"response_item\",\"synthetic_ignored\":\""+new string('x',220)+"\"}"); writer.WriteLine(Event("task_complete","A",1)); writer.WriteLine(Context("B",1)); }
                File.SetLastWriteTimeUtc(path,Now.AddSeconds(1).UtcDateTime);
                LiveTaskReport report=LiveTaskReader.Read(Now.AddSeconds(1));
                Assert(report.Tasks.Count == 2 && report.Tasks.Single(t => t.Id == "A").Phase == LiveTaskPhase.Completed && report.Tasks.Single(t => t.Id == "B").Phase == LiveTaskPhase.Active,"Tail evicted a known start");
                Assert(LiveTaskReader.Read(Now.AddSeconds(2)).Tasks.Count == 2,"Unchanged cached source lost turns");
            } finally {
                Environment.SetEnvironmentVariable("CODEX_HOME",previous);
                string resolved=Path.GetFullPath(root), parent=Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                if (resolved.StartsWith(parent,StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved)) Directory.Delete(resolved,true);
            }
        }
        private static void CheckIndependentAnimation()
        {
            using (Form host=new Form()) using (MissionDeck deck=new MissionDeck()) {
                host.ShowInTaskbar=false; host.StartPosition=FormStartPosition.Manual; host.Location=new Point(-32000,-32000);
                deck.Dock=DockStyle.Fill; host.Controls.Add(deck); host.ClientSize=new Size(540,110); host.Show();
                LiveTask a=Task("A","a",40),b=Task("B","b",30); deck.UpdateReport(Report(Now,a,b),Now); deck.SetMotion(true); deck.Advance();
                MissionCard bCard=deck.Cards.Single(c => c.Mission.Task.Id == "B"); double before=bCard.Radar.ElapsedSeconds;
                LiveTask done=a.Copy(); done.Phase=LiveTaskPhase.Completed; done.FinishedUtc=Now.AddSeconds(1);
                deck.UpdateReport(Report(Now.AddSeconds(1),done,b.Copy()),Now.AddSeconds(1)); deck.ConfirmCompletion(done); deck.SetMotion(true); deck.Advance();
                Assert(ReferenceEquals(bCard,deck.Cards.Single(c => c.Mission.Task.Id == "B")) && bCard.Radar.Phase == LiveTaskPhase.Active && bCard.Radar.ElapsedSeconds >= before,"B radar restarted or disappeared");
                Assert(deck.Cards.Single(c => c.Mission.Task.Id == "A").Radar.Completing,"A completion did not transition");
                deck.Cards.Single(c => c.Mission.Task.Id == "A").Radar.FinishMotion(); deck.ConfirmCompletion(done);
                Assert(!deck.Cards.Single(c => c.Mission.Task.Id == "A").Radar.Completing,"Completion replayed");
                deck.Visible=false; deck.SetMotion(false); Assert(deck.Cards.All(c => !c.Radar.MotionRunning),"Hidden radar clock running");
            }
        }
        private static void CheckColdSources()
        {
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"p0-cold-"+Guid.NewGuid().ToString("N"));
            string previous=Environment.GetEnvironmentVariable("CODEX_HOME");
            try {
                Environment.SetEnvironmentVariable("CODEX_HOME",root); string folder=Path.Combine(root,"sessions","2026","10","19"); Directory.CreateDirectory(folder);
                foreach (string id in new [] {"cold-A","cold-B"}) {
                    string path=Path.Combine(folder,"rollout-"+id+".jsonl");
                    using (StreamWriter writer=new StreamWriter(path)) {
                        writer.WriteLine("{\"type\":\"session_meta\",\"payload\":{\"thread_source\":\"user\"}}"); writer.WriteLine(Event("task_started",id,700));
                        for (int i=0;i<3000;i++) writer.WriteLine("{\"type\":\"response_item\",\"ignored\":\""+new string('x',220)+"\"}");
                    }
                    File.SetLastWriteTimeUtc(path,Now.UtcDateTime);
                }
                LiveTaskReport report=LiveTaskReader.Read(Now); Assert(report.Tasks.Count == 2 && report.Tasks.All(t => t.Phase == LiveTaskPhase.Active),"Only freshest long source expanded");
            } finally {
                Environment.SetEnvironmentVariable("CODEX_HOME",previous);
                if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),StringComparison.OrdinalIgnoreCase) && Directory.Exists(root)) Directory.Delete(root,true);
            }
        }
        private static void RenderLayouts()
        {
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"p0-qa"); Directory.CreateDirectory(folder);
            foreach (string layout in new [] {"Circular","Vertical","Compact","Micro"}) using (Form host=new Form()) using (MissionDeck deck=new MissionDeck()) {
                int width=layout == "Vertical" ? 327 : layout == "Compact" ? 642 : layout == "Micro" ? 468 : 512;
                int height=layout == "Vertical" ? 110 : layout == "Compact" ? 96 : layout == "Micro" ? 92 : 100;
                host.ShowInTaskbar=false; host.StartPosition=FormStartPosition.Manual; host.Location=new Point(-32000,-32000); host.ClientSize=new Size(width,height);
                deck.Dock=DockStyle.Fill; host.Controls.Add(deck); host.Show();
                foreach (int count in new [] {1,2,3}) {
                    LiveTask[] tasks=Enumerable.Range(1,count).Select(i => Task("T"+i,"source-"+i,20+i)).ToArray();
                    // Fresh tracker per scenario avoids turning disappeared fixtures into fake completions.
                    deck.UpdateReport(null,Now); deck.UpdateReport(Report(Now,tasks),Now); deck.PerformLayout();
                    Assert(deck.Count == count && deck.Cards.Count() == count,"Layout lost task cards");
                    Assert(deck.Cards.All(c => c.Controls.OfType<Label>().All(label => label.Bottom <= c.Height)),"Card text clips at actual activity height");
                    if (count == 2) { MissionCard[] cards=deck.Cards.ToArray(); Assert(Math.Abs(cards[0].Width-cards[1].Width)<=1,"Cards unequal"); }
                    using (Bitmap bitmap=new Bitmap(width,height+30)) {
                        deck.DrawToBitmap(bitmap,new Rectangle(0,0,width,height));
                        using (Graphics g=Graphics.FromImage(bitmap)) { g.FillRectangle(Brushes.Black,0,height,width,30); g.DrawString("SYNTHETIC TEST DATA · "+layout+" · "+count+" local turns",SystemFonts.DefaultFont,Brushes.Gold,5,height+6); }
                        bitmap.Save(Path.Combine(folder,"missions-"+layout+"-"+count+".png"));
                    }
                }
            }
        }
        private sealed class IconProbe : PeriscopeWindow
        { internal void Recreate() { RecreateHandle(); } }
        private static void CheckWindowIcons()
        {
            using (IconProbe window=new IconProbe()) {
                window.Text="CODEX PERISCOPE · SYNTHETIC TEST DATA";
                window.StartPosition=FormStartPosition.Manual; window.Location=new Point(-32000,-32000); window.Show();
                IntPtr process=Process.GetCurrentProcess().Handle;
                int beforeGdi=GetGuiResources(process,0), beforeUser=GetGuiResources(process,1);
                for (int i=0;i<20;i++) {
                    window.ShowInTaskbar=i%2 == 0; window.Hide(); window.Show(); window.Recreate();
                    window.WindowState=FormWindowState.Minimized; window.WindowState=FormWindowState.Normal;
                    PeriscopeWindow.SendMessage(window.Handle,0x007E,IntPtr.Zero,IntPtr.Zero);
                    foreach (int size in new [] {0,1}) {
                        IntPtr handle=PeriscopeWindow.SendMessage(window.Handle,0x007F,new IntPtr(size),IntPtr.Zero);
                        Assert(handle != IntPtr.Zero,"WM_GETICON missing "+size);
                        using (Icon icon=Icon.FromHandle(handle)) using (Bitmap bitmap=icon.ToBitmap()) {
                            int teal=0; for (int y=0;y<bitmap.Height;y++) for (int x=0;x<bitmap.Width;x++) { Color c=bitmap.GetPixel(x,y); if (c.A>20 && c.G>c.R*1.15 && c.B>c.R*1.15) teal++; }
                            Assert(teal>4,"Native window icon lacks radar pixels");
                        }
                    }
                }
                output.AppendLine("NATIVE HWND: small/big radar pixels verified across 20 visibility/taskbar/recreate/minimize/display cycles. Shell pin/cache visual confirmation remains FIELD REQUIRED.");
                int afterGdi=GetGuiResources(process,0), afterUser=GetGuiResources(process,1);
                output.AppendLine("NATIVE ICON HANDLES: GDI "+beforeGdi+" -> "+afterGdi+"; USER "+beforeUser+" -> "+afterUser);
                Assert(afterGdi-beforeGdi <= 4 && afterUser-beforeUser <= 4,"Window icon handles grow across recreations");
                using (Bitmap bitmap=new Bitmap(window.Width,window.Height)) { window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"p0-qa","native-window-icon.png")); }
            }
        }
        internal static string Performance()
        {
            StringBuilder text=new StringBuilder("SYNTHETIC TEST DATA — three independent radars, one UI clock/timer, no live readers\r\n");
            Process process=Process.GetCurrentProcess();
            using (Form host=new Form()) using (MissionDeck deck=new MissionDeck())
            using (System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer { Interval=34 })
            using (System.Windows.Forms.Timer sample=new System.Windows.Forms.Timer { Interval=250 })
            using (Bitmap frame=new Bitmap(540,200)) {
                host.ShowInTaskbar=false; host.StartPosition=FormStartPosition.Manual; host.Location=new Point(-32000,-32000); host.ClientSize=frame.Size;
                deck.Dock=DockStyle.Fill; host.Controls.Add(deck);
                deck.UpdateReport(Report(Now,Task("A","a",40),Task("B","b",30),Task("C","c",20)),Now);
                Stopwatch watch=new Stopwatch(); TimeSpan cpu=TimeSpan.Zero, hiddenCpu=TimeSpan.Zero;
                int frames=0, activeFrames=0; bool hidden=false;
                animation.Tick += delegate { deck.Advance(); deck.DrawToBitmap(frame,deck.ClientRectangle); frames++; };
                sample.Tick += delegate {
                    if (!hidden && watch.Elapsed.TotalSeconds >= 30) {
                        process.Refresh(); TimeSpan used=process.TotalProcessorTime-cpu;
                        text.AppendLine("Active: "+frames+" frames / "+watch.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture)+" s; FPS="+(frames/watch.Elapsed.TotalSeconds).ToString("F2",CultureInfo.InvariantCulture)+
                            "; CPU total-machine="+(used.TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100).ToString("F3",CultureInfo.InvariantCulture)+"%; single-core="+(used.TotalSeconds/watch.Elapsed.TotalSeconds*100).ToString("F2",CultureInfo.InvariantCulture)+"%");
                        animation.Stop(); deck.Visible=false; deck.SetMotion(false); hidden=true; activeFrames=frames; hiddenCpu=process.TotalProcessorTime; watch.Restart();
                    } else if (hidden && watch.Elapsed.TotalSeconds >= 5) {
                        process.Refresh(); text.AppendLine("Hidden: graphical timer enabled="+animation.Enabled+"; extra frames="+(frames-activeFrames)+"; radar clocks running="+deck.Cards.Count(c => c.Radar.MotionRunning)+
                            "; CPU total-machine="+((process.TotalProcessorTime-hiddenCpu).TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100).ToString("F3",CultureInfo.InvariantCulture)+"%");
                        sample.Stop(); host.Close();
                    }
                };
                host.Shown += delegate { deck.SetMotion(true); process.Refresh(); cpu=process.TotalProcessorTime; watch.Start(); animation.Start(); sample.Start(); };
                Application.Run(host);
            }
            for (int batch=0;batch<3;batch++) {
                GC.Collect(); GC.WaitForPendingFinalizers(); process.Refresh(); long memory=process.PrivateMemorySize64; int gdi=GetGuiResources(process.Handle,0);
                using (Form host=new Form()) using (MissionDeck deck=new MissionDeck()) using (Bitmap bitmap=new Bitmap(650,200)) {
                    host.ShowInTaskbar=false; host.StartPosition=FormStartPosition.Manual; host.Location=new Point(-32000,-32000); host.Controls.Add(deck); deck.Dock=DockStyle.Fill; host.Show();
                    for (int i=0;i<100;i++) {
                        host.ClientSize=new Size(i%2 == 0 ? 327 : 642,200);
                        deck.UpdateReport(Report(Now,Task("A","a",40),Task("B","b",30),Task("C","c",20)),Now); deck.DrawToBitmap(bitmap,deck.ClientRectangle);
                    }
                    host.Close();
                }
                GC.Collect(); GC.WaitForPendingFinalizers(); process.Refresh(); int after=GetGuiResources(process.Handle,0);
                text.AppendLine("Resize/update/dispose batch "+(batch+1)+" (100 cycles): private bytes "+memory+" -> "+process.PrivateMemorySize64+"; GDI "+gdi+" -> "+after);
            }
            text.AppendLine("Environment: "+Environment.OSVersion.VersionString+"; logical processors="+Environment.ProcessorCount);
            text.AppendLine("Synthetic components only; concurrent real Desktop missions and shell taskbar visuals remain FIELD REQUIRED.");
            return text.ToString();
        }
        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process,int flags);
    }
}
