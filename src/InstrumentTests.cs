using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    // Full production window, isolated synthetic inputs. No LocalStore or reader.
    internal static class InstrumentTests
    {
        private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        private static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
        {foreach(Control c in root.Controls){yield return c;foreach(Control child in Descendants(c))yield return child;}}
        private static void BoundsCheck(MonitorForm window,int count)
        {
            Assert(window.Missions.Count==count,"Unexpected mission count");
            foreach(MissionCard card in window.Missions.Cards) {
                ScrollableControl viewport=card.Parent as ScrollableControl;
                Assert(viewport!=null && !viewport.HorizontalScroll.Visible && !viewport.VerticalScroll.Visible,"Native scrollbar visible");
                if(count<=2)Assert(viewport.ClientRectangle.Contains(card.Bounds),"Card outside viewport");
                foreach(Control c in card.Controls.Cast<Control>().Where(c=>c.Visible))Assert(card.ClientRectangle.Contains(c.Bounds),"Card child bounds: "+c.GetType().Name);
            }
            foreach(Label label in Descendants(window).OfType<Label>().Where(l=>l.Visible && !(l.Parent is MissionCard && count>2))) {
                Assert(label.Parent.ClientRectangle.Contains(label.Bounds),"Text outside parent: "+label.Text);
                Assert(label.Height>=label.Font.Height,"Text height: "+label.Text);
            }
            Assert(!window.Missions.HasOverflow || count>2,"Overflow below three missions");
        }
        internal static string Run()
        {
            StringBuilder result=new StringBuilder("SYNTHETIC FIXTURES / full native windows; scaled geometry, fonts and GDI+ (not physical mixed-DPI proof)\r\n");int passed=0,total=0;
            Action<string,Action> check=(name,test)=>{total++;try{test();passed++;result.AppendLine("PASS "+name);}catch(Exception e){result.AppendLine("FAIL "+name+": "+e.Message);}};
            foreach(string layout in new [] {"Circular","Vertical","Compact","Micro"})
            foreach(int percent in new [] {100,125,150,200})
            foreach(int count in new [] {0,1,2,3}) {
                check(layout+" / "+percent+"% / "+count+" turns",delegate {
                    using(MonitorForm window=Window(layout,count,"Direct")) {
                        window.SetPresentationScale(percent/100f);window.Show();Application.DoEvents();BoundsCheck(window,count);
                        if(count==1) {
                            int minimum=layout=="Circular"?72:layout=="Vertical"?56:layout=="Compact"?42:28;
                            Assert(window.Missions.Cards.Single().Radar.Width>=(int)(minimum*percent/100f)-1,"Radar below instrument size");
                        }
                        if(layout=="Micro")Assert(window.ClientSize==new Size((int)Math.Round(484*percent/100f),(int)Math.Round(230*percent/100f)),"Micro geometry changed");
                        if(count==3 && window.Missions.HasOverflow) {
                            window.Missions.ScrollBy(5000);Application.DoEvents();
                            Assert(window.Missions.Cards.OrderBy(c=>c.Mission.Number).Last().Bottom<=window.Missions.Cards.First().Parent.Height,"Last mission unreachable");
                        }
                    }
                });
            }
            check("A completion keeps B identity and phase",delegate {
                using(MonitorForm window=Window("Circular",2,"Direct",true)) {
                    window.Show();Application.DoEvents();MissionCard b=window.Missions.Cards.OrderBy(c=>c.Mission.Number).Last();
                    window.Missions.SetMotion(true);window.Missions.Advance();double before=b.Radar.ElapsedSeconds;
                    LiveTaskReport report=Tasks(2);report.Tasks[0].Phase=LiveTaskPhase.Completed;report.Tasks[0].FinishedUtc=Now;report.Tasks[0].LastWriteUtc=Now;
                    window.Present(Quotas(false),report,Now,"Direct");window.Missions.ConfirmCompletion(report.Tasks[0]);window.Missions.SetMotion(true);window.Missions.Advance();
                    Assert(ReferenceEquals(b,window.Missions.Cards.OrderBy(c=>c.Mission.Number).Last()) && b.Radar.State==RadarState.Active && b.Radar.ElapsedSeconds>=before,"B restarted");
                    Assert(window.Missions.Cards.First().Radar.Completing,"A completion feedback missing");
                    window.Missions.Cards.First().Radar.FinishMotion();
                    Capture(window,Path.Combine(DirectoryFor("after"),"A-completed-B-active-100.png"),"A COMPLETED / B ACTIVE / synthetic transition");
                    window.Hide();window.Missions.SetMotion(false);Assert(window.Missions.Cards.All(c=>!c.Radar.MotionRunning),"Hidden clock running");
                }
            });
            check("Blocked quotas do not change observed JSONL missions",delegate {
                using(MonitorForm window=Window("Circular",2,"Blocked")) {
                    window.Show();Application.DoEvents();Assert(window.Missions.Cards.All(c=>c.Radar.State==RadarState.Active),"Blocked quotas muted independent task observations");
                    Assert(Descendants(window).OfType<Label>().Any(l=>l.Text.Contains("USAGE BLOCKED")),"Blocked state absent");
                }
            });
            check("Stale and unavailable never animate",delegate {
                using(MonitorForm window=Window("Circular",2,"Unverified")) {
                    LiveTaskReport report=Tasks(2);report.CheckedUtc=Now.AddMinutes(-2);window.Present(Quotas(false),report,Now,"Unverified");
                    window.Show();window.Missions.SetMotion(true);Assert(window.Missions.Cards.All(c=>c.Radar.State==RadarState.Stale && !c.Radar.MotionRunning),"Stale animation");
                    window.Present(null,null,Now,"Unavailable");Assert(window.Missions.Count==0 && !window.Missions.NeedsFrames,"Unavailable invented task");
                }
            });
            check("Custom preferences survive render and scale changes",delegate {
                Preferences p=Preferences.Defaults();p.BackgroundColor="#102C36";p.Color26="#BAA177";p.Opacity=.85;p.Layout="Circular";p.TaskAnimations=false;
                using(MonitorForm window=new MonitorForm(p)) {
                    window.Present(Quotas(false),Tasks(1),Now,"Direct");window.SetPresentationScale(2);window.SetPresentationScale(1);
                    Assert(p.BackgroundColor=="#102C36" && p.Color26=="#BAA177" && p.Opacity==.85 && p.CircularWidth==540,"Preferences mutated");
                    Assert(window.BackColor==ColorTranslator.FromHtml(p.BackgroundColor) && window.Opacity==.85,"Custom appearance not applied");
                }
            });
            foreach(string layout in new []{"Circular","Vertical","Compact","Micro"})
                check(layout+" rescale/restore cycle",delegate {
                    using(MonitorForm window=Window(layout,2,"Direct")) {
                        window.Show();
                        foreach(float scale in new []{1f,1.25f,1.5f,2f,1.25f,1f}) {
                            window.SetPresentationScale(scale);Application.DoEvents();BoundsCheck(window,2);
                        }
                    }
                });
            check("Production motion timer stops hidden/minimized/suspended",delegate {
                using(MonitorForm window=Window("Circular",2,"Direct",true)) {
                    window.Show();Application.DoEvents();Assert(window.MotionTimerRunning,"Visible clock not running");
                    window.Hide();Assert(!window.MotionTimerRunning,"Hidden timer running");window.Show();
                    window.WindowState=FormWindowState.Minimized;Application.DoEvents();Assert(!window.MotionTimerRunning,"Minimized timer running");
                    window.WindowState=FormWindowState.Normal;Application.DoEvents();Assert(window.MotionTimerRunning,"Restored clock stopped");
                    System.Reflection.FieldInfo suspended=typeof(MonitorForm).GetField("suspended",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                    suspended.SetValue(window,true);window.Present(Quotas(false),Tasks(2),Now,"Direct");Assert(!window.MotionTimerRunning,"Suspended timer running");
                }
            });
            foreach(int width in new []{470,720})
            foreach(int percent in new []{100,125,150,200})
            foreach(int count in new []{1,2})
                check("Circular width "+width+" / "+percent+"% / "+count+" turns",delegate {
                    Preferences p=Preferences.Defaults();p.CircularWidth=width;p.TaskAnimations=false;p.Top=false;
                    using(MonitorForm window=new MonitorForm(p)) {
                        window.Location=new Point(-30000,-30000);window.Present(Quotas(false),Tasks(count),Now,"Direct");
                        window.SetPresentationScale(percent/100f);window.Show();Application.DoEvents();BoundsCheck(window,count);
                        Assert(p.CircularWidth==width,"Stored width changed");
                    }
                });
            CaptureAll("after");result.AppendLine(passed+"/"+total+" instrument checks passed");
            if(passed==total)result.AppendLine("All instrument checks passed. Owner visual acceptance and physical mixed-DPI: HOLD.");return result.ToString();
        }
        internal static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-20T12:00:00Z");
        internal static Snapshot Quotas(bool blocked)
        {
            return new Snapshot { CapturedUtc=Now.AddSeconds(-24), UsageAllowed=!blocked,
                Five=new QuotaWindow { Minutes=300, Remaining=38, ResetUtc=Now.AddHours(2.5) },
                Week=new QuotaWindow { Minutes=10080, Remaining=67, ResetUtc=Now.AddDays(3) } };
        }
        internal static LiveTaskReport Tasks(int count)
        {
            LiveTaskReport report=new LiveTaskReport { CheckedUtc=Now, Message="Synthetic fixture" };
            for (int i=0;i<count;i++) report.Tasks.Add(new LiveTask { Id="fixture-"+i,
                SessionKey="synthetic-"+i, Workspace=i==0 ? "OBSIDIAN-NATIVE" : i==1 ? "RADAR-RENDER" : "LAYOUT-CHECK",
                StartedUtc=Now.AddMinutes(-12-i), LastWriteUtc=Now.AddSeconds(-2), Phase=LiveTaskPhase.Active,
                Model="fixture-model", Effort="high", IsRootConfirmed=true });
            return report;
        }
        internal static MonitorForm Window(string layout, int count, string state, bool motion=false)
        {
            Preferences p=Preferences.Defaults(); p.Layout=layout; p.Top=false; p.InTaskbar=false;
            p.TaskAnimations=motion; p.Opacity=1;
            MonitorForm window=new MonitorForm(p);
            window.Location=new Point(-30000,-30000);
            window.Present(Quotas(state=="Blocked"),Tasks(count),Now,state=="Blocked" ? "Direct" : state);
            return window;
        }
        internal static string DirectoryFor(string stage)
        { string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"instrument-qa",stage); Directory.CreateDirectory(path); return path; }
        internal static void Capture(MonitorForm window, string path, string label)
        {
            window.Show(); Application.DoEvents(); window.Refresh(); Application.DoEvents();
            using (Bitmap surface=new Bitmap(window.Width,window.Height))
            using (Bitmap evidence=new Bitmap(window.Width,window.Height+32)) {
                window.DrawToBitmap(surface,new Rectangle(Point.Empty,window.Size));
                using (Graphics g=Graphics.FromImage(evidence))
                using (Font font=new Font("Segoe UI",8,FontStyle.Bold)) {
                    g.Clear(Color.FromArgb(4,7,11)); g.DrawImageUnscaled(surface,0,0);
                    g.DrawString("SYNTHETIC FIXTURE | "+label,font,Brushes.White,5,window.Height+7);
                }
                evidence.Save(path,ImageFormat.Png);
            }
        }
        internal static void CaptureAll(string stage)
        {
            string directory=DirectoryFor(stage);
            foreach(string layout in new [] {"Circular","Vertical","Compact","Micro"})
            foreach(int percent in new [] {100,125,150,200}) {
                foreach(int count in new []{0,1,2,3})
                using(MonitorForm window=Window(layout,count,"Direct")) {
                    window.SetPresentationScale(percent/100f);
                    Capture(window,Path.Combine(directory,layout+"-"+count+"-"+percent+".png"),layout+" / "+count+" turns / "+percent+"% synthetic scale");
                }
                foreach(string state in new []{"Blocked","Offline","Stale"})
                using(MonitorForm window=Window(layout,state=="Stale"?2:0,state=="Offline"?"Unverified":state=="Stale"?"Unverified":state)) {
                    if(state=="Offline") {
                        Snapshot q=Quotas(false);q.CapturedUtc=Now.AddMinutes(-20);window.Present(q,null,Now,"Unverified");
                    } else if(state=="Stale") {
                        Snapshot q=Quotas(false);q.CapturedUtc=Now.AddMinutes(-20);LiveTaskReport report=Tasks(2);report.CheckedUtc=Now.AddMinutes(-2);window.Present(q,report,Now,"Unverified");
                    }
                    window.SetPresentationScale(percent/100f);
                    Capture(window,Path.Combine(directory,layout+"-"+state+"-"+percent+".png"),layout+" / "+state+" / "+percent+"% synthetic scale");
                }
            }
            using(MonitorForm window=Window("Circular",2,"Blocked"))Capture(window,Path.Combine(directory,"Blocked-with-2-observed-tasks-100.png"),"Blocked quota / two independently observed active tasks");
        }
        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process,int kind);
        internal static string Performance()
        {
            StringBuilder output=new StringBuilder("SYNTHETIC / complete Circular window, three radars, forced full-window bitmap at each tick (includes static gauges). No live collectors.\r\n");
            Process process=Process.GetCurrentProcess();
            using(MonitorForm window=Window("Circular",3,"Direct"))
            using(Bitmap frame=new Bitmap(window.Width,window.Height))
            using(System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer { Interval=34 })
            using(System.Windows.Forms.Timer sample=new System.Windows.Forms.Timer { Interval=250 }) {
                int frames=0,visibleFrames=0;bool hidden=false;Stopwatch watch=new Stopwatch();TimeSpan cpu=TimeSpan.Zero,hiddenCpu=TimeSpan.Zero;
                timer.Tick+=delegate {window.Missions.Advance();window.DrawToBitmap(frame,window.ClientRectangle);frames++;};
                sample.Tick+=delegate {
                    if(!hidden && watch.Elapsed.TotalSeconds>=30) {
                        process.Refresh();double used=(process.TotalProcessorTime-cpu).TotalSeconds;
                        output.AppendLine("Visible: "+frames+" frames / "+watch.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture)+" s; FPS="+(frames/watch.Elapsed.TotalSeconds).ToString("F2",CultureInfo.InvariantCulture)+
                            "; CPU total-machine="+(used/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100).ToString("F3",CultureInfo.InvariantCulture)+"%; single-core="+(used/watch.Elapsed.TotalSeconds*100).ToString("F2",CultureInfo.InvariantCulture)+"%");
                        visibleFrames=frames;timer.Stop();window.Hide();window.Missions.SetMotion(false);hidden=true;hiddenCpu=process.TotalProcessorTime;watch.Restart();
                    } else if(hidden && watch.Elapsed.TotalSeconds>=5) {
                        process.Refresh();output.AppendLine("Hidden: "+(frames-visibleFrames)+" frames; running radars="+window.Missions.Cards.Count(c=>c.Radar.MotionRunning)+
                            "; CPU total-machine="+((process.TotalProcessorTime-hiddenCpu).TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100).ToString("F3",CultureInfo.InvariantCulture)+"%");
                        sample.Stop();Application.ExitThread();
                    }
                };
                window.Show();Application.DoEvents();window.Missions.SetMotion(true);window.DrawToBitmap(frame,window.ClientRectangle);
                process.Refresh();cpu=process.TotalProcessorTime;watch.Start();timer.Start();sample.Start();Application.Run();
            }
            // Warm caches/runtime, then bound disposal and resize across whole windows.
            using(MonitorForm warm=Window("Micro",1,"Direct")){warm.Show();Application.DoEvents();}
            GC.Collect();GC.WaitForPendingFinalizers();Application.DoEvents();
            int gdi=GetGuiResources(process.Handle,0),user=GetGuiResources(process.Handle,1);
            process.Refresh();long bytes=process.PrivateMemorySize64;
            for(int i=0;i<80;i++)using(MonitorForm window=Window(i%2==0?"Circular":"Micro",2,"Direct")) {
                window.Show();window.SetPresentationScale(1.5f);window.SetPresentationScale(1);Application.DoEvents();
                using(Bitmap b=new Bitmap(window.Width,window.Height))window.DrawToBitmap(b,window.ClientRectangle);
            }
            GC.Collect();GC.WaitForPendingFinalizers();Application.DoEvents();process.Refresh();
            int afterGdi=GetGuiResources(process.Handle,0),afterUser=GetGuiResources(process.Handle,1);
            output.AppendLine("80 full-window create/scale/render/dispose cycles: GDI "+gdi+" -> "+afterGdi+"; USER "+user+" -> "+afterUser+
                "; private MiB "+(bytes/1048576d).ToString("F2",CultureInfo.InvariantCulture)+" -> "+(process.PrivateMemorySize64/1048576d).ToString("F2",CultureInfo.InvariantCulture));
            output.AppendLine("Logical processors: "+Environment.ProcessorCount+". Physical mixed-DPI and owner visual sign-off: NOT RUN / HOLD.");
            if(afterGdi-gdi>4 || afterUser-user>4)throw new Exception("Full-window GDI/USER handle growth");
            return output.ToString();
        }
    }
}
