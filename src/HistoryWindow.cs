using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal sealed class HistoryWindow : PeriscopeWindow
    {
        private readonly HistoryCanvas canvas = new HistoryCanvas();
        private readonly Label metadata = new Label(), footer = new Label();
        private readonly Label[] values = new Label[4];
        private readonly FlowLayoutPanel tools = new FlowLayoutPanel();
        private readonly DataGridView table = new DataGridView();
        private readonly System.Windows.Forms.Timer freshness = new System.Windows.Forms.Timer { Interval = 30000 };
        private HistoryDataset data;
        private static int sessionHours = 24;
        private static HistorySeries sessionSeries = HistorySeries.Both;
        private int hours = sessionHours;
        private HistorySeries series = sessionSeries;
        private readonly DateTimeOffset? fixtureNow;
        private readonly List<Button> filters = new List<Button>();
        internal HistoryWindow(LocalStore store) : this(store.ReadHistory(), null) { }
        internal HistoryWindow(HistoryDataset source, DateTimeOffset? now)
        {
            data = source; fixtureNow = now;
            Text = "Quota History — CODEX PERISCOPE";
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(920, 620); MinimumSize = new Size(620, 460);
            BackColor = Color.FromArgb(8,14,23); ForeColor = Color.FromArgb(240,245,250);
            Font = new Font("Segoe UI", 9f);
            Label title = new Label { Text = "Quota History", Font = new Font("Segoe UI", 20f, FontStyle.Bold), Dock = DockStyle.Top, Height = 48, Padding = new Padding(20,8,0,0) };
            Label subtitle = new Label { Text = "Recorded Codex quota samples", Dock = DockStyle.Top, Height = 25, Padding = new Padding(22,0,0,0), ForeColor = UiColors.Muted };
            metadata.Dock = DockStyle.Top; metadata.Height = 40; metadata.Padding = new Padding(22,0,16,0); metadata.ForeColor = UiColors.Muted;
            tools.Dock = DockStyle.Top; tools.AutoSize = true; tools.WrapContents = true; tools.Padding = new Padding(18,4,12,8);
            foreach (int range in new [] {24,72,168,720,0}) {
                int h = range;
                string label = h == 24 ? "24H" : h == 72 ? "3D" : h == 168 ? "7D" : h == 720 ? "30D" : "ALL";
                Button button = ButtonFor(label); button.Tag = h;
                button.Click += delegate { hours = sessionHours = h; Rebuild(); }; filters.Add(button); tools.Controls.Add(button);
            }
            foreach (HistorySeries choice in new [] {HistorySeries.Five, HistorySeries.Weekly, HistorySeries.Both}) {
                HistorySeries selected = choice;
                Button button = ButtonFor(choice == HistorySeries.Five ? "5H" : choice == HistorySeries.Weekly ? "WEEKLY" : "BOTH");
                button.Tag = choice;
                button.Click += delegate { series = sessionSeries = selected; Rebuild(); }; filters.Add(button); tools.Controls.Add(button);
            }
            Button dataButton = ButtonFor("Data table"); dataButton.Width = 96;
            dataButton.Click += delegate { table.Visible = !table.Visible; canvas.Visible = !table.Visible; if (table.Visible) { FillTable(); table.BringToFront(); table.Focus(); } };
            tools.Controls.Add(dataButton);
            TableLayoutPanel summary = new TableLayoutPanel { Dock = DockStyle.Top, Height = 86, ColumnCount = 4, Padding = new Padding(18,4,18,10) };
            string[] names = { "LATEST", "LOWEST", "HIGHEST", "SAMPLES" };
            for (int i = 0; i < 4; i++) {
                summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
                Panel card = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(17,28,41), Margin = new Padding(4,0,4,0), Padding = new Padding(10,6,6,0) };
                Label label = new Label { Text = names[i], Dock = DockStyle.Top, Height = 19, ForeColor = UiColors.Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
                values[i] = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f, FontStyle.Bold), AccessibleName = names[i] };
                card.Controls.Add(values[i]); card.Controls.Add(label); summary.Controls.Add(card,i,0);
            }
            footer.Dock = DockStyle.Bottom; footer.Height = 64; footer.Padding = new Padding(22,8,18,0); footer.ForeColor = UiColors.Muted;
            canvas.Dock = DockStyle.Fill; canvas.TabStop = true; canvas.AccessibleName = "Quota remaining (%). Use Data table for keyboard access to recorded samples.";
            table.Dock = DockStyle.Fill; table.Visible = false; table.ReadOnly = true; table.AllowUserToAddRows = false; table.AllowUserToDeleteRows = false;
            table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; table.BackgroundColor = BackColor; table.BorderStyle = BorderStyle.None;
            table.DefaultCellStyle.BackColor = Color.FromArgb(17,28,41); table.DefaultCellStyle.ForeColor = ForeColor;
            table.RowHeadersVisible = false; table.AccessibleName = "Recorded quota samples, local time with UTC offset";
            foreach (string name in new [] {"Local time", "Series", "Remaining", "Source", "Expected reset", "Observed reset"}) table.Columns.Add(name,name);
            Controls.Add(canvas); Controls.Add(table); Controls.Add(footer); Controls.Add(summary); Controls.Add(tools); Controls.Add(metadata); Controls.Add(subtitle); Controls.Add(title);
            Activated += delegate { Rebuild(); };
            freshness.Tick += delegate { Rebuild(); };
            VisibleChanged += delegate { freshness.Enabled = Visible; };
            Resize += delegate { footer.Height = ClientSize.Width < 760 ? 96 : 64; };
            Rebuild();
        }
        private Button ButtonFor(string text)
        {
            Button b = new Button { Text = text, Width = text == "WEEKLY" ? 76 : 54, Height = 30, FlatStyle = FlatStyle.Flat,
                Margin = new Padding(4,2,0,2), BackColor = Color.FromArgb(23,36,52), ForeColor = ForeColor, AccessibleName = text };
            b.FlatAppearance.BorderColor = Color.FromArgb(38,54,73); b.FlatAppearance.MouseOverBackColor = Color.FromArgb(38,54,73);
            return b;
        }
        private void Rebuild()
        {
            DateTimeOffset now = fixtureNow ?? DateTimeOffset.UtcNow;
            HistoryView view = HistoryData.Select(data, now, hours, series); canvas.View = view;
            foreach (Button b in filters) {
                bool chosen = b.Tag is int ? (int)b.Tag == hours : (HistorySeries)b.Tag == series;
                b.BackColor = chosen ? Color.FromArgb(32,76,81) : Color.FromArgb(23,36,52);
                b.AccessibleDescription = chosen ? "Selected" : "Not selected";
            }
            HistoryRow last = data.Rows.LastOrDefault(r => r.Five.HasValue || r.Week.HasValue);
            int validCount = data.Rows.Count(r => r.Five.HasValue || r.Week.HasValue);
            metadata.Text = validCount + " valid records · " + (last == null ? "Last recorded sample: unavailable" : "Last recorded sample: " +
                last.Utc.ToLocalTime().ToString("dd MMM yyyy · HH:mm:ss zzz", HistoryData.English) +
                ((now - last.Utc).TotalMinutes > 10 ? " · STALE" : " · RECORDED"));
            List<HistoryPoint> five = view.Points.Where(p => p.Five).ToList(), week = view.Points.Where(p => !p.Five).ToList();
            for (int i = 0; i < values.Length; i++) values[i].Text = SummaryValue(five,i) + "\n" + SummaryValue(week,i);
            footer.Text = "5H Remaining · teal     Weekly Remaining · blue     Dashed: expected reset     Ring: observed replenishment\n" +
                (data.Unavailable ? "History unavailable" : data.InvalidRows > 0 ? "Some samples could not be validated (" + data.InvalidRows + "). " : "") +
                view.Message + " · Source: " + (last == null ? "unavailable" : last.Source);
            if (table.Visible) FillTable();
        }
        private static string SummaryValue(List<HistoryPoint> points, int metric)
        {
            string prefix = points.Count == 0 ? "" : points[0].Five ? "5H " : "W ";
            if (points.Count == 0) return "—";
            return prefix + (metric == 3 ? points.Count.ToString(HistoryData.English) : HistoryData.Pct(metric == 0 ? points[points.Count-1].Remaining : metric == 1 ? points.Min(p => p.Remaining) : points.Max(p => p.Remaining)));
        }
        private void FillTable()
        {
            table.Rows.Clear();
            foreach (HistoryPoint p in canvas.View.Points) table.Rows.Add(
                p.Utc.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss zzz", HistoryData.English), p.Five ? "5H" : "Weekly", HistoryData.Pct(p.Remaining), p.Source,
                p.ExpectedReset.HasValue ? p.ExpectedReset.Value.ToLocalTime().ToString("dd MMM HH:mm zzz", HistoryData.English) : "Not recorded", p.ObservedReset ? "Observed replenishment" : "Not confirmed");
        }
        internal void RefreshFrom(LocalStore store) { data = store.ReadHistory(); Rebuild(); }
        protected override void Dispose(bool disposing) { if (disposing) freshness.Dispose(); base.Dispose(disposing); }
    }
    internal sealed class HistoryCanvas : Panel
    {
        private HistoryView view = new HistoryView { Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow, Message = "No history recorded yet" };
        private readonly ToolTip tooltip = new ToolTip { InitialDelay = 100, ReshowDelay = 50, AutoPopDelay = 15000 };
        private readonly List<KeyValuePair<PointF,HistoryPoint>> hits = new List<KeyValuePair<PointF,HistoryPoint>>();
        private HistoryPoint hovered;
        internal HistoryView View { get { return view; } set { view = value; hovered = null; tooltip.Hide(this); Invalidate(); } }
        internal HistoryCanvas() { DoubleBuffered = true; ResizeRedraw = true; BackColor = Color.FromArgb(8,14,23); ForeColor = UiColors.Text; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; hits.Clear();
            float fontScale = Font.Size / 9f;
            float scale = g.DpiX / 96f * fontScale;
            RectangleF plot = new RectangleF(60 * scale, 40 * scale, Math.Max(1, Width - 92 * scale), Math.Max(1, Height - 104 * scale));
            using (Font font = new Font("Segoe UI",8f * fontScale))
            using (Brush muted = new SolidBrush(UiColors.Muted))
            using (Pen grid = new Pen(Color.FromArgb(31,47,65))) {
                g.DrawString("Quota remaining (%)",font,muted,plot.Left,12 * scale);
                foreach (int tick in new [] {0,25,50,75,100}) {
                    float y = plot.Bottom - plot.Height * tick / 100;
                    g.DrawLine(grid,plot.Left,y,plot.Right,y); g.DrawString(tick + "%",font,muted,10 * scale,y - 7 * scale);
                }
                double duration = Math.Max(1,(view.End-view.Start).TotalSeconds);
                int ticks = Math.Max(2,Math.Min(6,(int)(plot.Width / (duration > 86400 && duration <= 259200 ? 150 * scale : 105 * scale))));
                string format = duration <= 86400 ? "HH:mm" : duration <= 259200 ? "ddd dd · HH:mm" : "dd MMM";
                if (duration <= 259200 && view.Start.ToLocalTime().Offset != view.End.ToLocalTime().Offset) format += " zzz";
                if (duration > 366 * 86400) format = "MMM yyyy";
                for (int i=0; i<=ticks; i++) {
                    float x = plot.Left + plot.Width * i/ticks;
                    g.DrawLine(grid,x,plot.Top,x,plot.Bottom);
                    string label = view.Start.AddSeconds(duration * i/ticks).ToLocalTime().ToString(format,HistoryData.English);
                    SizeF size = g.MeasureString(label,font); float tx = Math.Max(0,Math.Min(Width-size.Width,x-size.Width/2));
                    g.DrawString(label,font,muted,tx,plot.Bottom+10 * scale);
                }
                if (view.Points.Count == 0) {
                    g.DrawString(view.Message ?? "No samples in selected range",Font,muted,plot.Left+12,plot.Top+plot.Height/2);
                    return;
                }
                foreach (bool five in new [] {true,false}) {
                    Color color = five ? Color.FromArgb(55,223,201) : Color.FromArgb(122,154,255);
                    using (Pen line = new Pen(color,1.8f * scale))
                    using (Pen reset = new Pen(Color.FromArgb(100,color),1f))
                    using (Brush ink = new SolidBrush(color)) {
                        reset.DashStyle = DashStyle.Dash;
                        PointF? previous = null; HistoryPoint latest = null;
                        foreach (HistoryPoint p in view.Points) {
                            if (p.Five != five) continue;
                            PointF point = new PointF(plot.Left + (float)((p.Utc-view.Start).TotalSeconds/duration)*plot.Width,plot.Bottom-(float)p.Remaining/100*plot.Height);
                            if (previous.HasValue && p.ConnectPrevious) g.DrawLine(line,previous.Value,point);
                            g.FillEllipse(ink,point.X-2.5f*scale,point.Y-2.5f*scale,5*scale,5*scale);
                            if (p.ObservedReset) {
                                g.DrawLine(reset,point.X,plot.Top,point.X,plot.Bottom);
                                g.DrawEllipse(line,point.X-5*scale,point.Y-5*scale,10*scale,10*scale);
                                // The marker is at the first confirming observation, not an invented event timestamp.
                                string label = five ? "5H RESET" : "WEEKLY RESET";
                                g.DrawString(label,font,ink,Math.Min(point.X+4,plot.Right-g.MeasureString(label,font).Width),plot.Top+(five ? 0 : 16)*scale);
                            }
                            hits.Add(new KeyValuePair<PointF,HistoryPoint>(point,p)); previous = point; latest = p;
                        }
                        if (latest != null && latest.ExpectedReset.HasValue && latest.ExpectedReset.Value >= view.Start && latest.ExpectedReset.Value <= view.End) {
                            float x = plot.Left + (float)((latest.ExpectedReset.Value-view.Start).TotalSeconds/duration)*plot.Width;
                            g.DrawLine(reset,x,plot.Top,x,plot.Bottom);
                            string label = five ? "5H EXPECTED" : "WEEKLY EXPECTED";
                            g.DrawString(label,font,ink,Math.Max(plot.Left,Math.Min(x+4,plot.Right-g.MeasureString(label,font).Width)),plot.Bottom-(five ? 32 : 16)*scale);
                        }
                    }
                }
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            HistoryPoint nearest = null; double distance = 100;
            foreach (KeyValuePair<PointF,HistoryPoint> hit in hits) {
                double d = Math.Pow(hit.Key.X-e.X,2)+Math.Pow(hit.Key.Y-e.Y,2);
                if (d < distance) { distance = d; nearest = hit.Value; }
            }
            if (nearest == hovered) return;
            hovered = nearest; tooltip.Hide(this);
            if (nearest == null) return;
            Size measured = TextRenderer.MeasureText(nearest.Tooltip,Font);
            int x = Math.Max(0,Math.Min(Width-measured.Width-20,e.X+14));
            int y = Math.Max(0,Math.Min(Height-measured.Height-20,e.Y+18));
            tooltip.Show(nearest.Tooltip,this,x,y,15000);
        }
        protected override void OnMouseLeave(EventArgs e) { hovered = null; tooltip.Hide(this); base.OnMouseLeave(e); }
        protected override void Dispose(bool disposing) { if (disposing) tooltip.Dispose(); base.Dispose(disposing); }
    }
}
