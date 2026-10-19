using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ShinoCodexMonitor
{
    // Dedicated tray icon renderer for tiny Windows notification-area sizes.
    // It uses a thick progress ring and a manually drawn '%' glyph instead of text,
    // which remains far more legible at 16x16 than the previous detailed icon.
    internal static class QuotaTrayIcon
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        internal static Icon Create(double? remaining, Color accent, bool isCurrent, string mode, LiveTaskPhase task = LiveTaskPhase.Unknown)
        {
            const int n = 64;
            Color liveAccent = accent;
            if (!isCurrent || !remaining.HasValue) liveAccent = Color.FromArgb(147, 159, 175);
            double amount = remaining.HasValue && isCurrent ? Math.Max(0, Math.Min(100, remaining.Value)) : 0;

            using (Bitmap bitmap = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                RectangleF arc = new RectangleF(6, 6, 52, 52);
                using (Pen track = new Pen(Color.FromArgb(62, 80, 98), 10f))
                {
                    track.StartCap = LineCap.Round;
                    track.EndCap = LineCap.Round;
                    g.DrawArc(track, arc, 135, 270);
                }
                if (amount > 0)
                {
                    using (Pen fill = new Pen(liveAccent, 10f))
                    {
                        fill.StartCap = LineCap.Round;
                        fill.EndCap = LineCap.Round;
                        g.DrawArc(fill, arc, 135, (float)(270 * amount / 100.0));
                    }
                }

                // In Ring mode, keep the entire centre clear for maximum legibility at 16px.
                // In Percent mode, draw a simple high-contrast glyph over the live gauge.
                if (mode != "Ring")
                {
                    using (Pen glyph = new Pen(Color.FromArgb(238, 247, 255), 5.3f))
                    using (Brush dotBrush = new SolidBrush(Color.FromArgb(238, 247, 255)))
                    {
                        glyph.StartCap = LineCap.Round;
                        glyph.EndCap = LineCap.Round;
                        g.FillEllipse(dotBrush, 18, 17, 8, 8);
                        g.FillEllipse(dotBrush, 38, 39, 8, 8);
                        g.DrawLine(glyph, 24, 44, 40, 20);
                    }
                }

                // A small secondary dot keeps the original quota glyph legible.
                // No motion in the notification area: zero continuous icon churn.
                if (task == LiveTaskPhase.Active || task == LiveTaskPhase.Completed ||
                    task == LiveTaskPhase.Failed || task == LiveTaskPhase.Interrupted)
                {
                    Color dot = task == LiveTaskPhase.Active ? Color.FromArgb(41, 216, 195) :
                        task == LiveTaskPhase.Completed ? Color.FromArgb(81, 215, 175) :
                        task == LiveTaskPhase.Failed ? Color.FromArgb(238, 75, 85) :
                        Color.FromArgb(255, 182, 93);
                    using (Brush rim = new SolidBrush(Color.FromArgb(17, 25, 38)))
                    using (Brush spot = new SolidBrush(dot))
                    {
                        g.FillEllipse(rim, 41, 41, 23, 23);
                        g.FillEllipse(spot, 44, 44, 17, 17);
                    }
                }
                IntPtr native = bitmap.GetHicon();
                if (native == IntPtr.Zero)
                    throw new InvalidOperationException("Création d'icône Windows impossible.");
                try
                {
                    using (Icon borrowed = Icon.FromHandle(native))
                        return (Icon)borrowed.Clone();
                }
                finally { DestroyIcon(native); }
            }
        }
    }
}
