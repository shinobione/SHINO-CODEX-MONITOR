using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal sealed class ActivityGlyph : Control
    {
        private LiveTaskPhase phase = LiveTaskPhase.Unknown;
        private float angle;
        internal LiveTaskPhase Phase { get { return phase; } }
        internal ActivityGlyph()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }
        internal void SetPhase(LiveTaskPhase next)
        {
            if (phase == next) return;
            phase = next;
            angle = 0;
            Invalidate();
        }
        internal void Advance()
        {
            if (phase != LiveTaskPhase.Active || !Visible) return;
            angle = (angle + 13) % 360;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float side = Math.Min(Width, Height);
            if (side < 12) return;
            float penSize = Math.Max(2f, side * .105f);
            RectangleF ring = new RectangleF((Width - side) / 2 + penSize,
                (Height - side) / 2 + penSize, side - penSize * 2, side - penSize * 2);
            Color accent = phase == LiveTaskPhase.Active ? Color.FromArgb(73, 218, 185) :
                phase == LiveTaskPhase.Completed ? Color.FromArgb(86, 229, 179) :
                phase == LiveTaskPhase.Failed ? Color.FromArgb(238, 75, 85) :
                phase == LiveTaskPhase.Interrupted ? Color.FromArgb(255, 182, 93) :
                Color.FromArgb(139, 160, 177);
            using (Pen stroke = new Pen(accent, penSize))
            {
                stroke.StartCap = LineCap.Round;
                stroke.EndCap = LineCap.Round;
                if (phase == LiveTaskPhase.Active)
                {
                    // Lightweight radar sweep: two concentric range rings and a
                    // rotating beam, with no image assets or background thread.
                    using (Pen track = new Pen(Color.FromArgb(39, 92, 104), Math.Max(1f, penSize * .42f)))
                    using (Pen line = new Pen(accent, Math.Max(1.5f, penSize * .65f)))
                    using (SolidBrush ping = new SolidBrush(Color.FromArgb(111, 255, 213)))
                    {
                        g.DrawEllipse(track, ring);
                        RectangleF inner = new RectangleF(ring.X + ring.Width * .25f,
                            ring.Y + ring.Height * .25f, ring.Width * .5f, ring.Height * .5f);
                        g.DrawEllipse(track, inner);
                        float cx = ring.X + ring.Width / 2f;
                        float cy = ring.Y + ring.Height / 2f;
                        double radians = (angle - 90) * Math.PI / 180.0;
                        g.DrawLine(line, cx, cy,
                            cx + (float)Math.Cos(radians) * ring.Width * .5f,
                            cy + (float)Math.Sin(radians) * ring.Height * .5f);
                        g.FillEllipse(ping, cx + ring.Width * .19f, cy - ring.Height * .29f,
                            Math.Max(2f, penSize * .58f), Math.Max(2f, penSize * .58f));
                    }
                }
                else
                {
                    g.DrawEllipse(stroke, ring);
                    float x = Width / 2f, y = Height / 2f;
                    if (phase == LiveTaskPhase.Completed)
                    {
                        g.DrawLines(stroke, new PointF[] {
                            new PointF(x - side * .21f, y),
                            new PointF(x - side * .05f, y + side * .15f),
                            new PointF(x + side * .25f, y - side * .17f) });
                    }
                    else if (phase == LiveTaskPhase.Failed || phase == LiveTaskPhase.Interrupted)
                    {
                        g.DrawLine(stroke, x - side * .14f, y - side * .14f,
                            x + side * .14f, y + side * .14f);
                        g.DrawLine(stroke, x + side * .14f, y - side * .14f,
                            x - side * .14f, y + side * .14f);
                    }
                    else
                    {
                        using (Font f = new Font("Segoe UI", side * .53f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center })
                        using (Brush brush = new SolidBrush(accent))
                            g.DrawString("?", f, brush, new RectangleF(0, 0, Width, Height), center);
                    }
                }
            }
        }
    }
}
