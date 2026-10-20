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
        private bool exhausted;
        internal bool Target;
        private Bitmap arc;
        private void ClearArc(){if(arc!=null){arc.Dispose();arc=null;}}
        internal CircularGauge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }
        internal void SetReading(int? remaining, bool isCurrent, Color color)
        {SetReading(remaining,isCurrent,color,false);}
        internal void SetReading(int? remaining, bool isCurrent, Color color,bool rawExhausted)
        {
            int? clamped = remaining.HasValue ? Math.Max(0, Math.Min(100, remaining.Value)) : (int?)null;
            if (percentage == clamped && current == isCurrent && accent == color && exhausted==rawExhausted) return;
            exhausted=rawExhausted;ClearArc();
            percentage = clamped;
            current = isCurrent;
            accent = color;
            Invalidate();
        }
        private Bitmap bezel;
        private Color bezelColor;
        private Font numberFont,captionFont,percentFont;
        private void EnsureBezel()
        {
            Color background=BackColor.A==0 && Parent!=null ? Parent.BackColor : BackColor;
            if(bezel!=null && bezel.Size==ClientSize && bezelColor==background)return;
            if(bezel!=null)bezel.Dispose();bezel=new Bitmap(Math.Max(1,Width),Math.Max(1,Height));bezelColor=background;
            if(numberFont!=null)numberFont.Dispose();if(captionFont!=null)captionFont.Dispose();
            float unit=PeriscopeWindow.ScaleFor(this),size=Math.Min(Width,Height);
            if(percentFont!=null)percentFont.Dispose();percentFont=new Font("Consolas",size*.139f,FontStyle.Bold,GraphicsUnit.Pixel);
            ClearArc();numberFont=new Font("Consolas",size*.21f,FontStyle.Bold,GraphicsUnit.Pixel);
            captionFont=new Font("Consolas",Math.Max(9*unit,size*.0625f),FontStyle.Bold,GraphicsUnit.Pixel);
            using(Graphics g=Graphics.FromImage(bezel)) {
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
                float side=Math.Min(Width,Height),cx=Width/2f,cy=Height/2f,r=side*.4f;
                using(GraphicsPath path=new GraphicsPath()) {
                    path.AddEllipse(cx-r,cy-r,r*2,r*2);
                    using(PathGradientBrush fill=new PathGradientBrush(path)) {
                        fill.CenterColor=InstrumentStyle.Blend(background,Color.Black,.25);fill.SurroundColors=new []{background};
                        g.FillEllipse(fill,cx-r,cy-r,r*2,r*2);
                    }
                }
                using(Pen calibration=new Pen(Color.FromArgb(100,81,103,122),Math.Max(1,side*.0125f))) {
                    calibration.DashPattern=new []{2f,6f};
                    g.DrawEllipse(calibration,cx-side/3,cy-side/3,side*2/3,side*2/3);
                }
                using(Pen shadow=new Pen(Color.FromArgb(105,0,0,0),side*.09f))g.DrawEllipse(shadow,cx-r,cy-r,r*2,r*2);
                using(Pen track=new Pen(InstrumentStyle.Track,side*.0667f))g.DrawEllipse(track,cx-r,cy-r,r*2,r*2);

            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(Math.Min(Width,Height)<=14)return;EnsureBezel();
            Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.DrawImageUnscaled(bezel,0,0);
            float side=Math.Min(Width,Height),diameter=side*.8f;
            RectangleF circle=new RectangleF((Width-diameter)/2,(Height-diameter)/2,diameter,diameter);
            if(arc==null) {
                arc=new Bitmap(Width,Height);
                using(Graphics a=Graphics.FromImage(arc)) {a.SmoothingMode=SmoothingMode.AntiAlias;
                    if(percentage.HasValue && percentage.Value>0) {
                        float sweep=360f*percentage.Value/100;
                        if(current)InstrumentStyle.GlowArc(a,circle,-90,sweep,accent,side*.0667f,PeriscopeWindow.ScaleFor(this));
                        using(Pen fill=new Pen(current ? accent : ColorTranslator.FromHtml("#8BA0B1"),side*.0667f)) {
                            fill.StartCap=fill.EndCap=LineCap.Round;a.DrawArc(fill,circle,-90,sweep);
                        }
                    }
                }
            }g.DrawImageUnscaled(arc,0,0);
            string digits=!percentage.HasValue ? "--" : Target && percentage.Value>0?percentage.Value.ToString("00"):percentage.Value.ToString();
            using(Brush ink=new SolidBrush(!percentage.HasValue || !current ? InstrumentStyle.Muted : accent))
            using(StringFormat center=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}) {
                if(Target && percentage.HasValue) {
                    SizeF n=g.MeasureString(digits,numberFont,PointF.Empty,StringFormat.GenericTypographic),p=g.MeasureString("%",percentFont,PointF.Empty,StringFormat.GenericTypographic);
                    float x=(Width-n.Width-p.Width)/2,y=Height/2f-side*.21f;
                    g.DrawString(digits,numberFont,ink,new PointF(x,y),StringFormat.GenericTypographic);g.DrawString("%",percentFont,ink,new PointF(x+n.Width,y+side*.065f),StringFormat.GenericTypographic);
                } else g.DrawString(digits+(percentage.HasValue?"%":""),numberFont,ink,new RectangleF(0,-side*.04f,Width,Height),center);
            }
            using(Brush ink=new SolidBrush(current?accent:InstrumentStyle.Muted))
            using(StringFormat center=new StringFormat {Alignment=StringAlignment.Center})
                g.DrawString(!percentage.HasValue?"NO READING":current?(exhausted?"EXHAUSTED":"REMAINING"):"LAST KNOWN",captionFont,ink,new RectangleF(0,Height/2f+side*.17f,Width,side*.19f),center);
        }
        protected override void Dispose(bool disposing){if(disposing){ClearArc();if(bezel!=null){bezel.Dispose();bezel=null;}if(numberFont!=null)numberFont.Dispose();if(captionFont!=null)captionFont.Dispose();if(percentFont!=null)percentFont.Dispose();}base.Dispose(disposing);}
    }
}
