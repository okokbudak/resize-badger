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

    // Açılır kutu: kapalı görünümü tamamen kendimiz çiziyoruz (Windows'un çizimi koyu temada ve farklı ekran ölçeklerinde
    // beyaz bant / kaymış metin bırakıyor). Açılan liste Theme.DrawComboItem ile çizilir.
    class ThemedCombo : ComboBox
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct PAINTSTRUCT
        {
            public IntPtr hdc; public int fErase; public int L, T, R, B; public int fRestore, fIncUpdate;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgb;
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h, out PAINTSTRUCT ps);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EndPaint(IntPtr h, ref PAINTSTRUCT ps);
        bool hover;

        public ThemedCombo() { DropDownStyle = ComboBoxStyle.DropDownList; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0014) { m.Result = (IntPtr)1; return; }                 // WM_ERASEBKGND
            if (m.Msg == 0x000F && DropDownStyle == ComboBoxStyle.DropDownList)    // WM_PAINT
            {
                PAINTSTRUCT ps;
                IntPtr dc = BeginPaint(Handle, out ps);
                try { using (Graphics g = Graphics.FromHdc(dc)) Draw(g); }
                finally { EndPaint(Handle, ref ps); }
                return;
            }
            base.WndProc(ref m);
        }

        void Draw(Graphics g)
        {
            Palette p = Theme.P; Rectangle r = ClientRectangle;
            using (SolidBrush b = new SolidBrush(p.Field)) g.FillRectangle(b, r);
            using (Pen pen = new Pen(Focused ? p.Accent : (hover ? p.Dim : p.Border))) g.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
            string t = SelectedItem != null ? GetItemText(SelectedItem) : "";
            TextRenderer.DrawText(g, t, Theme.Body, new Rectangle(8, 0, Math.Max(10, r.Width - 8 - 28), r.Height), p.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = r.Width - 16, cy = r.Height / 2;
            using (Pen cp = new Pen(p.Dim, 1.6f)) g.DrawLines(cp, new Point[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
        }
    }

    // Bezel düzeltme şeması: ekranlar yan yana, aralarında gizli piksel boşluğu (boşluk genişliği orantılı çizilir)
    class MonitorStrip : Control
    {
        public int Count = 3;
        public int[] Gaps = new int[0];

        public MonitorStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 100;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Palette p = Theme.P;
            g.Clear(Parent != null ? Parent.BackColor : p.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int n = Math.Max(2, Count);
            float[] gw = new float[n - 1]; float gsum = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int gp = i < Gaps.Length ? Gaps[i] : 0;
                gw[i] = 16 + Math.Min(gp / 3f, 56f);
                gsum += gw[i];
            }
            float mw = Math.Max(30f, (Width - 4 - gsum) / n), mh = Height - 30, x = 2;
            for (int i = 0; i < n; i++)
            {
                Rectangle r = Rectangle.Round(new RectangleF(x, 4, mw, mh));
                using (GraphicsPath path = Gfx.Round(r, 6))
                using (SolidBrush b = new SolidBrush(p.Field))
                using (Pen pen = new Pen(p.Dim, 2f)) { g.FillPath(b, path); g.DrawPath(pen, path); }
                TextRenderer.DrawText(g, (i + 1).ToString(), Theme.H2, r, p.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += mw;
                if (i < n - 1)
                {
                    RectangleF gr = new RectangleF(x, 4, gw[i], mh);       // çerçevenin arkasında kalan (gizli) pikseller
                    using (HatchBrush hb = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(120, p.Accent), Color.Transparent)) g.FillRectangle(hb, gr);
                    int gp = i < Gaps.Length ? Gaps[i] : 0;
                    TextRenderer.DrawText(g, gp + " px", Theme.Small, Rectangle.Round(new RectangleF(x - 24, mh + 8, gw[i] + 48, 18)), gp > 0 ? p.Accent : p.Dim,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    x += gw[i];
                }
            }
        }
    }

    // Kaydırılan sayfa: WS_EX_COMPOSITED ile tüm çocuklar çift tamponlu çizilir (kaydırırken artık / üst üste binme olmaz)
    class Page : Panel
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowScrollBar(IntPtr h, int bar, bool show);
        public Page() { DoubleBuffered = true; ResizeRedraw = true; }
        // Sayfalar yalnızca dikey kaydırılır; yatay çubuk hiç gösterilmez (kartlar genişliğe uyar)
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (IsHandleCreated) ShowScrollBar(Handle, 0, false);
        }
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; }
        }
        // Gizlenirken kaydırma konumunu sıfırla: aksi halde tekrar gösterilince çocuklar yerinden kayar
        public void ResetScroll() { AutoScrollPosition = new Point(0, 0); }
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
            g.Clear(BackColor.A == 255 ? BackColor : (Parent != null ? Parent.BackColor : p.Surface));
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
        public bool Selected = false;
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
