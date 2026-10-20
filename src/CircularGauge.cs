using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal static class QuotaPalette
    {
        // Thresholds refer to the INTEGER percentage displayed to the user.
        internal static string Hex(int remaining, Preferences p)
        {
            int value = Math.Max(0, Math.Min(100, remaining));
            return value >= 76 ? p.Color76 : value >= 51 ? p.Color51 :
                value >= 26 ? p.Color26 : value >= 11 ? p.Color11 : p.Color0;
        }
        internal static Color ColorFor(int remaining, Preferences p)
        {
            return ColorTranslator.FromHtml(Hex(remaining, p));
        }
    }

    internal sealed class CircularGauge : Control
    {
        private int? percentage;
        private bool current;
        private Color accent = Color.Gray;
        internal CircularGauge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }
        internal void SetReading(int? remaining, bool isCurrent, Color color)
        {
            int? clamped = remaining.HasValue ? Math.Max(0, Math.Min(100, remaining.Value)) : (int?)null;
            if (percentage == clamped && current == isCurrent && accent == color) return;
            percentage = clamped;
            current = isCurrent;
            accent = color;
            Invalidate();
        }
        private Bitmap bezel;
        private Color bezelColor;
        private Font numberFont,captionFont;
        private void EnsureBezel()
        {
            Color background=BackColor.A==0 && Parent!=null ? Parent.BackColor : BackColor;
            if(bezel!=null && bezel.Size==ClientSize && bezelColor==background)return;
            if(bezel!=null)bezel.Dispose();bezel=new Bitmap(Math.Max(1,Width),Math.Max(1,Height));bezelColor=background;
            if(numberFont!=null)numberFont.Dispose();if(captionFont!=null)captionFont.Dispose();
            float unit=PeriscopeWindow.ScaleFor(this),size=Math.Min(Width,Height);
            numberFont=new Font("Segoe UI",size*.275f,FontStyle.Bold,GraphicsUnit.Pixel);
            captionFont=new Font("Segoe UI",Math.Max(10.5f*unit,size*.078f),FontStyle.Bold,GraphicsUnit.Pixel);
            using(Graphics g=Graphics.FromImage(bezel)) {
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
                float side=Math.Min(Width,Height),cx=Width/2f,cy=Height/2f,r=side*.44f;
                using(GraphicsPath path=new GraphicsPath()) {
                    path.AddEllipse(cx-r,cy-r,r*2,r*2);
                    using(PathGradientBrush fill=new PathGradientBrush(path)) {
                        fill.CenterColor=InstrumentStyle.Blend(background,Color.Black,.4);fill.SurroundColors=new []{InstrumentStyle.Blend(background,Color.FromArgb(21,40,56),.5)};
                        g.FillEllipse(fill,cx-r,cy-r,r*2,r*2);
                    }
                }
                using(Pen tick=new Pen(Color.FromArgb(48,78,98))) {
                    for(int d=0;d<360;d+=6) {
                        double angle=d*Math.PI/180;float outer=side*.49f,inner=outer-(d%30==0?side*.03f:side*.012f);
                        g.DrawLine(tick,cx+(float)Math.Cos(angle)*inner,cy+(float)Math.Sin(angle)*inner,cx+(float)Math.Cos(angle)*outer,cy+(float)Math.Sin(angle)*outer);
                    }
                    g.DrawEllipse(tick,cx-side*.37f,cy-side*.37f,side*.74f,side*.74f);
                }
                using(Pen shadow=new Pen(Color.FromArgb(105,0,0,0),side*.09f))g.DrawEllipse(shadow,cx-r,cy-r,r*2,r*2);
                using(Pen track=new Pen(Color.FromArgb(35,55,72),side*.06f))g.DrawEllipse(track,cx-r,cy-r,r*2,r*2);
                using(Pen rim=new Pen(Color.FromArgb(85,106,141,168),Math.Max(1,side*.006f)))g.DrawEllipse(rim,cx-side*.475f,cy-side*.475f,side*.95f,side*.95f);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(Math.Min(Width,Height)<=14)return;EnsureBezel();
            Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.DrawImageUnscaled(bezel,0,0);
            float side=Math.Min(Width,Height),diameter=side*.88f;
            RectangleF circle=new RectangleF((Width-diameter)/2,(Height-diameter)/2,diameter,diameter);
            if(percentage.HasValue && percentage.Value>0)
                using(Pen fill=new Pen(current ? accent : ColorTranslator.FromHtml("#8BA0B1"),side*.058f)) {
                    fill.StartCap=fill.EndCap=LineCap.Round;g.DrawArc(fill,circle,-90,360f*percentage.Value/100);
                }
            string text=!percentage.HasValue ? "--" : percentage.Value+"%";
            using(Brush ink=new SolidBrush(!percentage.HasValue || !current ? InstrumentStyle.Muted : InstrumentStyle.Text))
            using(StringFormat center=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                g.DrawString(text,numberFont,ink,new RectangleF(0,-side*.04f,Width,Height),center);
            using(Brush ink=new SolidBrush(current?InstrumentStyle.Muted:InstrumentStyle.Amber))
            using(StringFormat center=new StringFormat {Alignment=StringAlignment.Center})
                g.DrawString(!percentage.HasValue?"NO READING":current?"REMAINING":"LAST KNOWN",captionFont,ink,new RectangleF(0,Height/2f+side*.17f,Width,side*.19f),center);
        }
        protected override void Dispose(bool disposing){if(disposing){if(bezel!=null){bezel.Dispose();bezel=null;}if(numberFont!=null)numberFont.Dispose();if(captionFont!=null)captionFont.Dispose();}base.Dispose(disposing);}
    }
}
