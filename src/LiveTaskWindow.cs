using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    // Vector-only animation. No bitmaps, frameworks, network calls or permanent background animation.
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

    internal sealed class LiveTaskWindow : Form
    {
        private readonly Label summary;
        private readonly Label foot;
        private readonly ActivityGlyph[] glyphs = new ActivityGlyph[3];
        private readonly Label[] titles = new Label[3];
        private readonly Label[] subtitles = new Label[3];
        private readonly Label[] timings = new Label[3];
        private readonly System.Windows.Forms.Timer animation = new System.Windows.Forms.Timer();
        private LiveTaskReport report;

        internal LiveTaskWindow()
        {
            Text = "SHINO // LIVE TASK MONITOR";
            Size = new Size(510, 395);
            MinimumSize = new Size(510, 395);
            MaximumSize = new Size(510, 395);
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(17, 25, 38);
            ForeColor = Color.FromArgb(215, 230, 239);
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            Label heading = Label("LIVE TASK MONITOR", 18, 10, 450, 28, 12, true, ForeColor);
            Controls.Add(heading);
            summary = Label("Lecture des sessions locales…", 18, 39, 470, 22, 9, false,
                Color.FromArgb(139, 160, 177));
            Controls.Add(summary);
            for (int i = 0; i < glyphs.Length; i++)
            {
                int y = 70 + i * 83;
                Panel card = new Panel { BackColor = Color.FromArgb(27, 37, 53),
                    Location = new Point(16, y), Size = new Size(472, 77) };
                ActivityGlyph glyph = new ActivityGlyph { Location = new Point(12, 13), Size = new Size(46, 46) };
                card.Controls.Add(glyph);
                Label main = Label("—", 68, 7, 387, 21, 10, true, ForeColor);
                Label detail = Label("Aucune tâche", 68, 29, 390, 18, 8, false, Color.FromArgb(164, 177, 195));
                Label duration = Label("—", 68, 49, 390, 18, 8, false, Color.FromArgb(146, 166, 194));
                card.Controls.Add(main); card.Controls.Add(detail); card.Controls.Add(duration);
                glyphs[i] = glyph; titles[i] = main; subtitles[i] = detail; timings[i] = duration;
                Controls.Add(card);
            }
            foot = Label("Source : Codex local / sessions JSONL (format non documenté)", 18, 324,
                470, 33, 8, false, Color.FromArgb(139, 160, 177));
            Controls.Add(foot);
            animation.Interval = 85;
            animation.Tick += delegate {
                foreach (ActivityGlyph glyph in glyphs) glyph.Advance();
                // A running turn's duration is updated in real time, not inferred as percent complete.
                RefreshDuration();
            };
            VisibleChanged += delegate { UpdateAnimation(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };
        }

        private static Label Label(string text, int x, int y, int w, int h, int font,
            bool bold, Color color)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h),
                AutoEllipsis = true, ForeColor = color, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", font, bold ? FontStyle.Bold : FontStyle.Regular) };
        }

        internal void UpdateReport(LiveTaskReport next)
        {
            report = next;
            int count = next == null ? 0 : next.ActiveCount;
            summary.Text = count > 0 ? count + " tâche(s) observée(s) en cours" :
                next == null ? "État indisponible" : next.Message;
            LiveTask[] tasks = next == null ? new LiveTask[0] :
                next.Tasks.OrderBy(t => t.Phase == LiveTaskPhase.Active ? 0 :
                    t.Phase == LiveTaskPhase.Unconfirmed ? 1 : 2)
                    .ThenByDescending(t => t.LastWriteUtc).Take(3).ToArray();
            for (int i = 0; i < glyphs.Length; i++)
            {
                LiveTask task = i < tasks.Length ? tasks[i] : null;
                glyphs[i].SetPhase(task == null ? LiveTaskPhase.Unknown : task.Phase);
                titles[i].Text = task == null ? "Aucune autre tâche" :
                    task.Workspace + " · " + task.PhaseLabel;
                subtitles[i].Text = task == null ? "Aucune donnée supplémentaire" :
                    "Modèle : " + task.Model + "   ·   Effort : " + task.Effort;
                timings[i].Tag = task;
            }
            foot.Text = (next == null ? "Source indisponible" : next.Message) +
                "\nPas de surveillance Cloud garantie ; activité > 90 s non confirmée.";
            RefreshDuration();
            UpdateAnimation();
        }

        private void RefreshDuration()
        {
            for (int i = 0; i < timings.Length; i++)
            {
                LiveTask task = timings[i].Tag as LiveTask;
                timings[i].Text = task == null ? "—" :
                    "Durée : " + task.Duration(DateTimeOffset.UtcNow) + "  ·  Dernier fichier : " +
                    task.LastWriteUtc.ToLocalTime().ToString("HH:mm:ss");
            }
        }
        private void UpdateAnimation()
        {
            animation.Enabled = Visible && glyphs.Any(g => g.Phase == LiveTaskPhase.Active);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) animation.Dispose();
            base.Dispose(disposing);
        }
    }
}
