using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShinoCodexMonitor
{
    internal static class InstrumentStyle
    {
        internal static readonly Color Teal=ColorTranslator.FromHtml("#32E0C4");
        internal static readonly Color Blue=ColorTranslator.FromHtml("#58A6FF");
        internal static readonly Color Amber=ColorTranslator.FromHtml("#E9B85C");
        internal static readonly Color Red=ColorTranslator.FromHtml("#F35A65");
        internal static readonly Color Border=ColorTranslator.FromHtml("#344B60");
        internal static readonly Color Text=ColorTranslator.FromHtml("#E4EDF5");
        internal static readonly Color Muted=ColorTranslator.FromHtml("#B3C4D5");
        // Individual Stitch screens: optical/material roles, not user quota thresholds.
        internal static readonly Color Canvas=ColorTranslator.FromHtml("#080D14"),QuotaFace=ColorTranslator.FromHtml("#0E1620"),ActivityFace=ColorTranslator.FromHtml("#0B1219");
        internal static readonly Color Hairline=ColorTranslator.FromHtml("#1D2A37"),Track=ColorTranslator.FromHtml("#172331"),Bright=ColorTranslator.FromHtml("#F2F6F9");
        internal static readonly Color Violet=ColorTranslator.FromHtml("#A477FF"),AlertFace=ColorTranslator.FromHtml("#241013");
        internal static Color MissionAccent(int number) {return new []{Teal,Blue,Violet,Amber}[Math.Max(0,number-1)%4];}
        internal static bool IsStale(string state) {return state=="Stale" || state=="Stale data";}
        internal static bool Exhausted(double? raw,bool current) {return current && raw.HasValue && raw.Value==0;}
        internal static void GlowArc(Graphics g,RectangleF bounds,float start,float sweep,Color color,float stroke,float scale)
        {
            // Cached layered falloff approximates the reference's 6 logical-pixel blur.
            for(int i=10;i>=1;i--)using(Pen p=new Pen(Color.FromArgb((int)(24*(1-i/12f)),color),stroke+12*scale*i/10f)) {
                p.StartCap=p.EndCap=LineCap.Round;g.DrawArc(p,bounds,start,sweep);
            }
        }
        internal static Bitmap Halo(int side,Color color)
        {
            Bitmap b=new Bitmap(Math.Max(2,side),Math.Max(2,side));
            using(Graphics g=Graphics.FromImage(b))using(GraphicsPath path=new GraphicsPath()) {
                path.AddEllipse(0,0,b.Width,b.Height);
                using(PathGradientBrush brush=new PathGradientBrush(path)) {
                    brush.InterpolationColors=new ColorBlend {Positions=new []{0f,.4f,.75f,1f},Colors=new []{Color.FromArgb(0,color),Color.FromArgb(35,color),Color.FromArgb(150,color),Color.FromArgb(245,color)}};
                    g.FillEllipse(brush,0,0,b.Width,b.Height);
                }
            }return b;
        }
        internal static GraphicsPath Rounded(RectangleF bounds,float radius)
        {
            float d=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
            GraphicsPath p=new GraphicsPath();
            p.AddArc(bounds.Left,bounds.Top,d,d,180,90);p.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);
            p.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);p.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
        internal static Color Blend(Color a, Color b, double ratio)
        { return Color.FromArgb((int)(a.R*(1-ratio)+b.R*ratio),(int)(a.G*(1-ratio)+b.G*ratio),(int)(a.B*(1-ratio)+b.B*ratio)); }
        internal static void FontFor(Control control, float logicalPixels, FontStyle style)
        {
            InstrumentLabel label=control as InstrumentLabel;
            if (label!=null) label.SetFont(logicalPixels*PeriscopeWindow.ScaleFor(control),style);
        }
    }
    internal sealed class InstrumentLabel : Label
    {
        internal string Family="Segoe UI";
        internal bool Framed;
        private Font owned;
        internal InstrumentLabel() {UseMnemonic=false;}
        internal void SetFont(float pixels, FontStyle style)
        {
            if (owned!=null && Math.Abs(owned.Size-pixels)<.05 && owned.Style==style && owned.FontFamily.Name==Family) return;
            Font previous=owned; owned=new Font(Family,pixels,style,GraphicsUnit.Pixel); Font=owned;
            if (previous!=null) previous.Dispose();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(!Framed || Width<2 || Height<2)return;
            using(Pen p=new Pen(Color.FromArgb(100,ForeColor)))
            using(GraphicsPath path=InstrumentStyle.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),3*PeriscopeWindow.ScaleFor(this))) {
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.DrawPath(p,path);
            }
        }
        protected override void Dispose(bool disposing)
        { base.Dispose(disposing); if (disposing && owned!=null) { owned.Dispose(); owned=null; } }
    }
    // Cached bezel/inset. Paints allocate only when size or appearance changes.
    internal class InstrumentPanel : Panel
    {
        internal bool Warning;
        private bool flat;
        internal bool Flat {get{return flat;}set{if(flat==value)return;flat=value;ClearFace();}}
        private Bitmap face;
        private Color edge=InstrumentStyle.Border;
        private Color rail=Color.Transparent;
        internal Color Rail {get{return rail;}set{if(rail==value)return;rail=value;ClearFace();}}
        internal Color Edge { get { return edge; } set { if(edge==value)return; edge=value; ClearFace(); } }
        internal InstrumentPanel()
        { DoubleBuffered=true; ResizeRedraw=true; BackColor=Color.FromArgb(8,13,20); Margin=Padding.Empty; }
        private void ClearFace() { if(face!=null){face.Dispose();face=null;} Invalidate(); }
        protected override void OnBackColorChanged(EventArgs e) { ClearFace(); base.OnBackColorChanged(e); }
        protected override void OnSizeChanged(EventArgs e) { ClearFace(); base.OnSizeChanged(e); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if(Width<2 || Height<2) { base.OnPaintBackground(e); return; }
            if(face==null) {
                face=new Bitmap(Width,Height);
                using(Graphics g=Graphics.FromImage(face))
                using(LinearGradientBrush fill=new LinearGradientBrush(ClientRectangle,
                    flat?BackColor:InstrumentStyle.Blend(BackColor,Color.FromArgb(44,66,86),.26),flat?BackColor:InstrumentStyle.Blend(BackColor,Color.Black,.22),90))
                using(Pen border=new Pen(edge))
                using(Pen inset=new Pen(Color.FromArgb(38,190,215,234)))
                using(GraphicsPath shape=InstrumentStyle.Rounded(new RectangleF(.5f,.5f,Width-1,Height-1),4*PeriscopeWindow.ScaleFor(this))) {
                    g.Clear(Parent==null?BackColor:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
                    g.FillPath(fill,shape);g.DrawPath(border,shape);
                    g.DrawLine(inset,7,2,Width-8,2);
                    using(Pen shade=new Pen(Color.FromArgb(70,0,0,0)))g.DrawLine(shade,5,Height-3,Width-6,Height-3);
                    if(rail.A>0)using(Pen accent=new Pen(rail,2*PeriscopeWindow.ScaleFor(this)))g.DrawLine(accent,2,7,2,Height-8);
                }
            }
            e.Graphics.DrawImageUnscaled(face,0,0);
        }
        protected override void Dispose(bool disposing) { if(disposing)ClearFace(); base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(!Warning)return;float s=PeriscopeWindow.ScaleFor(this);
            using(Pen p=new Pen(InstrumentStyle.Red,1.4f*s)) {
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                e.Graphics.DrawPolygon(p,new []{new PointF(8*s,15*s),new PointF(14*s,4*s),new PointF(20*s,15*s)});
                e.Graphics.DrawLine(p,14*s,8*s,14*s,11*s);e.Graphics.DrawLine(p,14*s,12.5f*s,14*s,13*s);
            }
        }
    }
    // A narrow luminous core over the quota track; no timer or image allocation.
    internal sealed class InstrumentFill : Panel
    {
        internal bool Luminous;
        internal InstrumentFill(){DoubleBuffered=true;ResizeRedraw=true;}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(!Luminous || Width<1 || Height<1)return;
            using(LinearGradientBrush light=new LinearGradientBrush(new Rectangle(0,0,Math.Max(1,Width),Height),
                InstrumentStyle.Blend(BackColor,Color.White,.35),BackColor,90))e.Graphics.FillRectangle(light,ClientRectangle);
        }
    }
    // Explicit overflow only: no native horizontal/white scrollbar or AutoScroll feedback.
    internal sealed class InstrumentScroll : Control
    {
        private int extent, page, offset, dragY, dragOffset;
        private bool dragging;
        internal event EventHandler OffsetChanged;
        internal int Offset { get {return offset;} set {int next=Math.Max(0,Math.Min(value,Math.Max(0,extent-page))); if(next==offset)return;offset=next;Invalidate();if(OffsetChanged!=null)OffsetChanged(this,EventArgs.Empty);} }
        internal void Configure(int content, int viewport) { extent=content;page=viewport;Visible=content>viewport;Offset=offset;Invalidate(); }
        internal InstrumentScroll() { SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true); Cursor=Cursors.Hand; TabStop=false; }
        private Rectangle Thumb {
            get { int h=Math.Max(18,Height*page/Math.Max(1,extent));h=Math.Min(Height,h);
                return new Rectangle(2,(Height-h)*offset/Math.Max(1,extent-page),Math.Max(2,Width-4),h); }
        }
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(BackColor); using(Brush track=new SolidBrush(Color.FromArgb(32,50,65))) e.Graphics.FillRectangle(track,Width/2,0,1,Height);using(Brush ink=new SolidBrush(InstrumentStyle.Muted))e.Graphics.FillRectangle(ink,Thumb); }
        protected override void OnMouseDown(MouseEventArgs e) {base.OnMouseDown(e);Capture=true;dragging=true;dragY=e.Y;if(!Thumb.Contains(e.Location))Offset=(extent-page)*e.Y/Math.Max(1,Height);dragOffset=offset;}
        protected override void OnMouseMove(MouseEventArgs e) {base.OnMouseMove(e);if(dragging)Offset=dragOffset+(e.Y-dragY)*Math.Max(1,extent-page)/Math.Max(1,Height-Thumb.Height);}
        protected override void OnMouseUp(MouseEventArgs e) {dragging=false;Capture=false;base.OnMouseUp(e);}
    }
}
