using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal enum RadarState { Active, Complete, Unverified, Stale, Unavailable, Failed, Interrupted }
    internal static class RadarMotion
    {
        internal static float AngleAt(double seconds) { return (float)((seconds % 3) * 120); }
        internal static RadarState Map(LiveTaskPhase phase, bool fresh, bool unavailable)
        {
            if (unavailable) return RadarState.Unavailable;
            if (!fresh) return RadarState.Stale;
            switch (phase) {
                case LiveTaskPhase.Active: return RadarState.Active;
                case LiveTaskPhase.Completed: return RadarState.Complete;
                case LiveTaskPhase.Failed: return RadarState.Failed;
                case LiveTaskPhase.Interrupted: return RadarState.Interrupted;
                default: return RadarState.Unverified;
            }
        }
    }
    internal sealed class ActivityGlyph : Control
    {
        private LiveTaskPhase phase = LiveTaskPhase.Unknown;
        private RadarState state = RadarState.Unavailable;
        private readonly Stopwatch clock = new Stopwatch();
        private double lastFrame;
        private bool completing;
        private bool sharedClock, sharedRunning;
        private double sharedSeconds, completionOrigin;
        private double AnimationSeconds { get { return sharedClock ? (completing ? Math.Max(0,sharedSeconds-completionOrigin) : sharedSeconds) : clock.Elapsed.TotalSeconds; } }
        private float completionAngle;
        private readonly HashSet<string> completions = new HashSet<string>();
        private Bitmap lens;
        private Color lensColor;
        private RadarState lensState;
        private Color accent=InstrumentStyle.Teal;
        private Bitmap sweepLight,pingLight;
        internal bool Parity;
        internal Color Accent {get{return accent;}set {
            if(accent==value)return;accent=value;
            if(lens!=null){lens.Dispose();lens=null;}
            if(sweepLight!=null){sweepLight.Dispose();sweepLight=null;}
            if(pingLight!=null){pingLight.Dispose();pingLight=null;}Invalidate();
        }}
        private readonly Brush[] trail = new Brush[24];
        private readonly Pen head = new Pen(Color.FromArgb(55,223,201),1.5f);
        private readonly Pen success = new Pen(Color.FromArgb(72,214,155),2f);
        private readonly SolidBrush contact = new SolidBrush(Color.FromArgb(55,223,201));
        private readonly Pen halo = new Pen(Color.FromArgb(50,55,223,201),1f);
        private readonly SolidBrush stateInk = new SolidBrush(Color.FromArgb(150,168,187));
        private readonly Font stateFont = new Font("Segoe UI",7f,FontStyle.Bold);
        private readonly StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        internal LiveTaskPhase Phase { get { return phase; } }
        internal RadarState State { get { return state; } }
        internal bool Completing { get { return completing; } }
        internal bool NeedsFrames { get { return Visible && (state == RadarState.Active || completing); } }
        internal bool MotionRunning { get { return sharedClock ? sharedRunning : clock.IsRunning; } }
        internal double ElapsedSeconds { get { return AnimationSeconds; } }
        internal ActivityGlyph()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,true);
            BackColor = Color.Transparent; TabStop = false;
            for (int i=0; i<trail.Length; i++) trail[i] = new SolidBrush(Color.FromArgb(2+i*3,55,223,201));
            head.StartCap = head.EndCap = LineCap.Round; success.StartCap = success.EndCap = LineCap.Round;
        }
        internal void SetPhase(LiveTaskPhase next) { SetObservation(next,true,next == LiveTaskPhase.Unknown); }
        internal void SetObservation(LiveTaskPhase next, bool fresh, bool unavailable)
        {
            RadarState mapped = RadarMotion.Map(next,fresh,unavailable);
            if (phase == next && state == mapped) return;
            if (state == RadarState.Active && mapped == RadarState.Complete)
                completionAngle = (Parity?(float)((AnimationSeconds%4)*90):RadarMotion.AngleAt(AnimationSeconds))-90;
            phase = next; state = mapped; FinishMotion();
            AccessibleName = "Observed task radar: " + StateLabel;
            Invalidate();
        }
        internal void ConfirmCompletion(string eventKey)
        {
            if (string.IsNullOrEmpty(eventKey) || !completions.Add(eventKey) || state != RadarState.Complete || !Visible) return;
            completing = true;
            if (sharedClock) completionOrigin=sharedSeconds; else clock.Restart();
            lastFrame = 0; Invalidate();
        }
        internal void SetSharedMotion(bool enabled, double seconds, double phaseOffset)
        {
            sharedClock=true; clock.Reset(); sharedSeconds=seconds+phaseOffset;
            sharedRunning=enabled && NeedsFrames;
            if (!sharedRunning && completing) FinishMotion();
        }
        internal void SetMotion(bool enabled)
        {
            if (enabled && NeedsFrames) { if (!clock.IsRunning) { clock.Restart(); lastFrame = 0; } }
            else { if (clock.IsRunning || completing) FinishMotion(); }
        }
        internal void FinishMotion() { completing = false; sharedRunning=false; clock.Reset(); lastFrame = 0; Invalidate(); }
        internal void Advance()
        {
            if (!NeedsFrames || !MotionRunning) return;
            double seconds = AnimationSeconds;
            // Timer starvation / sleep resumes a new visual epoch, never a giant angular jump.
            if (!sharedClock && seconds-lastFrame > .5) { if (completing) { FinishMotion(); return; } clock.Restart(); seconds = 0; }
            if (completing && seconds >= .9) { FinishMotion(); return; }
            lastFrame = seconds; Invalidate();
        }
        private string StateLabel { get { return state == RadarState.Active ? "ACTIVE" : state == RadarState.Complete ? "COMPLETE" : state.ToString().ToUpperInvariant(); } }
        protected override void OnVisibleChanged(EventArgs e) { if (!Visible) FinishMotion(); base.OnVisibleChanged(e); }
        private void EnsureLens()
        {
            Color background = BackColor.A == 0 ? (Parent == null ? Color.FromArgb(8,14,23) : Parent.BackColor) : BackColor;
            if (lens != null && lens.Size == ClientSize && lensColor == background && lensState == state) return;
            if (lens != null) lens.Dispose();
            lens = new Bitmap(Math.Max(1,Width),Math.Max(1,Height)); lensColor = background; lensState = state;
            using (Graphics g = Graphics.FromImage(lens)) {
                g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
                float side = Math.Min(Width,Height), radius = side * (Parity?.49f:.43f);
                float cx = Width/2f, cy = Height/2f;
                RectangleF ring = new RectangleF(cx-radius,cy-radius,radius*2,radius*2);
                using (GraphicsPath path = new GraphicsPath()) {
                    path.AddEllipse(ring);
                    using (PathGradientBrush fill = new PathGradientBrush(path)) {
                        fill.InterpolationColors=new ColorBlend {Positions=new []{0f,.3f,1f},Colors=new []{Color.FromArgb(2,5,8),Color.FromArgb(4,9,14),Color.FromArgb(6,17,24)}};
                        g.FillEllipse(fill,ring);
                    }
                }
                using(Pen bezel=new Pen(state==RadarState.Active ? Color.FromArgb(105,accent) : Color.FromArgb(75,137,161,178),1f)) {
                    if(!Parity)g.DrawEllipse(bezel,cx-side*.48f,cy-side*.48f,side*.96f,side*.96f);
                    if(!Parity)g.DrawEllipse(bezel,cx-side*.45f,cy-side*.45f,side*.9f,side*.9f);
                }
                using (Pen track = new Pen(state == RadarState.Active || state == RadarState.Complete ? Color.FromArgb(78,accent) : Color.FromArgb(70,135,147,165),1f)) {
                    for (int i=1; i<=4; i++) {
                        float r = radius*i/4;
                        RectangleF inner = new RectangleF(cx-r,cy-r,r*2,r*2);
                        if (side >= 40 || i % 2 == 0) g.DrawEllipse(track, inner);
                    }
                    g.DrawLine(track,cx,ring.Top,cx,ring.Bottom); g.DrawLine(track,ring.Left,cy,ring.Right,cy);
                    if (!Parity && side >= 40) for (int degree=0; degree<360; degree+=30) {
                        double a = degree*Math.PI/180; float len = degree%90 == 0 ? 5 : 3;
                        g.DrawLine(track,cx+(float)Math.Cos(a)*radius,cy+(float)Math.Sin(a)*radius,
                            cx+(float)Math.Cos(a)*(radius-len),cy+(float)Math.Sin(a)*(radius-len));
                    }
                }
            }
            if(sweepLight!=null)sweepLight.Dispose();sweepLight=new Bitmap(Math.Max(2,Width),Math.Max(2,Height));
            using(Graphics g=Graphics.FromImage(sweepLight))using(GraphicsPath path=new GraphicsPath()) {
                float side=Math.Min(Width,Height),r=side*(Parity?.49f:.43f);RectangleF box=new RectangleF(Width/2f-r,Height/2f-r,r*2,r*2);
                path.AddEllipse(box);
                using(PathGradientBrush light=new PathGradientBrush(path)) {
                    light.CenterPoint=new PointF(Width/2f,Height/2f);
                    light.InterpolationColors=new ColorBlend {Positions=new []{0f,.4f,1f},Colors=new []{Color.FromArgb(0,accent),Color.FromArgb(38,accent),Color.FromArgb(204,accent)}};
                    g.SmoothingMode=SmoothingMode.AntiAlias;g.FillPie(light,box.X,box.Y,box.Width,box.Height,-45,45);
                }
            }
            if(pingLight==null)pingLight=InstrumentStyle.Halo(64,accent);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float side = Math.Min(Width,Height); if (side < 12) return;
            EnsureLens(); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawImageUnscaled(lens,0,0);
            float cx = Width/2f, cy = Height/2f, radius = side * (Parity?.49f:.41f);
            RectangleF ring = new RectangleF(cx-radius,cy-radius,radius*2,radius*2);
            double seconds = AnimationSeconds;
            if (state == RadarState.Active || completing) {
                float angle = completing ? completionAngle : (Parity?(float)((seconds%4)*90):RadarMotion.AngleAt(seconds))-90;
                float fade = completing ? (float)Math.Max(0,1-seconds/.55) : 1;
                if (fade > 0) {
                    if(!completing) {
                        GraphicsState saved=g.Save();g.TranslateTransform(cx,cy);g.RotateTransform(angle);
                        g.DrawImageUnscaled(sweepLight,-Width/2,-Height/2);g.Restore(saved);
                        using(Pen beam=new Pen(Color.FromArgb(230,accent),Math.Max(1.2f,side*.012f))) {
                            double beamAngle=angle*Math.PI/180;g.DrawLine(beam,cx,cy,cx+(float)Math.Cos(beamAngle)*radius,cy+(float)Math.Sin(beamAngle)*radius);
                        }
                    }
                    int count = completing ? (side < 40 ? 8 : 24) : 0;
                    for (int i=0; i<count; i++) {
                        // Non-overlapping angular slices: cached brushes, no persistent trails.
                        int brush = (int)(i*23f/Math.Max(1,count-1));
                        if (completing) brush = (int)(brush*fade);
                        g.FillPie(trail[brush],ring.X,ring.Y,ring.Width,ring.Height,angle-48+i*48f/count,48f/count+.2f);
                    }
                    double a = angle*Math.PI/180;
                    Pen stroke = head;head.Width=Math.Max(1.5f,side*.018f);
                    head.Color = Color.FromArgb((int)(255*fade),accent);
                    if(completing)g.DrawLine(stroke, cx, cy,cx+(float)Math.Cos(a)*radius,cy+(float)Math.Sin(a)*radius);
                }
                if (!completing) {
                    float pulse = (float)((1-Math.Cos(seconds*Math.PI*2/(Parity?2:1.5)))/2);
                    float x = cx+radius*.36f, y = cy-radius*.53f, r = Math.Max(1.7f,side*.025f);
                    float glow=r*(6+2*pulse);g.DrawImage(pingLight,x-glow/2,y-glow/2,glow,glow);
                    g.DrawImage(pingLight,cx-r*3,cy-r*3,r*6,r*6);
                    contact.Color=accent;
                    g.FillEllipse(contact,x-r,y-r,r*2,r*2);
                    g.FillEllipse(contact,cx-r*.55f,cy-r*.55f,r*1.1f,r*1.1f);
                }
            }
            if (state == RadarState.Complete) {
                success.Width = Math.Max(1.8f,side*.05f);
                float progress = completing ? (float)Math.Min(1,seconds/.9) : 1;
                if (completing) {
                    float wave = radius*(.25f+.75f*progress);
                    halo.Color = Color.FromArgb((int)(100*(1-progress)),72,214,155); halo.Width = 1.5f;
                    g.DrawEllipse(halo,cx-wave,cy-wave,wave*2,wave*2);
                }
                if (!completing || progress > .45f) {
                    g.DrawLine(success,cx-side*.2f,cy,cx-side*.05f,cy+side*.14f);
                    g.DrawLine(success,cx-side*.05f,cy+side*.14f,cx+side*.24f,cy-side*.17f);
                }
            }
            else if (state != RadarState.Active) {
                stateInk.Color = state == RadarState.Failed ? Color.FromArgb(241,106,101) : state == RadarState.Interrupted ? Color.FromArgb(231,183,89) : Color.FromArgb(135,147,165);
                if (side < 64) g.DrawString(state == RadarState.Failed || state == RadarState.Interrupted ? "×" : "?",stateFont,stateInk,new RectangleF(0,0,Width,Height),centered);
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                if (lens != null) lens.Dispose(); foreach (Brush brush in trail) brush.Dispose();
                if(sweepLight!=null)sweepLight.Dispose();if(pingLight!=null)pingLight.Dispose();
                head.Dispose(); success.Dispose(); halo.Dispose(); contact.Dispose(); stateInk.Dispose(); stateFont.Dispose(); centered.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
