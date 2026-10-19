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
    internal sealed class MissionCard : Panel
    {
        internal readonly ActivityGlyph Radar=new ActivityGlyph();
        private readonly Label title=new Label(), status=new Label(), elapsed=new Label(), metadata=new Label();
        private readonly ToolTip tooltip=new ToolTip();
        internal MissionView Mission { get; private set; }
        internal MissionCard()
        {
            DoubleBuffered=true; TabStop=true; BackColor=Color.FromArgb(17,28,41);
            foreach (Label label in new [] {title,status,elapsed,metadata}) {
                label.AutoEllipsis=true; label.BackColor=Color.Transparent; label.ForeColor=UiColors.Text;
                label.Font=new Font("Segoe UI",7.5f); Controls.Add(label);
            }
            Controls.Add(Radar);
        }
        internal void Update(MissionView mission, DateTimeOffset now)
        {
            Mission=mission; LiveTask task=mission.Task;
            title.Text=mission.Label;
            status.Text=mission.State == RadarState.Active ? "ACTIVE · observed" : mission.State == RadarState.Complete ? "COMPLETED" : mission.State.ToString().ToUpperInvariant();
            bool current=mission.State == RadarState.Active;
            bool durationKnown=!task.IdentityUncertain && task.StartedUtc <= now.AddSeconds(5) && task.LastWriteUtc >= task.StartedUtc && task.LastWriteUtc <= now.AddSeconds(5) &&
                (!task.FinishedUtc.HasValue || (task.FinishedUtc.Value >= task.StartedUtc && task.FinishedUtc.Value <= now.AddSeconds(5)));
            elapsed.Text=durationKnown ? (current ? "Elapsed " : "Recorded ") + task.Duration(current ? now : task.LastWriteUtc) : "Duration unverified";
            metadata.Text="Model: " + task.Model + " · Effort: " + task.Effort;
            status.ForeColor=mission.State == RadarState.Active ? Color.FromArgb(55,223,201) : mission.State == RadarState.Complete ? Color.FromArgb(72,214,155) : UiColors.Muted;
            LiveTaskPhase phase=mission.State == RadarState.Unverified ? LiveTaskPhase.Unconfirmed : task.Phase;
            Radar.SetObservation(phase,mission.State != RadarState.Stale,false);
            string detail=mission.Label + "\n" + status.Text + " · " + elapsed.Text + "\n" + metadata.Text +
                "\nLast source observation: " + task.LastWriteUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss zzz",CultureInfo.GetCultureInfo("en-GB")) +
                (mission.Missing ? "\nNot present in the latest bounded source read; completion unverified." : "\nLocal JSONL observation; active Desktop tab not guaranteed.");
            AccessibleName=title.Text + ", " + status.Text; AccessibleDescription=detail;
            foreach (Control child in Controls) tooltip.SetToolTip(child,detail);
            tooltip.SetToolTip(this,detail);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e); if (title == null) return;
            float scale=PeriscopeWindow.ScaleFor(this);
            Func<int,int> unit=value => (int)Math.Round(value*scale);
            bool wide=Width >= unit(300), tight=Height < unit(74);
            title.SetBounds(unit(6),unit(2),Math.Max(1,Width-unit(12)),unit(tight ? 17 : 20));
            Radar.SetBounds(unit(6),unit(tight ? 22 : 25),unit(wide ? 30 : 24),unit(wide ? 30 : 24));
            status.SetBounds(unit(wide ? 46 : 34),unit(tight ? 19 : 23),Math.Max(1,(wide ? unit(190) : Width)-unit(40)),unit(tight ? 15 : 17));
            elapsed.SetBounds(unit(wide ? 46 : 34),unit(tight ? 34 : 40),Math.Max(1,(wide ? unit(190) : Width)-unit(40)),unit(tight ? 15 : 17));
            int metaTop=unit(wide ? 26 : tight ? 50 : 56);
            metadata.SetBounds(unit(wide ? 212 : 6),metaTop,Math.Max(1,Width-unit(wide ? 218 : 12)),Math.Max(1,Math.Min(unit(wide ? 38 : 18),Height-metaTop)));
        }
        protected override void Dispose(bool disposing) { if (disposing) tooltip.Dispose(); base.Dispose(disposing); }
    }
    // One timer in MonitorForm, one monotonic clock here, many independent radar phases.
    internal sealed class MissionDeck : Panel
    {
        private readonly MissionTracker tracker=new MissionTracker();
        private readonly Dictionary<int,MissionCard> cards=new Dictionary<int,MissionCard>();
        private readonly Label heading=new Label();
        private readonly Panel viewport=new Panel { Dock=DockStyle.Fill, AutoScroll=true };
        private readonly Stopwatch clock=new Stopwatch();
        private double lastFrame;
        private List<MissionView> views=new List<MissionView>();
        internal int Count { get { return views.Count; } }
        internal IEnumerable<MissionCard> Cards { get { return cards.Values; } }
        internal bool NeedsFrames { get { return Visible && cards.Values.Any(c => InViewport(c) && c.Radar.NeedsFrames); } }
        internal MissionDeck()
        {
            DoubleBuffered=true; BackColor=Color.FromArgb(26,39,56);
            heading.Dock=DockStyle.Top; heading.Height=24; heading.Padding=new Padding(6,4,0,0); heading.ForeColor=UiColors.Muted;
            heading.Font=new Font("Segoe UI",7.5f,FontStyle.Bold);
            Controls.Add(viewport); Controls.Add(heading);
            viewport.SizeChanged += delegate { Arrange(); }; viewport.Scroll += delegate { SetMotion(clock.IsRunning); };
        }
        internal void UpdateReport(LiveTaskReport report, DateTimeOffset now)
        {
            tracker.Update(report); views=tracker.At(now);
            HashSet<int> present=new HashSet<int>(views.Select(v => v.Number));
            foreach (int number in cards.Keys.Where(k => !present.Contains(k)).ToArray()) { MissionCard card=cards[number]; cards.Remove(number); card.Dispose(); }
            foreach (MissionView view in views) {
                MissionCard card;
                if (!cards.TryGetValue(view.Number,out card)) { card=new MissionCard(); card.Update(view,now); cards.Add(view.Number,card); viewport.Controls.Add(card); }
                else card.Update(view,now);
            }
            heading.Text=views.Count == 0 ? "CODEX ACTIVITY · " + (report == null ? "UNAVAILABLE" : "UNVERIFIED") :
                views.Count == 1 ? "CODEX ACTIVITY · LOCAL / PARTIAL" : views.Count + " LOCAL TURNS · " + views.Count(v => v.State == RadarState.Active) + " OBSERVED ACTIVE";
            Arrange();
        }
        internal List<MissionView> At(DateTimeOffset now) { return tracker.At(now); }
        private bool InViewport(MissionCard card) { return viewport.Visible && viewport.ClientRectangle.IntersectsWith(card.Bounds); }
        private void Arrange()
        {
            if (viewport == null || heading == null || cards == null) return;
            float scale=PeriscopeWindow.ScaleFor(this);
            heading.Height=(int)Math.Round(24*scale);
            int columns=cards.Count == 2 || (cards.Count > 2 && viewport.ClientSize.Width >= 440*scale) ? 2 : 1;
            int rows=(cards.Count+columns-1)/columns;
            int height=cards.Count > 2 ? (int)Math.Round(76*scale) : Math.Max((int)Math.Round(64*scale),viewport.ClientSize.Height);
            int width=Math.Max(1,(viewport.ClientSize.Width-(rows*height > viewport.ClientSize.Height ? SystemInformation.VerticalScrollBarWidth : 0))/columns);
            int i=0; Point offset=viewport.AutoScrollPosition;
            foreach (MissionCard card in cards.Values.OrderBy(c => c.Mission.Number)) {
                card.SetBounds(i%columns*width+offset.X,i/columns*height+offset.Y,Math.Max(1,width-2),height-2); i++;
            }
            viewport.AutoScrollMinSize=new Size(0,rows*height);
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Arrange(); }
        internal void ConfirmCompletion(LiveTask task)
        {
            foreach (MissionCard card in cards.Values)
                if (TaskNotificationRules.SameTurn(card.Mission.Task,task) && card.Mission.State == RadarState.Complete) card.Radar.ConfirmCompletion(task.Identity);
        }
        internal void SetMotion(bool enabled)
        {
            bool running=enabled && NeedsFrames;
            if (running && !clock.IsRunning) { clock.Restart(); lastFrame=0; }
            if (!running) clock.Reset();
            foreach (MissionCard card in cards.Values) card.Radar.SetSharedMotion(running && InViewport(card),clock.Elapsed.TotalSeconds,card.Mission.Number*.41);
        }
        internal void Advance()
        {
            if (!clock.IsRunning) return;
            double t=clock.Elapsed.TotalSeconds;
            if (t-lastFrame > .5) { clock.Restart(); t=0; foreach (MissionCard card in cards.Values) card.Radar.FinishMotion(); }
            lastFrame=t;
            foreach (MissionCard card in cards.Values) if (InViewport(card)) { card.Radar.SetSharedMotion(true,t,card.Mission.Number*.41); card.Radar.Advance(); }
        }
    }
}
