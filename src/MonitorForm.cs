using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShinoCodexMonitor
{
    internal static class UiColors
    {
        internal static readonly Color Background = ColorTranslator.FromHtml("#111926");
        internal static readonly Color Text = ColorTranslator.FromHtml("#D7E6EF");
        internal static readonly Color Muted = ColorTranslator.FromHtml("#8BA0B1");
        internal static readonly Color Track = ColorTranslator.FromHtml("#29394C");
        internal static readonly Color Five = ColorTranslator.FromHtml("#51D7AF");
        internal static readonly Color Week = ColorTranslator.FromHtml("#E5B64D");
        internal static readonly Color Warning = ColorTranslator.FromHtml("#FFB65D");
    }
    internal sealed class Meter
    {
        internal Label Name, Number, Reset;
        internal Panel Track, Fill;
        internal CircularGauge Ring;
        internal QuotaWindow Data;
    }
    internal sealed class MonitorForm : Form
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "CODEX_PERISCOPE";
        private const string LegacyRunName = "SHINO_CODEX_MONITOR_V4";
        private const string OlderRunName = "SHINO_CODEX_MONITOR";
        private readonly LocalStore store;
        private readonly Preferences settings;
        private readonly NotifyIcon tray;
        private Icon dynamicTrayIcon;
        private Icon originalTrayIcon;
        private string trayIconSignature = "";
        private readonly ContextMenuStrip context = new ContextMenuStrip();
        private readonly Label header;
        private readonly Label version;
        private readonly Label close;
        private readonly Label status;
        private readonly Label statusAge;
        private readonly Panel statusDivider;
        private readonly Label resizeGrip;
        private readonly Meter five;
        private readonly Meter week;
        private readonly ToolTip tooltip = new ToolTip();
        private readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
        private HistoryWindow history;
        private Snapshot snapshot;
        private DateTimeOffset? lastSuccess;
        private DateTimeOffset nextRefresh = DateTimeOffset.MinValue;
        private Task<Snapshot> active;
        private readonly HashSet<string> alerted = new HashSet<string>();
        private int failures;
        private string lastError = "";
        private string binaryPath = "Not found";
        private bool exiting;
        private readonly RuntimeMetrics runtime = new RuntimeMetrics();
        private DateTimeOffset lastHeartbeatUtc = DateTimeOffset.MinValue;
        private DateTimeOffset lastUiPaintUtc = DateTimeOffset.MinValue;
        private DateTimeOffset invalidAfterUtc = DateTimeOffset.MinValue;
        private volatile int readerGeneration;
        private bool forcedStale;
        private bool suspended;
        private bool suppressNextAlert;
        private int wakeRefreshes;
        private string lastWakeReason = "None";
        private Point? dragMouse, dragOrigin;
        private Point? resizeMouse;
        private int resizeOriginalWidth;
        private readonly Dictionary<string, ToolStripMenuItem> intervalItems = new Dictionary<string, ToolStripMenuItem>();
        private ToolStripMenuItem updateMenuItem;
        private bool updateCheckRunning;
        private readonly Panel taskDock = new Panel();
        private readonly Panel taskRule = new Panel();
        private readonly ActivityGlyph taskGlyph = new ActivityGlyph();
        private readonly Label taskHeading;
        private readonly Label taskSource;
        private readonly Label taskBadge;
        private readonly Label taskName;
        private readonly Label taskMeta;
        private readonly Label taskExtra;
        private readonly System.Windows.Forms.Timer taskMotion = new System.Windows.Forms.Timer();
        private LiveTaskReport taskReport;
        private Task<LiveTaskReport> taskRead;
        private DateTimeOffset nextTaskPoll = DateTimeOffset.MinValue;
        private readonly HashSet<string> sentTaskNotifications = new HashSet<string>();

        internal MonitorForm()
        {
            store = new LocalStore();
            settings = store.Load();
            Text = "CODEX PERISCOPE V5.2.0";
            BackColor = UiColors.Background;
            ForeColor = UiColors.Text;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = settings.InTaskbar && settings.Pinned;
            TopMost = settings.Top;
            Opacity = settings.Opacity;
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 375, Screen.PrimaryScreen.WorkingArea.Top + 24);
            header = TextLabel("◉ CODEX PERISCOPE", 12, UiColors.Five, true);
            version = TextLabel("PERISCOPE / V5.2.0", 8, UiColors.Muted, false);
            close = TextLabel("×", 16, UiColors.Muted, false);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Hide(); };
            Controls.Add(header); Controls.Add(version); Controls.Add(close);
            // One window, always-visible task telemetry: no popup or detail window.
            taskDock.BackColor = ColorTranslator.FromHtml("#1A2738");
            taskRule.BackColor = UiColors.Track;
            taskHeading = TextLabel("CODEX ACTIVITY", 8, UiColors.Text, true);
            taskSource = TextLabel("LOCAL · PARTIAL", 7, UiColors.Muted, false);
            taskSource.TextAlign = ContentAlignment.MiddleRight;
            taskBadge = TextLabel("UNVERIFIED", 8, UiColors.Muted, true);
            taskBadge.TextAlign = ContentAlignment.MiddleRight;
            taskName = TextLabel("Scanning local sessions…", 9, UiColors.Text, true);
            taskName.AutoEllipsis = true;
            taskMeta = TextLabel("Model: —   ·   Effort: —", 8, UiColors.Muted, false);
            taskMeta.AutoEllipsis = true;
            taskExtra = TextLabel("No activity verified yet", 7, UiColors.Muted, false);
            taskExtra.AutoEllipsis = true;
            taskDock.Controls.Add(taskRule);
            taskDock.Controls.Add(taskHeading);
            taskDock.Controls.Add(taskSource);
            taskDock.Controls.Add(taskGlyph);
            taskDock.Controls.Add(taskBadge);
            taskDock.Controls.Add(taskName);
            taskDock.Controls.Add(taskMeta);
            taskDock.Controls.Add(taskExtra);
            Controls.Add(taskDock);
            five = BuildMeter("5-HOUR QUOTA");
            week = BuildMeter("WEEKLY QUOTA");
            status = TextLabel("● Initializing Codex...", 8, UiColors.Muted, false);
            Controls.Add(status);
            statusAge = TextLabel("Waiting", 8, UiColors.Muted, false);
            statusAge.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(statusAge);
            statusDivider = new Panel { BackColor = UiColors.Track };
            Controls.Add(statusDivider);
            resizeGrip = TextLabel("◢", 11, UiColors.Muted, true);
            resizeGrip.Cursor = Cursors.SizeNWSE;
            resizeGrip.TextAlign = ContentAlignment.MiddleCenter;
            tooltip.SetToolTip(resizeGrip, "Drag to resize gauges");
            Controls.Add(resizeGrip);
            originalTrayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            // WinForms Form.Icon is independent of NotifyIcon.Icon. When omitted,
            // Windows may show the generic multicolour WinForms squares in the taskbar.
            // Reuse the exact executable icon embedded by /win32icon in the build.
            Icon = originalTrayIcon ?? SystemIcons.Application;
            tray = new NotifyIcon { Icon = originalTrayIcon ?? SystemIcons.Application, Text = "CODEX PERISCOPE " + typeof(MonitorForm).Assembly.GetName().Version.ToString(3), Visible = true, ContextMenuStrip = context };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) ToggleVisibility();
            };
            Deactivate += delegate { if (!settings.Pinned && Visible) Hide(); };
            // In transient-panel mode, never start with a permanently visible widget.
            Shown += delegate { if (!settings.Pinned) BeginInvoke((Action)delegate { Hide(); }); };
            MakeMenu();
            ApplyAppearance();
            ApplyLayout();
            if (settings.X.HasValue && settings.Y.HasValue)
            {
                Rectangle proposed = new Rectangle(settings.X.Value, settings.Y.Value, Width, Height);
                // Restore into the closest connected monitor's usable area, including
                // after a satellite display was disconnected or its DPI/layout changed.
                Location = DesktopGeometry.ClampToScreen(proposed,
                    Screen.FromRectangle(proposed).WorkingArea).Location;
            }
            AttachDrag(this); AttachDrag(header); AttachDrag(version);
            AttachCircularResize();
            FormClosing += OnFormClosing;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
            tick.Interval = 1000;
            tick.Tick += delegate { Heartbeat(); };
            taskMotion.Interval = 85;
            taskMotion.Tick += delegate { taskGlyph.Advance(); };
            VisibleChanged += delegate { UpdateTaskMotion(); };
            tick.Start();
            Heartbeat();
        }
        private static Label TextLabel(string text, float size, Color color, bool bold)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = color, BackColor = Color.Transparent, AutoSize = false, UseCompatibleTextRendering = true };
        }
        private Meter BuildMeter(string title)
        {
            Meter m = new Meter();
            m.Name = TextLabel(title, 10, UiColors.Text, true);
            m.Number = TextLabel("—", 17, UiColors.Muted, true);
            m.Number.TextAlign = ContentAlignment.MiddleRight;
            m.Track = new Panel { BackColor = UiColors.Track, Height = 9 };
            m.Fill = new Panel { BackColor = UiColors.Five, Width = 0, Height = 9 };
            m.Track.Controls.Add(m.Fill);
            m.Ring = new CircularGauge();
            m.Reset = TextLabel("Waiting de Codex...", 8, UiColors.Muted, false);
            Controls.Add(m.Name); Controls.Add(m.Number); Controls.Add(m.Track); Controls.Add(m.Reset); Controls.Add(m.Ring);
            return m;
        }
        private void PlaceMeter(Meter m, int x, int y, int width, bool micro)
        {
            m.Name.SetBounds(x, y, width - 76, micro ? 20 : 24);
            m.Name.Font = new Font("Segoe UI", micro ? 8f : 10f, FontStyle.Bold);
            m.Number.SetBounds(x + width - 75, y - 3, 75, micro ? 23 : 30);
            m.Number.Font = new Font("Segoe UI", micro ? 11f : 17f, FontStyle.Bold);
            m.Track.SetBounds(x, y + (micro ? 31 : 31), width, micro ? 6 : 9);
            m.Fill.Height = m.Track.Height;
            m.Reset.SetBounds(x, y + 44, width, micro ? 18 : 22);
            m.Reset.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
        }
        private void PlaceCircular(Meter m, int x, int width)
        {
            int w = settings.CircularWidth;
            float ratio = w / (float)DesktopGeometry.DefaultWidth;
            m.Name.Bounds = DesktopGeometry.ScaleRect(x, 52, width, 25, w);
            m.Name.TextAlign = ContentAlignment.MiddleCenter;
            m.Name.Font = new Font("Segoe UI", 9f * ratio, FontStyle.Bold);
            m.Ring.Bounds = DesktopGeometry.ScaleRect(x + (width - 130) / 2, 78, 130, 130, w);
            m.Reset.Bounds = DesktopGeometry.ScaleRect(x, 209, width, 23, w);
            m.Reset.Font = new Font("Segoe UI", 8f * ratio, FontStyle.Regular);
            m.Reset.TextAlign = ContentAlignment.MiddleCenter;
            m.Number.Visible = false;
            m.Track.Visible = false;
            m.Ring.Visible = true;
        }
        private void MeterMode(Meter m, bool circular)
        {
            m.Ring.Visible = circular;
            m.Track.Visible = !circular;
            m.Number.Visible = !circular;
            m.Name.TextAlign = circular ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            m.Reset.TextAlign = circular ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
        }
        private void ApplyLayout()
        {
            Rectangle work = Screen.FromPoint(new Point(Left + 10, Top + 10)).WorkingArea;
            bool micro = settings.Layout == "Micro";
            bool circular = settings.Layout == "Circular";
            MeterMode(five, circular);
            MeterMode(week, circular);
            statusAge.Visible = circular;
            statusDivider.Visible = circular;
            if (circular)
            {
                int w = DesktopGeometry.ClampWidth(settings.CircularWidth);
                settings.CircularWidth = w;
                ClientSize = new Size(w, DesktopGeometry.HeightForWidth(w));
                float ratio = w / (float)DesktopGeometry.DefaultWidth;
                header.Bounds = DesktopGeometry.ScaleRect(17, 12, 340, 30, w);
                header.Font = new Font("Segoe UI", 11f * ratio, FontStyle.Bold);
                version.Visible = true;
                version.Bounds = DesktopGeometry.ScaleRect(348, 13, 146, 20, w);
                version.TextAlign = ContentAlignment.MiddleRight;
                PlaceCircular(five, 24, 236);
                PlaceCircular(week, 280, 236);
                statusDivider.Bounds = DesktopGeometry.ScaleRect(14, 239, 512, 1, w);
                status.Bounds = DesktopGeometry.ScaleRect(17, 247, 337, 22, w);
                statusAge.Bounds = DesktopGeometry.ScaleRect(355, 247, 160, 22, w);
                status.Font = new Font("Segoe UI", 8f * ratio, FontStyle.Regular);
                statusAge.Font = new Font("Segoe UI", 8f * ratio, FontStyle.Regular);
            }
            else if (micro)
            {
                version.TextAlign = ContentAlignment.MiddleLeft;
                ClientSize = new Size(484, 122);
                header.SetBounds(12, 5, 320, 22);
                header.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                version.Visible = false;
                PlaceMeter(five, 12, 33, 212, true);
                // Reserve 44px on the right for the close button at every DPI.
                PlaceMeter(week, 240, 33, 188, true);
                status.SetBounds(12, 99, 458, 18);
            }
            else if (settings.Layout == "Compact")
            {
                version.TextAlign = ContentAlignment.MiddleLeft;
                ClientSize = new Size(670, 177);
                header.SetBounds(17, 12, 430, 29);
                header.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                version.SetBounds(17, 40, 200, 18); version.Visible = true;
                PlaceMeter(five, 18, 70, 305, false);
                PlaceMeter(week, 347, 70, 305, false);
                status.SetBounds(17, 153, 630, 18);
            }
            else
            {
                version.TextAlign = ContentAlignment.MiddleLeft;
                ClientSize = new Size(355, 257);
                header.SetBounds(17, 12, 290, 29);
                header.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                version.SetBounds(17, 40, 200, 18); version.Visible = true;
                PlaceMeter(five, 18, 70, 317, false);
                PlaceMeter(week, 18, 155, 317, false);
                status.SetBounds(17, 238, 322, 18);
            }
            close.SetBounds(ClientSize.Width - 38, micro ? 3 : 10, 25, micro ? 24 : 28);
            int quotaHeight = ClientSize.Height;
            Rectangle activity = DesktopGeometry.ActivityBounds(ClientSize.Width, quotaHeight, settings.Layout);
            ClientSize = new Size(ClientSize.Width,
                DesktopGeometry.CombinedHeight(ClientSize.Width, quotaHeight, settings.Layout));
            taskDock.Bounds = activity;
            PlaceActivityControls(circular, micro);
            UpdateTaskSurface(DateTimeOffset.UtcNow);
            resizeGrip.Visible = circular && settings.Pinned;
            if (circular)
                resizeGrip.Bounds = new Rectangle(ClientSize.Width - DesktopGeometry.Scale(21, settings.CircularWidth),
                    ClientSize.Height - DesktopGeometry.Scale(21, settings.CircularWidth),
                    DesktopGeometry.Scale(17, settings.CircularWidth), DesktopGeometry.Scale(17, settings.CircularWidth));
            Location = new Point(Math.Max(work.Left, Math.Min(Left, work.Right - Width)),
                Math.Max(work.Top, Math.Min(Top, work.Bottom - Height)));
            PaintSnapshot();
        }
        private void PlaceActivityControls(bool circular, bool micro)
        {
            int width = taskDock.ClientSize.Width;
            // Circular layout follows its existing 470–720 px size preference.
            int basis = circular ? settings.CircularWidth : DesktopGeometry.DefaultWidth;
            Func<int, int> unit = value => circular ? DesktopGeometry.Scale(value, basis) : value;
            int side = unit(14);
            int rightWidth = Math.Max(unit(108), width / 4);
            taskRule.SetBounds(side, unit(25), width - side * 2, Math.Max(1, unit(1)));
            int sourceWidth = width < 400 ? unit(133) : unit(170);
            int sourceLeft = width - side - sourceWidth;
            taskHeading.SetBounds(side, unit(4), Math.Max(70, sourceLeft - side - unit(8)), unit(20));
            taskSource.SetBounds(sourceLeft, unit(4), sourceWidth, unit(20));
            taskGlyph.SetBounds(side, unit(34), unit(30), unit(30));
            int start = side + unit(40);
            taskName.SetBounds(start, unit(29), Math.Max(40, width - start - rightWidth - side), unit(23));
            taskBadge.SetBounds(width - side - rightWidth, unit(29), rightWidth, unit(23));
            taskMeta.SetBounds(start, unit(53), Math.Max(40, width - start - side), unit(20));
            taskExtra.SetBounds(side, unit(76), Math.Max(40, width - side * 2 - unit(12)), unit(18));
            if (circular)
            {
                float scale = basis / (float)DesktopGeometry.DefaultWidth;
                taskHeading.Font = new Font("Segoe UI", 8f * scale, FontStyle.Bold);
                taskName.Font = new Font("Segoe UI", 9f * scale, FontStyle.Bold);
                taskBadge.Font = new Font("Segoe UI", 8f * scale, FontStyle.Bold);
                taskMeta.Font = new Font("Segoe UI", 8f * scale);
                taskExtra.Font = new Font("Segoe UI", 7f * scale);
                taskSource.Font = new Font("Segoe UI", 7f * scale);
            }
            else if (micro)
            {
                taskHeading.Font = new Font("Segoe UI", 7f, FontStyle.Bold);
                taskName.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                taskMeta.Font = new Font("Segoe UI", 7f);
            }
        }

        private void UpdateTaskMotion()
        {
            taskMotion.Enabled = Visible && settings.TaskAnimations &&
                taskGlyph.Phase == LiveTaskPhase.Active;
        }

        private void ToggleVisibility()
        {
            if (Visible) { Hide(); return; }
            if (!settings.Pinned)
            {
                Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
                int x = Math.Min(work.Right - Width - 8, Math.Max(work.Left + 8, Cursor.Position.X - Width + 35));
                int y = Math.Min(work.Bottom - Height - 8, Math.Max(work.Top + 8, Cursor.Position.Y - Height - 12));
                Location = new Point(x, y);
                TopMost = true;
            }
            Show(); BringToFront(); Activate();
        }
        private void Save()
        {
            if (settings.Pinned) { settings.X = Left; settings.Y = Top; }
            store.Save(settings);
        }
        private void AttachDrag(Control control)
        {
            control.MouseDown += delegate(object sender, MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) { dragMouse = Cursor.Position; dragOrigin = Location; }
            };
            control.MouseMove += delegate(object sender, MouseEventArgs e) {
                if (dragMouse.HasValue && dragOrigin.HasValue && Control.MouseButtons == MouseButtons.Left)
                {
                    Point now = Cursor.Position;
                    Location = new Point(dragOrigin.Value.X + now.X - dragMouse.Value.X, dragOrigin.Value.Y + now.Y - dragMouse.Value.Y);
                }
            };
            control.MouseUp += delegate {
                if (!dragMouse.HasValue) return;
                dragMouse = null; dragOrigin = null;
                if (settings.Pinned && settings.SnapToEdges)
                    Location = DesktopGeometry.Snap(Bounds, Screen.FromRectangle(Bounds).WorkingArea, 20).Location;
                Save();
            };
        }
        private void AttachCircularResize()
        {
            resizeGrip.MouseDown += delegate(object sender, MouseEventArgs e) {
                if (e.Button != MouseButtons.Left || !settings.Pinned || settings.Layout != "Circular") return;
                resizeMouse = Cursor.Position;
                resizeOriginalWidth = settings.CircularWidth;
            };
            resizeGrip.MouseMove += delegate(object sender, MouseEventArgs e) {
                if (!resizeMouse.HasValue || Control.MouseButtons != MouseButtons.Left) return;
                int desired = DesktopGeometry.ClampWidth(resizeOriginalWidth + Cursor.Position.X - resizeMouse.Value.X);
                // Avoid expensive redraw for every single mouse pixel.
                if (Math.Abs(desired - settings.CircularWidth) >= 5)
                {
                    settings.CircularWidth = desired;
                    ApplyLayout();
                }
            };
            resizeGrip.MouseUp += delegate {
                if (!resizeMouse.HasValue) return;
                resizeMouse = null;
                if (settings.Pinned && settings.SnapToEdges)
                    Location = DesktopGeometry.Snap(Bounds, Screen.FromRectangle(Bounds).WorkingArea, 20).Location;
                Save();
            };
        }
        private static ToolStripMenuItem Menu(ContextMenuStrip menu, string name, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(name);
            if (handler != null) item.Click += handler;
            menu.Items.Add(item);
            return item;
        }
        private static ToolStripMenuItem Menu(ToolStripMenuItem parent, string name, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(name);
            if (handler != null) item.Click += handler;
            parent.DropDownItems.Add(item);
            return item;
        }
        private void MakeMenu()
        {
            // Two immediate actions; everything else is grouped by user intention.
            Menu(context, "Show / Hide", delegate { ToggleVisibility(); });
            Menu(context, "Refresh quotas", delegate { nextRefresh = DateTimeOffset.MinValue; });
            context.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem tasksMenu = Menu(context, "Codex Activity (local)", null);
            Menu(tasksMenu, "Refresh local activity", delegate {
                nextTaskPoll = DateTimeOffset.MinValue;
                PollTasks(DateTimeOffset.UtcNow);
            });
            Menu(tasksMenu, "Copy privacy-safe task diagnostics",
                delegate { CopyTaskDiagnostic(); });
            tasksMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem taskSourceInfo = Menu(tasksMenu, "Source: local logs (indicative)", null);
            taskSourceInfo.Enabled = false;
            tasksMenu.DropDownOpening += delegate {
                taskSourceInfo.Text = taskReport == null ?
                    "Source: no reading yet" :
                    "Sources: " + taskReport.ExaminedRoots + " primary, " +
                    taskReport.IgnoredSubagents + " subagents excluded";
            };
            context.Items.Add(new ToolStripSeparator());

            // QUOTAS: source, reset display and rate-limit polling interval.
            ToolStripMenuItem quotas = Menu(context, "Quotas & Data", null);
            Menu(quotas, "Official Codex dashboard", delegate {
                OpenUrl("https://chatgpt.com/codex/cloud/settings/analytics#usage");
            });
            Menu(quotas, "Codex data status", delegate { ShowSourceHealth(); });
            quotas.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem resets = Menu(quotas, "Reset display", null);
            AddOption(resets, "Date/time + countdown", "both", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Date/time only", "absolute", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Countdown only", "relative", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Hidden", "hidden", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            resets.DropDownItems[0].ToolTipText =
                "In circular mode: countdown for 5 hours and date for weekly quota.";
            ToolStripMenuItem refresh = Menu(quotas, "Refresh interval", null);
            foreach (int sec in new int[] { 30, 60, 120, 300 })
            {
                int interval = sec;
                ToolStripMenuItem option = new ToolStripMenuItem(sec == 30 ? "30 seconds" :
                    (sec / 60) + " minute" + (sec > 60 ? "s" : ""));
                option.Click += delegate {
                    settings.RefreshSeconds = interval;
                    foreach (ToolStripItem child in refresh.DropDownItems)
                        ((ToolStripMenuItem)child).Checked = child == option;
                    nextRefresh = DateTimeOffset.MinValue;
                    Save();
                };
                refresh.DropDownItems.Add(option);
            }
            refresh.DropDownOpening += delegate {
                int[] choices = { 30, 60, 120, 300 };
                for (int i = 0; i < choices.Length; i++)
                    ((ToolStripMenuItem)refresh.DropDownItems[i]).Checked = choices[i] == settings.RefreshSeconds;
            };

            // APPEARANCE: layout, sizes, opacity, palette and the tray icon.
            ToolStripMenuItem appearance = Menu(context, "Appearance", null);
            ToolStripMenuItem layouts = Menu(appearance, "Layout", null);
            foreach (string mode in new string[] { "Circular", "Vertical", "Compact", "Micro" })
            {
                string layout = mode;
                ToolStripMenuItem option = new ToolStripMenuItem(mode == "Circular" ?
                    "Circular · 2 gauges" : mode == "Micro" ? "Micro widget" :
                    mode == "Compact" ? "Horizontal compact" : "Vertical");
                option.Click += delegate {
                    settings.Layout = layout;
                    foreach (ToolStripItem child in layouts.DropDownItems)
                        ((ToolStripMenuItem)child).Checked = child == option;
                    ApplyLayout(); Save();
                };
                layouts.DropDownItems.Add(option);
            }
            layouts.DropDownOpening += delegate {
                string[] choices = { "Circular", "Vertical", "Compact", "Micro" };
                for (int i = 0; i < choices.Length; i++)
                    ((ToolStripMenuItem)layouts.DropDownItems[i]).Checked = choices[i] == settings.Layout;
            };
            ToolStripMenuItem circularSize = Menu(appearance, "Gauge size", null);
            circularSize.ToolTipText = "Pinned circular mode only; drag the handle to resize.";
            foreach (int value in new int[] { 480, 540, 620, 700 })
            {
                int pixels = value;
                ToolStripMenuItem option = new ToolStripMenuItem(value == 480 ? "Small · 480 px" :
                    value == 540 ? "Standard · 540 px" :
                    value == 620 ? "Large · 620 px" : "Extra large · 700 px");
                option.Tag = pixels;
                option.Click += delegate {
                    settings.CircularWidth = pixels;
                    settings.Layout = "Circular";
                    ApplyLayout(); Save();
                };
                circularSize.DropDownItems.Add(option);
            }
            circularSize.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem customSizeInfo = new ToolStripMenuItem() { Enabled = false };
            circularSize.DropDownItems.Add(customSizeInfo);
            circularSize.DropDownOpening += delegate {
                foreach (ToolStripItem child in circularSize.DropDownItems)
                {
                    ToolStripMenuItem option = child as ToolStripMenuItem;
                    if (option != null && option.Tag is int)
                        option.Checked = (int)option.Tag == settings.CircularWidth;
                }
                customSizeInfo.Text = "Current size: " + settings.CircularWidth + " px";
            };
            ToolStripMenuItem opacity = Menu(appearance, "Opacity", null);
            foreach (int value in new int[] { 70, 75, 80, 85, 90, 95, 96, 98, 100 })
            {
                int percent = value;
                ToolStripMenuItem option = new ToolStripMenuItem(value + " %") { Tag = percent };
                option.Click += delegate {
                    settings.Opacity = percent / 100.0;
                    Opacity = settings.Opacity;
                    Save();
                };
                opacity.DropDownItems.Add(option);
            }
            opacity.DropDownOpening += delegate {
                foreach (ToolStripItem child in opacity.DropDownItems)
                    ((ToolStripMenuItem)child).Checked =
                        Math.Abs(settings.Opacity * 100.0 - (int)((ToolStripMenuItem)child).Tag) < .1;
            };
            ToolStripMenuItem backgrounds = Menu(appearance, "Background color", null);
            AddBackgroundPreset(backgrounds, "Midnight blue", "#111926");
            AddBackgroundPreset(backgrounds, "Deep black", "#090C13");
            AddBackgroundPreset(backgrounds, "Charcoal gray", "#22272E");
            AddBackgroundPreset(backgrounds, "Petrol blue", "#102C36");
            Menu(backgrounds, "Custom dark background…", delegate { ChooseCustomBackground(); });
            backgrounds.DropDownOpening += delegate { SyncBackgroundMenu(backgrounds); };
            Menu(appearance, "Reset background and opacity", delegate {
                Preferences defaults = Preferences.Defaults();
                settings.Opacity = defaults.Opacity;
                settings.BackgroundColor = defaults.BackgroundColor;
                ApplyAppearance(); Save();
            });
            appearance.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem colors = Menu(appearance, "Gauge colors", null);
            AddColor(colors, "76–100 % · Purple", "76");
            AddColor(colors, "51–75 % · Blue / turquoise", "51");
            AddColor(colors, "26–50 % · Golden yellow", "26");
            AddColor(colors, "11–25 % · Orange", "11");
            AddColor(colors, "0–10 % · Red", "0");
            Menu(colors, "Reset colors", delegate {
                Preferences defaults = Preferences.Defaults();
                settings.Color76 = defaults.Color76; settings.Color51 = defaults.Color51;
                settings.Color26 = defaults.Color26; settings.Color11 = defaults.Color11;
                settings.Color0 = defaults.Color0;
                PaintSnapshot(); Save();
            });
            ToolStripMenuItem trayStyles = Menu(appearance, "Tray Icon", null);
            AddOption(trayStyles, "Ring + %", "Percent", delegate(string value) {
                settings.TrayStyle = value; Save(); UpdateQuotaTrayIcon(State(),
                    State() == "Direct" && snapshot != null && snapshot.UsageAllowed == false);
            }, settings.TrayStyle);
            AddOption(trayStyles, "Ring only", "Ring", delegate(string value) {
                settings.TrayStyle = value; Save(); UpdateQuotaTrayIcon(State(),
                    State() == "Direct" && snapshot != null && snapshot.UsageAllowed == false);
            }, settings.TrayStyle);
            appearance.DropDownItems.Add(new ToolStripSeparator());
            AddAlertToggle(appearance, "Animate Codex activity radar", settings.TaskAnimations,
                delegate(bool value) { settings.TaskAnimations = value; UpdateTaskMotion(); Save(); });
            ToolStripMenuItem motionToggle = appearance.DropDownItems[appearance.DropDownItems.Count - 1]
                as ToolStripMenuItem;
            appearance.DropDownOpening += delegate {
                if (motionToggle != null) motionToggle.Checked = settings.TaskAnimations;
            };

            // WINDOW: behavioral controls stay out of appearance/color settings.
            ToolStripMenuItem window = Menu(context, "Window", null);
            ToolStripMenuItem pinned = Menu(window, "Pin widget (otherwise open from tray)",
                delegate(object sender, EventArgs args) {
                    settings.Pinned = ((ToolStripMenuItem)sender).Checked;
                    TopMost = settings.Pinned ? settings.Top : true;
                    ShowInTaskbar = settings.Pinned && settings.InTaskbar;
                    if (!settings.Pinned) Hide();
                    ApplyLayout(); Save();
                });
            pinned.CheckOnClick = true;
            ToolStripMenuItem magnet = Menu(window, "Snap to screen edges",
                delegate(object sender, EventArgs args) {
                    settings.SnapToEdges = ((ToolStripMenuItem)sender).Checked;
                    Save();
                });
            magnet.CheckOnClick = true;
            ToolStripMenuItem top = Menu(window, "Always on top",
                delegate(object sender, EventArgs args) {
                    settings.Top = ((ToolStripMenuItem)sender).Checked;
                    TopMost = settings.Pinned ? settings.Top : true;
                    if (history != null && !history.IsDisposed) history.TopMost = settings.Top;
                    Save();
                });
            top.CheckOnClick = true;
            ToolStripMenuItem taskbar = Menu(window, "Show in taskbar",
                delegate(object sender, EventArgs args) {
                    settings.InTaskbar = ((ToolStripMenuItem)sender).Checked;
                    ShowInTaskbar = settings.Pinned && settings.InTaskbar;
                    Save();
                });
            taskbar.CheckOnClick = true;
            window.DropDownOpening += delegate {
                pinned.Checked = settings.Pinned;
                magnet.Checked = settings.SnapToEdges;
                top.Checked = settings.Top;
                taskbar.Checked = settings.InTaskbar;
                magnet.Enabled = settings.Pinned;
                top.Enabled = settings.Pinned;
                taskbar.Enabled = settings.Pinned;
            };

            ToolStripMenuItem alerts = Menu(context, "Notifications", null);
            ToolStripMenuItem notificationsEnabled = Menu(alerts, "Enable notifications",
                delegate(object sender, EventArgs args) {
                    settings.Alerts = ((ToolStripMenuItem)sender).Checked;
                    Save();
                });
            notificationsEnabled.CheckOnClick = true;
            ToolStripMenuItem testNotification = Menu(alerts, "Test Windows notification",
                delegate { TestWindowsNotification(); });
            testNotification.ToolTipText = "Manual test, independent of alert settings; display depends on Windows.";
            alerts.DropDownItems.Add(new ToolStripSeparator());
            AddAlertToggle(alerts, "Codex task completed", settings.TaskAlerts,
                delegate(bool value) { settings.TaskAlerts = value; Save(); });
            ToolStripMenuItem taskAlertsToggle = alerts.DropDownItems[alerts.DropDownItems.Count - 1]
                as ToolStripMenuItem;
            alerts.DropDownItems.Add(new ToolStripSeparator());
            AddAlertToggle(alerts, "Window 5 hours", settings.AlertFive,
                delegate(bool v) { settings.AlertFive = v; Save(); });
            AddAlertToggle(alerts, "Weekly window", settings.AlertWeek,
                delegate(bool v) { settings.AlertWeek = v; Save(); });
            alerts.DropDownItems.Add(new ToolStripSeparator());
            AddAlertToggle(alerts, "Threshold 50 %", settings.Alert50,
                delegate(bool v) { settings.Alert50 = v; Save(); });
            AddAlertToggle(alerts, "Threshold 25 %", settings.Alert25,
                delegate(bool v) { settings.Alert25 = v; Save(); });
            AddAlertToggle(alerts, "Threshold 10 %", settings.Alert10,
                delegate(bool v) { settings.Alert10 = v; Save(); });
            AddAlertToggle(alerts, "Reset observed", settings.AlertResets,
                delegate(bool v) { settings.AlertResets = v; Save(); });
            alerts.DropDownOpening += delegate {
                notificationsEnabled.Checked = settings.Alerts;
                if (taskAlertsToggle != null) taskAlertsToggle.Checked = settings.TaskAlerts;
                foreach (ToolStripItem child in alerts.DropDownItems)
                    if (child != notificationsEnabled && child != testNotification) child.Enabled = settings.Alerts;
            };

            ToolStripMenuItem historyMenu = Menu(context, "History", null);
            Menu(historyMenu, "Show history (24h / 7d / 14d)", delegate { OpenHistory(); });
            ToolStripMenuItem record = Menu(historyMenu, "Record local history",
                delegate(object sender, EventArgs args) {
                    settings.History = ((ToolStripMenuItem)sender).Checked;
                    Save();
                });
            record.CheckOnClick = true;
            historyMenu.DropDownOpening += delegate { record.Checked = settings.History; };
            Menu(historyMenu, "Open data folder", delegate {
                Process.Start("explorer.exe", "\"" + store.DirectoryPath + "\"");
            });

            ToolStripMenuItem application = Menu(context, "Application", null);
            updateMenuItem = Menu(application, "Check for updates", delegate { CheckForUpdates(); });
            Menu(application, "Releases GitHub", delegate {
                OpenUrl(ReleaseUpdater.RepoUrl + "/releases");
            });
            Menu(application, "About Codex Periscope", delegate {
                MessageBox.Show(this,
                    "CODEX PERISCOPE · 5.2.0 Preview\n" +
                    "Quota & Activity Radar for Codex\n\n" +
                    "Independent application, not affiliated with OpenAI.\n" +
                    "Local task data is indicative and may not match the active Codex Desktop tab.\n" +
                    "No paid model calls or cloud service.\n\n" +
                    "Source: https://github.com/shinobione/SHINO-CODEX-MONITOR",
                    "About Codex Periscope", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            ToolStripMenuItem autostart = Menu(application, "Start with Windows",
                delegate(object sender, EventArgs args) {
                    ToolStripMenuItem item = (ToolStripMenuItem)sender;
                    if (item.Checked) EnableStartup(); else DisableStartup();
                    item.Checked = StartupEnabled();
                });
            autostart.CheckOnClick = true;
            application.DropDownItems.Add(new ToolStripSeparator());
            Menu(application, "Diagnostic", delegate { ShowDiagnostic(); });
            Menu(application, "Copy diagnostics", delegate { CopyDiagnostic(); });
            application.DropDownOpening += delegate { autostart.Checked = StartupEnabled(); };

            context.Items.Add(new ToolStripSeparator());
            Menu(context, "Exit completely", delegate { exiting = true; Close(); });
        }
        private static void AddAlertToggle(ToolStripMenuItem menu, string title, bool current, Action<bool> onChange)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(title) { Checked = current, CheckOnClick = true };
            item.Click += delegate { onChange(item.Checked); };
            menu.DropDownItems.Add(item);
        }
        private static void AddOption(ToolStripMenuItem parent, string label, string value, Action<string> set, string current)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label) { Checked = current == value };
            item.Click += delegate {
                set(value);
                foreach (ToolStripItem sub in parent.DropDownItems) ((ToolStripMenuItem)sub).Checked = sub == item;
            };
            parent.DropDownItems.Add(item);
        }
        private void ApplyAppearance()
        {
            settings.Sanitize();
            BackColor = ColorTranslator.FromHtml(settings.BackgroundColor);
            Opacity = settings.Opacity;
            five.Ring.BackColor = BackColor;
            week.Ring.BackColor = BackColor;
            five.Ring.Invalidate();
            week.Ring.Invalidate();
            Invalidate();
        }
        private void SetBackground(string hex)
        {
            if (!Preferences.IsDarkBackground(hex)) return;
            settings.BackgroundColor = hex;
            ApplyAppearance();
            Save();
        }
        private void SyncBackgroundMenu(ToolStripMenuItem parent)
        {
            foreach (ToolStripItem child in parent.DropDownItems)
            {
                ToolStripMenuItem item = child as ToolStripMenuItem;
                if (item != null && item.Tag is string)
                    item.Checked = settings.BackgroundColor.Equals((string)item.Tag,
                        StringComparison.OrdinalIgnoreCase);
            }
        }
        private void AddBackgroundPreset(ToolStripMenuItem parent, string label, string hex)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label) { Tag = hex };
            item.Click += delegate {
                SetBackground(hex);
                SyncBackgroundMenu(parent);
            };
            parent.DropDownItems.Add(item);
        }
        private void ChooseCustomBackground()
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = ColorTranslator.FromHtml(settings.BackgroundColor);
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string hex = string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}",
                    dialog.Color.R, dialog.Color.G, dialog.Color.B);
                if (!Preferences.IsDarkBackground(hex))
                {
                    MessageBox.Show(this, "Choose a dark background to keep quotas and status labels readable.",
                        "CODEX PERISCOPE — Contraste", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                SetBackground(hex);
            }
        }
        private void AddColor(ToolStripMenuItem parent, string label, string key)
        {
            ToolStripMenuItem option = new ToolStripMenuItem(label);
            option.Click += delegate {
                string source = key == "76" ? settings.Color76 : key == "51" ? settings.Color51 :
                    key == "26" ? settings.Color26 : key == "11" ? settings.Color11 : settings.Color0;
                using (ColorDialog dialog = new ColorDialog())
                {
                    dialog.Color = ColorTranslator.FromHtml(source); dialog.FullOpen = true;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    string hex = ColorTranslator.ToHtml(dialog.Color);
                    hex = string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    if (key == "76") settings.Color76 = hex;
                    else if (key == "51") settings.Color51 = hex;
                    else if (key == "26") settings.Color26 = hex;
                    else if (key == "11") settings.Color11 = hex;
                    else settings.Color0 = hex;
                    Save(); PaintSnapshot();
                }
            };
            parent.DropDownItems.Add(option);
        }
        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { MessageBox.Show("Open this link in your browser:\n" + url, "Codex"); }
        }
        private async void CheckForUpdates()
        {
            if (updateCheckRunning) return;
            updateCheckRunning = true;
            if (updateMenuItem != null) { updateMenuItem.Enabled = false; updateMenuItem.Text = "Checking GitHub…"; }
            try
            {
                PublicRelease latest = await ReleaseUpdater.CheckAsync();
                if (IsDisposed || Disposing) return;
                Version installed = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                Version current = new Version(installed.Major, installed.Minor, installed.Build);
                if (ReleaseUpdater.IsNewer(latest, current))
                {
                    DialogResult choice = MessageBox.Show(
                        "Installed version: " + current.ToString(3) + "\n" +
                        "Nouvelle Release : " + latest.Tag + "\n\n" +
                        "Download and verify the official GitHub update?\n" +
                        "Downloading does not install anything. Installation requires separate confirmation.\n" +
                        "You can always use the Releases page if something goes wrong.",
                        "CODEX PERISCOPE — Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (choice == DialogResult.Yes) await DownloadAndConfirm(latest);
                }
                else
                {
                    MessageBox.Show(current.CompareTo(latest.Number) > 0 ?
                        "Installed version: " + current.ToString(3) + "\n" +
                        "Latest public release: " + latest.Tag + "\n" +
                        "This build is newer than the latest public release." :
                        "You are using the latest published stable version: " + latest.Tag,
                        "CODEX PERISCOPE — Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception)
            {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show("Cannot check GitHub releases right now.\n" +
                        "Check your internet connection or visit the Releases page.\n" +
                        "Codex quotas are not affected.",
                        "CODEX PERISCOPE — Update check unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                updateCheckRunning = false;
                if (!IsDisposed && updateMenuItem != null && !updateMenuItem.IsDisposed)
                {
                    updateMenuItem.Text = "Check for updates";
                    updateMenuItem.Enabled = true;
                }
            }
        }

        private async Task DownloadAndConfirm(PublicRelease latest)
        {
            StagedUpdate staged = null;
            if (updateMenuItem != null) updateMenuItem.Text = "Secure download…";
            try
            {
                // Refuse absent or contradictory GitHub release asset digest.
                ReleaseAsset asset = ReleasePackage.SelectAsset(latest, latest.ApiResponse);
                staged = await ReleasePackage.StageAsync(asset);
                if (IsDisposed || Disposing) return;
                DialogResult yes = MessageBox.Show(
                    "Official package downloaded and SHA-256 verified.\n" +
                    "Version : " + latest.Tag + "\n" +
                    "Size: " + asset.Size + " bytes\n" +
                    "Empreinte GitHub : " + asset.Sha256 + "\n\n" +
                    "Install this version now? The widget will close.\n" +
                    "The previous version will be backed up. This executable is NOT signed.",
                    "CODEX PERISCOPE — Confirmer l'installation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (yes != DialogResult.Yes) return;
                UpdateHandoff.Start(staged);
                exiting = true;
                Close();
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show("Update canceled. Your installation was not changed.\n" +
                        ex.Message + "\n\nSee also: " + latest.PageUrl,
                        "CODEX PERISCOPE — Update unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed && updateMenuItem != null) updateMenuItem.Text = "Check for updates";
                // Do not retain rejected downloads; never remove an in-progress installation.
                if (staged != null && !exiting)
                    try { Directory.Delete(staged.Directory, true); } catch { }
            }
        }

        private void OpenHistory()
        {
            if (history == null || history.IsDisposed) history = new HistoryWindow(store);
            history.TopMost = TopMost;
            history.RefreshFrom(store);
            history.Show(); history.BringToFront();
        }
        private static string StartupValue(string valueName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key == null ? null : key.GetValue(valueName) as string;
            }
            catch (System.Security.SecurityException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static string StartupPath(string raw)
        {
            // Never invoke or expand arbitrary commands from a Run entry.
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().Trim('"');
        }

        private static bool OwnsStartupValue(string raw)
        {
            string target = StartupPath(raw);
            return !string.IsNullOrEmpty(target) &&
                string.Equals(target, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKnownLegacyEntry(string raw)
        {
            string path = StartupPath(raw);
            return !string.IsNullOrEmpty(path) &&
                string.Equals(System.IO.Path.GetFileName(path), "SHINO-CODEX-MONITOR.exe",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartupEnabled()
        {
            return OwnsStartupValue(StartupValue(RunName)) ||
                OwnsStartupValue(StartupValue(LegacyRunName)) ||
                OwnsStartupValue(StartupValue(OlderRunName));
        }

        private void EnableStartup()
        {
            try
            {
                string current = StartupValue(RunName);
                string legacy = StartupValue(LegacyRunName);
                string older = StartupValue(OlderRunName);
                if (OwnsStartupValue(current) && string.IsNullOrEmpty(legacy) &&
                    string.IsNullOrEmpty(older)) return;
                if ((!string.IsNullOrEmpty(legacy) && !IsKnownLegacyEntry(legacy)) ||
                    (!string.IsNullOrEmpty(older) && !IsKnownLegacyEntry(older)))
                {
                    MessageBox.Show(this, "An unknown startup command was found under a legacy entry. " +
                        "No registry values were changed. Please review Windows Startup settings.",
                        "Windows startup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!string.IsNullOrEmpty(current) && !OwnsStartupValue(current))
                {
                    MessageBox.Show(this, "A different application already uses the Codex Periscope " +
                        "startup key. No registry values were changed.",
                        "Windows startup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if ((!string.IsNullOrEmpty(legacy) || !string.IsNullOrEmpty(older)) &&
                    MessageBox.Show(this,
                        "Legacy Codex monitor startup entries were found. " +
                        "Move them to a single Codex Periscope startup entry? " +
                        "No unrelated application entries will be removed.",
                        "Migrate Windows startup", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) != DialogResult.Yes) return;
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    key.SetValue(RunName, "\"" + Application.ExecutablePath + "\"",
                        RegistryValueKind.String);
                    if (IsKnownLegacyEntry(legacy)) key.DeleteValue(LegacyRunName, false);
                    if (IsKnownLegacyEntry(older)) key.DeleteValue(OlderRunName, false);
                }
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Windows startup"); }
        }

        private void DisableStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    foreach (string name in new [] { RunName, LegacyRunName, OlderRunName })
                        if (OwnsStartupValue(key.GetValue(name) as string))
                            key.DeleteValue(name, false);
                }
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Windows startup"); }
        }
        private string DiagnosticText()
        {
            string last = lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") : "No";
            Version runningVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return "Version : " + runningVersion.ToString(3) + " (C# / Windows Forms)\n" +
                "Source : Codex CLI / account/rateLimits/read\n" +
                "Executable : " + Application.ExecutablePath + "\n" +
                "CLI binary: " + binaryPath + "\n" +
                "Status : " + State() + "\n" +
                "Tray icon: " + settings.TrayStyle + "\n" +
                "Pinned widget: " + settings.Pinned + "\n" +
                "Circular size: " + settings.CircularWidth + " px\n" +
                "Snap to edges: " + settings.SnapToEdges + "\n" +
                "5h / weekly alerts: " + settings.AlertFive + "/" + settings.AlertWeek + "\n" +
                "Threshold/reset alerts: " + settings.Alert50 + "/" + settings.Alert25 + "/" + settings.Alert10 + "/" + settings.AlertResets + "\n" +
                "Last valid reading: " + last + "\n" +
                "Wakeups / invalidations: " + wakeRefreshes + "\n" +
                "Last wake event: " + lastWakeReason + "\n" +
                "Collection suspended: " + suspended + "\n" +
                runtime.Summary() + "\n" +
                "Consecutive failures: " + failures + "\n" +
                "Error: " + (lastError.Length == 0 ? "None" : lastError) + "\n" +
                "Local data: " + store.DirectoryPath + "\n" +
                "The widget never accesses cookies, tokens or passwords.";
        }
        private void ShowSourceHealth()
        {
            string report = SourceHealth.Build(snapshot, State(), lastSuccess, failures,
                lastError, nextRefresh, DateTimeOffset.UtcNow);
            MessageBox.Show(this, report, "CODEX PERISCOPE — Data status",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void ShowDiagnostic()
        {
            MessageBox.Show(this, DiagnosticText(), "CODEX PERISCOPE — Diagnostic", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void CopyDiagnostic()
        {
            try { Clipboard.SetText(DiagnosticText()); }
            catch (Exception e) { MessageBox.Show(this, "Copie impossible : " + e.Message, "Diagnostic"); }
        }
        private void DispatchSystemEvent(Action action)
        {
            if (exiting || IsDisposed || !IsHandleCreated) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (InvalidOperationException) { }
        }
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
        {
            if (args.Mode == PowerModes.Suspend)
                DispatchSystemEvent(delegate { suspended = true; InvalidateForWake("Mise en veille Windows"); });
            else if (args.Mode == PowerModes.Resume)
                DispatchSystemEvent(delegate { suspended = false; InvalidateForWake("Resuming from sleep"); Heartbeat(); });
        }
        private void OnTimeChanged(object sender, EventArgs args)
        {
            DispatchSystemEvent(delegate { InvalidateForWake("Windows clock changed"); Heartbeat(); });
        }
        private void InvalidateForWake(string reason)
        {
            readerGeneration++;
            forcedStale = true;
            suppressNextAlert = true;
            lastWakeReason = reason;
            wakeRefreshes++;
            invalidAfterUtc = DateTimeOffset.UtcNow;
            nextRefresh = DateTimeOffset.MinValue;
            lastUiPaintUtc = DateTimeOffset.MinValue;
            active = null;
            taskRead = null;
            taskReport = null;
            nextTaskPoll = DateTimeOffset.MinValue;
            taskGlyph.SetPhase(LiveTaskPhase.Unknown);
            UpdateTaskMotion();
            UpdateTaskSurface(DateTimeOffset.UtcNow);
            CodexReader.Cancel();
            PaintSnapshot();
        }
        private void Heartbeat()
        {
            if (exiting) return;
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            if (lastHeartbeatUtc != DateTimeOffset.MinValue && !suspended && WakeGuard.Gap(lastHeartbeatUtc, nowUtc))
                InvalidateForWake("Interruption de l'horloge d'interface (> 15 s)");
            lastHeartbeatUtc = nowUtc;
            runtime.Sample(nowUtc);
            // Also expire green terminal glyphs and stale scans while Codex is idle.
            if (taskReport != null) UpdateTaskSurface(nowUtc);
            PollTasks(nowUtc);
            // The reset countdown is displayed in whole minutes. Avoid repainting every second.
            if (nowUtc >= lastUiPaintUtc.AddSeconds(10) || lastUiPaintUtc == DateTimeOffset.MinValue)
            {
                PaintSnapshot();
                lastUiPaintUtc = nowUtc;
            }
            if (suspended || active != null || nowUtc < nextRefresh) return;
            // Discovery also runs off the UI thread (npm / VS Code trees may be large).
            int requestGeneration = readerGeneration;
            active = Task.Run(delegate {
                string detected = CodexLocator.Find();
                if (requestGeneration != readerGeneration)
                    throw new OperationCanceledException("Codex lookup canceled after resume.");
                binaryPath = string.IsNullOrEmpty(detected) ? "Not found" : detected;
                if (detected == null) throw new FileNotFoundException("Codex CLI not found. Check its installation.");
                return CodexReader.Read(detected, 20000);
            });
            Task<Snapshot> running = active;
            running.ContinueWith(delegate(Task<Snapshot> result) {
                if (!IsHandleCreated || IsDisposed) return;
                try { BeginInvoke((Action)delegate {
                    // A pre-suspend read must never become a fresh post-resume quota.
                    if (requestGeneration != readerGeneration || exiting || suspended) return;
                    active = null;
                    if (result.Status == TaskStatus.RanToCompletion &&
                        WakeGuard.Accept(requestGeneration, readerGeneration, result.Result.CapturedUtc, invalidAfterUtc))
                        Success(result.Result);
                    else Failure(result.Exception == null ? "Stale reading or no response from Codex." : SafeError(result.Exception.GetBaseException()));
                }); }
                catch (InvalidOperationException) { }
            });
        }
        private void CopyTaskDiagnostic()
        {
            // Explicit, local user action. Never copy session IDs, prompts or paths.
            string details = taskReport == null ?
                "CODEX PERISCOPE V5.2 — no task reading available." :
                taskReport.RedactedDiagnostics(DateTimeOffset.UtcNow);
            try
            {
                Clipboard.SetText(details);
                MessageBox.Show(this, "Task diagnostics copied (without prompts, paths or identifiers).",
                    "CODEX PERISCOPE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                MessageBox.Show(this, "Could not copy diagnostics to the clipboard.",
                    "CODEX PERISCOPE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TestWindowsNotification()
        {
            // Diagnostic action requested explicitly by the user, not a claim of OS delivery.
            tray.ShowBalloonTip(6500, "CODEX PERISCOPE — TEST",
                "Test notification. If it is not shown, check Windows Notifications and Do not disturb.",
                ToolTipIcon.Info);
        }

        private void PollTasks(DateTimeOffset now)
        {
            if (suspended || taskRead != null || now < nextTaskPoll) return;
            nextTaskPoll = now.AddSeconds(5);
            int generation = readerGeneration;
            taskRead = Task.Run(delegate { return LiveTaskReader.Read(DateTimeOffset.UtcNow); });
            taskRead.ContinueWith(delegate(Task<LiveTaskReport> result) {
                if (!IsHandleCreated || IsDisposed) return;
                try { BeginInvoke((Action)delegate {
                    if (generation != readerGeneration || exiting || suspended) return;
                    taskRead = null;
                    LiveTaskReport fresh;
                    if (result.Status == TaskStatus.RanToCompletion) fresh = result.Result;
                    else fresh = new LiveTaskReport { CheckedUtc = DateTimeOffset.UtcNow,
                        Message = "Unverified · local read error" };
                    bool canAlert = taskReport != null && settings.Alerts && settings.TaskAlerts;
                    if (canAlert)
                    {
                        foreach (LiveTask current in TaskNotificationRules.NewTerminals(taskReport, fresh))
                        {
                            string key = current.SessionKey + ":" +
                                current.StartedUtc.ToUnixTimeMilliseconds() + ":" + current.Phase;
                            if (!sentTaskNotifications.Add(key)) continue;
                            tray.ShowBalloonTip(5500, "Codex · " + current.PhaseLabel,
                                current.Workspace + " · " + current.Duration(DateTimeOffset.UtcNow),
                                current.Phase == LiveTaskPhase.Failed ? ToolTipIcon.Error : ToolTipIcon.Info);
                        }
                        if (sentTaskNotifications.Count > 256) sentTaskNotifications.Clear();
                    }
                    taskReport = fresh;
                    UpdateTaskBadge();
                }); }
                catch (InvalidOperationException) { }
            });
        }

        private void UpdateTaskBadge()
        {
            UpdateTaskSurface(DateTimeOffset.UtcNow);
            UpdateTaskMotion();
            string state = State();
            bool blocked = state == "Direct" && snapshot != null && snapshot.UsageAllowed == false;
            UpdateQuotaTrayIcon(state, blocked);
        }

        private void UpdateTaskSurface(DateTimeOffset now)
        {
            TaskDisplay display = taskReport == null ? new TaskDisplay() : taskReport.DisplayAt(now);
            LiveTask task = display.Primary;
            // These icons describe the OBSERVED local rollout, never all Codex Desktop tasks.
            LiveTaskPhase phase = task == null ? LiveTaskPhase.Unknown :
                display.Kind == TaskDisplayKind.ObservedActive ? LiveTaskPhase.Active :
                display.Kind == TaskDisplayKind.Uncertain ? LiveTaskPhase.Unconfirmed :
                task.Phase;
            taskGlyph.SetPhase(phase);
            if (task == null)
            {
                taskName.Text = taskReport == null ? "Scanning local events…" :
                    "Current Codex status unavailable";
                taskBadge.Text = "UNVERIFIED";
                taskMeta.Text = "No active task confirmed in local logs";
                if (display.LastTerminal != null)
                {
                    LiveTask last = display.LastTerminal;
                    taskExtra.Text = "Last local turn: " + last.Workspace + " · " +
                        (last.Phase == LiveTaskPhase.Completed ? "completed" :
                            last.Phase == LiveTaskPhase.Failed ? "failed" : "interrupted") +
                        " at " + last.FinishedUtc.Value.ToLocalTime().ToString("HH:mm");
                }
                else
                    taskExtra.Text = taskReport == null ? "Waiting for first reading" :
                        display.Detail + " · partial source";
            }
            else
            {
                taskName.Text = task.Workspace == "Project not provided" ?
                    "Session Codex locale" : task.Workspace;
                string duration = task.Duration(now);
                taskBadge.Text = display.Kind == TaskDisplayKind.ObservedActive ? "● OBSERVED " + duration :
                    display.Kind == TaskDisplayKind.Uncertain ? "? UNVERIFIED" :
                    task.Phase == LiveTaskPhase.Completed ? "✓ FIN " + duration :
                    task.Phase == LiveTaskPhase.Failed ? "× FAILED " + duration : "■ STOP " + duration;
                taskMeta.Text = "Model: " + task.Model + "   ·   Effort: " + task.Effort;
                int otherActive = taskReport.ActiveCount -
                    (display.Kind == TaskDisplayKind.ObservedActive ? 1 : 0);
                if (display.Kind == TaskDisplayKind.ObservedActive)
                    taskExtra.Text = "Elapsed: " + duration + " · activity observed in local log; tab unknown" +
                        (otherActive > 0 ? " · +" + otherActive + " autres" : "");
                else if (display.Kind == TaskDisplayKind.Uncertain)
                    taskExtra.Text = "Last log activity: " + task.LastWriteUtc.ToLocalTime().ToString("HH:mm:ss") +
                        " · current status unverified";
                else
                    taskExtra.Text = "Last local turn, not the global Codex status · " +
                        task.FinishedUtc.Value.ToLocalTime().ToString("HH:mm:ss") +
                        " · " + duration;
            }
            taskBadge.ForeColor = phase == LiveTaskPhase.Active ? Color.FromArgb(156, 126, 255) :
                phase == LiveTaskPhase.Completed ? UiColors.Five :
                phase == LiveTaskPhase.Failed ? Color.FromArgb(238, 75, 85) :
                phase == LiveTaskPhase.Interrupted ? UiColors.Warning : UiColors.Muted;
            string description = taskReport == null ? "Local reading incomplete" :
                display.Detail + " · source : local JSONL files; may not match the active tab";
            tooltip.SetToolTip(taskDock, description);
            tooltip.SetToolTip(taskGlyph, description);
            tooltip.SetToolTip(taskBadge, description);
            // Stop animation when observation expires, even if no new file arrives.
            UpdateTaskMotion();
        }

        private static string SafeError(Exception error)
        {
            string message = error.Message ?? "Codex read error";
            if (message.Contains("401") || message.Contains("403")) return "Access denied (401/403)";
            return message.Length <= 120 ? message : message.Substring(0, 120);
        }
        private void Failure(string error)
        {
            failures++;
            lastError = error;
            int delay = QuotaHealth.Backoff(failures, settings.RefreshSeconds);
            nextRefresh = DateTimeOffset.UtcNow.AddSeconds(delay);
            PaintSnapshot();
        }
        private void Success(Snapshot data)
        {
            Snapshot previousSample = snapshot;
            snapshot = data;
            lastSuccess = data.CapturedUtc;
            forcedStale = false;
            lastError = "";
            failures = 0;
            nextRefresh = DateTimeOffset.UtcNow.AddSeconds(settings.RefreshSeconds);
            NotifyChanges(suppressNextAlert ? null : previousSample, data);
            suppressNextAlert = false;
            store.Add(data, settings.History);
            if (history != null && !history.IsDisposed && history.Visible) history.RefreshFrom(store);
            PaintSnapshot();
        }
        private void NotifyChanges(Snapshot previous, Snapshot latest)
        {
            if (!settings.Alerts || previous == null || latest.UsageAllowed == false || previous.UsageAllowed == false) return;
            // Suppress alerts across gaps or after an invalid/stale former sample.
            if (latest.CapturedUtc - previous.CapturedUtc > TimeSpan.FromMinutes(15) ||
                latest.CapturedUtc < previous.CapturedUtc) return;
            List<string> messages = new List<string>();
            if (settings.AlertFive)
                CollectNotification(messages, "5 h", previous.Five, latest.Five, latest.CapturedUtc);
            if (settings.AlertWeek)
                CollectNotification(messages, "7 days", previous.Week, latest.Week, latest.CapturedUtc);
            if (messages.Count == 0) return;
            tray.BalloonTipTitle = "CODEX PERISCOPE";
            tray.BalloonTipText = string.Join(" · ", messages.ToArray());
            tray.ShowBalloonTip(6000);
        }
        private void CollectNotification(List<string> messages, string label, QuotaWindow oldValue,
            QuotaWindow newValue, DateTimeOffset measuredUtc)
        {
            if (settings.AlertResets && AlertRules.ConfirmedReset(oldValue, newValue, measuredUtc))
            {
                string token = label + ":reset:" + newValue.ResetUtc.Value.ToUnixTimeSeconds();
                if (alerted.Add(token)) messages.Add("Quota " + label + " reset (confirmed reading)");
                return;
            }
            int? crossed = AlertRules.CrossedThreshold(oldValue, newValue,
                settings.Alert50, settings.Alert25, settings.Alert10);
            if (crossed.HasValue)
            {
                string token = label + ":seuil:" + crossed.Value + ":" + newValue.ResetUtc.Value.ToUnixTimeSeconds();
                if (alerted.Add(token))
                    messages.Add("Quota " + label + " : " + crossed.Value + " % remaining reached");
            }
            if (alerted.Count > 100) alerted.Clear();
        }
        private string State()
        {
            if (forcedStale) return lastSuccess.HasValue ? "Unverified" : "Unavailable";
            return QuotaHealth.State(lastSuccess, failures, settings.RefreshSeconds, DateTimeOffset.UtcNow);
        }
        private void PaintSnapshot()
        {
            string state = State();
            bool blocked = state == "Direct" && snapshot != null && snapshot.UsageAllowed == false;
            bool live = state == "Direct" && !blocked;
            DrawMeter(five, snapshot == null ? null : snapshot.Five, "five", live, state, blocked);
            DrawMeter(week, snapshot == null ? null : snapshot.Week, "week", live, state, blocked);
            string sample = lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime().ToString("HH:mm:ss") : "—";
            if (blocked)
            {
                status.Text = "⚠ Codex usage reported blocked · reading " + sample;
                status.ForeColor = ColorTranslator.FromHtml("#EE4B55");
            }
            else if (live)
            {
                status.Text = (settings.Layout == "Micro" ? "● Direct" : "● Connected") + " · reading " + sample;
                if (snapshot != null && snapshot.ResetsAvailable.HasValue)
                    status.Text += " · " + snapshot.ResetsAvailable.Value + " resets available";
                status.ForeColor = UiColors.Five;
            }
            else
            {
                status.Text = "⚠ " + state + " · reading " + sample;
                status.ForeColor = UiColors.Warning;
            }
            if (settings.Layout == "Circular")
            {
                status.Text = blocked ? "⚠ Codex usage blocked" : live ? "● Direct · Codex CLI" : "⚠ " + state;
                if (live && snapshot != null && snapshot.ResetsAvailable.HasValue)
                    status.Text += " · " + snapshot.ResetsAvailable.Value + " resets available";
                statusAge.Text = FreshnessLabel(lastSuccess, DateTimeOffset.UtcNow);
                tooltip.SetToolTip(statusAge, "Last reading Codex : " +
                    (lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") : "none") +
                    " (Windows local time). Status : " + state);
            }
            tooltip.SetToolTip(status, live ? "Source : Codex CLI / account/rateLimits/read" : lastError);
            string caption = live && snapshot != null && snapshot.Week != null ?
                "CODEX PERISCOPE · " + Math.Round(snapshot.Week.Remaining) + "% weekly" : "CODEX PERISCOPE · " + state;
            tray.Text = caption.Length > 63 ? "CODEX PERISCOPE" : caption;
            UpdateQuotaTrayIcon(state, blocked);
        }
        private void UpdateQuotaTrayIcon(string state, bool blocked)
        {
            bool current = state == "Direct" && !blocked && snapshot != null;
            double? remaining = null;
            string window = "";
            if (current)
            {
                if (snapshot.Five != null)
                {
                    remaining = snapshot.Five.Remaining;
                    window = "5 h";
                }
                if (snapshot.Week != null && (!remaining.HasValue || snapshot.Week.Remaining < remaining.Value))
                {
                    remaining = snapshot.Week.Remaining;
                    window = "7 j";
                }
            }
            int rounded = remaining.HasValue ? (int)Math.Round(remaining.Value, MidpointRounding.AwayFromZero) : -1;
            TaskDisplay taskDisplay = taskReport == null ? new TaskDisplay() :
                taskReport.DisplayAt(DateTimeOffset.UtcNow);
            LiveTaskPhase livePhase = taskDisplay.Primary == null ? LiveTaskPhase.Unknown :
                taskDisplay.Kind == TaskDisplayKind.ObservedActive ? LiveTaskPhase.Active :
                taskDisplay.Kind == TaskDisplayKind.Uncertain ? LiveTaskPhase.Unconfirmed :
                taskDisplay.Primary.Phase;
            int activeTasks = taskDisplay.Kind == TaskDisplayKind.ObservedActive ?
                taskReport.ActiveCount : 0;
            tray.Text = current && rounded >= 0 ?
                "CODEX PERISCOPE · " + window + " : " + rounded + "% remaining" :
                "CODEX PERISCOPE · " + (blocked ? "Usage blocked" : state);
            Color color = current && rounded >= 0 ? QuotaColor(rounded) : UiColors.Muted;
            if (activeTasks > 0 && tray.Text.Length <= 48)
                tray.Text += " · " + activeTasks + " task(s)";
            string signature = (current ? "1" : "0") + ":" + rounded + ":" +
                color.ToArgb() + ":" + settings.TrayStyle + ":" + livePhase;
            if (signature == trayIconSignature) return;
            try
            {
                Icon created = QuotaTrayIcon.Create(remaining, color, current, settings.TrayStyle, livePhase);
                Icon previous = dynamicTrayIcon;
                tray.Icon = created;
                dynamicTrayIcon = created;
                trayIconSignature = signature;
                if (previous != null) previous.Dispose();
            }
            catch { /* Static icon remains available if GDI+ fails. */ }
        }
        private void DrawMeter(Meter meter, QuotaWindow data, string kind, bool live, string state, bool blocked)
        {
            meter.Reset.Visible = settings.ResetStyle != "hidden";
            if (data == null)
            {
                meter.Number.Text = "—"; meter.Number.ForeColor = UiColors.Muted;
                meter.Fill.Width = 0;
                meter.Ring.SetReading(null, false, UiColors.Muted);
                meter.Reset.Text = state == "Unavailable" ? "Unavailable" : "Window unavailable";
                return;
            }
            int percent = Math.Max(0, Math.Min(100, (int)Math.Round(data.Remaining, MidpointRounding.AwayFromZero)));
            Color color = live ? QuotaColor(percent) : UiColors.Muted;
            meter.Number.Text = percent + (live ? "%" : "%*");
            meter.Number.ForeColor = color;
            meter.Ring.SetReading(percent, live, color);
            meter.Fill.Width = (int)Math.Round(meter.Track.Width * data.Remaining / 100.0);
            meter.Fill.BackColor = color;
            // Blocked ordinary usage does not make a fresh reset timestamp stale.
            // The block remains clearly visible in the status/footer and tooltips.
            meter.Reset.Text = ResetLabel(data.ResetUtc, kind, settings.ResetStyle,
                settings.Layout, live || blocked, DateTimeOffset.UtcNow);
            tooltip.SetToolTip(meter.Ring, blocked ? "Codex usage blocked" :
                live ? "Remaining quota · Codex CLI source" : "Stale quota reading");
            tooltip.SetToolTip(meter.Number, blocked ? "Codex reports usage is blocked" :
                live ? "Remaining quota · Codex CLI source" : "Stale quota reading");
            tooltip.SetToolTip(meter.Reset, (live || blocked) && data.ResetUtc.HasValue ?
                "Reset : " + data.ResetUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + " (Windows local time)" : "Reset unavailable or unverified");
        }
        internal static string FreshnessLabel(DateTimeOffset? last, DateTimeOffset now)
        {
            if (!last.HasValue) return "No reading";
            TimeSpan age = now - last.Value;
            if (age.TotalSeconds < -5) return "Check system clock";
            if (age.TotalSeconds < 60) return "Read less than 1 minute ago";
            if (age.TotalHours < 1) return "Read " + (int)age.TotalMinutes + " min ago";
            return "Read " + (int)age.TotalHours + " h ago";
        }
        private Color QuotaColor(int p)
        {
            return QuotaPalette.ColorFor(p, settings);
        }
        // This is deliberately independent of ordinaryUsageAllowed: an explicit
        // reset from a fresh rate-limit window remains valid when usage is blocked.
        internal static string ResetLabel(DateTimeOffset? utc, string kind, string style,
            string layout, bool sourceCurrent, DateTimeOffset nowUtc)
        {
            if (style == "hidden") return "";
            if (!sourceCurrent) return "Stale reading · reset unverified";
            if (!utc.HasValue) return "Reset not provided";
            TimeSpan left = utc.Value - nowUtc;
            if (left <= TimeSpan.Zero) return "Reset due · checking…";
            DateTimeOffset local = utc.Value.ToLocalTime();
            string absolute;
            if (kind == "five") absolute = "at " + local.ToString("HH:mm");
            else
            {
                CultureInfo english = CultureInfo.GetCultureInfo("en-US");
                string month = local.ToString("MMM", english).TrimEnd('.');
                month = char.ToUpperInvariant(month[0]) + month.Substring(1);
                absolute = "on " + month + " " + local.Day + " at " + local.ToString("HH:mm");
            }
            string relative = left.TotalDays >= 1 ?
                "in " + (int)Math.Floor(left.TotalDays) + "d " + left.Hours + "h" :
                "in " + (int)Math.Floor(left.TotalHours) + "h " + left.Minutes + "min";
            if (layout == "Circular" && style == "both")
                return kind == "five" ? "Reset " + relative : "Reset " + absolute;
            if (style == "absolute") return "Reset " + absolute;
            if (style == "relative") return "Reset " + relative;
            return layout == "Micro" ? "Reset " + absolute : "Reset " + absolute + " · " + relative;
        }
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            exiting = true;
            tick.Stop();
            taskMotion.Stop();
            taskMotion.Dispose();
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
            readerGeneration++;
            CodexReader.Cancel();
            Save();
            tray.Visible = false;
            tray.Dispose();
            if (dynamicTrayIcon != null) dynamicTrayIcon.Dispose();
            if (originalTrayIcon != null) originalTrayIcon.Dispose();
            if (history != null && !history.IsDisposed) history.Close();
            context.Dispose();
        }
    }
}
