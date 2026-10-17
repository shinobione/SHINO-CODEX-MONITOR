using System;
using System.Drawing;

namespace ShinoCodexMonitor
{
    // Pure geometry for the optional resizable Windows desktop widget.
    // No screen queries or side effects here: all monitor working areas are passed in.
    internal static class DesktopGeometry
    {
        internal const int DefaultWidth = 540;
        internal const int MinimumWidth = 470;
        internal const int MaximumWidth = 720;

        internal static int ClampWidth(int proposed)
        {
            return Math.Max(MinimumWidth, Math.Min(MaximumWidth, proposed));
        }
        internal static int HeightForWidth(int width)
        {
            return (int)Math.Round(278.0 * ClampWidth(width) / DefaultWidth,
                MidpointRounding.AwayFromZero);
        }
        internal static int Scale(int value, int width)
        {
            return (int)Math.Round(value * (double)ClampWidth(width) / DefaultWidth,
                MidpointRounding.AwayFromZero);
        }
        internal static Rectangle ScaleRect(int x, int y, int width, int height, int widgetWidth)
        {
            return new Rectangle(Scale(x, widgetWidth), Scale(y, widgetWidth),
                Math.Max(1, Scale(width, widgetWidth)), Math.Max(1, Scale(height, widgetWidth)));
        }
        internal static Rectangle ClampToScreen(Rectangle proposed, Rectangle work)
        {
            int x = Math.Max(work.Left, Math.Min(proposed.Left, work.Right - proposed.Width));
            int y = Math.Max(work.Top, Math.Min(proposed.Top, work.Bottom - proposed.Height));
            return new Rectangle(x, y, proposed.Width, proposed.Height);
        }
        internal static Rectangle Snap(Rectangle proposed, Rectangle work, int distance)
        {
            if (distance < 0) distance = 0;
            int x = proposed.Left;
            int y = proposed.Top;
            int right = work.Right - proposed.Width;
            int bottom = work.Bottom - proposed.Height;
            if (Math.Abs(x - work.Left) <= distance) x = work.Left;
            else if (Math.Abs(x - right) <= distance) x = right;
            if (Math.Abs(y - work.Top) <= distance) y = work.Top;
            else if (Math.Abs(y - bottom) <= distance) y = bottom;
            return ClampToScreen(new Rectangle(x, y, proposed.Width, proposed.Height), work);
        }
    }
}
