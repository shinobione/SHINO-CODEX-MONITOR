using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal static class QuotaPalette
    {
        // Thresholds refer to the INTEGER percentage displayed to the user.
        internal static string Hex(int remaining, Preferences p)
        {
            int value = Math.Max(0, Math.Min(100, remaining));
            return value >= 76 ? p.Color76 : value >= 51 ? p.Color51 :
                value >= 26 ? p.Color26 : value >= 11 ? p.Color11 : p.Color0;
        }
        internal static Color ColorFor(int remaining, Preferences p)
        {
            return ColorTranslator.FromHtml(Hex(remaining, p));
        }
    }

    internal sealed class CircularGauge : Control
    {
        private int? percentage;
        private bool current;
        private Color accent = Color.Gray;
        internal CircularGauge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = ColorTranslator.FromHtml("#111926");
            TabStop = false;
        }
        internal void SetReading(int? remaining, bool isCurrent, Color color)
        {
            int? clamped = remaining.HasValue ? Math.Max(0, Math.Min(100, remaining.Value)) : (int?)null;
            if (percentage == clamped && current == isCurrent && accent == color) return;
            percentage = clamped;
            current = isCurrent;
            accent = color;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            int side = Math.Min(ClientSize.Width, ClientSize.Height);
            if (side <= 14) return;
            float stroke = Math.Max(5f, side * .086f);
            float diameter = side - stroke - 5f;
            RectangleF circle = new RectangleF((Width - diameter) / 2f, (Height - diameter) / 2f, diameter, diameter);
            using (Pen track = new Pen(ColorTranslator.FromHtml("#29394C"), stroke))
                g.DrawArc(track, circle, -90f, 360f);
            if (percentage.HasValue && percentage.Value > 0)
            {
                using (Pen fill = new Pen(current ? accent : ColorTranslator.FromHtml("#8BA0B1"), stroke))
                {
                    fill.StartCap = LineCap.Round;
                    fill.EndCap = LineCap.Round;
                    g.DrawArc(fill, circle, -90f, 360f * percentage.Value / 100f);
                }
            }
            string text = !percentage.HasValue ? "—" : percentage.Value + (current ? "%" : "%*");
            using (Font center = new Font("Segoe UI", percentage.HasValue ? side * .185f : side * .22f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush ink = new SolidBrush(!percentage.HasValue || !current ? ColorTranslator.FromHtml("#8BA0B1") : accent))
            using (StringFormat alignment = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, center, ink, new RectangleF(0, 0, Width, Height), alignment);
        }
    }
}
