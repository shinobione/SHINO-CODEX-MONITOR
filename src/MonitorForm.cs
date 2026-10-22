using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
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
        internal Label Name, Number, Reset, Hint, Badge, Relative;
        internal InstrumentPanel Face;
        internal Panel Track, Fill;
        internal CircularGauge Ring;
    }
    internal sealed class MonitorForm : PeriscopeWindow
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
        private readonly Label sourceDetail;
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
        private readonly MissionRadarDock compactRadars=new MissionRadarDock();
        private readonly InstrumentLabel resetCount=new InstrumentLabel {Name="QuotaResets",AutoEllipsis=true,BackColor=Color.Transparent};
        private readonly InstrumentLabel footerLeft=new InstrumentLabel {Name="InstrumentFooter",AutoEllipsis=true,BackColor=Color.Transparent};
        private readonly InstrumentLabel footerRead=new InstrumentLabel {Name="QuotaObserved",AutoEllipsis=true,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleCenter};
        private readonly InstrumentLabel footerState=new InstrumentLabel {AutoEllipsis=true,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleRight};
        private bool targetBlockedLayout;
        private readonly InstrumentPanel sourceFace = new InstrumentPanel();
        private Bitmap chrome;
        private readonly MissionDeck taskDeck = new MissionDeck();
        private readonly System.Windows.Forms.Timer taskMotion = new System.Windows.Forms.Timer();
        private DateTimeOffset? presentationTime;
        private string presentationState;
        private bool resourcesDisposed;

        private LiveTaskReport taskReport;
        private Task<LiveTaskReport> taskRead;
        private DateTimeOffset nextTaskPoll = DateTimeOffset.MinValue;
        private readonly Dictionary<string,DateTimeOffset> sentTaskNotifications = new Dictionary<string,DateTimeOffset>();

        internal MonitorForm() : this(new LocalStore(), null) { }
        // Presentation-only construction shares the production window and controls,
        // but has no store, tray, collectors, system subscriptions or persistence.
        internal MonitorForm(Preferences appearance) : this(null, appearance) { }
        internal float? PresentationScale { get; private set; }
        internal void SetPresentationScale(float scale)
        { if(store!=null)throw new InvalidOperationException("Isolated presentation only."); PresentationScale=scale;ApplyLayout(); }
        private MonitorForm(LocalStore persistence, Preferences appearance)
        {
            store = persistence;
            settings = store == null ? appearance : store.Load();
            Text = "CODEX PERISCOPE V5.2.0";
            BackColor = UiColors.Background;
            ForeColor = UiColors.Text;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = settings.InTaskbar && settings.Pinned;
            TopMost = settings.Top;
            Opacity = settings.Opacity;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered=true;
            // Diagnostic HWNDs may exceed a CI runner's virtual desktop. An explicit
            // maximum avoids the default desktop tracking-size cap truncating a
            // synthetic 200% capture. Production screen constraints remain native.
            if (store == null) MaximumSize=new Size(4096,4096);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 375, Screen.PrimaryScreen.WorkingArea.Top + 24);
            header = TextLabel("CODEX PERISCOPE", 12, InstrumentStyle.Text, true);
            version = TextLabel("PERISCOPE / V5.2.0", 8, UiColors.Muted, false);
            close = TextLabel("×", 16, UiColors.Muted, false);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Hide(); };
            Controls.Add(header); Controls.Add(version); Controls.Add(close);
            // One window, always-visible task telemetry: no popup or detail window.
            taskDock.BackColor = ColorTranslator.FromHtml("#1A2738");
            taskDeck.Dock = DockStyle.Fill;
            taskDock.Controls.Add(taskDeck);
            Controls.Add(taskDock);Controls.Add(compactRadars);compactRadars.Visible=false;taskDeck.CompactRadars=compactRadars;
            five = BuildMeter("5-HOUR QUOTA");
            week = BuildMeter("WEEKLY QUOTA");
            status = TextLabel("● Initializing Codex...", 8, UiColors.Muted, false);
            sourceFace.Controls.Add(status);
            statusAge = TextLabel("Waiting", 8, UiColors.Muted, false);
            statusAge.TextAlign = ContentAlignment.MiddleRight;
            sourceFace.Controls.Add(statusAge);
            sourceDetail=TextLabel("",8,InstrumentStyle.Muted,false);sourceDetail.Name="QuotaSource";
            sourceFace.Controls.Add(sourceDetail);
            status.Name="QuotaStatus";statusAge.Name="QuotaObserved";
            Controls.Add(sourceFace);sourceFace.Controls.Add(resetCount);Controls.Add(footerLeft);Controls.Add(footerRead);Controls.Add(footerState);resetCount.Visible=false;
            statusDivider = new Panel { BackColor = UiColors.Track };
            Controls.Add(statusDivider);
            resizeGrip = TextLabel("◢", 11, UiColors.Muted, true);
            resizeGrip.Cursor = Cursors.SizeNWSE;
            resizeGrip.AutoEllipsis=false;
            resizeGrip.TextAlign = ContentAlignment.MiddleCenter;
            tooltip.SetToolTip(resizeGrip, "Drag to resize gauges");
            Controls.Add(resizeGrip);
            originalTrayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            // WinForms Form.Icon is independent of NotifyIcon.Icon. When omitted,
            // Windows may show the generic multicolour WinForms squares in the taskbar.
            // Reuse the exact executable icon embedded by /win32icon in the build.
            Icon = originalTrayIcon ?? SystemIcons.Application;
            if (store != null) {
            tray = new NotifyIcon { Icon = originalTrayIcon ?? SystemIcons.Application, Text = "CODEX PERISCOPE " + typeof(MonitorForm).Assembly.GetName().Version.ToString(3), Visible = true, ContextMenuStrip = context };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) ToggleVisibility();
            };
            Deactivate += delegate { if (!settings.Pinned && Visible) Hide(); };
            // In transient-panel mode, never start with a permanently visible widget.
            Shown += delegate { if (!settings.Pinned) BeginInvoke((Action)delegate { Hide(); }); };
            MakeMenu();
            }
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
            if (store != null) {
            AttachDrag(this); AttachDrag(header); AttachDrag(version);
            AttachCircularResize();
            FormClosing += OnFormClosing;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
            }
            tick.Interval = 1000;
            tick.Tick += delegate { Heartbeat(); };
            taskMotion.Interval = 34; // At most 29.4 requested frames per second.
            taskMotion.Tick += delegate { taskDeck.Advance(); UpdateTaskMotion(); };
            VisibleChanged += delegate { UpdateTaskMotion(); };
            if (store != null) { tick.Start(); Heartbeat(); }
        }
        internal void Present(Snapshot quotas, LiveTaskReport missions, DateTimeOffset now, string state)
        {
            if (store != null) throw new InvalidOperationException("Presentation requires an isolated window.");
            presentationTime=now; presentationState=state; snapshot=quotas;
            lastSuccess=quotas == null ? (DateTimeOffset?)null : quotas.CapturedUtc;
            taskReport=missions;
            UpdateTaskSurface(now); PaintSnapshot();
        }
        internal MissionDeck Missions { get { return taskDeck; } }
        internal bool MotionTimerRunning { get { return taskMotion.Enabled; } }
        private DateTimeOffset UiNow { get { return presentationTime ?? DateTimeOffset.UtcNow; } }
        private static Label TextLabel(string text, float size, Color color, bool bold)
        {
            InstrumentLabel label=new InstrumentLabel { Text=text, ForeColor=color, BackColor=Color.Transparent, AutoSize=false, AutoEllipsis=true };
            label.SetFont(size*96/72,bold ? FontStyle.Bold : FontStyle.Regular);return label;
        }
        private Meter BuildMeter(string title)
        {
            Meter m = new Meter();
            m.Name = TextLabel(title, 10, UiColors.Text, true);
            m.Number = TextLabel("—", 17, UiColors.Muted, true);
            m.Number.TextAlign = ContentAlignment.MiddleRight;
            m.Track = new InstrumentPanel { BackColor = UiColors.Track, Height = 9 };
            m.Fill = new InstrumentFill { BackColor = UiColors.Five, Width = 0, Height = 9 };
            m.Track.Controls.Add(m.Fill);
            m.Ring = new CircularGauge();
            m.Reset = TextLabel("Waiting for Codex...", 8, UiColors.Muted, false);
            m.Name.Name="QuotaTitle";m.Number.Name="QuotaValue";m.Reset.Name="QuotaReset";
            m.Badge=TextLabel("",9,InstrumentStyle.Muted,true);m.Badge.Name="QuotaSeverity";m.Badge.TextAlign=ContentAlignment.MiddleCenter;
            m.Relative=TextLabel("",10,InstrumentStyle.Muted,false);m.Relative.Name="QuotaReset";m.Relative.TextAlign=ContentAlignment.MiddleRight;
            m.Face=new InstrumentPanel();m.Hint=TextLabel("REMAINING",6.5f,InstrumentStyle.Muted,false);m.Hint.Name="QuotaProvenance";
            m.Face.Controls.Add(m.Name); m.Face.Controls.Add(m.Number); m.Face.Controls.Add(m.Track); m.Face.Controls.Add(m.Reset); m.Face.Controls.Add(m.Ring);m.Face.Controls.Add(m.Hint);
            Controls.Add(m.Face);
            m.Face.Controls.Add(m.Badge);m.Face.Controls.Add(m.Relative);m.Badge.Visible=m.Relative.Visible=false;
            return m;
        }
        private int Unit(int value) {return (int)Math.Round(value*PeriscopeWindow.ScaleFor(this));}
        private Rectangle Units(Rectangle r) {return new Rectangle(Unit(r.X),Unit(r.Y),Math.Max(1,Unit(r.Width)),Math.Max(1,Unit(r.Height)));}
        private void PlaceMeter(Meter m,int x,int y,int width,bool micro)
        {
            int pad=micro?8:12;
            m.Face.Bounds=Units(new Rectangle(x,y,width,micro?62:80));
            m.Name.Bounds=Units(new Rectangle(pad,micro?5:7,width-108-pad,18));
            InstrumentStyle.FontFor(m.Name,micro?11:12.5f,FontStyle.Bold);
            m.Name.TextAlign=ContentAlignment.MiddleLeft;
            m.Number.Bounds=Units(new Rectangle(width-104,2,92,micro?29:32));
            InstrumentStyle.FontFor(m.Number,micro?20:24,FontStyle.Bold);
            m.Hint.Bounds=Units(new Rectangle(pad,micro?21:27,width-105-pad,micro?13:15));
            InstrumentStyle.FontFor(m.Hint,micro?9:10,FontStyle.Regular);
            m.Track.Bounds=Units(new Rectangle(pad,micro?35:43,width-2*pad,micro?5:7));
            m.Fill.Location=new Point(1,1);m.Fill.Height=Math.Max(1,m.Track.Height-2);
            m.Reset.Bounds=Units(new Rectangle(pad,micro?43:54,width-2*pad,micro?17:20));
            InstrumentStyle.FontFor(m.Reset,micro?11:11.5f,FontStyle.Regular);m.Reset.TextAlign=ContentAlignment.MiddleLeft;
        }
        private void PlaceCircular(Meter m,int x,int width)
        {
            int w=settings.CircularWidth;float ratio=w/(float)DesktopGeometry.DefaultWidth;
            m.Face.Bounds=Units(DesktopGeometry.ScaleRect(x,44,width,162,w));
            m.Name.Bounds=Units(DesktopGeometry.ScaleRect(6,4,width-12,20,w));
            m.Name.TextAlign=ContentAlignment.MiddleCenter;
            InstrumentStyle.FontFor(m.Name,12.5f*ratio,FontStyle.Bold);
            m.Ring.Bounds=Units(DesktopGeometry.ScaleRect((width-118)/2,24,118,118,w));
            m.Reset.Bounds=Units(DesktopGeometry.ScaleRect(7,142,width-14,19,w));
            InstrumentStyle.FontFor(m.Reset,11.5f*ratio,FontStyle.Regular);m.Reset.TextAlign=ContentAlignment.MiddleCenter;
        }
        private void MeterMode(Meter m,bool circular)
        {
            m.Ring.Visible=circular;m.Track.Visible=!circular;m.Number.Visible=!circular;m.Hint.Visible=!circular;
        }
        private void ApplyLayout()
        {
            if(five==null || week==null || statusAge==null || sourceDetail==null)return;
            SuspendLayout();
            try {
                compactRadars.Visible=false;resetCount.Visible=footerLeft.Visible=footerRead.Visible=footerState.Visible=false;
                if(settings.ObsidianLayout){ApplyParityLayout();ClearChrome();return;}
                foreach(Meter m in new []{five,week}){m.Face.Flat=false;m.Face.FooterRule=0;m.Ring.Target=false;((InstrumentFill)m.Fill).Luminous=false;((InstrumentLabel)m.Badge).Framed=false;foreach(InstrumentLabel l in m.Face.Controls.OfType<InstrumentLabel>()){l.Family="Segoe UI";l.Tracking=0;l.Marker=Color.Transparent;}}
                sourceFace.Strip=false;((InstrumentLabel)header).Family="Segoe UI";((InstrumentLabel)header).Tracking=0;
                five.Badge.Visible=week.Badge.Visible=five.Relative.Visible=week.Relative.Visible=false;taskDeck.Parity=false;taskDeck.Flat=false;sourceFace.Flat=false;sourceDetail.Visible=true;
                bool micro=settings.Layout=="Micro",circular=settings.Layout=="Circular";
                MeterMode(five,circular);MeterMode(week,circular);
                int width,quotaHeight;
                if(circular){settings.CircularWidth=DesktopGeometry.ClampWidth(settings.CircularWidth);width=settings.CircularWidth;quotaHeight=DesktopGeometry.HeightForWidth(width);}
                else {width=micro?484:settings.Layout=="Compact"?670:355;quotaHeight=micro?122:settings.Layout=="Compact"?177:257;}
                ClientSize=new Size(Unit(width),Unit(DesktopGeometry.CombinedHeight(width,quotaHeight,settings.Layout)));
                header.Bounds=Units(new Rectangle(micro?29:34,micro?4:8,micro?290:width-200,micro?21:24));
                InstrumentStyle.FontFor(header,micro?13:14,FontStyle.Bold);
                version.Visible=true;version.Text=LayoutVersionLabel(settings.ObsidianLayout,settings.Layout);
                version.TextAlign=ContentAlignment.MiddleRight;
                version.Bounds=Units(new Rectangle(width-169,10,126,21));InstrumentStyle.FontFor(version,settings.ObsidianLayout?10.5f:8.5f,FontStyle.Regular);
                close.Bounds=Units(new Rectangle(width-34,micro?3:7,25,24));InstrumentStyle.FontFor(close,16,FontStyle.Regular);
                if(circular) {
                    PlaceCircular(five,14,246);PlaceCircular(week,280,246);
                    Rectangle sourceBounds=DesktopGeometry.ScaleRect(14,214,512,37,width);
                    if(sourceBounds.Height<37) {sourceBounds.Height=37;sourceBounds.Y=DesktopGeometry.ActivityBounds(width,quotaHeight,settings.Layout).Top-42;}
                    sourceFace.Bounds=Units(sourceBounds);
                } else if(micro) {
                    PlaceMeter(five,12,31,220,true);PlaceMeter(week,240,31,232,true);
                    sourceFace.Bounds=Units(new Rectangle(8,98,468,35));
                } else if(settings.Layout=="Compact") {
                    PlaceMeter(five,14,53,317,false);PlaceMeter(week,339,53,317,false);
                    sourceFace.Bounds=Units(new Rectangle(14,139,642,39));
                } else {
                    PlaceMeter(five,14,48,327,false);PlaceMeter(week,14,136,327,false);
                    sourceFace.Bounds=Units(new Rectangle(14,220,327,39));
                }
                statusAge.Visible=true;statusDivider.Visible=false;
                int ageWidth=Unit(micro?124:142);
                int sourceTop=Math.Max(Unit(2),(sourceFace.Height-Unit(35))/2);
                status.SetBounds(Unit(9),sourceTop,Math.Max(1,sourceFace.Width-Unit(18)),Unit(micro?17:19));
                sourceDetail.SetBounds(Unit(9),sourceTop+Unit(micro?17:19),Math.Max(1,sourceFace.Width-ageWidth-Unit(20)),Unit(15));
                statusAge.SetBounds(sourceFace.Width-ageWidth-Unit(8),sourceTop+Unit(micro?17:19),ageWidth,Unit(15));
                InstrumentStyle.FontFor(status,micro?12:13,FontStyle.Bold);
                InstrumentStyle.FontFor(sourceDetail,11,FontStyle.Regular);InstrumentStyle.FontFor(statusAge,11,FontStyle.Regular);
                taskDock.Bounds=Units(DesktopGeometry.ActivityBounds(width,quotaHeight,settings.Layout));
                taskDeck.LayoutMode=settings.Layout;PlaceActivityControls(circular,micro);
                UpdateTaskSurface(UiNow);
                resizeGrip.Visible=circular && settings.Pinned;
                resizeGrip.Bounds=Units(new Rectangle(width-20,DesktopGeometry.CombinedHeight(width,quotaHeight,settings.Layout)-20,16,16));
                InstrumentStyle.FontFor(resizeGrip,11,FontStyle.Regular);
                if(store!=null) Location=DesktopGeometry.ClampToScreen(Bounds,Screen.FromRectangle(Bounds).WorkingArea).Location;
                PaintSnapshot();
            } finally {ResumeLayout(true);}
            ClearChrome();
        }
        private bool parityBlockedChrome;
        private void ClearChrome(){if(chrome!=null){chrome.Dispose();chrome=null;}Invalidate();}
        protected override void OnSizeChanged(EventArgs e){ClearChrome();base.OnSizeChanged(e);}
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if(Width<2 || Height<2){base.OnPaintBackground(e);return;}
            if(chrome==null) {
                chrome=new Bitmap(Width,Height);
                using(Graphics g=Graphics.FromImage(chrome))
                using(Brush body=new SolidBrush(BackColor))
                using(Brush bar=new SolidBrush(settings!=null && settings.ObsidianLayout && parityBlockedChrome?InstrumentStyle.AlertFace:(settings!=null && settings.ObsidianLayout?InstrumentStyle.Titlebar:InstrumentStyle.Blend(BackColor,Color.FromArgb(19,30,44),.6))))
                using(Pen edge=new Pen(InstrumentStyle.Border))
                using(Pen teal=new Pen(InstrumentStyle.Teal)) {
                    g.FillRectangle(body,ClientRectangle);g.FillRectangle(bar,0,0,Width,Unit(settings!=null && settings.ObsidianLayout?(settings.Layout=="Micro"?32:settings.Layout=="Compact"?36:40):settings!=null && settings.Layout=="Micro"?28:38));
                    g.DrawRectangle(edge,0,0,Width-1,Height-1);
                    if(settings!=null && settings.ObsidianLayout) {
                        int head=settings.Layout=="Micro"?32:settings.Layout=="Compact"?36:40,foot=settings.Layout=="Micro"?20:24;
                        using(Brush footer=new SolidBrush(InstrumentStyle.FooterFace))g.FillRectangle(footer,0,Height-Unit(foot),Width,Unit(foot));
                        using(Pen rule=new Pen(InstrumentStyle.Hairline)){g.DrawLine(rule,0,Unit(head),Width,Unit(head));g.DrawLine(rule,0,Height-Unit(foot),Width,Height-Unit(foot));}
                        Color point=parityBlockedChrome?InstrumentStyle.Red:InstrumentStyle.Teal;
                        using(Bitmap glow=InstrumentStyle.Halo(Unit(24),point))g.DrawImageUnscaled(glow,Unit(18-12),Unit(head/2-12));
                        using(Brush dot=new SolidBrush(point))g.FillEllipse(dot,Unit(15),Unit(head/2-3),Unit(6),Unit(6));
                    } else {
                    int r=Unit(12),x=Unit(settings!=null && settings.Layout=="Micro"?11:14),y=Unit(settings!=null && settings.Layout=="Micro"?9:14);
                    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.DrawEllipse(teal,x,y,r,r);g.DrawLine(teal,x+r/2,y+r/2,x+r,y+r/2);
                    }
                }
            }
            e.Graphics.DrawImageUnscaled(chrome,0,0);
        }
        protected override void WndProc(ref Message m)
        {
            int message=m.Msg;base.WndProc(ref m);
            if(message==0x02E0 && five!=null && week!=null && statusAge!=null && sourceDetail!=null)ApplyLayout();
        }
        private void PlaceActivityControls(bool circular, bool micro)
        { taskDeck.PerformLayout(); }

        internal static string LayoutVersionLabel(bool obsidian,string layout)
        {
            string name=layout=="Compact"?"COMPACT":layout=="Micro"?"MICRO":layout=="Vertical"?"VERTICAL":"CIRCULAR";
            return (obsidian?name:"CLASSIC "+name)+" \u00b7 v5.2.0";
        }
        internal static void SyncLayoutChoices(ToolStripMenuItem classic,ToolStripMenuItem obsidian,bool activeObsidian,string activeLayout)
        {
            foreach(ToolStripMenuItem option in classic.DropDownItems)option.Checked=!activeObsidian && option.Text==activeLayout;
            foreach(ToolStripMenuItem option in obsidian.DropDownItems)option.Checked=activeObsidian && option.Text==activeLayout;
        }
        internal void SelectTargetLayout(string layout)
        { SelectTargetLayout(layout,true); }
        internal void SelectTargetLayout(string layout,bool obsidian)
        {
            if(!new []{"Circular","Vertical","Compact","Micro"}.Contains(layout))throw new ArgumentException("Unknown layout");
            if(settings.Layout==layout && settings.ObsidianLayout==obsidian)return;
            settings.Layout=layout;settings.ObsidianLayout=obsidian;ApplyLayout();Save();
        }
        private void PlaceParityMeter(Meter m,int x,int y,int w,int h,bool circle)
        {
            m.Face.Bounds=Units(new Rectangle(x,y,w,h));m.Face.Flat=true;m.Face.Edge=InstrumentStyle.Hairline;m.Face.BackColor=InstrumentStyle.QuotaFace;
            ((InstrumentFill)m.Fill).Luminous=true;((InstrumentLabel)m.Badge).Framed=true;
            MeterMode(m,circle);m.Name.TextAlign=ContentAlignment.MiddleLeft;m.Hint.TextAlign=ContentAlignment.MiddleLeft;
            m.Name.Bounds=Units(new Rectangle(16,14,w-112,19));InstrumentStyle.Type(m.Name,InstrumentType.Panel);
            m.Badge.Bounds=Units(new Rectangle(w-86,14,70,18));InstrumentStyle.Type(m.Badge,InstrumentType.Badge);m.Badge.Visible=circle;
            m.Reset.TextAlign=ContentAlignment.MiddleLeft;
            if(circle) {
                m.Ring.Bounds=Units(new Rectangle((w-144)/2,44,144,144));m.Ring.Target=true;
                m.Reset.Bounds=Units(new Rectangle(16,h-29,w-32,18));m.Face.FooterRule=Unit(h-40);
            } else {
                bool micro=settings.Layout=="Micro";
                m.Name.Bounds=Units(new Rectangle(micro?8:12,micro?6:10,w-(micro?78:151),18));InstrumentStyle.Type(m.Name,InstrumentType.Label);
                m.Number.Bounds=Units(new Rectangle(w-(micro?69:139),micro?3:5,micro?61:61,micro?23:28));InstrumentStyle.Type(m.Number,micro?InstrumentType.MicroValue:InstrumentType.Value);
                m.Hint.Bounds=Units(new Rectangle(w-75,14,63,13));InstrumentStyle.Type(m.Hint,InstrumentType.Badge);m.Hint.Visible=!micro;
                m.Track.Bounds=Units(new Rectangle(micro?8:12,micro?27:39,w-(micro?16:24),micro?6:10));m.Track.BackColor=InstrumentStyle.Track;
                m.Fill.SetBounds(Unit(2),Unit(2),Math.Max(0,m.Fill.Width),Unit(micro?3:6));
                m.Reset.Bounds=Units(new Rectangle(micro?8:12,h-(micro?18:24),w-(micro?16:24),micro?13:17));m.Face.FooterRule=0;
            }
            InstrumentStyle.Type(m.Reset,settings.Layout=="Micro"?InstrumentType.Metadata:InstrumentType.Technical);InstrumentStyle.Type(m.Relative,settings.Layout=="Micro"?InstrumentType.Metadata:InstrumentType.Technical);
        }
        private void ApplyParityLayout()
        {
            settings.Sanitize();bool micro=settings.Layout=="Micro",circle=settings.Layout=="Circular",vertical=settings.Layout=="Vertical",compact=settings.Layout=="Compact";
            targetBlockedLayout=State()=="Direct" && snapshot!=null && snapshot.UsageAllowed==false;
            int w=circle?settings.ObsidianCircularWidth:micro?484:vertical?360:640,h=circle?480:micro?230:vertical?580:260;
            ClientSize=new Size(Unit(w),Unit(h));BackColor=ColorTranslator.FromHtml(settings.BackgroundColor);
            header.SetBounds(Unit(31),Unit(micro?4:8),Unit(170),Unit(24));InstrumentStyle.Type(header,InstrumentType.Product);header.ForeColor=InstrumentStyle.Bright;
            version.Visible=true;version.Text=LayoutVersionLabel(settings.ObsidianLayout,settings.Layout);version.TextAlign=ContentAlignment.MiddleRight;
            version.SetBounds(Unit(w-178),Unit(micro?6:10),Unit(138),Unit(18));InstrumentStyle.Type(version,InstrumentType.Metadata);if(!settings.ObsidianLayout)InstrumentStyle.FontFor(version,8.5f,FontStyle.Regular);version.ForeColor=InstrumentStyle.Metadata;
            close.SetBounds(Unit(w-31),Unit(micro?3:7),Unit(23),Unit(24));InstrumentStyle.FontFor(close,16,FontStyle.Regular);
            taskDeck.Parity=true;taskDeck.Flat=true;taskDeck.LayoutMode=settings.Layout;taskDeck.BackColor=InstrumentStyle.Canvas;taskDeck.Edge=Color.Transparent;
            compactRadars.Visible=compact;
            if(circle) {
                int mw=(w-48)/2;
                PlaceParityMeter(five,16,targetBlockedLayout?84:56,mw,targetBlockedLayout?212:234,true);
                PlaceParityMeter(week,32+mw,targetBlockedLayout?84:56,mw,targetBlockedLayout?212:234,true);
                sourceFace.Bounds=Units(targetBlockedLayout?new Rectangle(0,40,w,30):new Rectangle(16,298,w-32,25));
                taskDock.Bounds=Units(targetBlockedLayout?new Rectangle(16,308,w-32,140):new Rectangle(16,332,w-32,114));
            } else if(vertical) {
                PlaceParityMeter(five,14,86,332,84,false);PlaceParityMeter(week,14,178,332,84,false);
                sourceFace.Bounds=Units(new Rectangle(14,54,332,28));taskDock.Bounds=Units(new Rectangle(14,270,332,274));
            } else if(micro) {
                PlaceParityMeter(five,10,42,228,56,false);PlaceParityMeter(week,246,42,228,56,false);
                sourceFace.Bounds=Units(new Rectangle(0,210,484,20));taskDock.Bounds=Units(new Rectangle(10,136,464,64));
            } else {
                PlaceParityMeter(five,120,48,256,84,false);PlaceParityMeter(week,120,140,256,84,false);
                sourceFace.Bounds=Units(new Rectangle(0,236,640,24));taskDock.Bounds=Units(new Rectangle(388,48,240,176));compactRadars.Bounds=Units(new Rectangle(12,48,96,176));
            }
            sourceFace.Flat=true;sourceFace.Strip=!vertical;sourceFace.TopRule=micro||compact;sourceFace.Rail=Color.Transparent;
            sourceFace.BackColor=micro||compact?InstrumentStyle.FooterFace:InstrumentStyle.Canvas;sourceFace.Edge=InstrumentStyle.Hairline;
            statusDivider.Visible=sourceDetail.Visible=false;statusAge.Visible=resetCount.Visible=true;
            InstrumentStyle.Type(status,micro?InstrumentType.Metadata:InstrumentType.Technical);InstrumentStyle.Type(statusAge,InstrumentType.Metadata);InstrumentStyle.Type(resetCount,InstrumentType.Metadata);
            status.TextAlign=ContentAlignment.MiddleLeft;resetCount.TextAlign=ContentAlignment.MiddleRight;
            if(micro||compact) {
                status.SetBounds(Unit(10),Unit(2),Unit(compact?120:102),Unit(16));statusAge.SetBounds(Unit(compact?140:116),Unit(2),Unit(compact?180:145),Unit(16));
                resetCount.SetBounds(sourceFace.Width-Unit(140),Unit(2),Unit(130),Unit(16));
            } else if(vertical) {
                status.SetBounds(Unit(10),Unit(5),Unit(155),Unit(18));resetCount.SetBounds(Unit(174),Unit(5),Unit(148),Unit(18));statusAge.Visible=false;
            } else {
                status.SetBounds(Unit(targetBlockedLayout?36:8),Unit(3),Unit(targetBlockedLayout?280:170),Unit(19));
                resetCount.SetBounds(Unit(182),Unit(3),Unit(170),Unit(19));statusAge.SetBounds(sourceFace.Width-Unit(194),Unit(3),Unit(186),Unit(19));
                if(targetBlockedLayout)resetCount.SetBounds(Unit(326),Unit(3),Unit(126),Unit(19));
                else if(w<600){status.Width=Unit(100);resetCount.SetBounds(Unit(114),Unit(3),Unit(110),Unit(19));}
            }
            foreach(InstrumentLabel label in new []{footerLeft,footerRead,footerState}){label.Visible=circle||vertical;InstrumentStyle.Type(label,InstrumentType.Metadata);label.ForeColor=InstrumentStyle.Metadata;}
            footerLeft.SetBounds(Unit(12),Unit(h-21),Unit(vertical?104:300),Unit(17));footerRead.SetBounds(Unit(vertical?118:w-260),Unit(h-21),Unit(vertical?128:130),Unit(17));footerState.SetBounds(Unit(w-(vertical?112:128)),Unit(h-21),Unit(vertical?100:116),Unit(17));
            resizeGrip.Visible=false;taskDeck.PerformLayout();UpdateTaskSurface(UiNow);PaintSnapshot();ClearChrome();
        }
        private void PaintClassicSource(TargetSource view,string state,bool blocked,bool live)
        {
            if(blocked){status.Text="CODEX USAGE BLOCKED";sourceDetail.Text="LAST KNOWN / CLI · "+view.ResetCountShort;status.ForeColor=InstrumentStyle.Red;}
            else if(live){status.Text="QUOTAS / DIRECT READING";sourceDetail.Text="SOURCE / CLI · "+view.ResetCountShort;status.ForeColor=InstrumentStyle.Teal;}
            else {
                status.Text=state=="Unavailable"?"QUOTA SOURCE UNAVAILABLE":InstrumentStyle.IsStale(state)?"STALE QUOTA READING":"QUOTA READING UNVERIFIED";
                sourceDetail.Text=(lastSuccess.HasValue?"LAST KNOWN / CLI":"NO READING")+" · "+view.ResetCountShort;status.ForeColor=InstrumentStyle.Amber;
            }
            InstrumentStyle.FontFor(status,live?11.5f:settings.Layout=="Micro"?12:13,FontStyle.Bold);
            statusAge.Text=lastSuccess.HasValue?lastSuccess.Value.ToLocalTime().ToString("dd MMM HH:mm:ss",CultureInfo.GetCultureInfo("en-GB")):"No observation";
            statusAge.ForeColor=sourceDetail.ForeColor=InstrumentStyle.Muted;
            sourceFace.Edge=InstrumentStyle.Blend(InstrumentStyle.Border,status.ForeColor,.25);
            sourceFace.Rail=InstrumentStyle.Blend(InstrumentStyle.Border,status.ForeColor,.7);sourceFace.Warning=blocked;status.Left=Unit(blocked?27:9);
            status.Width=Math.Max(1,sourceFace.Width-status.Left-Unit(9));
        }
        private void PaintTargetSource(TargetSource view)
        {
            bool footer=settings.Layout=="Compact"||settings.Layout=="Micro",vertical=settings.Layout=="Vertical";
            status.Text=footer || (settings.Layout=="Circular" && Width<Unit(600) && !view.Blocked)?view.CompactStatus:vertical?(view.Current?"CLI Direct":view.CompactStatus):view.Status;
            status.ForeColor=view.Blocked?InstrumentStyle.Red:view.Current?InstrumentStyle.Teal:InstrumentStyle.Amber;
            resetCount.Text=view.ResetCount;resetCount.ForeColor=InstrumentStyle.Metadata;resetCount.Visible=true;
            statusAge.Text=view.ReadTime;statusAge.ForeColor=InstrumentStyle.Metadata;
            sourceFace.BackColor=view.Blocked?InstrumentStyle.AlertFace:footer?InstrumentStyle.FooterFace:InstrumentStyle.Canvas;sourceFace.Rail=Color.Transparent;sourceFace.Warning=view.Blocked && settings.Layout=="Circular";
            footerLeft.Text="LOCAL JSONL / PARTIAL";footerRead.Text=view.ReadTime;footerState.Text=view.Footer;footerState.ForeColor=view.Blocked?InstrumentStyle.Red:view.Current?InstrumentStyle.Teal:InstrumentStyle.Amber;
            string detail=view.Freshness+" / "+(lastSuccess.HasValue?lastSuccess.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss zzz",CultureInfo.GetCultureInfo("en-GB")):"No reading")+"\nResetsAvailable is the explicit CLI field; absent is unknown, never inferred from timestamps.\nLocal activity is independently observed JSONL, not guaranteed Desktop state.";
            foreach(Control c in new Control[]{status,statusAge,resetCount,footerRead,footerState})tooltip.SetToolTip(c,detail);
        }

        private void UpdateTaskMotion()
        {
            taskMotion.Enabled = Visible && WindowState != FormWindowState.Minimized && !suspended && settings.TaskAnimations &&
                taskDeck.NeedsFrames;
            taskDeck.SetMotion(taskMotion.Enabled);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if(settings!=null && taskDeck!=null && taskMotion!=null) UpdateTaskMotion();
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
            if (store == null) return;
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
                int desired = DesktopGeometry.ClampWidth(resizeOriginalWidth + (int)Math.Round((Cursor.Position.X - resizeMouse.Value.X) / PeriscopeWindow.ScaleFor(this)));
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
                    taskReport.IgnoredSubagents + " child sessions inspected";
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
            ToolStripMenuItem classicLayouts=Menu(layouts,"CLASSIC",null);
            ToolStripMenuItem obsidianLayouts=Menu(layouts,"OBSIDIAN",null);
            foreach (string family in new string[] { "CLASSIC", "OBSIDIAN" })
            {
                ToolStripMenuItem familyMenu=family=="CLASSIC"?classicLayouts:obsidianLayouts;
                bool obsidian=family=="OBSIDIAN";
                foreach (string mode in new string[] { "Circular", "Vertical", "Compact", "Micro" })
                {
                    string layout = mode;
                    ToolStripMenuItem option = new ToolStripMenuItem(mode);
                    option.ToolTipText="Select the "+family+" "+layout+" presentation.";
                    option.Click += delegate { SelectTargetLayout(layout,obsidian); };
                    familyMenu.DropDownItems.Add(option);
                }
            }
            layouts.DropDownOpening += delegate { SyncLayoutChoices(classicLayouts,obsidianLayouts,settings.ObsidianLayout,settings.Layout); };
            ToolStripMenuItem circularSize = Menu(appearance, "Gauge size", null);
            circularSize.ToolTipText = "Circular layout width; legacy pinned mode also supports the resize handle.";
            foreach (int value in new int[] { 480, 540, 620, 700 })
            {
                int pixels = value;
                ToolStripMenuItem option = new ToolStripMenuItem(value == 480 ? "Small · 480 px" :
                    value == 540 ? "Standard · 540 px" :
                    value == 620 ? "Large · 620 px" : "Extra large · 700 px");
                option.Tag = pixels;
                option.Click += delegate {
                if(settings.ObsidianLayout)settings.ObsidianCircularWidth=pixels;else settings.CircularWidth = pixels;
                    settings.Layout = "Circular";
                    ApplyLayout(); Save();
                };
                circularSize.DropDownItems.Add(option);
            }
            circularSize.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem customSizeInfo = new ToolStripMenuItem() { Enabled = false };
            circularSize.DropDownItems.Add(customSizeInfo);
            circularSize.DropDownOpening += delegate {
                int activeWidth=settings.ObsidianLayout?settings.ObsidianCircularWidth:settings.CircularWidth;
                foreach (ToolStripItem child in circularSize.DropDownItems)
                {
                    ToolStripMenuItem option = child as ToolStripMenuItem;
                    if (option != null && option.Tag is int)
                        option.Checked = (int)option.Tag == activeWidth;
                }
                customSizeInfo.Text = "Current size: " + activeWidth + " px";
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
            AddBackgroundPreset(backgrounds, "Premium / Deep Instrument", "#080E17");
            AddBackgroundPreset(backgrounds, "Obsidian / Sonar", "#080D14");
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
            Menu(historyMenu, "Show history (24H / 3D / 7D / 30D / ALL)", delegate { OpenHistory(); });
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
            five.Face.BackColor=InstrumentStyle.Blend(BackColor,Color.Black,.38);
            week.Face.BackColor=five.Face.BackColor;
            five.Ring.BackColor = Color.Transparent;
            week.Ring.BackColor = Color.Transparent;
            sourceFace.BackColor=InstrumentStyle.Blend(BackColor,Color.Black,.5);
            taskDock.BackColor=BackColor;taskDeck.BackColor=sourceFace.BackColor;
            five.Ring.Invalidate();
            week.Ring.Invalidate();
            taskDeck.Invalidate();
            ClearChrome();
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
                        "CODEX PERISCOPE — Contrast", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        "New release: " + latest.Tag + "\n\n" +
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
                    "Version: " + latest.Tag + "\n" +
                    "Size: " + asset.Size + " bytes\n" +
                    "GitHub SHA-256: " + asset.Sha256 + "\n\n" +
                    "Install this version now? The widget will close.\n" +
                    "The previous version will be backed up. This executable is NOT signed.",
                    "CODEX PERISCOPE — Confirm installation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
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
            string last = lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss", CultureInfo.GetCultureInfo("en-GB")) : "No";
            Version runningVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return "Version: " + runningVersion.ToString(3) + " (C# / Windows Forms)\n" +
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
            catch (Exception e) { MessageBox.Show(this, "Could not copy: " + e.Message, "Diagnostic"); }
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
                DispatchSystemEvent(delegate { suspended = true; InvalidateForWake("Windows entering sleep"); });
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
            taskDeck.UpdateReport(null,DateTimeOffset.UtcNow);
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
                InvalidateForWake("UI clock gap (> 15 s)");
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
                    LiveTaskReport previousTasks = taskReport;
                    bool canAlert = taskReport != null && settings.Alerts && settings.TaskAlerts;
                    if (canAlert)
                    {
                        foreach (LiveTask current in TaskNotificationRules.NewTerminals(taskReport, fresh))
                        {
                            string key = current.Identity + ":" + current.Phase;
                            if (sentTaskNotifications.ContainsKey(key)) continue;
                            sentTaskNotifications.Add(key,fresh.CheckedUtc);
                            tray.ShowBalloonTip(5500, "Codex · " + current.PhaseLabel,
                                current.Workspace + " · " + current.Duration(DateTimeOffset.UtcNow),
                                current.Phase == LiveTaskPhase.Failed ? ToolTipIcon.Error : ToolTipIcon.Info);
                        }
                    }
                    foreach (string key in sentTaskNotifications.Where(pair => fresh.CheckedUtc-pair.Value > TimeSpan.FromMinutes(10)).Select(pair => pair.Key).ToArray())
                        sentTaskNotifications.Remove(key);
                    taskReport = fresh;
                    UpdateTaskBadge();
                    // Same confirmed-event rules as notifications, independent of alert opt-in.
                    // No replay on startup, missing logs, stale scans or repeated terminals.
                    foreach (LiveTask terminal in TaskNotificationRules.NewTerminals(previousTasks, fresh))
                        if (terminal.Phase == LiveTaskPhase.Completed) taskDeck.ConfirmCompletion(terminal);
                    UpdateTaskMotion();
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
            taskDeck.UpdateReport(taskReport,now);
            tooltip.SetToolTip(taskDock,"Local observed turns · partial JSONL source; not guaranteed Desktop mission state.");
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
                string token = label + ":threshold:" + crossed.Value + ":" + newValue.ResetUtc.Value.ToUnixTimeSeconds();
                if (alerted.Add(token))
                    messages.Add("Quota " + label + " : " + crossed.Value + " % remaining reached");
            }
            if (alerted.Count > 100) alerted.Clear();
        }
        private string State()
        {
            if (store == null && presentationState != null) return presentationState;
            if (forcedStale) return lastSuccess.HasValue ? "Unverified" : "Unavailable";
            return QuotaHealth.State(lastSuccess, failures, settings.RefreshSeconds, DateTimeOffset.UtcNow);
        }
        private void PaintSnapshot()
        {
            string state = State();
            bool blocked = state == "Direct" && snapshot != null && snapshot.UsageAllowed == false;
            bool live = state == "Direct" && !blocked;
            if(settings.ObsidianLayout && targetBlockedLayout!=blocked){ApplyLayout();return;}
            if(settings.ObsidianLayout) {
                if(parityBlockedChrome!=blocked){parityBlockedChrome=blocked;ClearChrome();}
            }
            DrawMeter(five, snapshot == null ? null : snapshot.Five, "five", live, state, blocked);
            DrawMeter(week, snapshot == null ? null : snapshot.Week, "week", live, state, blocked);
            TargetSource source=TargetSource.From(snapshot,state,lastSuccess,UiNow);
            if(settings.ObsidianLayout)PaintTargetSource(source);
            else PaintClassicSource(source,state,blocked,live);
            string observation="Last Codex reading: "+(lastSuccess.HasValue ? lastSuccess.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss zzz",CultureInfo.GetCultureInfo("en-GB")) : "none")+
                "\n"+FreshnessLabel(lastSuccess,UiNow)+". Status: "+(blocked?"Usage blocked":state)+". Task availability is independent.";
            tooltip.SetToolTip(statusAge,observation);tooltip.SetToolTip(sourceDetail,observation);tooltip.SetToolTip(status,observation+"\n"+(live?"Source: Codex CLI / account/rateLimits/read":lastError));
            string caption = live && snapshot != null && snapshot.Week != null ?
                "CODEX PERISCOPE · " + Math.Round(snapshot.Week.Remaining) + "% weekly" : "CODEX PERISCOPE · " + state;
            if (tray != null) tray.Text = caption.Length > 63 ? "CODEX PERISCOPE" : caption;
            UpdateQuotaTrayIcon(state, blocked);
        }
        private void UpdateQuotaTrayIcon(string state, bool blocked)
        {
            if (tray == null) return;
            bool current = state == "Direct" && !blocked && snapshot != null;
            double? remaining = null;
            string window = "";
            if (current)
            {
                if (snapshot.Five != null)
                {
                    remaining = snapshot.Five.Remaining;
                    window = "5h";
                }
                if (snapshot.Week != null && (!remaining.HasValue || snapshot.Week.Remaining < remaining.Value))
                {
                    remaining = snapshot.Week.Remaining;
                    window = "7d";
                }
            }
            int rounded = remaining.HasValue ? (int)Math.Round(remaining.Value, MidpointRounding.AwayFromZero) : -1;
            List<MissionView> missions=taskDeck.At(DateTimeOffset.UtcNow);
            int activeTasks=missions.Count(v => v.State == RadarState.Active);
            LiveTaskPhase livePhase=activeTasks > 0 ? LiveTaskPhase.Active :
                missions.Any(v => v.State == RadarState.Complete) ? LiveTaskPhase.Completed : LiveTaskPhase.Unknown;
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
            if(settings.ObsidianLayout) {meter.Face.BackColor=blocked?ColorTranslator.FromHtml("#110F15"):InstrumentStyle.QuotaFace;meter.Face.Edge=blocked?ColorTranslator.FromHtml("#302026"):InstrumentStyle.Hairline;}
            meter.Reset.Visible = settings.ResetStyle != "hidden";
            bool exhausted=InstrumentStyle.Exhausted(data==null?(double?)null:data.Remaining,live);
            meter.Hint.Text=live ? (exhausted?"EXHAUSTED":"REMAINING") : data==null ? "NO READING" : "LAST KNOWN";
            meter.Relative.Visible=false;
            if (data == null)
            {
                ((InstrumentLabel)meter.Name).Marker=settings.ObsidianLayout && settings.Layout!="Micro"?UiColors.Muted:Color.Transparent;
                meter.Badge.Text="NO READING";meter.Badge.ForeColor=meter.Hint.ForeColor=UiColors.Muted;meter.Badge.BackColor=InstrumentStyle.QuotaFace;
                meter.Number.Text = "—"; meter.Number.ForeColor = UiColors.Muted;
                meter.Fill.Width = 0;
                meter.Ring.SetReading(null, false, UiColors.Muted);
                meter.Reset.Text = state == "Unavailable" ? "Unavailable" : "Window unavailable";
                return;
            }
            int percent = Math.Max(0, Math.Min(100, (int)Math.Round(data.Remaining, MidpointRounding.AwayFromZero)));
            Color color = live ? QuotaColor(percent) : UiColors.Muted;
            if(settings.ObsidianLayout){meter.Name.ForeColor=InstrumentStyle.Caption;((InstrumentLabel)meter.Name).Marker=settings.Layout=="Micro"?Color.Transparent:color;}
            meter.Hint.ForeColor=color;
            meter.Badge.Text=!live?"LAST KNOWN":exhausted?"EXHAUSTED":percent<=10?"CRITICAL":percent<=25?"ELEVATED":"NOMINAL";
            meter.Badge.ForeColor=color;meter.Badge.BackColor=InstrumentStyle.Blend(InstrumentStyle.QuotaFace,color,.12);
            meter.Number.Text = (settings.ObsidianLayout && percent>0 && percent<10 ? percent.ToString("00") : percent.ToString()) + "%";
            meter.Number.ForeColor = color;
            meter.Ring.SetReading(percent, live, color,exhausted);
            meter.Fill.Width = (int)Math.Round(Math.Max(0,meter.Track.Width-2) * data.Remaining / 100.0);
            meter.Fill.BackColor = color;
            // Blocked ordinary usage does not make a fresh reset timestamp stale.
            // The block remains clearly visible in the status/footer and tooltips.
            meter.Reset.Text = ResetLabel(data.ResetUtc, kind, settings.ResetStyle,
                settings.Layout, live || blocked, UiNow);
            if(settings.ObsidianLayout) {
                int total=meter.Face.Width-Unit(20);meter.Reset.Width=total;
                if(total<Unit(220) && !live && !blocked)meter.Reset.Text="Reset unverified";
                if(settings.ResetStyle=="both" && (live||blocked) && data.ResetUtc.HasValue) {
                    meter.Reset.Text=ResetLabel(data.ResetUtc,kind,"absolute",settings.Layout,true,UiNow).Replace("Reset on ","Reset ").Replace(" at "," ");
                    meter.Relative.Text=ResetLabel(data.ResetUtc,kind,"relative",settings.Layout,true,UiNow).Replace("Reset ","");
                    int rw=Unit(settings.Layout=="Micro"?82:90);meter.Reset.Width=Math.Max(1,total-rw-Unit(5));
                    if(meter.Reset.Width<Unit(132))meter.Reset.Text="Reset "+data.ResetUtc.Value.ToLocalTime().ToString(kind=="five"?"HH:mm":"MMM dd",CultureInfo.GetCultureInfo("en-GB"));
                    meter.Relative.SetBounds(meter.Face.Width-Unit(10)-rw,meter.Reset.Top,rw,meter.Reset.Height);meter.Relative.Visible=true;meter.Relative.ForeColor=live?color:UiColors.Muted;
                }
            }
            tooltip.SetToolTip(meter.Ring, blocked ? "Codex usage blocked" :
                live ? "Remaining quota · Codex CLI source" : "Stale quota reading");
            tooltip.SetToolTip(meter.Number, blocked ? "Codex reports usage is blocked" :
                live ? "Remaining quota · Codex CLI source" : "Stale quota reading");
            tooltip.SetToolTip(meter.Reset, (live || blocked) && data.ResetUtc.HasValue ?
                "Reset : " + data.ResetUtc.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss", CultureInfo.GetCultureInfo("en-GB")) + " (Windows local time)" : "Reset unavailable or unverified");
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
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
            readerGeneration++;
            CodexReader.Cancel();
            Save();
            tray.Visible = false;

            if (history != null && !history.IsDisposed) history.Close();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed) {
                resourcesDisposed=true;
                tick.Dispose(); taskMotion.Dispose(); tooltip.Dispose(); context.Dispose();
                if (tray != null) tray.Dispose();
                if (dynamicTrayIcon != null) dynamicTrayIcon.Dispose();
                if (originalTrayIcon != null) originalTrayIcon.Dispose();
                if(chrome!=null){chrome.Dispose();chrome=null;}
                if(store!=null){SystemEvents.PowerModeChanged-=OnPowerModeChanged;SystemEvents.TimeChanged-=OnTimeChanged;}
            }
            base.Dispose(disposing);
        }
    }
}

