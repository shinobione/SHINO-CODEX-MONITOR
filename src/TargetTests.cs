using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;

namespace ShinoCodexMonitor
{
    // Isolated native HWND diagnostics: no store, collectors, notifications or user data.
    internal static class TargetTests
    {
        private static void Assert(bool value,string message){if(!value)throw new Exception(message);}
        internal static IEnumerable<Control> Walk(Control root)
        {foreach(Control child in root.Controls){yield return child;foreach(Control nested in Walk(child))yield return nested;}}
        internal static Snapshot Quotas(bool blocked=false)
        {Snapshot q=InstrumentTests.Quotas(blocked);q.Five.Remaining=blocked?0:3;q.Week.Remaining=blocked?16:17;q.ResetsAvailable=2;return q;}
        internal static MonitorForm TargetWindow(string layout,bool motion=false,bool obsidian=true)
        {
            Preferences p=Preferences.Defaults();p.ObsidianLayout=obsidian;p.BackgroundColor=obsidian?"#080D14":"#111926";p.Color0="#F35A65";p.Color11="#E9B85C";p.Layout=layout;p.Top=false;p.InTaskbar=false;p.TaskAnimations=motion;p.Opacity=1;
            MonitorForm window=new MonitorForm(p);window.Location=new Point(-30000,-30000);return window;
        }
        internal static void Capture()
        {
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"target-qa");Directory.CreateDirectory(folder);
            foreach(bool obsidian in new[]{false,true})foreach(string layout in new []{"Circular","Vertical","Compact","Micro"})foreach(string variant in new []{"single","double","A-completed-B-active","blocked-unverified","blocked-active","stale","unavailable","unverified"}) {
                bool blocked=variant.StartsWith("blocked");int count=variant=="single" || variant=="blocked-unverified"?1:2;
                using(MonitorForm window=TargetWindow(layout,false,obsidian)) {
                    LiveTaskReport tasks=InstrumentTests.Tasks(count);
                    foreach(LiveTask t in tasks.Tasks){t.Model="gpt-6.1-sol";t.Effort="Medium";t.StartedUtc=InstrumentTests.Now.AddSeconds(-79);}
                    tasks.Tasks[0].Workspace="SHINO-TV-V06-AUDIT";
                    if(variant=="A-completed-B-active"){tasks.Tasks[0].Phase=LiveTaskPhase.Completed;tasks.Tasks[0].FinishedUtc=InstrumentTests.Now;tasks.Tasks[0].LastWriteUtc=InstrumentTests.Now;}
                    if(variant=="blocked-unverified" || variant=="unverified")foreach(LiveTask t in tasks.Tasks)t.IdentityUncertain=true;
                    if(variant=="stale")window.Present(Quotas(),tasks,InstrumentTests.Now,"Stale data");
                    else if(variant=="unavailable")window.Present(null,tasks,InstrumentTests.Now,"Unavailable");
                    else if(variant=="unverified")window.Present(null,tasks,InstrumentTests.Now,"Unverified");
                    else window.Present(Quotas(blocked),tasks,InstrumentTests.Now,"Direct");
                    window.SetPresentationScale(1);
                    window.Show();Application.DoEvents();int phase=0;foreach(MissionCard c in window.Missions.Cards)c.Radar.SetSharedMotion(false,1,phase++*.25);
                    if(layout=="Vertical" && variant=="single")File.WriteAllLines(Path.Combine(folder,"vertical-controls.txt"),Walk(window).Select(c=>c.GetType().Name+" "+c.Text+" "+c.Bounds+" visible="+c.Visible));
                    string family=obsidian?"Obsidian":"Classic";
                    InstrumentTests.Capture(window,Path.Combine(folder,family+"-"+layout+"-"+variant+"-100.png"),family+" / "+layout+" / "+variant+" / explicit target fixture");
                }
            }
        }
        // Samples the composed native WinForms client surface; this remains usable on headless CI.
        // No owner window is opened, closed or captured.
        internal static void Video()
        {
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"motion");Directory.CreateDirectory(root);
            foreach(string layout in new[]{"Vertical","Compact"})using(MonitorForm window=TargetWindow(layout,true)) {
                string folder=Path.Combine(root,layout);Directory.CreateDirectory(folder);
                window.Location=new Point(Screen.PrimaryScreen.WorkingArea.Left+40,Screen.PrimaryScreen.WorkingArea.Top+40);window.TopMost=true;
                Label mark=new InstrumentLabel {Text="SYNTHETIC QA / NATIVE MOTION",ForeColor=InstrumentStyle.Amber,BackColor=InstrumentStyle.Titlebar};
                window.Controls.Add(mark);mark.SetBounds(31,8,170,24);InstrumentStyle.Type(mark,InstrumentType.Metadata);mark.BringToFront();
                DateTimeOffset epoch=InstrumentTests.Now;LiveTaskReport fixture=InstrumentTests.Tasks(2);foreach(LiveTask t in fixture.Tasks){t.StartedUtc=epoch.AddSeconds(-79);t.Model="gpt-6.1-sol";t.Effort="Medium";}
                int frames=0,report=-1,parents=0,handles=0,materials=0,blank=0,sampledParents=0,sampledHandles=0,sampledMaterials=0;double previous=0;StringBuilder csv=new StringBuilder("frame,seconds,delta_ms,missions,bright_quota_pixels,bright_mission_pixels,parent_events,handle_events,material_events\r\n");
                var watch=new System.Diagnostics.Stopwatch();LiveTaskReport initial=new LiveTaskReport {CheckedUtc=epoch};initial.Tasks.Add(fixture.Tasks[0].Copy());window.Present(Quotas(),initial,epoch,"Direct");window.Show();window.BringToFront();Application.DoEvents();
                foreach(Control c in Walk(window)) {c.ParentChanged+=delegate{parents++;};c.HandleCreated+=delegate{handles++;};c.HandleDestroyed+=delegate{handles++;};if(c is InstrumentPanel)c.BackColorChanged+=delegate{materials++;};}
                using(var timer=new System.Windows.Forms.Timer {Interval=16}) {
                    timer.Tick+=delegate {
                        double seconds=watch.Elapsed.TotalSeconds;int sec=(int)seconds;
                        if(sec!=report) {
                            report=sec;DateTimeOffset now=epoch.AddSeconds(seconds);LiveTaskReport tasks=new LiveTaskReport {CheckedUtc=now};
                            foreach(LiveTask original in fixture.Tasks.Take(sec<2?1:2)) {LiveTask t=original.Copy();t.LastWriteUtc=now;if(sec>=5 && t.SessionKey==fixture.Tasks[0].SessionKey){t.Phase=LiveTaskPhase.Completed;t.FinishedUtc=epoch.AddSeconds(5);}tasks.Tasks.Add(t);}
                            window.Present(Quotas(),tasks,now,"Direct");
                        }
                        window.Missions.SetMotion(true);window.Missions.Advance();
                        sampledParents=parents;sampledHandles=handles;sampledMaterials=materials;
                        using(Bitmap image=new Bitmap(window.ClientSize.Width,window.ClientSize.Height)) {
                            window.DrawToBitmap(image,window.ClientRectangle);
                            Rectangle quota=layout=="Vertical"?new Rectangle(220,90,110,40):new Rectangle(230,50,130,35);
                            Rectangle mission=layout=="Vertical"?new Rectangle(20,278,310,250):new Rectangle(395,55,230,160);
                            Func<Rectangle,int> bright=r=>{int n=0;for(int y=r.Top;y<r.Bottom;y+=2)for(int x=r.Left;x<r.Right;x+=2){Color c=image.GetPixel(x,y);if(Math.Max(c.R,Math.Max(c.G,c.B))>150)n++;}return n;};
                            int q=bright(quota),m=bright(mission);if(q<5 || m<5)blank++;
                            csv.AppendLine(string.Join(",",frames,seconds.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),((seconds-previous)*1000).ToString("F2",System.Globalization.CultureInfo.InvariantCulture),window.Missions.Count,q,m,sampledParents,sampledHandles,sampledMaterials));previous=seconds;
                            image.Save(Path.Combine(folder,frames.ToString("00000")+".png"),System.Drawing.Imaging.ImageFormat.Png);frames++;
                        }
                        if(seconds>=8){File.WriteAllText(Path.Combine(folder,"stable-window-events.txt"),"During sampled frames, before window teardown: parent="+sampledParents+"; HWND="+sampledHandles+"; material="+sampledMaterials+".");timer.Stop();Application.ExitThread();}
                    };
                    watch.Start();timer.Start();Application.Run();
                }
                File.WriteAllText(Path.Combine(folder,"frames.csv"),csv.ToString());
                File.WriteAllText(Path.Combine(folder,"measurement.txt"),"SYNTHETIC native WinForms DrawToBitmap client pixels; "+frames+" frames / "+watch.Elapsed.TotalSeconds.ToString("F3")+" seconds; blank quota/mission samples="+blank+"; structure transitions at 0/2/5 seconds. During sampled frames: parent="+sampledParents+"; HWND="+sampledHandles+"; material="+sampledMaterials+". Normal teardown is not counted. Not a physical mixed-DPI or sub-frame scanout proof.");
                window.Hide();
            }
        }
        internal static string Run()
        {
            StringBuilder report=new StringBuilder("SYNTHETIC / Final Target Match / native controls and HWND events\r\n");int pass=0,total=0;
            Action<string,Action> check=(name,test)=>{total++;try{test();pass++;report.AppendLine("PASS "+name);}catch(Exception e){report.AppendLine("FAIL "+name+": "+e.Message);}};
            foreach(string layout in new []{"Vertical","Compact"})check("Unchanged reports preserve parent, HWND and material / "+layout,delegate {
                using(MonitorForm window=InstrumentTests.ParityWindow(layout,2,false)) {
                    window.Show();Application.DoEvents();window.Present(Quotas(),InstrumentTests.Tasks(2),InstrumentTests.Now,"Direct");
                    using(Bitmap warm=new Bitmap(window.Width,window.Height))window.DrawToBitmap(warm,window.ClientRectangle);
                    int parents=0,handles=0,colors=0,layouts=window.Missions.GeometryPasses;
                    foreach(Control c in Walk(window).ToArray()) {
                        c.ParentChanged+=delegate {parents++;};c.HandleCreated+=delegate {handles++;};c.HandleDestroyed+=delegate {handles++;};
                        if(c is InstrumentPanel)c.BackColorChanged+=delegate {colors++;};
                    }
                    for(int i=0;i<10;i++){window.Present(Quotas(),InstrumentTests.Tasks(2),InstrumentTests.Now,"Direct");Application.DoEvents();}
                    report.AppendLine("MEASURE "+layout+" /10 reports: parent changes="+parents+"; HWND changes="+handles+"; mission face changes="+colors);
                    Assert(parents==0 && handles==0 && colors==0 && layouts==window.Missions.GeometryPasses,"Unchanged telemetry rebuilds/reparents/recolors stable surfaces");
                }
            });
            foreach(string layout in new []{"Circular","Vertical","Compact","Micro"}) {
                check("Layout/version header / "+layout,delegate {
                    using(MonitorForm window=InstrumentTests.ParityWindow(layout,1,false)) {
                        window.Show();Application.DoEvents();
                        Assert(Walk(window).OfType<Label>().Any(l=>l.Visible && l.Text==MonitorForm.LayoutVersionLabel(true,layout)),"Missing layout/version heading");
                        Assert(!Walk(window).OfType<Label>().Any(l=>l.Visible && l.Text.Contains("PERISCOPE / V5")),"Duplicate product title");
                    }
                });
                check("Authoritative reset count / "+layout,delegate {
                    using(MonitorForm window=InstrumentTests.ParityWindow(layout,2,false)) {
                        window.Show();window.Present(Quotas(),InstrumentTests.Tasks(2),InstrumentTests.Now,"Direct");Application.DoEvents();
                        Label field=Walk(window).OfType<Label>().FirstOrDefault(l=>l.Visible && l.Name=="QuotaResets");
                        Assert(field!=null && field.Text.Contains("2"),"Collected ResetsAvailable not visible");
                        Snapshot unknown=Quotas();unknown.ResetsAvailable=null;window.Present(unknown,InstrumentTests.Tasks(2),InstrumentTests.Now,"Direct");
                        Assert(!field.Text.Contains("2"),"Reset timestamps fabricate an available count");
                        window.Present(Quotas(),InstrumentTests.Tasks(2),InstrumentTests.Now,"Stale data");
                        Assert(field.Text.IndexOf("cached",StringComparison.OrdinalIgnoreCase)>=0,"Stale available count lacks cached provenance");
                    }
                });
            }
            foreach(bool obsidian in new[]{false,true})foreach(string layout in new[]{"Circular","Vertical","Compact","Micro"})
                check("Shared quota/source/reset contract / "+(obsidian?"Obsidian ":"Classic ")+layout,delegate {
                    using(MonitorForm window=TargetWindow(layout,false,obsidian)) {
                        window.Show();Application.DoEvents();
                        LiveTaskReport missions=InstrumentTests.Tasks(1);window.Present(Quotas(),missions,InstrumentTests.Now,"Direct");
                        string fieldName=obsidian?"QuotaResets":"QuotaSource";
                        Label resets=Walk(window).OfType<Label>().FirstOrDefault(l=>l.Name==fieldName && l.Visible);
                        Assert(resets!=null && resets.Text.Contains(obsidian?"2 resets avail":"2 RESETS"),"Available reset count is not visible");
                        Snapshot zero=Quotas();zero.ResetsAvailable=0;window.Present(zero,missions,InstrumentTests.Now,"Direct");
                        Assert(resets.Text.Contains(obsidian?"0 resets avail":"0 RESETS"),"Zero resets is confused with missing information");
                        Snapshot missing=Quotas();missing.ResetsAvailable=null;window.Present(missing,missions,InstrumentTests.Now,"Direct");
                        Assert(resets.Text.IndexOf("RESETS --",StringComparison.OrdinalIgnoreCase)>=0,"Missing reset count was inferred");
                        Snapshot stale=Quotas();window.Present(stale,missions,InstrumentTests.Now,"Stale data");
                        Assert(resets.Text.IndexOf("cached",StringComparison.OrdinalIgnoreCase)>=0 || resets.Text.IndexOf("CACHED",StringComparison.Ordinal)>=0,"Cached reset count lacks provenance");
                        Assert(Walk(window).OfType<Label>().Any(l=>l.Name=="QuotaStatus" && l.Text.IndexOf("STALE",StringComparison.OrdinalIgnoreCase)>=0),"Stale source state is hidden");
                        window.Present(null,missions,InstrumentTests.Now,"Unavailable");
                        Assert(resets.Text.IndexOf("RESETS --",StringComparison.OrdinalIgnoreCase)>=0 && window.Missions.Cards.Any(c=>c.Radar.State==RadarState.Active),"Unavailable quotas fabricated resets or suspended observed mission");
                        Snapshot resetTimes=Quotas();window.Present(resetTimes,missions,InstrumentTests.Now,"Direct");
                        string[] resetLabels=Walk(window).OfType<Label>().Where(l=>l.Name=="QuotaReset").Select(l=>l.Text).ToArray();
                        Assert(resetLabels.Count(t=>t.IndexOf("Reset",StringComparison.OrdinalIgnoreCase)>=0)>=2,"Both reset times are missing from quota controls");
                    }
                });
            foreach(string layout in new[]{"Circular","Vertical","Compact","Micro"})foreach(int count in new[]{1,2})check("Target metadata and truth / "+layout+" / "+count,delegate {
                using(MonitorForm window=TargetWindow(layout)) {
                    window.Present(Quotas(),InstrumentTests.Tasks(count),InstrumentTests.Now,"Direct");window.Show();Application.DoEvents();
                    foreach(float scale in new[]{1f,1.25f,1.5f,2f}) {
                        window.SetPresentationScale(scale);
                        foreach(MissionCard card in window.Missions.Cards) {
                            Assert(Walk(card).OfType<Label>().Any(l=>l.Visible && l.Name=="MissionModel" && l.Text.Contains(card.Mission.Task.Model)),"Model hidden");
                            Assert(Walk(card).OfType<Label>().Any(l=>l.Visible && (l.Name=="MissionEffort" || l.Name=="MissionModel") && l.Text.Contains(card.Mission.Task.Effort)),"Effort hidden");
                            Assert(Walk(card).OfType<Label>().Any(l=>l.Visible && l.Name=="MissionDuration" && l.Text.Length>0),"Duration hidden");
                        }
                    }
                    window.Present(Quotas(true),InstrumentTests.Tasks(count),InstrumentTests.Now,"Direct");
                    Assert(Walk(window).OfType<Label>().Any(l=>l.Visible && l.Name=="QuotaResets" && l.Text=="2 resets cached"),"Blocked count has no cached provenance");
                    Assert(window.Missions.Cards.All(c=>c.Radar.State==RadarState.Active),"Source block suspended independent activity");
                    Assert(!Walk(window).OfType<Label>().Any(l=>l.Visible && (l.Text.Contains("429") || l.Text.Contains("SUSPENDED") || l.Text=="EXHAUSTED")),"Illustrative blocked facts leaked into product");
                }
            });
            check("One explicit layout selector adopts target without replacing saved width",delegate {
                Preferences p=Preferences.Defaults();p.CircularWidth=620;p.Top=false;
                using(MonitorForm window=new MonitorForm(p)) {
                    var select=typeof(MonitorForm).GetMethod("SelectTargetLayout",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(string)},null);
                    Assert(select!=null,"No unified selection action");select.Invoke(window,new object[]{"Circular"});
                    Assert(p.ObsidianLayout && p.CircularWidth==620 && window.ClientSize==new Size(680,480),"Explicit selection crosses legacy geometry or misses target");
                }
            });
            foreach(bool obsidian in new[]{false,true})foreach(string layout in new[]{"Circular","Vertical","Compact","Micro"})
                check("Explicit family/layout identity and selection / "+(obsidian?"Obsidian ":"Classic ")+layout,delegate {
                    Preferences p=Preferences.Defaults();p.Layout=layout;p.ObsidianLayout=obsidian;p.Top=false;p.TaskAnimations=false;p.CircularWidth=620;p.ObsidianCircularWidth=700;p.Opacity=.83;p.ResetStyle="relative";
                    using(MonitorForm window=new MonitorForm(p)) {
                        string expected=MonitorForm.LayoutVersionLabel(obsidian,layout);
                        Assert(Walk(window).OfType<Label>().Any(l=>l.Name=="LayoutVersion" && l.Text==expected) ||
                            Walk(window).OfType<Label>().Any(l=>l.Text==expected),"Family/layout title is not explicit");
                        Size before=window.ClientSize;int geometry=window.Missions.GeometryPasses;
                        window.SelectTargetLayout(layout,obsidian);
                        Assert(p.Layout==layout && p.ObsidianLayout==obsidian && window.ClientSize==before && window.Missions.GeometryPasses==geometry,"Selecting the current option recomposed or changed its identity");
                        Assert(p.CircularWidth==620 && p.ObsidianCircularWidth==700 && p.Opacity==.83 && p.ResetStyle=="relative" && !p.Top,"Selection replaced saved dimensions or preferences");
                    }
                });
            check("Appearance menu exposes exactly eight family-specific radio choices",delegate {
                Preferences p=Preferences.Defaults();p.Layout="Circular";p.ObsidianLayout=false;p.Top=false;
                using(MonitorForm window=new MonitorForm(p)) {
                    typeof(MonitorForm).GetMethod("MakeMenu",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(window,null);
                    var field=typeof(MonitorForm).GetField("context",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    ContextMenuStrip menu=(ContextMenuStrip)field.GetValue(window);
                    ToolStripMenuItem appearance=menu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="Appearance");
                    ToolStripMenuItem layout=appearance.DropDownItems.OfType<ToolStripMenuItem>().Single(x=>x.Text=="Layout");
                    Assert(layout.DropDownItems.Count==2,"Layout menu must contain only the two family groups");
                    ToolStripMenuItem classic=(ToolStripMenuItem)layout.DropDownItems[0],obsidian=(ToolStripMenuItem)layout.DropDownItems[1];
                    Assert(classic.Text=="CLASSIC" && obsidian.Text=="OBSIDIAN","Family group names differ");
                    Assert(classic.DropDownItems.Count==4 && obsidian.DropDownItems.Count==4,"Expected four choices in each family");
                    MonitorForm.SyncLayoutChoices(classic,obsidian,p.ObsidianLayout,p.Layout);
                    Assert(classic.DropDownItems.OfType<ToolStripMenuItem>().Count(x=>x.Checked)==1 &&
                        classic.DropDownItems.OfType<ToolStripMenuItem>().Single(x=>x.Checked).Text=="Circular" &&
                        obsidian.DropDownItems.OfType<ToolStripMenuItem>().All(x=>!x.Checked),"Menu did not identify the exact active choice");
                    obsidian.DropDownItems[0].PerformClick();
                    Assert(p.ObsidianLayout && p.Layout=="Circular","Obsidian Circular did not switch family explicitly");
                }
            });
            report.AppendLine(pass+"/"+total+" final target checks passed");return report.ToString();
        }
    }
}
