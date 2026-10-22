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
        internal DateTimeOffset ParentEvidenceUtc;
        internal DateTimeOffset ChildEvidenceUtc;
        internal int ActiveChildCount;
        internal bool DelegatedEvidenceSeen;
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
            List<LiveTask> roots=report.Tasks.Where(t => t.IsRootConfirmed && !t.IsSubagent && !string.IsNullOrEmpty(t.SessionKey)).ToList();
            foreach (LiveTask task in roots.Where(t =>
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
                view.Task=task.Copy(); view.Missing=false; view.ParentEvidenceUtc=task.LastWriteUtc;
                view.ChildEvidenceUtc=DateTimeOffset.MinValue; view.ActiveChildCount=0;
                if (!task.FinishedUtc.HasValue && !task.IdentityUncertain && !string.IsNullOrEmpty(task.ThreadId) &&
                    roots.Count(r => !r.FinishedUtc.HasValue && string.Equals(r.ThreadId,task.ThreadId,StringComparison.Ordinal))==1) {
                    List<LiveTask> linked=report.Tasks.Where(c => c.IsSubagent && !c.IdentityUncertain &&
                        !string.IsNullOrEmpty(c.ParentThreadId) && string.Equals(c.ParentThreadId,task.ThreadId,StringComparison.Ordinal) &&
                        !string.IsNullOrEmpty(c.SessionKey) && c.StartedUtc<=report.CheckedUtc.AddSeconds(5)).ToList();
                    view.DelegatedEvidenceSeen=linked.Count>0;
                    foreach (LiveTask child in linked.GroupBy(c=>c.SessionKey,StringComparer.Ordinal).Where(g=>g.Count()==1).Select(g=>g.First())) {
                        if(child.LastWriteUtc>view.ChildEvidenceUtc)view.ChildEvidenceUtc=child.LastWriteUtc;
                        if(child.Phase==LiveTaskPhase.Active && !child.FinishedUtc.HasValue &&
                            (report.CheckedUtc-child.LastWriteUtc).TotalSeconds>=-5 &&
                            (report.CheckedUtc-child.LastWriteUtc).TotalSeconds<=LiveTaskReader.ActivityMaxAgeSeconds)
                            view.ActiveChildCount++;
                    }
                }
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
                else if (view.Missing || task.IdentityUncertain) view.State=RadarState.Unverified;
                else if (task.FinishedUtc.HasValue && (task.FinishedUtc.Value<task.StartedUtc || task.FinishedUtc.Value>now.AddSeconds(5))) view.State=RadarState.Unverified;
                else if (task.FinishedUtc.HasValue) view.State=RadarMotion.Map(task.Phase,true,false);
                else if (view.ActiveChildCount>0 && (now-view.ChildEvidenceUtc).TotalSeconds>=-5 &&
                    (now-view.ChildEvidenceUtc).TotalSeconds<=LiveTaskReader.ActivityMaxAgeSeconds) view.State=RadarState.Delegated;
                else if (view.DelegatedEvidenceSeen) view.State=RadarState.Unverified;
                else if (task.Phase==LiveTaskPhase.Unconfirmed) view.State=RadarState.Unverified;
                else if (age < -5 || (task.Phase == LiveTaskPhase.Active && age > LiveTaskReader.ActivityMaxAgeSeconds)) view.State=RadarState.Stale;
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
        private readonly Label modelCaption=new InstrumentLabel {Text="MODEL"},effortCaption=new InstrumentLabel {Text="EFFORT"},elapsedCaption=new InstrumentLabel {Text="ELAPSED"};
        private void TargetValues()
        {
            if(!Parity || Mission==null)return;
            if(LayoutMode=="Circular")observation.Text="CODEX ACTIVITY / "+(Mission.State==RadarState.Delegated?"DELEGATED · OBSERVED":Mission.State==RadarState.Unverified?"UNVERIFIED":Mission.State==RadarState.Stale?"STALE OBSERVATION":"LOCAL OBSERVED");
            title.Text=Mission.Label;status.Text=Mission.State==RadarState.Active?"ACTIVE":Mission.State==RadarState.Delegated?"DELEGATED · OBSERVED":Mission.State==RadarState.Complete?"COMPLETED":Mission.State.ToString().ToUpperInvariant();
            model.Text=Mission.Task.Model;effort.Text=Mission.Task.Effort;
            elapsed.Text=elapsedReadout=="Duration unverified"?"--":elapsedReadout.Replace("Elapsed  ","").Replace("Recorded  ","");
            elapsedCaption.Text=Mission.State==RadarState.Active?"ELAPSED":Mission.State==RadarState.Delegated?"PARENT ELAPSED":"RECORDED";
            if(Condensed)elapsed.Text=elapsed.Text.Replace(" h ","h").Replace(" min","m").Replace(" d ","d");
            if(LayoutMode=="Micro") {model.Text="Model: "+Mission.Task.Model+"  / "+Mission.Task.Effort;elapsed.Text=(Condensed?"T ":Mission.State==RadarState.Active?"Elapsed: ":"Recorded: ")+elapsed.Text;}
            status.ForeColor=Mission.State==RadarState.Active?Radar.Accent:Mission.State==RadarState.Complete?InstrumentStyle.Secondary:Mission.State==RadarState.Failed?InstrumentStyle.Red:InstrumentStyle.Amber;
            elapsed.ForeColor=Mission.State==RadarState.Active?Radar.Accent:InstrumentStyle.Secondary;
        }
        internal MissionCard()
        {
            TabStop=true;
            foreach(Label label in new [] {title,status,elapsed,model,effort}) {
                label.AutoEllipsis=true;label.BackColor=Color.Transparent;label.ForeColor=InstrumentStyle.Text;
                Controls.Add(label);
            }
            title.Name="MissionTitle";status.Name="MissionState";elapsed.Name="MissionDuration";model.Name="MissionModel";effort.Name="MissionEffort";
            Controls.Add(Radar);
            foreach(Label l in new[]{modelCaption,effortCaption,elapsedCaption}) {l.BackColor=Color.Transparent;l.ForeColor=InstrumentStyle.Metadata;Controls.Add(l);}
            observation.Text="LOCAL / OBSERVED";observation.ForeColor=InstrumentStyle.Muted;observation.BackColor=Color.Transparent;Controls.Add(observation);
        }
        internal void Update(MissionView mission, DateTimeOffset now)
        {
            Mission=mission; LiveTask task=mission.Task;
            Radar.Accent=InstrumentStyle.MissionAccent(mission.Number);
            title.Text=mission.Number.ToString("00",CultureInfo.InvariantCulture)+" / "+(task.Workspace=="Project not provided"?"Codex task":task.Workspace);
            status.Text=mission.State==RadarState.Active ? "ACTIVE / OBSERVED" : mission.State==RadarState.Delegated ? "DELEGATED · OBSERVED" : mission.State==RadarState.Complete ? "COMPLETED" : mission.State.ToString().ToUpperInvariant();
            bool current=mission.State==RadarState.Active;
            bool durationKnown=!task.IdentityUncertain && task.StartedUtc<=now.AddSeconds(5) && task.LastWriteUtc>=task.StartedUtc && task.LastWriteUtc<=now.AddSeconds(5) &&
                (!task.FinishedUtc.HasValue || (task.FinishedUtc.Value>=task.StartedUtc && task.FinishedUtc.Value<=now.AddSeconds(5)));
            elapsedReadout=durationKnown ? (current ? "Elapsed  " : mission.State==RadarState.Delegated ? "Parent elapsed  " : "Recorded  ")+task.Duration(current || mission.State==RadarState.Delegated ? now : task.LastWriteUtc) : "Duration unverified";
            elapsed.Text=elapsedReadout;
            model.Text="MODEL  "+task.Model;effort.Text="EFFORT  "+task.Effort;
            status.ForeColor=mission.State==RadarState.Active || mission.State==RadarState.Delegated ? Radar.Accent : mission.State==RadarState.Complete ? Color.FromArgb(72,214,155) :
                mission.State==RadarState.Failed ? InstrumentStyle.Red : InstrumentStyle.Amber;
            Edge=Parity?InstrumentStyle.Hairline:InstrumentStyle.Blend(InstrumentStyle.Border,status.ForeColor,.3);
            Rail=Parity?Color.Transparent:InstrumentStyle.Blend(InstrumentStyle.Border,status.ForeColor,.4);
            LiveTaskPhase phase=mission.State==RadarState.Unverified ? LiveTaskPhase.Unconfirmed : task.Phase;
            if(mission.State==RadarState.Delegated)Radar.SetDelegatedObserved(true);
            else Radar.SetObservation(phase,mission.State!=RadarState.Stale,false);
            string detail=mission.Label+"\n"+status.Text+" / "+elapsed.Text+"\n"+model.Text+"\n"+effort.Text+
                "\nLast source observation: "+task.LastWriteUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss zzz",CultureInfo.GetCultureInfo("en-GB"))+
                (mission.Missing ? "\nNot present in latest bounded read; completion unverified." : "\nLocal JSONL observation; active Desktop tab not guaranteed.");
            AccessibleName=title.Text+", "+status.Text;AccessibleDescription=detail;
            foreach(Control child in Controls)tooltip.SetToolTip(child,detail);tooltip.SetToolTip(this,detail);TargetValues();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(title==null)return;
            observation.Visible=Parity;Flat=Parity;
            if(Parity){ArrangeParity();return;}
            modelCaption.Visible=effortCaption.Visible=elapsedCaption.Visible=false;
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
            int w=(int)Math.Round(Width/scale),h=(int)Math.Round(Height/scale);
            bool micro=LayoutMode=="Micro",dock=LayoutMode=="Compact",vertical=LayoutMode=="Vertical";
            bool tall=vertical&&!Condensed;int radar=tall?100:micro?(Condensed?36:46):Condensed?(w<260?42:58):88;
            Radar.Parity=true;if(!dock)Radar.SetBounds(u(tall?(w-radar)/2:micro?8:12),u(tall?44:(h-radar)/2),u(radar),u(radar));
            int x=tall?12:dock?12:radar+(micro?20:28),available=Math.Max(1,w-x-12);
            Action<Control,int,int,int,int> box=(c,l,t,ww,hh)=>c.SetBounds(u(l),u(t),u(Math.Max(1,ww)),u(hh));
            foreach(Label l in new[]{title,status,elapsed,model,effort,observation,modelCaption,effortCaption,elapsedCaption})l.Visible=true;
            observation.Text=tall?"TACTICAL RADAR SCOPE":dock?"MISSION MONITOR":"CODEX ACTIVITY / LOCAL OBSERVED";
            observation.ForeColor=InstrumentStyle.Metadata;title.ForeColor=InstrumentStyle.Bright;model.ForeColor=effort.ForeColor=InstrumentStyle.Secondary;
            InstrumentStyle.Type(title,InstrumentType.Body);InstrumentStyle.Type(status,InstrumentType.Badge);InstrumentStyle.Type(observation,InstrumentType.Label);
            foreach(Label l in new[]{model,effort,elapsed})InstrumentStyle.Type(l,InstrumentType.Technical);
            foreach(Label l in new[]{modelCaption,effortCaption,elapsedCaption})InstrumentStyle.Type(l,InstrumentType.Metadata);
            ((InstrumentLabel)status).Framed=true;status.TextAlign=ContentAlignment.MiddleCenter;
            title.TextAlign=tall?ContentAlignment.MiddleCenter:ContentAlignment.MiddleLeft;
            int badge=Condensed?76:88;
            box(observation,tall?12:x,14,Math.Max(1,w-(tall?badge+36:x+badge+24)),15);box(status,w-badge-12,14,badge,18);
            if(tall) {
                box(title,12,164,w-24,23);
                box(modelCaption,22,197,w/2-28,12);box(effortCaption,w/2+4,197,w/2-26,12);
                box(model,22,211,w/2-28,16);box(effort,w/2+4,211,w/2-26,16);
                box(elapsedCaption,22,242,w/2,12);box(elapsed,w-112,240,90,17);elapsed.TextAlign=ContentAlignment.MiddleRight;FooterRule=u(155);
            } else if(micro) {
                observation.Visible=effort.Visible=modelCaption.Visible=effortCaption.Visible=elapsedCaption.Visible=false;FooterRule=0;
                int line=Condensed?3:12;InstrumentStyle.Type(title,Condensed?InstrumentType.Technical:InstrumentType.Panel);
                box(title,x,line,available-(Condensed?0:70),18);box(status,Condensed?x:w-72,Condensed?45:16,Condensed?72:62,14);
                InstrumentStyle.Type(model,InstrumentType.Metadata);InstrumentStyle.Type(elapsed,InstrumentType.Metadata);
                box(model,x,Condensed?25:34,Condensed?available:200,14);
                box(elapsed,Condensed?x+80:x+204,Condensed?45:34,Condensed?available-80:available-204,14);
            } else if(vertical && Condensed) {
                observation.Visible=false;int size=48;
                Radar.SetBounds(u((w-size)/2),u(31),u(size),u(size));
                InstrumentStyle.Type(title,InstrumentType.Technical);box(title,12,5,w-112,18);box(status,w-92,7,80,16);
                int cell=(w-32)/3;box(modelCaption,12,89,cell-4,12);box(effortCaption,16+cell,89,cell-4,12);box(elapsedCaption,20+cell*2,89,cell-4,12);
                box(model,12,104,cell-4,16);box(effort,16+cell,104,cell-4,16);box(elapsed,20+cell*2,104,cell-4,16);FooterRule=u(83);
            } else if(Condensed) {
                observation.Visible=false;FooterRule=0;InstrumentStyle.Type(title,InstrumentType.Technical);
                box(title,x,6,available,18);box(status,x,27,76,16);
                box(elapsed,x+82,27,available-82,16);box(model,x,49,available/2+8,16);box(effort,x+available/2+12,49,available/2-12,16);
                modelCaption.Visible=effortCaption.Visible=elapsedCaption.Visible=false;
                if(dock) {box(model,x,48,available/2+8,15);box(effort,x+available/2+12,48,available/2-12,15);}
            } else {
                box(title,x,dock?50:40,available,23);int y=dock?102:72;FooterRule=u(dock?94:65);
                int cell=available/(dock?2:3);
                box(modelCaption,x,y,cell,12);box(model,x,y+14,cell,17);
                box(effortCaption,x+cell+4,y,cell-4,12);box(effort,x+cell+4,y+14,cell-4,17);
                box(elapsedCaption,dock?x:x+cell*2+8,dock?140:y,dock?available:cell-8,12);
                box(elapsed,dock?x:x+cell*2+8,dock?154:y+14,dock?available:cell-8,17);
            }

            TargetValues();Flat=true;Rail=Color.Transparent;Edge=InstrumentStyle.Hairline;BackColor=InstrumentStyle.ActivityFace;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if(Parity && LayoutMode=="Vertical" && !Condensed) {
                float s=PeriscopeWindow.ScaleFor(this);
                using(Brush fill=new SolidBrush(InstrumentStyle.Canvas))using(Pen edge=new Pen(InstrumentStyle.Hairline))using(var path=InstrumentStyle.Rounded(new RectangleF(12*s,190*s,Width-24*s,72*s),3*s)) {
                    e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(edge,path);e.Graphics.DrawLine(edge,22*s,233*s,Width-22*s,233*s);
                }
            }
        }
        protected override void Dispose(bool disposing) {if(disposing){Radar.Dispose();tooltip.Dispose();}base.Dispose(disposing);}
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
        private string geometryKey;
        internal int GeometryPasses;
        private string layoutMode="Circular";
        internal bool Parity;
        internal MissionRadarDock CompactRadars;
        private List<MissionView> views=new List<MissionView>();
        internal string LayoutMode {get{return layoutMode;}set{if(layoutMode==value)return;layoutMode=value;Arrange();}}
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
            emptyRadar.Visible=cards.Count==0 && !(Parity && layoutMode=="Compact");emptyTitle.Visible=emptyDetail.Visible=cards.Count==0;
            Arrange();
        }
        internal List<MissionView> At(DateTimeOffset now){return tracker.At(now);}
        private bool InViewport(MissionCard card){return viewport.Visible && viewport.ClientRectangle.IntersectsWith(card.Bounds);}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Arrange();}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Arrange();}
        private void Arrange()
        {
            if(arranging || viewport==null || heading==null || cards==null || scroll==null)return;
            string key=Width+"/"+Height+"/"+PeriscopeWindow.ScaleFor(this)+"/"+layoutMode+"/"+Parity+"/"+scroll.Offset+"/"+BackColor.ToArgb()+"/"+string.Join(",",cards.Keys.OrderBy(n=>n));
            if(key==geometryKey) {if(Parity && layoutMode=="Compact" && CompactRadars!=null)CompactRadars.UpdateCards(cards.Values);return;}
            geometryKey=key;GeometryPasses++;arranging=true;
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
                    card.LayoutMode=layoutMode;card.Condensed=cards.Count>1;card.Parity=Parity;card.Radar.Parity=Parity;card.BackColor=Parity?InstrumentStyle.ActivityFace:InstrumentStyle.Blend(BackColor,Color.Black,.16);
                    card.SetBounds(i%columns*(cw+gap),i/columns*(rowHeight+gap)-scroll.Offset,cw,rowHeight);card.PerformLayout();i++;
                }
                if(CompactRadars!=null) {
                    if(Parity && layoutMode=="Compact")CompactRadars.UpdateCards(cards.Values.OrderBy(c=>c.Mission.Number));
                    else CompactRadars.Release();
                }
                int radar=Math.Min(u(layoutMode=="Micro"?40:layoutMode=="Circular"?100:72),Math.Max(1,height-u(4)));
                emptyRadar.SetBounds(u(6),(height-radar)/2,radar,radar);emptyRadar.BackColor=Color.Transparent;
                emptyTitle.SetBounds(radar+u(16),Math.Max(u(4),height/2-u(18)),Math.Max(1,viewport.Width-radar-u(24)),u(18));
                emptyDetail.SetBounds(emptyTitle.Left,emptyTitle.Bottom+u(2),emptyTitle.Width,u(17));
                if(Parity && layoutMode=="Compact") {emptyTitle.SetBounds(u(12),u(36),Math.Max(1,viewport.Width-u(24)),u(36));emptyDetail.SetBounds(u(12),u(82),emptyTitle.Width,u(34));}
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
    // Compact's radar column is disjoint from both quotas and mission details.
    // Existing radar controls move only when the selected layout/identity changes.
    internal sealed class MissionRadarDock : InstrumentPanel
    {
        private sealed class Slot {internal MissionCard Card;internal InstrumentPanel Face;internal InstrumentLabel State;}
        private readonly Dictionary<int,Slot> slots=new Dictionary<int,Slot>();
        private readonly ActivityGlyph empty=new ActivityGlyph();
        private bool arranging;
        internal MissionRadarDock(){Flat=true;Edge=InstrumentStyle.Hairline;BackColor=InstrumentStyle.Canvas;Controls.Add(empty);empty.SetObservation(LiveTaskPhase.Unknown,false,true);}
        internal void UpdateCards(IEnumerable<MissionCard> cards)
        {
            MissionCard[] list=cards.ToArray();bool changed=false;
            foreach(int number in slots.Keys.Where(n=>!list.Any(c=>c.Mission.Number==n)).ToArray()) {slots[number].Face.Dispose();slots.Remove(number);changed=true;}
            foreach(MissionCard card in list) {
                Slot slot;
                if(!slots.TryGetValue(card.Mission.Number,out slot)) {
                    slot=new Slot {Card=card,Face=new InstrumentPanel {Flat=true,BackColor=InstrumentStyle.ActivityFace,Edge=InstrumentStyle.Hairline},State=new InstrumentLabel {AutoEllipsis=true,TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.Transparent}};
                    slots.Add(card.Mission.Number,slot);Controls.Add(slot.Face);slot.Face.Controls.Add(slot.State);changed=true;
                }
                if(card.Radar.Parent!=slot.Face){card.Radar.Parent=slot.Face;changed=true;}
                card.Radar.Visible=true;slot.State.Text=card.Mission.State.ToString().ToUpperInvariant();slot.State.ForeColor=card.Radar.Accent;
            }
            empty.Visible=list.Length==0;if(changed)ArrangeSlots();
        }
        internal void Release()
        {
            if(slots.Count==0)return;
            foreach(Slot slot in slots.Values) {if(!slot.Card.IsDisposed && !slot.Card.Radar.IsDisposed)slot.Card.Radar.Parent=slot.Card;slot.Face.Dispose();}
            slots.Clear();empty.Visible=true;
        }
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);ArrangeSlots();}
        private void ArrangeSlots()
        {
            if(arranging || slots==null || empty==null)return;arranging=true;
            try {
                float scale=PeriscopeWindow.ScaleFor(this);Func<int,int> u=n=>(int)Math.Round(n*scale);
                int rows=Math.Max(1,slots.Count),gap=slots.Count>1?u(4):0,row=(Height-(rows-1)*gap)/rows,index=0;
                foreach(Slot slot in slots.Values.OrderBy(x=>x.Card.Mission.Number)) {
                    slot.Face.SetBounds(0,index++*(row+gap),Width,row);int side=u(slots.Count==1?68:46);
                    slot.Card.Radar.SetBounds((Width-side)/2,Math.Max(u(6),(row-side-u(17))/2),side,side);
                    slot.State.SetBounds(u(4),row-u(18),Math.Max(1,Width-u(8)),u(14));InstrumentStyle.FontFor(slot.State,slots.Count==1?9:8,FontStyle.Bold);
                }
                int radar=u(68);empty.SetBounds((Width-radar)/2,(Height-radar)/2,radar,radar);
            } finally {arranging=false;}
        }
    }

}

