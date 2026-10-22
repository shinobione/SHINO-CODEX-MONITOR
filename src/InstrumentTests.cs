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
                foreach(Control c in card.Controls.Cast<Control>().Where(c=>c.Visible))Assert(card.ClientRectangle.Contains(c.Bounds),"Card child bounds: "+c.GetType().Name+" "+c.Bounds+" / "+card.ClientRectangle);
                Label[] labels=card.Controls.OfType<Label>().Where(l=>l.Visible && l.Text.Length>0).ToArray();
                for(int i=0;i<labels.Length;i++)for(int j=i+1;j<labels.Length;j++)
                    Assert(!labels[i].Bounds.IntersectsWith(labels[j].Bounds),"Mission text overlaps: "+labels[i].Name+" / "+labels[j].Name);
            }
            foreach(Label label in Descendants(window).OfType<Label>().Where(l=>l.Visible)) {
                Assert(label.Parent.ClientRectangle.Contains(label.Bounds),"Text outside parent: "+label.Text);
                Assert(label.Height>=label.Font.Height,"Text height: "+label.Text);
                if(new []{"QuotaValue","QuotaReset","QuotaStatus","QuotaObserved","MissionState","MissionDuration"}.Contains(label.Name)) {
                    Size text=TextRenderer.MeasureText(label.Text,label.Font,new Size(10000,10000),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
                    Assert(text.Width<=label.Width-6,"Critical text truncates: "+label.Text+" ("+text.Width+" / "+label.Width+")");
                }
            }
            Assert(!window.Missions.HasOverflow || count>2,"Overflow below three missions");
        }
        internal static string Run()
        {
            StringBuilder result=new StringBuilder("SYNTHETIC FIXTURES / full native windows; scaled geometry, fonts and GDI+ (not physical mixed-DPI proof)\r\n");int passed=0,total=0;
            result.AppendLine("ENV virtual desktop="+SystemInformation.VirtualScreen+"; default max tracking size="+SystemInformation.MaxWindowTrackSize);
            Action<string,Action> check=(name,test)=>{total++;try{test();passed++;result.AppendLine("PASS "+name);}catch(Exception e){result.AppendLine("FAIL "+name+": "+e.Message);}};
            foreach(string layout in new [] {"Circular","Vertical","Compact","Micro"})
            foreach(int percent in new [] {100,125,150,200})
            foreach(int count in new [] {0,1,2,3}) {
                check(layout+" / "+percent+"% / "+count+" turns",delegate {
                    using(MonitorForm window=Window(layout,count,"Direct")) {
                        window.SetPresentationScale(percent/100f);window.Show();Application.DoEvents();BoundsCheck(window,count);
                        Size logical=layout=="Circular"?new Size(540,398):layout=="Vertical"?new Size(355,383):layout=="Compact"?new Size(670,289):new Size(484,230);
                        Assert(window.ClientSize==new Size((int)Math.Round(logical.Width*percent/100f),(int)Math.Round(logical.Height*percent/100f)),"Native capture size was constrained by desktop");
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
            check("Finish Circular promotes the radar within unchanged window bounds",delegate {
                using(MonitorForm window=Window("Circular",1,"Direct")) {
                    window.Show();Application.DoEvents();
                    Assert(window.ClientSize==new Size(540,398),"Circular grew instead of redistributing space");
                    int radar=window.Missions.Cards.Single().Radar.Width;
                    Assert(radar>=94 && radar<=110,"Circular radar is still secondary");
                }
            });
            check("Finish blocked warning is a readable integrated alert",delegate {
                using(MonitorForm window=Window("Circular",2,"Blocked")) {
                    window.Show();Application.DoEvents();
                    Label warning=Descendants(window).OfType<Label>().FirstOrDefault(l=>l.Text=="CODEX USAGE BLOCKED");
                    Assert(warning!=null && warning.Font.Size>=12,"Critical blocked readout is missing or too small");
                    Assert(Descendants(window).OfType<Label>().Any(l=>l.Text.Contains("LAST KNOWN")),"Cached provenance missing");
                }
            });
            foreach(string layout in new []{"Circular","Vertical","Compact","Micro"})
            foreach(int count in new []{1,2})
                check("Finish long labels and critical warnings / "+layout+" / "+count,delegate {
                    using(MonitorForm window=Window(layout,0,"Direct")) {
                        window.Show();Snapshot q=Quotas(false);q.Five.Remaining=100;q.Week.Remaining=0;
                        LiveTaskReport report=Tasks(count);
                        foreach(LiveTask t in report.Tasks) {t.Workspace="LONG-PROJECT-&-INSTRUMENTATION-WORKSPACE";t.Model="fixture-model-with-a-deliberately-long-name-for-layout-review";t.Effort="extra-high-fixture-effort";t.StartedUtc=Now.AddHours(-18).AddMinutes(-29);}
                        foreach(string state in new []{"Direct","Stale","Unverified","Unavailable"}) {
                            window.Present(state=="Unavailable"?null:q,report,Now,state);Application.DoEvents();BoundsCheck(window,count);
                        }
                        foreach(LiveTask t in report.Tasks) {t.IdentityUncertain=true;t.StartedUtc=Now.AddDays(1);}
                        window.Present(q,null,Now,"Unverified");
                        LiveTaskReport uncertain=new LiveTaskReport {CheckedUtc=Now};uncertain.Tasks.AddRange(report.Tasks.Select(t=>t.Copy()));
                        window.Present(q,uncertain,Now,"Unverified");Application.DoEvents();BoundsCheck(window,count);
                        Assert(window.Missions.Cards.All(c=>c.Controls.OfType<Label>().Single(l=>l.Name=="MissionTitle").Text.StartsWith(c.Mission.Number.ToString("00")+" / ")),"Visible mission number lost");
                        foreach(Label field in Descendants(window).OfType<Label>().Where(l=>l.Name=="MissionModel"||l.Name=="MissionEffort"))
                            Assert(!field.Visible || (field.AutoEllipsis && field.Height>=field.Font.Height && !field.Text.Contains("\n")),"Metadata wraps into a missing field");
                        Capture(window,Path.Combine(DirectoryFor("after"),layout+"-long-labels-"+count+"-100.png"),layout+" / intentional project and metadata ellipsis / critical state and duration remain visible");
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
            check("Oversized native capture survives a smaller tracking limit",delegate {
                using(MonitorForm window=Window("Compact",2,"Direct")) {
                    window.MaximumSize=new Size(900,900);window.SetPresentationScale(2);window.Show();Application.DoEvents();
                    Assert(window.Width<=900,"Fixture did not reproduce native size constraint");
                    window.MaximumSize=new Size(4096,4096);window.SetPresentationScale(2);Application.DoEvents();
                    Assert(window.ClientSize==new Size(1340,578),"Native 200% window truncated");BoundsCheck(window,2);
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
            check("Parity real stale classifier renders STALE",delegate {
                using(MonitorForm window=Window("Vertical",1,"Direct")) {
                    string state=QuotaHealth.State(Now.AddMinutes(-5),0,60,Now);
                    window.Present(Quotas(false),Tasks(1),Now,state);window.Show();Application.DoEvents();
                    Assert(Descendants(window).OfType<Label>().Any(l=>l.Text=="STALE QUOTA READING"),"Production Stale data mislabelled");
                }
            });
            check("Parity raw reliable zero is EXHAUSTED, fractional zero is not",delegate {
                using(MonitorForm window=Window("Vertical",1,"Direct")) {
                    Snapshot q=Quotas(false);q.Five.Remaining=0;window.Present(q,Tasks(1),Now,"Direct");window.Show();Application.DoEvents();
                    Assert(Descendants(window).OfType<Label>().Any(l=>l.Name=="QuotaProvenance" && l.Text=="EXHAUSTED"),"Raw zero lacks EXHAUSTED");
                    q.Five.Remaining=.4;window.Present(q,Tasks(1),Now,"Direct");
                    Assert(!Descendants(window).OfType<Label>().Any(l=>l.Text=="EXHAUSTED"),"Rounded zero falsely exhausted");
                    q.Five.Remaining=0;window.Present(q,Tasks(1),Now,"Stale data");
                    Assert(!Descendants(window).OfType<Label>().Any(l=>l.Text=="EXHAUSTED"),"Historical zero falsely current");
                }
            });
            check("Parity stable simultaneous identity accents",delegate {
                using(MonitorForm window=Window("Circular",2,"Direct")) {
                    window.Show();Application.DoEvents();
                    var accent=typeof(ActivityGlyph).GetProperty("Accent",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    Assert(accent!=null,"Radar has no independent accent");
                    MissionCard[] cards=window.Missions.Cards.OrderBy(c=>c.Mission.Number).ToArray();
                    Color a=(Color)accent.GetValue(cards[0].Radar,null),b=(Color)accent.GetValue(cards[1].Radar,null);
                    Assert(a!=b,"Simultaneous missions share one color");
                    LiveTaskReport report=Tasks(2);report.Tasks[0].Phase=LiveTaskPhase.Completed;report.Tasks[0].FinishedUtc=Now;report.Tasks[0].LastWriteUtc=Now;
                    window.Present(Quotas(true),report,Now,"Direct");
                    Assert((Color)accent.GetValue(cards[1].Radar,null)==b && cards[1].Radar.State==RadarState.Active,"A completion/block changed B");
                }
            });
            foreach(string layout in new []{"Circular","Vertical","Compact","Micro"})
            foreach(int count in new []{1,2})check("Parity opt-in geometry and DPI / "+layout+" / "+count,delegate {
                var enabled=typeof(Preferences).GetProperty("ObsidianLayout");Assert(enabled!=null,"Missing explicit opt-in");
                Preferences p=Preferences.Defaults();p.Layout=layout;p.CircularWidth=620;p.Top=false;p.Opacity=1;p.TaskAnimations=false;
                Assert(!(bool)enabled.GetValue(p,null),"Existing defaults silently opt in");enabled.SetValue(p,true,null);
                Size expected=layout=="Circular"?new Size(680,480):layout=="Vertical"?new Size(360,580):layout=="Compact"?new Size(640,260):new Size(484,230);
                using(MonitorForm window=new MonitorForm(p)) {
                    window.Location=new Point(-30000,-30000);window.Present(Quotas(false),Tasks(count),Now,"Direct");window.Show();Application.DoEvents();
                    foreach(float scale in new []{1f,1.25f,1.5f,2f,1f}) {
                        window.SetPresentationScale(scale);Application.DoEvents();BoundsCheck(window,count);
                        Assert(window.ClientSize==new Size((int)Math.Round(expected.Width*scale),(int)Math.Round(expected.Height*scale)),"Parity composition geometry differs");
                    }
                    Assert(p.CircularWidth==620,"Recorded legacy width overwritten");
                }
            });
            check("Diffuse halo has bright filled center and transparent boundary",delegate {
                using(Bitmap halo=InstrumentStyle.Halo(64,InstrumentStyle.Teal))
                    Assert(halo.GetPixel(32,32).A>halo.GetPixel(60,32).A+100,"Halo falloff inverted");
            });
            check("Compact quota is visible in complete native bitmap",delegate {
                foreach(int count in new []{0,1,2})using(MonitorForm window=ParityWindow("Compact",count,false))using(Bitmap image=new Bitmap(window.Width,window.Height)) {
                    window.Show();Application.DoEvents();window.DrawToBitmap(image,window.ClientRectangle);
                    int bright=0;for(int y=52;y<80;y++)for(int x=300;x<370;x++){Color c=image.GetPixel(x,y);if(c.R>150 && c.G<150)bright++;}
                    Assert(bright>20,"Quota readout occluded by activity surface");
                }
            });
            check("Parity preferences load legacy JSON and round-trip without replacing geometry",delegate {
                string path=Path.Combine(DirectoryFor("preferences"),"synthetic-settings.json");
                LocalStore isolated=(LocalStore)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(LocalStore));
                typeof(LocalStore).GetField("settingPath",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(isolated,path);
                File.WriteAllText(path,"{\"CircularWidth\":620,\"X\":123,\"Y\":234,\"Color0\":\"#AB3456\"}");
                Preferences p=isolated.Load();Assert(!p.ObsidianLayout && p.CircularWidth==620,"Legacy JSON opted in or lost width");
                p.ObsidianLayout=true;p.ObsidianCircularWidth=600;isolated.Save(p);Preferences restored=isolated.Load();
                Assert(restored.ObsidianLayout && restored.ObsidianCircularWidth==600 && restored.CircularWidth==620 && restored.X==123 && restored.Y==234 && restored.Color0=="#AB3456","Appearance round-trip overwrote settings");
                using(MonitorForm window=new MonitorForm(restored)) {
                    window.Show();window.Present(Quotas(false),Tasks(1),Now,"Direct");
                    restored.ObsidianLayout=false;window.SetPresentationScale(1);Assert(window.Width==620,"Disable lost recorded geometry");
                    restored.ObsidianLayout=true;window.SetPresentationScale(1);Assert(window.Width==600,"Enable lost opt-in width");
                    Assert(restored.X==123 && restored.Y==234 && restored.Color0=="#AB3456","Toggle replaced user preferences");
                }
            });
            foreach(string layout in new []{"Circular","Vertical","Compact","Micro"})
                check("Parity degraded states and contradictory observations / "+layout,delegate {
                    using(MonitorForm window=ParityWindow(layout,2,false)) {
                        window.Show();Application.DoEvents();
                        foreach(float scale in new []{1f,1.25f,1.5f,2f})foreach(string state in new []{"Direct","Stale data","Unverified","Unavailable","Blocked"}) {
                            Snapshot q=Quotas(state=="Blocked");q.Five.Remaining=0;
                            window.Present(state=="Unavailable"?null:q,Tasks(2),Now,state=="Blocked"?"Direct":state);window.SetPresentationScale(scale);BoundsCheck(window,2);
                            bool exhausted=Descendants(window).OfType<Label>().Any(l=>l.Text=="EXHAUSTED");
                            Assert(exhausted==(state=="Direct"),"Cached or absent zero presented as confirmed exhausted");
                            Assert(window.Missions.Cards.All(c=>c.Radar.State==RadarState.Active),"Quota state suspended observed task");
                        }
                        LiveTaskReport conflict=Tasks(2);conflict.Tasks[0].Phase=LiveTaskPhase.Completed;conflict.Tasks[0].FinishedUtc=Now.AddDays(1);conflict.Tasks[1].IdentityUncertain=true;
                        window.Present(Quotas(false),conflict,Now,"Direct");BoundsCheck(window,2);
                        Assert(window.Missions.Cards.All(c=>c.Radar.State==RadarState.Unverified),"Contradictory event invented completion or activity");
                        window.Present(null,null,Now,"Unavailable");window.SetPresentationScale(1);
                        Capture(window,Path.Combine(DirectoryFor("parity"),layout+"-Unavailable-100.png"),layout+" / unavailable / no invented missions");
                    }
                });
            check("Blocked material resets without moving quotas or suspending missions",delegate {
                using(MonitorForm window=ParityWindow("Circular",2,true))using(Bitmap image=new Bitmap(window.Width,window.Height)) {
                    window.Show();Application.DoEvents();window.DrawToBitmap(image,window.ClientRectangle);Color warning=image.GetPixel(300,20);
                    Assert(warning.R>warning.B,"Blocked title bar lacks alert material");
                    window.Present(Quotas(false),Tasks(2),Now,"Direct");window.DrawToBitmap(image,window.ClientRectangle);Color direct=image.GetPixel(300,20);
                    Assert(direct.B>direct.R && window.Missions.Cards.All(c=>c.Radar.State==RadarState.Active),"Warning material or mission state persisted after recovery");
                }
            });
            foreach(int width in new []{470,720})foreach(int count in new []{1,2})
                check("Parity width and reset preferences / "+width+" / "+count,delegate {
                    Preferences p=Preferences.Defaults();p.ObsidianLayout=true;p.ObsidianCircularWidth=width;p.Layout="Circular";p.Top=false;p.TaskAnimations=false;
                    using(MonitorForm window=new MonitorForm(p)) {
                        window.Location=new Point(-30000,-30000);window.Show();
                        foreach(float scale in new []{1f,1.25f,1.5f,2f})foreach(string reset in new []{"both","absolute","relative","hidden"}) {
                            LiveTaskReport longRun=Tasks(count);foreach(LiveTask t in longRun.Tasks)t.StartedUtc=Now.AddHours(-20).AddMinutes(-29);
                            p.ResetStyle=reset;window.Present(Quotas(false),longRun,Now,"Direct");window.SetPresentationScale(scale);BoundsCheck(window,count);
                            window.Present(Quotas(false),longRun,Now,"Stale data");BoundsCheck(window,count);
                            foreach(LiveTask t in longRun.Tasks)t.IdentityUncertain=true;
                            LiveTaskReport uncertain=new LiveTaskReport {CheckedUtc=Now};uncertain.Tasks.AddRange(longRun.Tasks.Select(t=>t.Copy()));
                            window.Present(Quotas(false),uncertain,Now,"Direct");BoundsCheck(window,count);
                        }
                        Assert(p.CircularWidth==540 && p.ObsidianCircularWidth==width,"Width preferences crossed");
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
                    g.DrawString("SYNTHETIC TEST DATA | "+label,font,Brushes.White,5,window.Height+7);
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
                foreach(string state in new []{"Blocked","Offline","Stale","Unverified","Unavailable"})
                using(MonitorForm window=Window(layout,state=="Stale"?2:0,state=="Offline"?"Unverified":state=="Stale"?"Unverified":state)) {
                    if(state=="Offline") {
                        Snapshot q=Quotas(false);q.CapturedUtc=Now.AddMinutes(-20);window.Present(q,null,Now,"Unverified");
                    } else if(state=="Stale") {
                        Snapshot q=Quotas(false);q.CapturedUtc=Now.AddMinutes(-20);LiveTaskReport report=Tasks(2);report.CheckedUtc=Now.AddMinutes(-2);window.Present(q,report,Now,"Stale");
                    } else if(state=="Unverified") {
                        window.Present(null,null,Now,"Unverified");
                        LiveTaskReport uncertain=Tasks(1);uncertain.Tasks[0].IdentityUncertain=true;window.Present(Quotas(false),uncertain,Now,"Unverified");
                    } else if(state=="Unavailable")window.Present(null,null,Now,"Unavailable");
                    window.SetPresentationScale(percent/100f);
                    Capture(window,Path.Combine(directory,layout+"-"+state+"-"+percent+".png"),layout+" / "+state+" / "+percent+"% synthetic scale");
                }
            }
            using(MonitorForm window=Window("Circular",2,"Blocked"))Capture(window,Path.Combine(directory,"Blocked-with-2-observed-tasks-100.png"),"Blocked quota / two independently observed active tasks");
            CaptureParity();
        }
        internal static MonitorForm ParityWindow(string layout,int count,bool blocked)
        {
            Preferences p=Preferences.Defaults();p.ObsidianLayout=true;p.BackgroundColor="#080D14";p.Layout=layout;p.Top=false;p.InTaskbar=false;p.TaskAnimations=false;p.Opacity=1;
            // Explicit reference-palette fixture; production always retains configured colors.
            p.Color0="#F35A65";p.Color11="#E9B85C";
            MonitorForm window=new MonitorForm(p);window.Location=new Point(-30000,-30000);
            Snapshot q=Quotas(blocked);q.Five.Remaining=blocked?0:3;q.Week.Remaining=blocked?16:17;
            LiveTaskReport report=Tasks(count);foreach(LiveTask task in report.Tasks){task.Model="gpt-6.1-sol";task.Effort="Medium";}
            window.Present(q,report,Now,"Direct");return window;
        }
        internal static void CaptureParity()
        {
            string directory=DirectoryFor("parity");
            foreach(string layout in new []{"Circular","Vertical","Compact","Micro"})
            foreach(int scale in new []{100,125,150,200})
            foreach(int count in new []{1,2}) {
                foreach(bool blocked in new []{false,true})using(MonitorForm window=ParityWindow(layout,count,blocked)) {
                    window.SetPresentationScale(scale/100f);window.Show();Application.DoEvents();
                    int phase=0;foreach(MissionCard card in window.Missions.Cards)card.Radar.SetSharedMotion(false,1,phase++*.25);
                    Capture(window,Path.Combine(directory,layout+(blocked?"-Blocked":"")+"-"+count+"-"+scale+".png"),layout+" / reference-palette fixture / "+(blocked?"BLOCKED + independently active tasks":"Direct 3% / 17%")+" / "+count+" turns / "+scale+"%");
                }
            }
            foreach(string layout in new []{"Circular","Vertical","Compact","Micro"})
            foreach(string state in new []{"Exhausted","Fractional-zero","Stale data","Unverified"})using(MonitorForm window=ParityWindow(layout,2,false)) {
                Snapshot q=Quotas(false);q.Five.Remaining=state=="Fractional-zero"?.4:0;
                LiveTaskReport report=Tasks(2);if(state=="Unverified")foreach(LiveTask task in report.Tasks)task.IdentityUncertain=true;
                window.Present(q,report,Now,state=="Exhausted" || state=="Fractional-zero"?"Direct":state);window.SetPresentationScale(1);
                Capture(window,Path.Combine(directory,layout+"-"+state.Replace(" ","-")+"-100.png"),layout+" / "+state+" / synthetic truth fixture");
            }
            foreach(string layout in new []{"Circular","Vertical","Compact","Micro"})using(MonitorForm window=ParityWindow(layout,2,false)) {
                LiveTaskReport report=Tasks(2);report.Tasks[0].Phase=LiveTaskPhase.Completed;report.Tasks[0].FinishedUtc=Now;report.Tasks[0].LastWriteUtc=Now;
                window.Present(Quotas(false),report,Now,"Direct");
                Capture(window,Path.Combine(directory,layout+"-A-completed-B-active-100.png"),layout+" / A completed / B active / independent identity colors");
            }
        }
        [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process,int kind);
        internal static string Performance(bool parity=false)
        {
            StringBuilder output=new StringBuilder("SYNTHETIC / complete Circular window, three radars, forced full-window bitmap at each tick (includes static gauges). No live collectors.\r\n");
            Process process=Process.GetCurrentProcess();
            if(parity)output.Clear().AppendLine("SYNTHETIC / opt-in 680x480 Circular, two visible independent radars; forced full-window bitmap; no collectors.");
            using(MonitorForm window=parity?ParityWindow("Circular",2,false):Window("Circular",3,"Direct"))
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
                        output.AppendLine("Visible RAM working MiB="+(process.WorkingSet64/1048576d).ToString("F2",CultureInfo.InvariantCulture)+"; private MiB="+(process.PrivateMemorySize64/1048576d).ToString("F2",CultureInfo.InvariantCulture)+"; GDI="+GetGuiResources(process.Handle,0)+"; USER="+GetGuiResources(process.Handle,1));
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
            using(MonitorForm warm=parity?ParityWindow("Micro",1,false):Window("Micro",1,"Direct")){warm.Show();Application.DoEvents();}
            GC.Collect();GC.WaitForPendingFinalizers();Application.DoEvents();
            int gdi=GetGuiResources(process.Handle,0),user=GetGuiResources(process.Handle,1);
            process.Refresh();long bytes=process.PrivateMemorySize64;
            for(int i=0;i<80;i++)using(MonitorForm window=parity?ParityWindow(i%2==0?"Circular":"Micro",2,false):Window(i%2==0?"Circular":"Micro",2,"Direct")) {
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
