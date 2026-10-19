using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal sealed class HistoryWindow : Form
    {
        private readonly HistoryCanvas canvas;
        private readonly Label period;
        internal HistoryWindow(LocalStore store)
        {
            Text = "CODEX PERISCOPE — History local";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(700, 420);
            MinimumSize = new Size(520, 320);
            BackColor = UiColors.Background;
            ForeColor = UiColors.Text;
            Font = new Font("Segoe UI", 9f);
            Label header = new Label { Text = "Remaining quota (%) · real samples only · gaps are not interpolated",
                Dock = DockStyle.Top, Height = 34, ForeColor = UiColors.Muted, Padding = new Padding(12, 8, 0, 0) };
            FlowLayoutPanel tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(8, 3, 0, 0) };
            canvas = new HistoryCanvas { Dock = DockStyle.Fill, TabStop = true };
            foreach (int hours in new int[] { 24, 24 * 7, 24 * 14 })
            {
                int value = hours;
                Button button = MakeButton(hours == 24 ? "24 h" : hours == 168 ? "7 j" : "14 j", 60);
                button.Click += delegate { SetHours(value); };
                tools.Controls.Add(button);
            }
            Button zoomIn = MakeButton("Zoom +", 80);
            zoomIn.Click += delegate { SetHours(Math.Max(6, canvas.VisibleHours / 2)); };
            tools.Controls.Add(zoomIn);
            Button zoomOut = MakeButton("Zoom −", 80);
            zoomOut.Click += delegate { SetHours(Math.Min(336, canvas.VisibleHours * 2)); };
            tools.Controls.Add(zoomOut);
            period = new Label { Width = 145, Height = 28, ForeColor = UiColors.Muted,
                Padding = new Padding(8, 6, 0, 0) };
            tools.Controls.Add(period);
            Controls.Add(canvas);
            Controls.Add(tools);
            Controls.Add(header);
            canvas.MouseWheel += delegate(object sender, MouseEventArgs e) {
                SetHours(Math.Max(6, Math.Min(336, canvas.VisibleHours * (e.Delta > 0 ? 0.5 : 2))));
            };
            canvas.Click += delegate { canvas.Focus(); };
            SetHours(24);
            RefreshFrom(store);
        }
        private static Button MakeButton(string label, int width)
        {
            return new Button { Text = label, Width = width, Height = 27,
                ForeColor = UiColors.Text, BackColor = UiColors.Track,
                FlatStyle = FlatStyle.Flat, Margin = new Padding(3, 3, 3, 0) };
        }
        private void SetHours(double hours)
        {
            canvas.VisibleHours = Math.Max(6, Math.Min(336, hours));
            period.Text = "Period: " + (canvas.VisibleHours < 48 ?
                canvas.VisibleHours.ToString("0.#", CultureInfo.InvariantCulture) + " h" :
                (canvas.VisibleHours / 24).ToString("0.#", CultureInfo.InvariantCulture) + " j");
            canvas.Invalidate();
        }
        internal void RefreshFrom(LocalStore store) { canvas.Rows = store.GetHistory(); canvas.Invalidate(); }
    }
    internal sealed class HistoryCanvas : Panel
    {
        internal List<HistoryRow> Rows = new List<HistoryRow>();
        internal double VisibleHours = 24;
        internal HistoryCanvas() { DoubleBuffered = true; BackColor = UiColors.Background; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int left = 48, right = Math.Max(60, Width - 20), top = 20, bottom = Math.Max(90, Height - 65);
            float width = Math.Max(1, right - left), height = Math.Max(1, bottom - top);
            DateTimeOffset maxTime = DateTimeOffset.UtcNow;
            DateTimeOffset minTime = maxTime.AddHours(-VisibleHours);
            int count = 0;
            foreach (HistoryRow row in Rows) if (row.Utc >= minTime && row.Utc <= maxTime) count++;
            using (Font font = new Font("Segoe UI", 8f))
            using (SolidBrush muted = new SolidBrush(UiColors.Muted))
            using (Pen grid = new Pen(UiColors.Track, 1f))
            {
                foreach (int tick in new int[] { 0, 25, 50, 75, 100 })
                {
                    float y = bottom - tick / 100f * height;
                    g.DrawLine(grid, left, y, right, y);
                    g.DrawString(tick.ToString(CultureInfo.InvariantCulture), font, muted, 8, y - 7);
                }
                if (count == 0)
                    g.DrawString("No samples in this period.", font, muted, 65, Height / 2f);
                else
                {
                    DrawSeries(g, Rows, minTime, maxTime, left, width, height, bottom, true, UiColors.Five);
                    DrawSeries(g, Rows, minTime, maxTime, left, width, height, bottom, false, UiColors.Week);
                }
                string leftLabel = VisibleHours <= 48 ? "dd/MM HH:mm" : "dd/MM";
                g.DrawString(minTime.ToLocalTime().ToString(leftLabel), font, muted, left, bottom + 12);
                g.DrawString(maxTime.ToLocalTime().ToString("dd/MM HH:mm"), font, muted,
                    Math.Max(left + 4, right - 95), bottom + 12);
                using (SolidBrush five = new SolidBrush(UiColors.Five))
                using (SolidBrush week = new SolidBrush(UiColors.Week))
                {
                    g.DrawString("━  5 h", font, five, left, Height - 34);
                    g.DrawString("━  weekly", font, week, left + 85, Height - 34);
                }
                g.DrawString(count + " sample" + (count > 1 ? "s" : ""), font, muted,
                    Math.Max(left + 155, right - 100), Height - 34);
            }
        }
        private static void DrawSeries(Graphics g, List<HistoryRow> rows, DateTimeOffset min, DateTimeOffset max,
            int left, float width, float height, int bottom, bool five, Color color)
        {
            PointF? previous = null;
            DateTimeOffset previousTime = DateTimeOffset.MinValue;
            double duration = Math.Max(1, (max - min).TotalSeconds);
            using (Pen pen = new Pen(color, 1.8f))
            using (SolidBrush brush = new SolidBrush(color))
            {
                foreach (HistoryRow row in rows)
                {
                    double? amount = five ? row.Five : row.Week;
                    if (!amount.HasValue || row.Utc < min || row.Utc > max) { previous = null; continue; }
                    float x = left + (float)((row.Utc - min).TotalSeconds / duration) * width;
                    float y = bottom - (float)(amount.Value / 100.0) * height;
                    PointF point = new PointF(x, y);
                    if (previous.HasValue && row.Utc - previousTime <= TimeSpan.FromMinutes(30))
                        g.DrawLine(pen, previous.Value, point);
                    g.FillEllipse(brush, x - 2.25f, y - 2.25f, 4.5f, 4.5f);
                    previous = point;
                    previousTime = row.Utc;
                }
            }
        }
    }
}
