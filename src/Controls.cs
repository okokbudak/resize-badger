// Modern görünümlü özel kontroller: Kart, Anahtar (toggle), Gezinme düğmesi.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ResizeBadger
{
    static class Gfx
    {
        public static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // Yuvarlak köşeli kart paneli
    class Card : Panel
    {
        public Card() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.P.Bg);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Gfx.Round(r, 8))
            using (SolidBrush b = new SolidBrush(Theme.P.Surface))
            using (Pen pen = new Pen(Theme.P.Border))
            {
                e.Graphics.FillPath(b, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    // Açık/kapalı anahtarı (CheckBox yerine)
    class Switch : CheckBox
    {
        public Switch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; AutoSize = false; Height = 30; Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Palette p = Theme.P;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = 40, h = 20, y = (Height - h) / 2;
            Rectangle pill = new Rectangle(1, y, w, h);
            using (GraphicsPath path = Gfx.Round(pill, h / 2))
            {
                if (Checked) using (SolidBrush b = new SolidBrush(p.Accent)) g.FillPath(b, path);
                else using (Pen pen = new Pen(p.Dim, 1.5f)) g.DrawPath(pen, path);
            }
            int k = 12, kx = Checked ? pill.Right - k - 4 : pill.X + 5, ky = y + (h - k) / 2;
            using (SolidBrush kb = new SolidBrush(Checked ? p.AccentText : p.Dim)) g.FillEllipse(kb, kx, ky, k, k);
            Rectangle tr = new Rectangle(w + 14, 0, Width - w - 16, Height);
            TextRenderer.DrawText(g, Text, Theme.Body, tr, Enabled ? p.Text : p.Dim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
    }

    // Yan menü düğmesi
    class NavButton : Control
    {
        public string Glyph = "";
        public bool Selected;
        bool hover;
        static readonly bool HasIcons = HasFont("Segoe MDL2 Assets");
        static bool HasFont(string n) { foreach (FontFamily f in FontFamily.Families) if (f.Name == n) return true; return false; }

        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Height = 42; Cursor = Cursors.Hand; TabStop = true;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Palette p = Theme.P;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(p.Surface);
            Rectangle r = new Rectangle(8, 3, Width - 16, Height - 6);
            if (Selected || hover)
                using (GraphicsPath path = Gfx.Round(r, 6))
                using (SolidBrush b = new SolidBrush(Selected ? p.Hover : Theme.Mix(p.Surface, p.Hover, 0.6))) g.FillPath(b, path);
            if (Selected)
                using (GraphicsPath bar = Gfx.Round(new Rectangle(8, Height / 2 - 9, 4, 18), 2))
                using (SolidBrush b = new SolidBrush(p.Accent)) g.FillPath(b, bar);
            int x = 24;
            if (HasIcons && Glyph.Length > 0)
            {
                using (Font f = new Font("Segoe MDL2 Assets", 12f))
                    TextRenderer.DrawText(g, Glyph, f, new Rectangle(x, 0, 28, Height), Selected ? p.Accent : p.Dim,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                x += 34;
            }
            TextRenderer.DrawText(g, Text, Selected ? Theme.Semi : Theme.Body, new Rectangle(x, 0, Width - x - 8, Height), p.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }
}
