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
        private const string RunName = "SHINO_CODEX_MONITOR_V4";
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
        private string binaryPath = "Non trouvé";
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
        private string lastWakeReason = "Aucune";
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
            Text = "SHINO // CODEX MONITOR V5.0.0";
            BackColor = UiColors.Background;
            ForeColor = UiColors.Text;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = settings.InTaskbar && settings.Pinned;
            TopMost = settings.Top;
            Opacity = settings.Opacity;
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 375, Screen.PrimaryScreen.WorkingArea.Top + 24);
            header = TextLabel("● SHINO // CODEX", 12, UiColors.Five, true);
            version = TextLabel("MONITOR / V5.0.0", 8, UiColors.Muted, false);
            close = TextLabel("×", 16, UiColors.Muted, false);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Hide(); };
            Controls.Add(header); Controls.Add(version); Controls.Add(close);
            // One window, always-visible task telemetry: no popup or detail window.
            taskDock.BackColor = ColorTranslator.FromHtml("#1A2738");
            taskRule.BackColor = UiColors.Track;
            taskHeading = TextLabel("ACTIVITÉ CODEX", 8, UiColors.Text, true);
            taskSource = TextLabel("LOCAL · INDICATIF", 7, UiColors.Muted, false);
            taskSource.TextAlign = ContentAlignment.MiddleRight;
            taskBadge = TextLabel("À VÉRIFIER", 8, UiColors.Muted, true);
            taskBadge.TextAlign = ContentAlignment.MiddleRight;
            taskName = TextLabel("Recherche de sessions locales…", 9, UiColors.Text, true);
            taskName.AutoEllipsis = true;
            taskMeta = TextLabel("Modèle : —   ·   Effort : —", 8, UiColors.Muted, false);
            taskMeta.AutoEllipsis = true;
            taskExtra = TextLabel("Aucune activité encore vérifiée", 7, UiColors.Muted, false);
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
            five = BuildMeter("QUOTA 5 HEURES");
            week = BuildMeter("QUOTA 7 JOURS");
            status = TextLabel("● Initialisation Codex...", 8, UiColors.Muted, false);
            Controls.Add(status);
            statusAge = TextLabel("En attente", 8, UiColors.Muted, false);
            statusAge.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(statusAge);
            statusDivider = new Panel { BackColor = UiColors.Track };
            Controls.Add(statusDivider);
            resizeGrip = TextLabel("◢", 11, UiColors.Muted, true);
            resizeGrip.Cursor = Cursors.SizeNWSE;
            resizeGrip.TextAlign = ContentAlignment.MiddleCenter;
            tooltip.SetToolTip(resizeGrip, "Glisser pour redimensionner les roues");
            Controls.Add(resizeGrip);
            originalTrayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            // WinForms Form.Icon is independent of NotifyIcon.Icon. When omitted,
            // Windows may show the generic multicolour WinForms squares in the taskbar.
            // Reuse the exact executable icon embedded by /win32icon in the build.
            Icon = originalTrayIcon ?? SystemIcons.Application;
            tray = new NotifyIcon { Icon = originalTrayIcon ?? SystemIcons.Application, Text = "SHINO // CODEX MONITOR " + typeof(MonitorForm).Assembly.GetName().Version.ToString(3), Visible = true, ContextMenuStrip = context };
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
            m.Reset = TextLabel("En attente de Codex...", 8, UiColors.Muted, false);
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
            Menu(context, "Afficher / masquer", delegate { ToggleVisibility(); });
            Menu(context, "Actualiser maintenant", delegate { nextRefresh = DateTimeOffset.MinValue; });
            context.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem tasksMenu = Menu(context, "Tâches Codex · LIVE", null);
            ToolStripMenuItem testNotification = Menu(tasksMenu, "Tester la notification Windows",
                delegate { TestWindowsNotification(); });
            testNotification.ToolTipText = "Envoie une notification de test, même si les alertes Codex sont désactivées.";
            tasksMenu.DropDownItems.Add(new ToolStripSeparator());
            AddAlertToggle(tasksMenu, "Notifier les tâches terminées", settings.TaskAlerts,
                delegate(bool value) { settings.TaskAlerts = value; Save(); });
            AddAlertToggle(tasksMenu, "Animations (roue en rotation)", settings.TaskAnimations,
                delegate(bool value) { settings.TaskAnimations = value; UpdateTaskMotion(); Save(); });
            context.Items.Add(new ToolStripSeparator());

            // QUOTAS: source, reset display and rate-limit polling interval.
            ToolStripMenuItem quotas = Menu(context, "Quotas & données", null);
            Menu(quotas, "Tableau officiel Codex", delegate {
                OpenUrl("https://chatgpt.com/codex/cloud/settings/analytics#usage");
            });
            Menu(quotas, "État des données Codex", delegate { ShowSourceHealth(); });
            quotas.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem resets = Menu(quotas, "Réinitialisations", null);
            AddOption(resets, "Date/heure + décompte", "both", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Date/heure uniquement", "absolute", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Décompte uniquement", "relative", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Masquées", "hidden", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            resets.DropDownItems[0].ToolTipText =
                "En mode Circulaire : décompte pour 5 h, date pour 7 jours.";
            ToolStripMenuItem refresh = Menu(quotas, "Fréquence d'actualisation", null);
            foreach (int sec in new int[] { 30, 60, 120, 300 })
            {
                int interval = sec;
                ToolStripMenuItem option = new ToolStripMenuItem(sec == 30 ? "30 secondes" :
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
            ToolStripMenuItem appearance = Menu(context, "Apparence", null);
            ToolStripMenuItem layouts = Menu(appearance, "Disposition", null);
            foreach (string mode in new string[] { "Circular", "Vertical", "Compact", "Micro" })
            {
                string layout = mode;
                ToolStripMenuItem option = new ToolStripMenuItem(mode == "Circular" ?
                    "Circulaire · 2 roues" : mode == "Micro" ? "Micro-widget" :
                    mode == "Compact" ? "Compact horizontal" : "Vertical");
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
            ToolStripMenuItem circularSize = Menu(appearance, "Taille des roues", null);
            circularSize.ToolTipText = "Uniquement pour le mode Circulaire épinglé ; glisser la poignée pour une taille personnalisée.";
            foreach (int value in new int[] { 480, 540, 620, 700 })
            {
                int pixels = value;
                ToolStripMenuItem option = new ToolStripMenuItem(value == 480 ? "Petit · 480 px" :
                    value == 540 ? "Standard · 540 px" :
                    value == 620 ? "Grand · 620 px" : "Très grand · 700 px");
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
                customSizeInfo.Text = "Taille actuelle : " + settings.CircularWidth + " px";
            };
            ToolStripMenuItem opacity = Menu(appearance, "Opacité", null);
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
            ToolStripMenuItem backgrounds = Menu(appearance, "Couleur du fond", null);
            AddBackgroundPreset(backgrounds, "Bleu nuit", "#111926");
            AddBackgroundPreset(backgrounds, "Noir profond", "#090C13");
            AddBackgroundPreset(backgrounds, "Gris anthracite", "#22272E");
            AddBackgroundPreset(backgrounds, "Bleu pétrole", "#102C36");
            Menu(backgrounds, "Personnalisée (fond sombre)...", delegate { ChooseCustomBackground(); });
            backgrounds.DropDownOpening += delegate { SyncBackgroundMenu(backgrounds); };
            Menu(appearance, "Réinitialiser fond et opacité", delegate {
                Preferences defaults = Preferences.Defaults();
                settings.Opacity = defaults.Opacity;
                settings.BackgroundColor = defaults.BackgroundColor;
                ApplyAppearance(); Save();
            });
            appearance.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem colors = Menu(appearance, "Couleurs des jauges", null);
            AddColor(colors, "76–100 % · Violet", "76");
            AddColor(colors, "51–75 % · Bleu / turquoise", "51");
            AddColor(colors, "26–50 % · Jaune doré", "26");
            AddColor(colors, "11–25 % · Orange", "11");
            AddColor(colors, "0–10 % · Rouge", "0");
            Menu(colors, "Réinitialiser les couleurs", delegate {
                Preferences defaults = Preferences.Defaults();
                settings.Color76 = defaults.Color76; settings.Color51 = defaults.Color51;
                settings.Color26 = defaults.Color26; settings.Color11 = defaults.Color11;
                settings.Color0 = defaults.Color0;
                PaintSnapshot(); Save();
            });
            ToolStripMenuItem trayStyles = Menu(appearance, "Tray Icon", null);
            AddOption(trayStyles, "Anneau + %", "Percent", delegate(string value) {
                settings.TrayStyle = value; Save(); UpdateQuotaTrayIcon(State(),
                    State() == "Direct" && snapshot != null && snapshot.UsageAllowed == false);
            }, settings.TrayStyle);
            AddOption(trayStyles, "Anneau seul", "Ring", delegate(string value) {
                settings.TrayStyle = value; Save(); UpdateQuotaTrayIcon(State(),
                    State() == "Direct" && snapshot != null && snapshot.UsageAllowed == false);
            }, settings.TrayStyle);

            // WINDOW: behavioral controls stay out of appearance/color settings.
            ToolStripMenuItem window = Menu(context, "Fenêtre", null);
            ToolStripMenuItem pinned = Menu(window, "Épingler le widget (sinon panneau au clic)",
                delegate(object sender, EventArgs args) {
                    settings.Pinned = ((ToolStripMenuItem)sender).Checked;
                    TopMost = settings.Pinned ? settings.Top : true;
                    ShowInTaskbar = settings.Pinned && settings.InTaskbar;
                    if (!settings.Pinned) Hide();
                    ApplyLayout(); Save();
                });
            pinned.CheckOnClick = true;
            ToolStripMenuItem magnet = Menu(window, "Aimantation aux bords des écrans",
                delegate(object sender, EventArgs args) {
                    settings.SnapToEdges = ((ToolStripMenuItem)sender).Checked;
                    Save();
                });
            magnet.CheckOnClick = true;
            ToolStripMenuItem top = Menu(window, "Toujours au-dessus",
                delegate(object sender, EventArgs args) {
                    settings.Top = ((ToolStripMenuItem)sender).Checked;
                    TopMost = settings.Pinned ? settings.Top : true;
                    if (history != null && !history.IsDisposed) history.TopMost = settings.Top;
                    Save();
                });
            top.CheckOnClick = true;
            ToolStripMenuItem taskbar = Menu(window, "Afficher dans la barre des tâches",
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
            ToolStripMenuItem notificationsEnabled = Menu(alerts, "Activer les notifications",
                delegate(object sender, EventArgs args) {
                    settings.Alerts = ((ToolStripMenuItem)sender).Checked;
                    Save();
                });
            notificationsEnabled.CheckOnClick = true;
            alerts.DropDownItems.Add(new ToolStripSeparator());
            AddAlertToggle(alerts, "Fenêtre 5 heures", settings.AlertFive,
                delegate(bool v) { settings.AlertFive = v; Save(); });
            AddAlertToggle(alerts, "Fenêtre 7 jours", settings.AlertWeek,
                delegate(bool v) { settings.AlertWeek = v; Save(); });
            alerts.DropDownItems.Add(new ToolStripSeparator());
            AddAlertToggle(alerts, "Seuil 50 %", settings.Alert50,
                delegate(bool v) { settings.Alert50 = v; Save(); });
            AddAlertToggle(alerts, "Seuil 25 %", settings.Alert25,
                delegate(bool v) { settings.Alert25 = v; Save(); });
            AddAlertToggle(alerts, "Seuil 10 %", settings.Alert10,
                delegate(bool v) { settings.Alert10 = v; Save(); });
            AddAlertToggle(alerts, "Réinitialisation constatée", settings.AlertResets,
                delegate(bool v) { settings.AlertResets = v; Save(); });
            alerts.DropDownOpening += delegate {
                notificationsEnabled.Checked = settings.Alerts;
                foreach (ToolStripItem child in alerts.DropDownItems)
                    if (child != notificationsEnabled) child.Enabled = settings.Alerts;
            };

            ToolStripMenuItem historyMenu = Menu(context, "Historique", null);
            Menu(historyMenu, "Afficher l'historique (24 h / 7 j / 14 j)", delegate { OpenHistory(); });
            ToolStripMenuItem record = Menu(historyMenu, "Enregistrer l'historique local",
                delegate(object sender, EventArgs args) {
                    settings.History = ((ToolStripMenuItem)sender).Checked;
                    Save();
                });
            record.CheckOnClick = true;
            historyMenu.DropDownOpening += delegate { record.Checked = settings.History; };
            Menu(historyMenu, "Ouvrir le dossier de données", delegate {
                Process.Start("explorer.exe", "\"" + store.DirectoryPath + "\"");
            });

            ToolStripMenuItem application = Menu(context, "Application", null);
            updateMenuItem = Menu(application, "Vérifier les mises à jour", delegate { CheckForUpdates(); });
            Menu(application, "Releases GitHub", delegate {
                OpenUrl(ReleaseUpdater.RepoUrl + "/releases");
            });
            ToolStripMenuItem autostart = Menu(application, "Démarrer avec Windows",
                delegate(object sender, EventArgs args) {
                    ToolStripMenuItem item = (ToolStripMenuItem)sender;
                    if (item.Checked) EnableStartup(); else DisableStartup();
                    item.Checked = StartupEnabled();
                });
            autostart.CheckOnClick = true;
            application.DropDownItems.Add(new ToolStripSeparator());
            Menu(application, "Diagnostic", delegate { ShowDiagnostic(); });
            Menu(application, "Copier le diagnostic", delegate { CopyDiagnostic(); });
            application.DropDownOpening += delegate { autostart.Checked = StartupEnabled(); };

            context.Items.Add(new ToolStripSeparator());
            Menu(context, "Quitter complètement", delegate { exiting = true; Close(); });
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
                    MessageBox.Show(this, "Choisis une couleur de fond sombre pour conserver la lisibilité des quotas et des états.",
                        "SHINO // CODEX — Contraste", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            catch { MessageBox.Show("Ouvre ce lien dans ton navigateur :\n" + url, "Codex"); }
        }
        private async void CheckForUpdates()
        {
            if (updateCheckRunning) return;
            updateCheckRunning = true;
            if (updateMenuItem != null) { updateMenuItem.Enabled = false; updateMenuItem.Text = "Vérification GitHub..."; }
            try
            {
                PublicRelease latest = await ReleaseUpdater.CheckAsync();
                if (IsDisposed || Disposing) return;
                Version installed = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                Version current = new Version(installed.Major, installed.Minor, installed.Build);
                if (ReleaseUpdater.IsNewer(latest, current))
                {
                    DialogResult choice = MessageBox.Show(
                        "Version installée : " + current.ToString(3) + "\n" +
                        "Nouvelle Release : " + latest.Tag + "\n\n" +
                        "Télécharger et vérifier la mise à jour officielle GitHub ?\n" +
                        "Le téléchargement n'installe rien. Installation après un deuxième accord explicite.\n" +
                        "En cas de problème, tu pourras utiliser la page Releases.",
                        "SHINO // CODEX — Mise à jour disponible", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (choice == DialogResult.Yes) await DownloadAndConfirm(latest);
                }
                else
                {
                    MessageBox.Show(current.CompareTo(latest.Number) > 0 ?
                        "Version installée : " + current.ToString(3) + "\n" +
                        "Dernière Release publique : " + latest.Tag + "\n" +
                        "Cette version est plus récente que la dernière Release publique." :
                        "Vous utilisez la dernière version stable publiée : " + latest.Tag,
                        "SHINO // CODEX — Mises à jour", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception)
            {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show("Impossible de vérifier les Releases GitHub pour le moment.\n" +
                        "Vérifiez la connexion Internet ou consultez la page Releases.\n" +
                        "Les quotas Codex ne sont pas affectés.",
                        "SHINO // CODEX — Vérification indisponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                updateCheckRunning = false;
                if (!IsDisposed && updateMenuItem != null && !updateMenuItem.IsDisposed)
                {
                    updateMenuItem.Text = "Vérifier les mises à jour";
                    updateMenuItem.Enabled = true;
                }
            }
        }

        private async Task DownloadAndConfirm(PublicRelease latest)
        {
            StagedUpdate staged = null;
            if (updateMenuItem != null) updateMenuItem.Text = "Téléchargement sécurisé...";
            try
            {
                // Refuse absent or contradictory GitHub release asset digest.
                ReleaseAsset asset = ReleasePackage.SelectAsset(latest, latest.ApiResponse);
                staged = await ReleasePackage.StageAsync(asset);
                if (IsDisposed || Disposing) return;
                DialogResult yes = MessageBox.Show(
                    "Archive officielle téléchargée et SHA-256 vérifié.\n" +
                    "Version : " + latest.Tag + "\n" +
                    "Taille : " + asset.Size + " octets\n" +
                    "Empreinte GitHub : " + asset.Sha256 + "\n\n" +
                    "Installer cette version maintenant ? Le widget va se fermer.\n" +
                    "La version précédente restera sauvegardée. L'exécutable n'est PAS signé.",
                    "SHINO // CODEX — Confirmer l'installation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (yes != DialogResult.Yes) return;
                UpdateHandoff.Start(staged);
                exiting = true;
                Close();
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show("Mise à jour interrompue sans modification de l'installation.\n" +
                        ex.Message + "\n\nConsulte si nécessaire : " + latest.PageUrl,
                        "SHINO // CODEX — Mise à jour indisponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed && updateMenuItem != null) updateMenuItem.Text = "Vérifier les mises à jour";
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
        private static string StartupValue()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key == null ? null : key.GetValue(RunName) as string;
            }
            catch { return null; }
        }
        private static bool StartupEnabled()
        {
            string value = StartupValue();
            if (string.IsNullOrEmpty(value)) return false;
            return string.Equals(value.Trim().Trim('"'), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        private void EnableStartup()
        {
            try
            {
                string current = StartupValue();
                if (!string.IsNullOrEmpty(current) && !StartupEnabled())
                {
                    DialogResult replace = MessageBox.Show(
                        "Un ancien emplacement V4 est encore enregistré au démarrage Windows.\n\nLe remplacer par cette V4.5 ?",
                        "Démarrage Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (replace != DialogResult.Yes) return;
                }
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key != null && key.GetValue("SHINO_CODEX_MONITOR") != null)
                    {
                        DialogResult decision = MessageBox.Show(
                            "Un démarrage automatique V3 est détecté. Activer aussi la V4.5 peut lancer deux widgets.\n\nActiver sans modifier la V3 ?",
                            "Démarrage Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (decision != DialogResult.Yes) return;
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                    key.SetValue(RunName, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
            }
            catch (Exception e) { MessageBox.Show(e.Message, "Démarrage Windows"); }
        }
        private void DisableStartup()
        {
            try { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (key != null) key.DeleteValue(RunName, false); }
            catch (Exception e) { MessageBox.Show(e.Message, "Démarrage Windows"); }
        }
        private string DiagnosticText()
        {
            string last = lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") : "Aucun";
            Version runningVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return "Version : " + runningVersion.ToString(3) + " (C# / Windows Forms)\n" +
                "Source : Codex CLI / account/rateLimits/read\n" +
                "Executable : " + Application.ExecutablePath + "\n" +
                "Binaire CLI : " + binaryPath + "\n" +
                "État : " + State() + "\n" +
                "Icône de notification : " + settings.TrayStyle + "\n" +
                "Widget épinglé : " + settings.Pinned + "\n" +
                "Taille circulaire : " + settings.CircularWidth + " px\n" +
                "Aimantation : " + settings.SnapToEdges + "\n" +
                "Alertes 5 h / 7 j : " + settings.AlertFive + "/" + settings.AlertWeek + "\n" +
                "Alertes 50/25/10/reset : " + settings.Alert50 + "/" + settings.Alert25 + "/" + settings.Alert10 + "/" + settings.AlertResets + "\n" +
                "Dernier relevé valide : " + last + "\n" +
                "Reprises / invalidations : " + wakeRefreshes + "\n" +
                "Dernier signal : " + lastWakeReason + "\n" +
                "Collecte suspendue : " + suspended + "\n" +
                runtime.Summary() + "\n" +
                "Échecs successifs : " + failures + "\n" +
                "Erreur : " + (lastError.Length == 0 ? "Aucune" : lastError) + "\n" +
                "Données locales : " + store.DirectoryPath + "\n" +
                "Aucun cookie, jeton ou mot de passe n'est consulté par le widget.";
        }
        private void ShowSourceHealth()
        {
            string report = SourceHealth.Build(snapshot, State(), lastSuccess, failures,
                lastError, nextRefresh, DateTimeOffset.UtcNow);
            MessageBox.Show(this, report, "SHINO // CODEX — État des données",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void ShowDiagnostic()
        {
            MessageBox.Show(this, DiagnosticText(), "SHINO // CODEX — Diagnostic", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                DispatchSystemEvent(delegate { suspended = false; InvalidateForWake("Reprise après veille"); Heartbeat(); });
        }
        private void OnTimeChanged(object sender, EventArgs args)
        {
            DispatchSystemEvent(delegate { InvalidateForWake("Horloge Windows modifiée"); Heartbeat(); });
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
            if (taskReport != null && taskReport.ActiveCount > 0)
                UpdateTaskSurface(nowUtc);
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
                    throw new OperationCanceledException("Recherche Codex annulée après reprise.");
                binaryPath = string.IsNullOrEmpty(detected) ? "Non trouvé" : detected;
                if (detected == null) throw new FileNotFoundException("Codex CLI introuvable. Vérifie l'installation npm.");
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
                    else Failure(result.Exception == null ? "Relevé obsolète ou aucune réponse Codex." : SafeError(result.Exception.GetBaseException()));
                }); }
                catch (InvalidOperationException) { }
            });
        }
        private void TestWindowsNotification()
        {
            // Diagnostic action requested explicitly by the user, not a claim of OS delivery.
            tray.ShowBalloonTip(6500, "SHINO // CODEX — TEST",
                "Notification de test du widget. Si rien n'apparaît, vérifiez Notifications et Ne pas déranger dans Windows.",
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
                        Message = "À vérifier · erreur de lecture locale" };
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
            LiveTask task = taskReport == null ? null : taskReport.Featured;
            LiveTaskPhase phase = task == null ? LiveTaskPhase.Unknown : task.Phase;
            taskGlyph.SetPhase(phase);
            if (task == null)
            {
                taskName.Text = taskReport == null ? "Lecture des tâches locales…" :
                    "Aucune tâche locale récente confirmée";
                taskBadge.Text = "—";
                taskMeta.Text = "Modèle : —   ·   Effort : —";
                taskExtra.Text = taskReport == null ? "En attente du premier relevé" :
                    taskReport.Message;
            }
            else
            {
                taskName.Text = task.Workspace == "Projet non communiqué" ?
                    "Session Codex" : task.Workspace;
                string duration = task.Duration(now);
                taskBadge.Text = phase == LiveTaskPhase.Active ? "● LIVE " + duration :
                    phase == LiveTaskPhase.Completed ? "✓ FIN " + duration :
                    phase == LiveTaskPhase.Failed ? "× ÉCHEC " + duration :
                    phase == LiveTaskPhase.Interrupted ? "■ STOP " + duration : "? À VÉRIFIER";
                taskMeta.Text = "Modèle : " + task.Model + "   ·   Effort : " + task.Effort;
                int otherActive = taskReport.ActiveCount - (phase == LiveTaskPhase.Active ? 1 : 0);
                if (otherActive > 0)
                    taskExtra.Text = "+" + otherActive + " autre(s) tâche(s) active(s) · " +
                        "source : sessions locales indicatives";
                else if (phase == LiveTaskPhase.Unconfirmed)
                    taskExtra.Text = "Sans activité récente : état à vérifier · journaux locaux";
                else
                {
                    TimeSpan age = now - task.LastWriteUtc;
                    string lag = age.TotalSeconds < 0 ? "horloge à vérifier" :
                        age.TotalMinutes < 1 ? "moins d'1 min" :
                        ((int)age.TotalMinutes) + " min";
                    taskExtra.Text = "Dernier journal : " + lag + " · données locales indicatives";
                }
            }
            taskBadge.ForeColor = phase == LiveTaskPhase.Active ? Color.FromArgb(156, 126, 255) :
                phase == LiveTaskPhase.Completed ? UiColors.Five :
                phase == LiveTaskPhase.Failed ? Color.FromArgb(238, 75, 85) :
                phase == LiveTaskPhase.Interrupted ? UiColors.Warning : UiColors.Muted;
            tooltip.SetToolTip(taskDock, task == null ?
                (taskReport == null ? "Lecture locale non terminée" : taskReport.Message) :
                task.PhaseLabel + " · " + task.Workspace + " · " + task.Source);
        }

        private static string SafeError(Exception error)
        {
            string message = error.Message ?? "Erreur de lecture Codex";
            if (message.Contains("401") || message.Contains("403")) return "Accès refusé (401/403)";
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
                CollectNotification(messages, "7 jours", previous.Week, latest.Week, latest.CapturedUtc);
            if (messages.Count == 0) return;
            tray.BalloonTipTitle = "SHINO // CODEX MONITOR";
            tray.BalloonTipText = string.Join(" · ", messages.ToArray());
            tray.ShowBalloonTip(6000);
        }
        private void CollectNotification(List<string> messages, string label, QuotaWindow oldValue,
            QuotaWindow newValue, DateTimeOffset measuredUtc)
        {
            if (settings.AlertResets && AlertRules.ConfirmedReset(oldValue, newValue, measuredUtc))
            {
                string token = label + ":reset:" + newValue.ResetUtc.Value.ToUnixTimeSeconds();
                if (alerted.Add(token)) messages.Add("Quota " + label + " réinitialisé (relevé confirmé)");
                return;
            }
            int? crossed = AlertRules.CrossedThreshold(oldValue, newValue,
                settings.Alert50, settings.Alert25, settings.Alert10);
            if (crossed.HasValue)
            {
                string token = label + ":seuil:" + crossed.Value + ":" + newValue.ResetUtc.Value.ToUnixTimeSeconds();
                if (alerted.Add(token))
                    messages.Add("Quota " + label + " : " + crossed.Value + " % restants atteints");
            }
            if (alerted.Count > 100) alerted.Clear();
        }
        private string State()
        {
            if (forcedStale) return lastSuccess.HasValue ? "À vérifier" : "Indisponible";
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
                status.Text = "⚠ Usage Codex signalé bloqué · relevé " + sample;
                status.ForeColor = ColorTranslator.FromHtml("#EE4B55");
            }
            else if (live)
            {
                status.Text = (settings.Layout == "Micro" ? "● Direct" : "● Connecté") + " · relevé " + sample;
                if (snapshot != null && snapshot.ResetsAvailable.HasValue)
                    status.Text += " · " + snapshot.ResetsAvailable.Value + " réinit. dispo.";
                status.ForeColor = UiColors.Five;
            }
            else
            {
                status.Text = "⚠ " + state + " · relevé " + sample;
                status.ForeColor = UiColors.Warning;
            }
            if (settings.Layout == "Circular")
            {
                status.Text = blocked ? "⚠ Usage Codex bloqué" : live ? "● Direct · Codex CLI" : "⚠ " + state;
                if (live && snapshot != null && snapshot.ResetsAvailable.HasValue)
                    status.Text += " · " + snapshot.ResetsAvailable.Value + " réinit. dispo.";
                statusAge.Text = FreshnessLabel(lastSuccess, DateTimeOffset.UtcNow);
                tooltip.SetToolTip(statusAge, "Dernier relevé Codex : " +
                    (lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") : "aucun") +
                    " (heure Windows). État : " + state);
            }
            tooltip.SetToolTip(status, live ? "Source : Codex CLI / account/rateLimits/read" : lastError);
            string caption = live && snapshot != null && snapshot.Week != null ?
                "SHINO // CODEX · " + Math.Round(snapshot.Week.Remaining) + "% semaine" : "SHINO // CODEX · " + state;
            tray.Text = caption.Length > 63 ? "SHINO // CODEX MONITOR" : caption;
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
            LiveTaskPhase livePhase = taskReport == null || taskReport.Featured == null ?
                LiveTaskPhase.Unknown : taskReport.Featured.Phase;
            int activeTasks = taskReport == null ? 0 : taskReport.ActiveCount;
            tray.Text = current && rounded >= 0 ?
                "SHINO // CODEX · " + window + " : " + rounded + "% restants" :
                "SHINO // CODEX · " + (blocked ? "Usage bloqué" : state);
            Color color = current && rounded >= 0 ? QuotaColor(rounded) : UiColors.Muted;
            if (activeTasks > 0 && tray.Text.Length <= 48)
                tray.Text += " · " + activeTasks + " tâche(s)";
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
            catch { /* Le glyph statique reste disponible même si GDI+ échoue. */ }
        }
        private void DrawMeter(Meter meter, QuotaWindow data, string kind, bool live, string state, bool blocked)
        {
            meter.Reset.Visible = settings.ResetStyle != "hidden";
            if (data == null)
            {
                meter.Number.Text = "—"; meter.Number.ForeColor = UiColors.Muted;
                meter.Fill.Width = 0;
                meter.Ring.SetReading(null, false, UiColors.Muted);
                meter.Reset.Text = state == "Indisponible" ? "Indisponible" : "Fenêtre non communiquée";
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
            tooltip.SetToolTip(meter.Ring, blocked ? "Usage Codex bloqué" :
                live ? "Quota restant · source Codex CLI" : "Ancienne mesure non actuelle");
            tooltip.SetToolTip(meter.Number, blocked ? "Codex indique que l'usage ordinaire est bloqué" :
                live ? "Quota restant · source Codex CLI" : "Dernier relevé NON ACTUEL");
            tooltip.SetToolTip(meter.Reset, (live || blocked) && data.ResetUtc.HasValue ?
                "Reset : " + data.ResetUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + " (heure Windows)" : "Réinitialisation indisponible ou non vérifiée");
        }
        internal static string FreshnessLabel(DateTimeOffset? last, DateTimeOffset now)
        {
            if (!last.HasValue) return "Aucun relevé";
            TimeSpan age = now - last.Value;
            if (age.TotalSeconds < -5) return "Horloge à vérifier";
            if (age.TotalSeconds < 60) return "Relevé il y a moins d’1 min";
            if (age.TotalHours < 1) return "Relevé il y a " + (int)age.TotalMinutes + " min";
            return "Relevé il y a " + (int)age.TotalHours + " h";
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
            if (!sourceCurrent) return "Ancienne mesure · reset non vérifié";
            if (!utc.HasValue) return "Reset non communiqué";
            TimeSpan left = utc.Value - nowUtc;
            if (left <= TimeSpan.Zero) return "Reset attendu · vérification...";
            DateTimeOffset local = utc.Value.ToLocalTime();
            string absolute;
            if (kind == "five") absolute = "à " + local.ToString("HH:mm");
            else
            {
                CultureInfo fr = CultureInfo.GetCultureInfo("fr-FR");
                string month = local.ToString("MMM", fr).TrimEnd('.');
                month = char.ToUpperInvariant(month[0]) + month.Substring(1);
                absolute = "le " + local.Day + " " + month + " " + local.ToString("HH:mm");
            }
            string relative = left.TotalDays >= 1 ?
                "dans " + (int)Math.Floor(left.TotalDays) + "j " + left.Hours + "h" :
                "dans " + (int)Math.Floor(left.TotalHours) + "h " + left.Minutes + "min";
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
