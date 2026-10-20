using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal sealed class MissionView
    {
        internal LiveTask Task;
        internal int Number;
        internal bool Missing;
        internal RadarState State;
        internal string Label { get { return Task.Workspace == "Project not provided" ? "Codex task #" + Number : Task.Workspace + " · #" + Number; } }
    }
    // Keeps missing observations uncertain; absence is never a terminal event.
    internal sealed class MissionTracker
    {
        private readonly Dictionary<string,MissionView> known = new Dictionary<string,MissionView>();
        private LiveTaskReport last;
        private int sequence;
        internal void Update(LiveTaskReport report)
        {
            if (ReferenceEquals(last,report)) return;
            last=report;
            if (report == null) { known.Clear(); return; }
            foreach (MissionView view in known.Values) view.Missing=true;
            foreach (LiveTask task in report.Tasks.Where(t => t.IsRootConfirmed && !t.IsSubagent && !string.IsNullOrEmpty(t.SessionKey) &&
                (t.FinishedUtc.HasValue ? report.CheckedUtc-t.FinishedUtc.Value <= TimeSpan.FromSeconds(30) : report.CheckedUtc-t.LastWriteUtc < TimeSpan.FromMinutes(30)))) {
                MissionView view;
                if (!known.TryGetValue(task.Identity,out view)) {
                    // A missing ID may become available later; retain the same card only
                    // if the previous observation has one unambiguous matching start.
                    List<MissionView> aliases=known.Values.Where(v => v.Missing && TaskNotificationRules.SameTurn(v.Task,task)).ToList();
                    if (aliases.Count == 1) { view=aliases[0]; known.Remove(view.Task.Identity); }
                    else view=new MissionView { Number=++sequence };
                    known[task.Identity]=view;
                }
                view.Task=task.Copy(); view.Missing=false;
            }
        }
        internal List<MissionView> At(DateTimeOffset now)
        {
            foreach (string key in known.Where(p =>
                p.Value.Task.FinishedUtc.HasValue ? now-p.Value.Task.FinishedUtc.Value > TimeSpan.FromSeconds(30) :
                now-p.Value.Task.LastWriteUtc > TimeSpan.FromMinutes(30)).Select(p => p.Key).ToArray()) known.Remove(key);
            bool scanFresh=last != null && last.CheckedUtc != DateTimeOffset.MinValue &&
                (now-last.CheckedUtc).TotalSeconds >= -5 && (now-last.CheckedUtc).TotalSeconds <= 25;
            foreach (MissionView view in known.Values) {
                LiveTask task=view.Task;
                double age=(now-task.LastWriteUtc).TotalSeconds;
                if (!scanFresh) view.State=RadarState.Stale;
                else if (view.Missing || task.IdentityUncertain || task.Phase == LiveTaskPhase.Unconfirmed) view.State=RadarState.Unverified;
                else if (age < -5 || (task.Phase == LiveTaskPhase.Active && age > LiveTaskReader.ActivityMaxAgeSeconds)) view.State=RadarState.Stale;
                else if (task.FinishedUtc.HasValue && (task.FinishedUtc.Value < task.StartedUtc || task.FinishedUtc.Value > now.AddSeconds(5))) view.State=RadarState.Unverified;
                else view.State=RadarMotion.Map(task.Phase,true,false);
            }
            return known.Values.OrderBy(v => v.Number).ToList();
        }
    }
    internal sealed class MissionCard : InstrumentPanel
    {
        internal readonly ActivityGlyph Radar=new ActivityGlyph();
        private readonly Label title=new InstrumentLabel(), status=new InstrumentLabel(), elapsed=new InstrumentLabel(), model=new InstrumentLabel(), effort=new InstrumentLabel();
        private readonly ToolTip tooltip=new ToolTip();
        private string elapsedReadout;
        internal MissionView Mission { get; private set; }
        internal string LayoutMode="Circular";
        internal bool Condensed;
        internal bool Parity;
        private readonly Label observation=new InstrumentLabel();
        internal MissionCard()
        {
            TabStop=true;
            foreach(Label label in new [] {title,status,elapsed,model,effort}) {
                label.AutoEllipsis=true;label.BackColor=Color.Transparent;label.ForeColor=InstrumentStyle.Text;
                Controls.Add(label);
            }
            title.Name="MissionTitle";status.Name="MissionState";elapsed.Name="MissionDuration";model.Name="MissionModel";effort.Name="MissionEffort";
            Controls.Add(Radar);
            observation.Text="LOCAL / OBSERVED";observation.ForeColor=InstrumentStyle.Muted;observation.BackColor=Color.Transparent;Controls.Add(observation);
        }
        internal void Update(MissionView mission, DateTimeOffset now)
        {
            Mission=mission; LiveTask task=mission.Task;
            Radar.Accent=InstrumentStyle.MissionAccent(mission.Number);
            title.Text=mission.Number.ToString("00",CultureInfo.InvariantCulture)+" / "+(task.Workspace=="Project not provided"?"Codex task":task.Workspace);
            status.Text=mission.State==RadarState.Active ? "ACTIVE / OBSERVED" : mission.State==RadarState.Complete ? "COMPLETED" : mission.State.ToString().ToUpperInvariant();
            bool current=mission.State==RadarState.Active;
            bool durationKnown=!task.IdentityUncertain && task.StartedUtc<=now.AddSeconds(5) && task.LastWriteUtc>=task.StartedUtc && task.LastWriteUtc<=now.AddSeconds(5) &&
                (!task.FinishedUtc.HasValue || (task.FinishedUtc.Value>=task.StartedUtc && task.FinishedUtc.Value<=now.AddSeconds(5)));
            elapsedReadout=durationKnown ? (current ? "Elapsed  " : "Recorded  ")+task.Duration(current ? now : task.LastWriteUtc) : "Duration unverified";
            elapsed.Text=elapsedReadout;
            model.Text="MODEL  "+task.Model;effort.Text="EFFORT  "+task.Effort;
            status.ForeColor=mission.State==RadarState.Active ? InstrumentStyle.Teal : mission.State==RadarState.Complete ? Color.FromArgb(72,214,155) :
                mission.State==RadarState.Failed ? InstrumentStyle.Red : InstrumentStyle.Amber;
            Edge=InstrumentStyle.Blend(InstrumentStyle.Border,status.ForeColor,.3);
            Rail=InstrumentStyle.Blend(InstrumentStyle.Border,status.ForeColor,.4);
            LiveTaskPhase phase=mission.State==RadarState.Unverified ? LiveTaskPhase.Unconfirmed : task.Phase;
            Radar.SetObservation(phase,mission.State!=RadarState.Stale,false);
            string detail=mission.Label+"\n"+status.Text+" / "+elapsed.Text+"\n"+model.Text+"\n"+effort.Text+
                "\nLast source observation: "+task.LastWriteUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss zzz",CultureInfo.GetCultureInfo("en-GB"))+
                (mission.Missing ? "\nNot present in latest bounded read; completion unverified." : "\nLocal JSONL observation; active Desktop tab not guaranteed.");
            AccessibleName=title.Text+", "+status.Text;AccessibleDescription=detail;
            foreach(Control child in Controls)tooltip.SetToolTip(child,detail);tooltip.SetToolTip(this,detail);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(title==null)return;
            observation.Visible=Parity;Flat=Parity;
            if(Parity){ArrangeParity();return;}
            float scale=PeriscopeWindow.ScaleFor(this);Func<int,int> u=v=>(int)Math.Round(v*scale);
            int w=(int)Math.Round(Width/scale),h=(int)Math.Round(Height/scale);
            bool shortRow=h<50,tight=h<68,flat=h<78,spacious=h>=94 && !Condensed && LayoutMode=="Circular";
            int desired=LayoutMode=="Micro" ? 40 : LayoutMode=="Compact" ? 60 : LayoutMode=="Vertical" ? 78 : 104;
            if(Condensed)desired=Math.Min(desired,76);
            if(shortRow)desired=30;
            int radar=Math.Max(20,Math.Min(desired,h-4));
            Radar.Bounds=new Rectangle(u(6),u((h-radar)/2),u(radar),u(radar));Radar.BackColor=Color.Transparent;
            int x=radar+18,available=Math.Max(1,w-x-9);
            title.SetBounds(u(x),u(shortRow?2:flat && !tight?3:4),u(available),u(shortRow?17:tight?18:flat?21:23));
            InstrumentStyle.FontFor(title,shortRow?11.5f:spacious?15:Condensed?12.5f:tight?12:14,FontStyle.Bold);
            int sy=shortRow?21:tight?23:flat?26:32,ey=shortRow?21:tight?39:spacious?62:Math.Min(55,h-21);
            int stateHeight=shortRow||tight?15:flat?17:18,elapsedHeight=shortRow||tight?16:19;
            status.SetBounds(u(x),u(sy),u(available),u(stateHeight));
            InstrumentStyle.FontFor(status,shortRow?10.5f:tight?11:12,FontStyle.Bold);
            if(Mission!=null)status.Text=Mission.State==RadarState.Active ? (shortRow || available<145?"ACTIVE":"ACTIVE / OBSERVED") : Mission.State==RadarState.Complete?"COMPLETED":Mission.State.ToString().ToUpperInvariant();
            elapsed.Visible=true;elapsed.Text=elapsedReadout ?? "";
            if(shortRow || available<160)elapsed.Text=elapsed.Text.Replace("Elapsed  ","Elapsed ").Replace("Recorded  ","Recorded ").Replace(" h ","h ").Replace(" min","m").Replace(" d ","d ").Replace("Duration unverified","Time unverified");
            elapsed.SetBounds(u(x),u(ey),u(available),u(elapsedHeight));
            InstrumentStyle.FontFor(elapsed,shortRow?10.5f:tight?11.5f:spacious?14:13,FontStyle.Regular);elapsed.ForeColor=InstrumentStyle.Text;
            if(shortRow) {
                int ew=Math.Min(132,available/2);
                elapsed.SetBounds(u(w-ew-9),u(ey),u(ew),u(elapsedHeight));status.Width=Math.Max(1,elapsed.Left-status.Left-u(8));
            }
            bool metadataFits=w>=390 && h>=55;
            model.Visible=effort.Visible=metadataFits;
            if(metadataFits) {
                int mx=Math.Max(x+184,w*3/5),mw=Math.Max(1,w-mx-9);
                model.SetBounds(u(mx),u(sy),u(mw),u(tight?15:17));effort.SetBounds(u(mx),u(ey),u(mw),u(tight?15:17));
                status.Width=elapsed.Width=u(mx-x-9);
                InstrumentStyle.FontFor(model,tight?10.5f:11.5f,FontStyle.Regular);InstrumentStyle.FontFor(effort,tight?10.5f:11.5f,FontStyle.Regular);
                model.ForeColor=effort.ForeColor=InstrumentStyle.Muted;
            }
        }
        private void ArrangeParity()
        {
            float scale=PeriscopeWindow.ScaleFor(this);Func<int,int> u=v=>(int)Math.Round(v*scale);
            int w=(int)Math.Round(Width/scale),h=(int)Math.Round(Height/scale);bool vertical=LayoutMode=="Vertical"&&!Condensed;
            bool micro=LayoutMode=="Micro",dock=LayoutMode=="Compact";
            int radar=vertical?100:micro?(Condensed?40:46):dock?(Condensed?60:68):Condensed?76:88;
            radar=Math.Min(radar,h-8);Radar.Parity=true;
            Radar.SetBounds(u(vertical?(w-radar)/2:micro?8:dock?14:12),u(vertical?46:(h-radar)/2),u(radar),u(radar));
            int x=vertical?12:dock?388:radar+(micro?20:28),available=Math.Max(1,w-x-12);
            bool small=Condensed || (micro && h<80);
            title.TextAlign=vertical?ContentAlignment.MiddleCenter:ContentAlignment.MiddleLeft;
            title.SetBounds(u(x),u(vertical?164:small?4:32),u(available),u(21));
            status.SetBounds(u(x),u(small?27:8),u(available),u(17));
            status.Text=Mission==null?"":Mission.State==RadarState.Active?(available<130?"ACTIVE":"ACTIVE / OBSERVED"):Mission.State==RadarState.Complete?"COMPLETED":Mission.State.ToString().ToUpperInvariant();
            elapsed.Text=elapsedReadout ?? "";elapsed.Visible=true;
            if(available<160)elapsed.Text=elapsed.Text.Replace("Elapsed  ","Elapsed ").Replace("Recorded  ","Recorded ").Replace(" h ","h ").Replace(" min","m").Replace(" d ","d ").Replace("Duration unverified","Time unverified");
            elapsed.SetBounds(u(x),u(vertical?242:small?47:70),u(available),u(20));
            observation.Visible=!small;observation.SetBounds(u(x),u(8),u(Math.Max(1,available-138)),u(16));
            if(!small)status.SetBounds(u(w-140),u(8),u(128),u(17));
            model.Visible=effort.Visible=!small;
            if(vertical) {
                observation.SetBounds(u(12),u(10),u(w-160),u(16));
                model.SetBounds(u(16),u(207),u((w-40)/2),u(17));effort.SetBounds(u(w/2+4),u(207),u(w/2-20),u(17));
            } else if(!small && dock) {
                title.Top=u(32);status.SetBounds(u(x),u(57),u(available),u(17));observation.Width=u(available);
                model.SetBounds(u(x),u(86),u(available),u(17));effort.SetBounds(u(x),u(106),u(available),u(17));elapsed.Top=u(128);
            } else if(!small) {
                int mw=available/3,ew=available/5;
                model.SetBounds(u(x),u(70),u(mw),u(17));effort.SetBounds(u(x+mw+8),u(70),u(ew),u(17));
                elapsed.SetBounds(u(x+mw+ew+16),u(68),u(available-mw-ew-16),u(21));
            }
            if(micro && !Condensed) {
                title.Width=u(Math.Max(1,available-128));status.SetBounds(u(w-136),u(5),u(124),u(17));
                model.Visible=true;effort.Visible=false;model.SetBounds(u(x),u(33),u(170),u(17));elapsed.SetBounds(u(x+182),u(32),u(available-182),u(19));
            }
            if(micro && Condensed) {title.Top=u(3);title.Height=u(18);status.Top=u(24);status.Height=u(15);elapsed.Top=u(43);elapsed.Height=u(17);}
            if(elapsed.Width<u(175))elapsed.Text=elapsed.Text.Replace("Elapsed  ","Elapsed ").Replace("Recorded  ","Recorded ").Replace(" h ","h ").Replace(" min","m").Replace(" d ","d ").Replace("Duration unverified","Time unverified");
            InstrumentStyle.FontFor(title,small?12:vertical?13:15,FontStyle.Bold);
            InstrumentStyle.FontFor(status,small?10:11,FontStyle.Bold);InstrumentStyle.FontFor(elapsed,small?11:12.5f,FontStyle.Regular);
            InstrumentStyle.FontFor(observation,9.5f,FontStyle.Bold);InstrumentStyle.FontFor(model,10.5f,FontStyle.Regular);InstrumentStyle.FontFor(effort,10.5f,FontStyle.Regular);
            status.ForeColor=Mission!=null && Mission.State==RadarState.Active?Radar.Accent:status.ForeColor;elapsed.ForeColor=Mission!=null && Mission.State==RadarState.Active?Radar.Accent:InstrumentStyle.Text;
            Flat=true;Rail=Color.Transparent;Edge=InstrumentStyle.Hairline;BackColor=InstrumentStyle.ActivityFace;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if(!Parity || LayoutMode!="Compact"){base.OnPaintBackground(e);return;}
            e.Graphics.Clear(Parent==null?InstrumentStyle.Canvas:Parent.BackColor);float s=PeriscopeWindow.ScaleFor(this);
            using(Brush b=new SolidBrush(InstrumentStyle.ActivityFace))using(Pen p=new Pen(InstrumentStyle.Hairline))
                foreach(RectangleF box in new []{new RectangleF(.5f,.5f,96*s-1,Height-1),new RectangleF(376*s,.5f,Width-376*s-1,Height-1)})
                    using(System.Drawing.Drawing2D.GraphicsPath shape=InstrumentStyle.Rounded(box,5*s)){e.Graphics.FillPath(b,shape);e.Graphics.DrawPath(p,shape);}
        }
        protected override void Dispose(bool disposing) {if(disposing)tooltip.Dispose();base.Dispose(disposing);}
    }
    // One timer in MonitorForm, one monotonic clock here, independent mission phases.
    internal sealed class MissionDeck : InstrumentPanel
    {
        private readonly MissionTracker tracker=new MissionTracker();
        private readonly Dictionary<int,MissionCard> cards=new Dictionary<int,MissionCard>();
        private readonly Label heading=new InstrumentLabel(),emptyTitle=new InstrumentLabel(),emptyDetail=new InstrumentLabel();
        private readonly Panel viewport=new Panel { AutoScroll=false, Margin=Padding.Empty };
        private readonly ActivityGlyph emptyRadar=new ActivityGlyph();
        private readonly InstrumentScroll scroll=new InstrumentScroll();
        private readonly Stopwatch clock=new Stopwatch();
        private double lastFrame;
        private bool arranging;
        private string layoutMode="Circular";
        internal bool Parity;
        private List<MissionView> views=new List<MissionView>();
        internal string LayoutMode {get{return layoutMode;}set{layoutMode=value;Arrange();}}
        internal int Count {get{return views.Count;}}
        internal Control QuotaEmptyHost {get{return viewport;}}
        internal IEnumerable<MissionCard> Cards {get{return cards.Values;}}
        internal bool HasOverflow {get{return scroll.Visible;}}
        internal bool NeedsFrames {get{return Visible && cards.Values.Any(c=>InViewport(c) && c.Radar.NeedsFrames);}}
        internal MissionDeck()
        {
            heading.BackColor=Color.Transparent;heading.ForeColor=InstrumentStyle.Teal;
            Controls.Add(viewport);Controls.Add(heading);Controls.Add(scroll);
            viewport.Controls.Add(emptyRadar);viewport.Controls.Add(emptyTitle);viewport.Controls.Add(emptyDetail);
            foreach(Label l in new []{emptyTitle,emptyDetail}) {l.BackColor=Color.Transparent;l.AutoEllipsis=true;l.ForeColor=InstrumentStyle.Muted;}
            scroll.OffsetChanged+=delegate{Arrange();};
            viewport.SizeChanged+=delegate{Arrange();};
            viewport.MouseWheel+=Wheel;emptyRadar.MouseWheel+=Wheel;
            emptyTitle.MouseWheel+=Wheel;emptyDetail.MouseWheel+=Wheel;
        }
        private void Wheel(object sender,MouseEventArgs e){ScrollBy(-Math.Sign(e.Delta)*Math.Max(20,(int)(40*PeriscopeWindow.ScaleFor(this))));}
        protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Wheel(this,e);}
        internal void ScrollBy(int pixels){scroll.Offset+=pixels;}
        internal void UpdateReport(LiveTaskReport report,DateTimeOffset now)
        {
            tracker.Update(report);views=tracker.At(now);
            HashSet<int> present=new HashSet<int>(views.Select(v=>v.Number));
            foreach(int number in cards.Keys.Where(k=>!present.Contains(k)).ToArray()) {MissionCard card=cards[number];cards.Remove(number);card.Dispose();}
            foreach(MissionView view in views) {
                MissionCard card;
                if(!cards.TryGetValue(view.Number,out card)) {
                    card=new MissionCard();card.Update(view,now); // Initialize before parent layout can run.
                    cards.Add(view.Number,card);viewport.Controls.Add(card);
                    card.MouseWheel+=Wheel;foreach(Control child in card.Controls)child.MouseWheel+=Wheel;
                } else card.Update(view,now);
            }
            heading.Text=views.Count<=1 ? "CODEX ACTIVITY / LOCAL OBSERVATION" : views.Count+" LOCAL TURNS / "+views.Count(v=>v.State==RadarState.Active)+" OBSERVED ACTIVE";
            emptyTitle.Text=report==null ? "LOCAL ACTIVITY UNAVAILABLE" : "LOCAL ACTIVITY UNVERIFIED";
            emptyDetail.Text="LOCAL JSONL / partial observation";
            emptyRadar.SetObservation(LiveTaskPhase.Unconfirmed,true,report==null);
            emptyRadar.Visible=emptyTitle.Visible=emptyDetail.Visible=cards.Count==0;
            Arrange();
        }
        internal List<MissionView> At(DateTimeOffset now){return tracker.At(now);}
        private bool InViewport(MissionCard card){return viewport.Visible && viewport.ClientRectangle.IntersectsWith(card.Bounds);}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Arrange();}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Arrange();}
        private void Arrange()
        {
            if(arranging || viewport==null || heading==null || cards==null || scroll==null)return;
            arranging=true;
            try {
                float scale=PeriscopeWindow.ScaleFor(this);Func<int,int> u=v=>(int)Math.Round(v*scale);
                int inset=Parity?0:u(4),gap=u(4),top=Parity?0:u(24);heading.Visible=!Parity;
                heading.SetBounds(u(9),u(4),Math.Max(1,Width-u(18)),u(18));
                InstrumentStyle.FontFor(heading,10.5f,FontStyle.Bold);
                int width=Math.Max(1,Width-2*inset),height=Math.Max(1,Height-top-inset);
                int columns=cards.Count>1 && width>=u(420) ? 2 : 1;
                if(Parity && layoutMode=="Compact")columns=1;
                int rows=(cards.Count+columns-1)/columns;
                int rowHeight=cards.Count<=2 ? Math.Max(1,(height-Math.Max(0,rows-1)*gap)/Math.Max(1,rows)) : u(66);
                if(Parity && layoutMode=="Compact" && cards.Count>2)rowHeight=u(160);
                int extent=rows*rowHeight+Math.Max(0,rows-1)*gap;
                bool overflow=cards.Count>2 && extent>height;
                scroll.SetBounds(Width-u(12),top,u(8),height);scroll.BackColor=BackColor;
                scroll.Configure(overflow?extent:height,height);
                if(!overflow)scroll.Offset=0;
                viewport.SetBounds(inset,top,Math.Max(1,width-(overflow?u(12):0)),height);
                viewport.BackColor=BackColor;viewport.AutoScrollMinSize=Size.Empty;viewport.AutoScrollMargin=Size.Empty;
                int cw=Math.Max(1,(viewport.Width-(columns-1)*gap)/columns),i=0;
                foreach(MissionCard card in cards.Values.OrderBy(c=>c.Mission.Number)) {
                    card.LayoutMode=layoutMode;card.Condensed=cards.Count>1;card.Parity=Parity;card.Radar.Parity=Parity;card.BackColor=InstrumentStyle.Blend(BackColor,Color.Black,.16);
                    card.SetBounds(i%columns*(cw+gap),i/columns*(rowHeight+gap)-scroll.Offset,cw,rowHeight);card.PerformLayout();i++;
                }
                int radar=Math.Min(u(layoutMode=="Micro"?40:layoutMode=="Circular"?100:72),Math.Max(1,height-u(4)));
                emptyRadar.SetBounds(u(6),(height-radar)/2,radar,radar);emptyRadar.BackColor=Color.Transparent;
                emptyTitle.SetBounds(radar+u(16),Math.Max(u(4),height/2-u(18)),Math.Max(1,viewport.Width-radar-u(24)),u(18));
                emptyDetail.SetBounds(emptyTitle.Left,emptyTitle.Bottom+u(2),emptyTitle.Width,u(17));
                if(Parity && layoutMode=="Compact") {
                    emptyRadar.SetBounds(u(14),u(46),u(68),u(68));
                    emptyTitle.SetBounds(u(388),u(36),Math.Max(1,viewport.Width-u(400)),u(36));
                    emptyDetail.SetBounds(u(388),u(82),emptyTitle.Width,u(34));
                }
                InstrumentStyle.FontFor(emptyTitle,11.5f,FontStyle.Bold);InstrumentStyle.FontFor(emptyDetail,11,FontStyle.Regular);
            } finally {arranging=false;}
        }
        internal void ConfirmCompletion(LiveTask task)
        {foreach(MissionCard card in cards.Values)if(TaskNotificationRules.SameTurn(card.Mission.Task,task) && card.Mission.State==RadarState.Complete)card.Radar.ConfirmCompletion(task.Identity);}
        internal void SetMotion(bool enabled)
        {
            bool running=enabled && NeedsFrames;
            if(running && !clock.IsRunning){clock.Restart();lastFrame=0;}if(!running)clock.Reset();
            foreach(MissionCard card in cards.Values)card.Radar.SetSharedMotion(running && InViewport(card),clock.Elapsed.TotalSeconds,card.Mission.Number*.41);
        }
        internal void Advance()
        {
            if(!clock.IsRunning)return;double t=clock.Elapsed.TotalSeconds;
            if(t-lastFrame>.5){clock.Restart();t=0;foreach(MissionCard card in cards.Values)card.Radar.FinishMotion();}
            lastFrame=t;
            foreach(MissionCard card in cards.Values)if(InViewport(card)){card.Radar.SetSharedMotion(true,t,card.Mission.Number*.41);card.Radar.Advance();}
        }
    }
}
