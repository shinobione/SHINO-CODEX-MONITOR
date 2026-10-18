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
            Color accent = phase == LiveTaskPhase.Active ? Color.FromArgb(156, 126, 255) :
                phase == LiveTaskPhase.Completed ? Color.FromArgb(81, 215, 175) :
                phase == LiveTaskPhase.Failed ? Color.FromArgb(238, 75, 85) :
                phase == LiveTaskPhase.Interrupted ? Color.FromArgb(255, 182, 93) :
                Color.FromArgb(139, 160, 177);
            using (Pen stroke = new Pen(accent, penSize))
            {
                stroke.StartCap = LineCap.Round;
                stroke.EndCap = LineCap.Round;
                if (phase == LiveTaskPhase.Active)
                {
                    using (Pen track = new Pen(Color.FromArgb(41, 57, 76), penSize))
                        g.DrawEllipse(track, ring);
                    g.DrawArc(stroke, ring, -90 + angle, 260);
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
