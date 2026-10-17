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
        private readonly Dictionary<string, ToolStripMenuItem> intervalItems = new Dictionary<string, ToolStripMenuItem>();
        private ToolStripMenuItem updateMenuItem;
        private bool updateCheckRunning;

        internal MonitorForm()
        {
            store = new LocalStore();
            settings = store.Load();
            Text = "SHINO // CODEX MONITOR V4.5.1";
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
            version = TextLabel("MONITOR / V4.5.1", 8, UiColors.Muted, false);
            close = TextLabel("×", 16, UiColors.Muted, false);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Hide(); };
            Controls.Add(header); Controls.Add(version); Controls.Add(close);
            five = BuildMeter("QUOTA 5 HEURES");
            week = BuildMeter("QUOTA 7 JOURS");
            status = TextLabel("● Initialisation Codex...", 8, UiColors.Muted, false);
            Controls.Add(status);
            originalTrayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            tray = new NotifyIcon { Icon = originalTrayIcon ?? SystemIcons.Application, Text = "SHINO // CODEX MONITOR V4.5.1", Visible = true, ContextMenuStrip = context };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) ToggleVisibility();
            };
            Deactivate += delegate { if (!settings.Pinned && Visible) Hide(); };
            // In transient-panel mode, never start with a permanently visible widget.
            Shown += delegate { if (!settings.Pinned) BeginInvoke((Action)delegate { Hide(); }); };
            MakeMenu();
            ApplyLayout();
            if (settings.X.HasValue && settings.Y.HasValue)
            {
                Rectangle proposed = new Rectangle(settings.X.Value, settings.Y.Value, Width, Height);
                foreach (Screen screen in Screen.AllScreens)
                    if (screen.WorkingArea.IntersectsWith(proposed)) { Location = proposed.Location; break; }
            }
            AttachDrag(this); AttachDrag(header); AttachDrag(version);
            FormClosing += OnFormClosing;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
            tick.Interval = 1000;
            tick.Tick += delegate { Heartbeat(); };
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
            m.Reset = TextLabel("En attente de Codex...", 8, UiColors.Muted, false);
            Controls.Add(m.Name); Controls.Add(m.Number); Controls.Add(m.Track); Controls.Add(m.Reset);
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
            m.Reset.SetBounds(x, y + (micro ? 44 : 44), width, micro ? 18 : 22);
        }
        private void ApplyLayout()
        {
            Rectangle work = Screen.FromPoint(new Point(Left + 10, Top + 10)).WorkingArea;
            bool micro = settings.Layout == "Micro";
            if (micro)
            {
                ClientSize = new Size(484, 122);
                header.SetBounds(12, 5, 320, 22);
                header.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                version.Visible = false;
                PlaceMeter(five, 12, 33, 222, true);
                PlaceMeter(week, 252, 33, 220, true);
                status.SetBounds(12, 99, 458, 18);
            }
            else if (settings.Layout == "Compact")
            {
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
                ClientSize = new Size(355, 257);
                header.SetBounds(17, 12, 290, 29);
                header.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                version.SetBounds(17, 40, 200, 18); version.Visible = true;
                PlaceMeter(five, 18, 70, 317, false);
                PlaceMeter(week, 18, 155, 317, false);
                status.SetBounds(17, 238, 322, 18);
            }
            close.SetBounds(Width - 38, 10, 25, 28);
            Location = new Point(Math.Max(work.Left, Math.Min(Left, work.Right - Width)),
                Math.Max(work.Top, Math.Min(Top, work.Bottom - Height)));
            PaintSnapshot();
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
            control.MouseUp += delegate { if (dragMouse.HasValue) { dragMouse = null; dragOrigin = null; Save(); } };
        }
        private static ToolStripMenuItem Menu(ContextMenuStrip menu, string name, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(name);
            if (handler != null) item.Click += handler;
            menu.Items.Add(item);
            return item;
        }
        private void MakeMenu()
        {
            Menu(context, "Afficher / masquer", delegate { ToggleVisibility(); });
            Menu(context, "Actualiser maintenant", delegate { nextRefresh = DateTimeOffset.MinValue; });
            Menu(context, "Tableau officiel Codex", delegate { OpenUrl("https://chatgpt.com/codex/cloud/settings/analytics#usage"); });
            updateMenuItem = Menu(context, "Vérifier les mises à jour", delegate { CheckForUpdates(); });
            Menu(context, "Releases GitHub", delegate { OpenUrl(ReleaseUpdater.RepoUrl + "/releases"); });
            context.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem layouts = Menu(context, "Disposition", null);
            foreach (string mode in new string[] { "Vertical", "Compact", "Micro" })
            {
                string layout = mode;
                ToolStripMenuItem item = new ToolStripMenuItem(mode == "Micro" ? "Micro-widget" : mode == "Compact" ? "Compact horizontal" : "Vertical");
                item.Checked = settings.Layout == mode;
                item.Click += delegate {
                    settings.Layout = layout;
                    foreach (ToolStripItem child in layouts.DropDownItems) ((ToolStripMenuItem)child).Checked = child == item;
                    ApplyLayout(); Save();
                };
                layouts.DropDownItems.Add(item);
            }
            ToolStripMenuItem pinned = Menu(context, "Épingler le widget (sinon panneau au clic)", delegate(object s, EventArgs e) {
                settings.Pinned = ((ToolStripMenuItem)s).Checked;
                TopMost = settings.Pinned ? settings.Top : true;
                ShowInTaskbar = settings.Pinned && settings.InTaskbar;
                if (!settings.Pinned) Hide();
                Save();
            });
            pinned.CheckOnClick = true;
            pinned.Checked = settings.Pinned;
            ToolStripMenuItem resets = Menu(context, "Réinitialisations", null);
            AddOption(resets, "Date/heure + décompte", "both", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Date/heure uniquement", "absolute", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Décompte uniquement", "relative", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            AddOption(resets, "Masquées", "hidden", delegate(string value) { settings.ResetStyle = value; PaintSnapshot(); Save(); }, settings.ResetStyle);
            ToolStripMenuItem refresh = Menu(context, "Actualisation", null);
            foreach (int sec in new int[] { 30, 60, 120, 300 })
            {
                int interval = sec;
                ToolStripMenuItem item = new ToolStripMenuItem(sec == 30 ? "30 secondes" : (sec / 60) + " minute" + (sec > 60 ? "s" : ""));
                item.Checked = settings.RefreshSeconds == sec;
                item.Click += delegate {
                    settings.RefreshSeconds = interval;
                    foreach (ToolStripItem it in refresh.DropDownItems) ((ToolStripMenuItem)it).Checked = it == item;
                    nextRefresh = DateTimeOffset.MinValue; Save();
                };
                refresh.DropDownItems.Add(item);
            }
            ToolStripMenuItem colors = Menu(context, "Couleurs des jauges", null);
            AddColor(colors, "100–75 %", "high");
            AddColor(colors, "74–50 %", "mid");
            AddColor(colors, "49–25 %", "low");
            AddColor(colors, "24–0 %", "crit");
            ToolStripMenuItem defaults = new ToolStripMenuItem("Réinitialiser les couleurs");
            defaults.Click += delegate {
                Preferences p = Preferences.Defaults(); settings.ColorHigh = p.ColorHigh; settings.ColorMid = p.ColorMid;
                settings.ColorLow = p.ColorLow; settings.ColorCrit = p.ColorCrit; PaintSnapshot(); Save();
            };
            colors.DropDownItems.Add(defaults);
            ToolStripMenuItem trayStyles = Menu(context, "Icône de notification", null);
            AddOption(trayStyles, "Anneau + %", "Percent", delegate(string value) {
                settings.TrayStyle = value; Save(); UpdateQuotaTrayIcon(State(),
                    State() == "Direct" && snapshot != null && snapshot.UsageAllowed == false);
            }, settings.TrayStyle);
            AddOption(trayStyles, "Anneau seul", "Ring", delegate(string value) {
                settings.TrayStyle = value; Save(); UpdateQuotaTrayIcon(State(),
                    State() == "Direct" && snapshot != null && snapshot.UsageAllowed == false);
            }, settings.TrayStyle);
            context.Items.Add(new ToolStripSeparator());
            Menu(context, "Afficher l'historique (24 h / 7 j / 14 j)", delegate { OpenHistory(); });
            ToolStripMenuItem record = Menu(context, "Enregistrer l'historique local", delegate(object s, EventArgs e) {
                ToolStripMenuItem item = (ToolStripMenuItem)s; settings.History = item.Checked; Save();
            });
            record.CheckOnClick = true; record.Checked = settings.History;
            Menu(context, "Ouvrir le dossier de données", delegate { Process.Start("explorer.exe", "\"" + store.DirectoryPath + "\""); });
            ToolStripMenuItem top = Menu(context, "Toujours au-dessus", delegate(object s, EventArgs e) {
                settings.Top = ((ToolStripMenuItem)s).Checked; TopMost = settings.Pinned ? settings.Top : true;
                if (history != null && !history.IsDisposed) history.TopMost = settings.Top;
                Save();
            });
            top.CheckOnClick = true; top.Checked = settings.Top;
            ToolStripMenuItem taskbar = Menu(context, "Afficher dans la barre des tâches", delegate(object s, EventArgs e) {
                settings.InTaskbar = ((ToolStripMenuItem)s).Checked; ShowInTaskbar = settings.Pinned && settings.InTaskbar; Save();
            });
            taskbar.CheckOnClick = true; taskbar.Checked = settings.InTaskbar;
            ToolStripMenuItem alerts = Menu(context, "Notifications intelligentes", null);
            AddAlertToggle(alerts, "Activer les notifications", settings.Alerts, delegate(bool v) { settings.Alerts = v; Save(); });
            alerts.DropDownItems.Add(new ToolStripSeparator());
            AddAlertToggle(alerts, "Seuil 50 %", settings.Alert50, delegate(bool v) { settings.Alert50 = v; Save(); });
            AddAlertToggle(alerts, "Seuil 25 %", settings.Alert25, delegate(bool v) { settings.Alert25 = v; Save(); });
            AddAlertToggle(alerts, "Seuil 10 %", settings.Alert10, delegate(bool v) { settings.Alert10 = v; Save(); });
            AddAlertToggle(alerts, "Réinitialisation constatée", settings.AlertResets, delegate(bool v) { settings.AlertResets = v; Save(); });
            ToolStripMenuItem autostart = Menu(context, "Démarrer V4 avec Windows", delegate(object s, EventArgs e) {
                ToolStripMenuItem item = (ToolStripMenuItem)s;
                if (item.Checked) EnableStartup(); else DisableStartup();
                item.Checked = StartupEnabled();
            });
            autostart.CheckOnClick = true; autostart.Checked = StartupEnabled();
            Menu(context, "Diagnostic", delegate { ShowDiagnostic(); });
            Menu(context, "Copier le diagnostic", delegate { CopyDiagnostic(); });
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
        private void AddColor(ToolStripMenuItem parent, string label, string key)
        {
            ToolStripMenuItem option = new ToolStripMenuItem(label);
            option.Click += delegate {
                string source = key == "high" ? settings.ColorHigh : key == "mid" ? settings.ColorMid : key == "low" ? settings.ColorLow : settings.ColorCrit;
                using (ColorDialog dialog = new ColorDialog())
                {
                    dialog.Color = ColorTranslator.FromHtml(source); dialog.FullOpen = true;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    string hex = ColorTranslator.ToHtml(dialog.Color);
                    hex = string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    if (key == "high") settings.ColorHigh = hex;
                    else if (key == "mid") settings.ColorMid = hex;
                    else if (key == "low") settings.ColorLow = hex;
                    else settings.ColorCrit = hex;
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
                        "Ouvrir la page officielle GitHub pour télécharger la mise à jour ?\n" +
                        "Aucun fichier ne sera installé automatiquement.",
                        "SHINO // CODEX — Mise à jour disponible", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (choice == DialogResult.Yes) OpenUrl(latest.PageUrl);
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
            return "Version : 4.5.1 (C# / Windows Forms)\n" +
                "Source : Codex CLI / account/rateLimits/read\n" +
                "Executable : " + Application.ExecutablePath + "\n" +
                "Binaire CLI : " + binaryPath + "\n" +
                "État : " + State() + "\n" +
                "Icône de notification : " + settings.TrayStyle + "\n" +
                "Widget épinglé : " + settings.Pinned + "\n" +
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
            CollectNotification(messages, "5 h", previous.Five, latest.Five, latest.CapturedUtc);
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
            tray.Text = current && rounded >= 0 ?
                "SHINO // CODEX · " + window + " : " + rounded + "% restants" :
                "SHINO // CODEX · " + (blocked ? "Usage bloqué" : state);
            Color color = current && rounded >= 0 ? QuotaColor(rounded) : UiColors.Muted;
            string signature = (current ? "1" : "0") + ":" + rounded + ":" + color.ToArgb() + ":" + settings.TrayStyle;
            if (signature == trayIconSignature) return;
            try
            {
                Icon created = QuotaTrayIcon.Create(remaining, color, current, settings.TrayStyle);
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
                meter.Reset.Text = state == "Indisponible" ? "Indisponible" : "Fenêtre non communiquée";
                return;
            }
            int percent = Math.Max(0, Math.Min(100, (int)Math.Round(data.Remaining, MidpointRounding.AwayFromZero)));
            Color color = live ? QuotaColor(percent) : UiColors.Muted;
            meter.Number.Text = percent + (live ? "%" : "%*");
            meter.Number.ForeColor = color;
            meter.Fill.Width = (int)Math.Round(meter.Track.Width * data.Remaining / 100.0);
            meter.Fill.BackColor = color;
            meter.Reset.Text = blocked ? "Usage Codex bloqué" : live ? ResetText(data.ResetUtc, kind) : "Ancienne mesure · reset non vérifié";
            tooltip.SetToolTip(meter.Number, blocked ? "Codex indique que l'usage ordinaire est bloqué" :
                live ? "Quota restant · source Codex CLI" : "Dernier relevé NON ACTUEL");
            tooltip.SetToolTip(meter.Reset, live && data.ResetUtc.HasValue ?
                "Reset : " + data.ResetUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + " (heure Windows)" : "Réinitialisation indisponible ou non vérifiée");
        }
        private Color QuotaColor(int p)
        {
            return ColorTranslator.FromHtml(p >= 75 ? settings.ColorHigh : p >= 50 ? settings.ColorMid : p >= 25 ? settings.ColorLow : settings.ColorCrit);
        }
        private string ResetText(DateTimeOffset? utc, string kind)
        {
            if (!utc.HasValue) return "Reset non communiqué";
            TimeSpan left = utc.Value - DateTimeOffset.UtcNow;
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
            if (settings.ResetStyle == "absolute") return "Reset " + absolute;
            if (settings.ResetStyle == "relative") return "Reset " + relative;
            if (settings.ResetStyle == "hidden") return "";
            return settings.Layout == "Micro" ? "Reset " + absolute : "Reset " + absolute + " · " + relative;
        }
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            exiting = true;
            tick.Stop();
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
