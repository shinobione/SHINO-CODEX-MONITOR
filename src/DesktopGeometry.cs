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
        // Compute an integrated activity strip underneath the existing quota footer.
        // This is geometry only: no session access or WinForms side effects.
        internal static Rectangle ActivityBounds(int width, int baseHeight, string layout)
        {
            bool circular = layout == "Circular";
            int margin = circular ? Scale(14, width) : layout == "Micro" ? 8 : 14;
            int gap = circular ? Scale(8, width) : 8;
            int activityHeight = circular ? Scale(100, width) :
                layout == "Micro" ? 92 : layout == "Compact" ? 96 : 110;
            int top=baseHeight+gap;
            // Rebalance existing space; preserve the exact outer dimensions even
            // when independent logical-pixel rounding differs at custom widths.
            if(circular) { int bottom=top+activityHeight;top=baseHeight-Scale(22,width);activityHeight=bottom-top; }
            if(layout=="Micro") { top+=8;activityHeight-=8; }
            return new Rectangle(margin, top,
                Math.Max(1, width - 2 * margin), activityHeight);
        }
        internal static int CombinedHeight(int width, int baseHeight, string layout)
        {
            Rectangle dock = ActivityBounds(width, baseHeight, layout);
            return dock.Bottom + (layout == "Circular" ? Scale(12, width) : 8);
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
