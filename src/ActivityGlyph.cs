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
        private float completionAngle;
        private readonly HashSet<string> completions = new HashSet<string>();
        private Bitmap lens;
        private Color lensColor;
        private RadarState lensState;
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
        internal bool MotionRunning { get { return clock.IsRunning; } }
        internal double ElapsedSeconds { get { return clock.Elapsed.TotalSeconds; } }
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
                completionAngle = RadarMotion.AngleAt(clock.Elapsed.TotalSeconds)-90;
            phase = next; state = mapped; FinishMotion();
            AccessibleName = "Observed task radar: " + StateLabel;
            Invalidate();
        }
        internal void ConfirmCompletion(string eventKey)
        {
            if (string.IsNullOrEmpty(eventKey) || !completions.Add(eventKey) || state != RadarState.Complete || !Visible) return;
            completing = true; clock.Restart(); lastFrame = 0; Invalidate();
        }
        internal void SetMotion(bool enabled)
        {
            if (enabled && NeedsFrames) { if (!clock.IsRunning) { clock.Restart(); lastFrame = 0; } }
            else { if (clock.IsRunning || completing) FinishMotion(); }
        }
        internal void FinishMotion() { completing = false; clock.Reset(); lastFrame = 0; Invalidate(); }
        internal void Advance()
        {
            if (!NeedsFrames || !clock.IsRunning) return;
            double seconds = clock.Elapsed.TotalSeconds;
            // Timer starvation / sleep resumes a new visual epoch, never a giant angular jump.
            if (seconds-lastFrame > .5) { if (completing) { FinishMotion(); return; } clock.Restart(); seconds = 0; }
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
                g.Clear(background); g.SmoothingMode = SmoothingMode.AntiAlias;
                float side = Math.Min(Width,Height), radius = side * .43f;
                float cx = Width/2f, cy = Height/2f;
                RectangleF ring = new RectangleF(cx-radius,cy-radius,radius*2,radius*2);
                using (GraphicsPath path = new GraphicsPath()) {
                    path.AddEllipse(ring);
                    using (PathGradientBrush fill = new PathGradientBrush(path)) {
                        fill.CenterColor = Color.FromArgb(20,46,59); fill.SurroundColors = new [] {Color.FromArgb(8,17,28)}; g.FillEllipse(fill,ring);
                    }
                }
                using (Pen track = new Pen(state == RadarState.Active || state == RadarState.Complete ? Color.FromArgb(70,69,113,131) : Color.FromArgb(70,135,147,165),1f)) {
                    for (int i=1; i<=4; i++) {
                        float r = radius*i/4;
                        RectangleF inner = new RectangleF(cx-r,cy-r,r*2,r*2);
                        if (side >= 40 || i % 2 == 0) g.DrawEllipse(track, inner);
                    }
                    g.DrawLine(track,cx,ring.Top,cx,ring.Bottom); g.DrawLine(track,ring.Left,cy,ring.Right,cy);
                    if (side >= 40) for (int degree=0; degree<360; degree+=30) {
                        double a = degree*Math.PI/180; float len = degree%90 == 0 ? 5 : 3;
                        g.DrawLine(track,cx+(float)Math.Cos(a)*radius,cy+(float)Math.Sin(a)*radius,
                            cx+(float)Math.Cos(a)*(radius-len),cy+(float)Math.Sin(a)*(radius-len));
                    }
                }
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float side = Math.Min(Width,Height); if (side < 12) return;
            EnsureLens(); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawImageUnscaled(lens,0,0);
            float cx = Width/2f, cy = Height/2f, radius = side * .41f;
            RectangleF ring = new RectangleF(cx-radius,cy-radius,radius*2,radius*2);
            double seconds = clock.Elapsed.TotalSeconds;
            if (state == RadarState.Active || completing) {
                float angle = completing ? completionAngle : RadarMotion.AngleAt(seconds)-90;
                float fade = completing ? (float)Math.Max(0,1-seconds/.55) : 1;
                if (fade > 0) {
                    int count = side < 40 ? 8 : 24;
                    for (int i=0; i<count; i++) {
                        // Non-overlapping angular slices: cached brushes, no persistent trails.
                        int brush = (int)(i*23f/Math.Max(1,count-1));
                        if (completing) brush = (int)(brush*fade);
                        g.FillPie(trail[brush],ring.X,ring.Y,ring.Width,ring.Height,angle-48+i*48f/count,48f/count+.2f);
                    }
                    double a = angle*Math.PI/180;
                    Pen stroke = head;
                    head.Color = Color.FromArgb((int)(255*fade),55,223,201);
                    g.DrawLine(stroke, cx, cy,cx+(float)Math.Cos(a)*radius,cy+(float)Math.Sin(a)*radius);
                }
                if (!completing) {
                    float pulse = (float)((1-Math.Cos(seconds*Math.PI*2/1.5))/2);
                    float x = cx+radius*.36f, y = cy-radius*.53f, r = Math.Max(1.7f,side*.025f);
                    halo.Color = Color.FromArgb(50,55,223,201);
                    halo.Width = 1+2*pulse;
                    g.DrawEllipse(halo,x-r*(2+pulse),y-r*(2+pulse),r*(4+2*pulse),r*(4+2*pulse));
                    g.FillEllipse(contact,x-r,y-r,r*2,r*2);
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
            if (side >= 72) {
                stateInk.Color = state == RadarState.Active ? Color.FromArgb(55,223,201) : state == RadarState.Complete ? Color.FromArgb(72,214,155) : Color.FromArgb(150,168,187);
                g.DrawString(StateLabel,stateFont,stateInk,new RectangleF(0,Height-side*.24f,Width,side*.15f),centered);
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                if (lens != null) lens.Dispose(); foreach (Brush brush in trail) brush.Dispose();
                head.Dispose(); success.Dispose(); halo.Dispose(); contact.Dispose(); stateInk.Dispose(); stateFont.Dispose(); centered.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
