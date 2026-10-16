// Tema motoru: Açık / Koyu / Windows teması. Renkleri ve vurgu rengini Windows'tan çeker.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ResizeBadger
{
    class Palette
    {
        public bool Dark;
        public Color Bg, Surface, Field, Text, Dim, Border, Hover, Accent, AccentHover, AccentText;
    }

    static class Theme
    {
        public static string Mode = "system";          // system | light | dark
        public static Palette P;
        public static Font Body, Semi, H1, H2, Small;

        [DllImport("dwmapi.dll")] static extern int DwmGetColorizationColor(out uint c, out bool opaque);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string idList);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr rect, IntPtr rgn, uint flags);

        // Yerel kaydırma çubukları / açılır listeler temayı (açık/koyu) izlesin
        static void NativeTheme(Control c, string darkApp, string darkClass)
        {
            c.HandleCreated -= OnNativeHandle; c.HandleCreated += OnNativeHandle;
            if (c.IsHandleCreated) ApplyNative(c, darkApp, darkClass);
        }
        static void OnNativeHandle(object s, EventArgs e) { Control c = (Control)s; ApplyNative(c, c is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer", c is ComboBox ? "Combobox" : null); }
        static void ApplyNative(Control c, string darkApp, string darkClass)
        {
            try
            {
                if (P.Dark) SetWindowTheme(c.Handle, darkApp, darkClass); else SetWindowTheme(c.Handle, "Explorer", null);
                RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0400 | 0x0080 | 0x0100);   // INVALIDATE | FRAME | ALLCHILDREN | UPDATENOW
            }
            catch { }
        }

        static Theme()
        {
            Body = new Font("Segoe UI", 9.5f);
            Semi = new Font("Segoe UI Semibold", 9.5f);
            H1 = new Font("Segoe UI Semibold", 18f);
            H2 = new Font("Segoe UI Semibold", 11.5f);
            Small = new Font("Segoe UI", 8.75f);
            Reload();
        }

        // Windows "uygulamalar için açık tema" kapalıysa koyu
        public static bool SystemDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        static Color SystemAccent()
        {
            try
            {
                uint c; bool o;
                if (DwmGetColorizationColor(out c, out o) == 0)
                    return Color.FromArgb(255, (int)((c >> 16) & 255), (int)((c >> 8) & 255), (int)(c & 255));
            }
            catch { }
            return Color.FromArgb(0, 120, 212);
        }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        static double Lum(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0; }

        public static void Reload()
        {
            bool dark = Mode == "dark" || (Mode != "light" && SystemDark());
            Palette p = new Palette(); p.Dark = dark;
            Color acc = SystemAccent();
            if (dark) { while (Lum(acc) < 0.42) acc = Mix(acc, Color.White, 0.25); }
            else { while (Lum(acc) > 0.62) acc = Mix(acc, Color.Black, 0.2); }
            p.Accent = acc;
            p.AccentHover = dark ? Mix(acc, Color.White, 0.15) : Mix(acc, Color.Black, 0.12);
            p.AccentText = Lum(acc) > 0.6 ? Color.FromArgb(20, 20, 20) : Color.White;
            if (dark)
            {
                p.Bg = Color.FromArgb(31, 31, 31); p.Surface = Color.FromArgb(43, 43, 43); p.Field = Color.FromArgb(51, 51, 51);
                p.Text = Color.FromArgb(242, 242, 242); p.Dim = Color.FromArgb(166, 166, 166);
                p.Border = Color.FromArgb(70, 70, 70); p.Hover = Color.FromArgb(58, 58, 58);
            }
            else
            {
                p.Bg = Color.FromArgb(243, 243, 243); p.Surface = Color.White; p.Field = Color.FromArgb(244, 244, 244);
                p.Text = Color.FromArgb(27, 27, 27); p.Dim = Color.FromArgb(97, 97, 97);
                p.Border = Color.FromArgb(222, 222, 222); p.Hover = Color.FromArgb(234, 234, 234);
            }
            P = p;
        }

        static void DrawComboItem(object sender, DrawItemEventArgs e)
        {
            ComboBox cb = (ComboBox)sender; Palette p = P;
            bool closedBox = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool hot = !closedBox && (e.State & DrawItemState.Selected) != 0;
            Color back = hot ? Mix(p.Surface, p.Accent, 0.35) : p.Field;
            using (SolidBrush b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index < 0 || e.Index >= cb.Items.Count) return;
            Rectangle r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, cb.GetItemText(cb.Items[e.Index]), Body, r, p.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        static int Bgr(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        // Başlık çubuğunu temaya uydur (Windows 10 20H1+ koyu, Windows 11'de renk)
        public static void TitleBar(Form f)
        {
            try
            {
                if (!f.IsHandleCreated) { f.HandleCreated += delegate { TitleBar(f); }; return; }
                int d = P.Dark ? 1 : 0;
                DwmSetWindowAttribute(f.Handle, 20, ref d, 4);
                if (DwmSetWindowAttribute(f.Handle, 20, ref d, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref d, 4);
                int cap = Bgr(P.Bg), txt = Bgr(P.Text), bor = Bgr(P.Border);
                DwmSetWindowAttribute(f.Handle, 35, ref cap, 4);   // başlık rengi (Windows 11)
                DwmSetWindowAttribute(f.Handle, 36, ref txt, 4);   // başlık yazısı
                DwmSetWindowAttribute(f.Handle, 34, ref bor, 4);   // kenarlık
            }
            catch { }
        }

        // Bir formu / kontrol ağacını geçerli temayla boya
        public static void Apply(Control root)
        {
            Form f = root as Form;
            if (f != null) { f.BackColor = P.Bg; f.ForeColor = P.Text; TitleBar(f); }
            foreach (Control c in root.Controls) Style(c);
            root.Invalidate(true);
        }

        static void Style(Control c)
        {
            Palette p = P;
            string role = c.Tag as string ?? "";
            if (c is Card) { c.BackColor = p.Surface; c.ForeColor = p.Text; }
            else if (c is NavButton) { c.ForeColor = p.Text; }
            else if (c is Switch) { c.ForeColor = p.Text; c.BackColor = c.Parent != null ? c.Parent.BackColor : p.Surface; }
            else if (c is Button)
            {
                Button b = (Button)c;
                b.FlatStyle = FlatStyle.Flat; b.Cursor = Cursors.Hand; b.UseVisualStyleBackColor = false;
                b.FlatAppearance.BorderSize = 1;
                if (role == "primary")
                {
                    b.BackColor = p.Accent; b.ForeColor = p.AccentText; b.FlatAppearance.BorderColor = p.Accent;
                    b.FlatAppearance.MouseOverBackColor = p.AccentHover; b.FlatAppearance.MouseDownBackColor = p.AccentHover;
                }
                else
                {
                    b.BackColor = p.Field; b.ForeColor = p.Text; b.FlatAppearance.BorderColor = p.Border;
                    b.FlatAppearance.MouseOverBackColor = p.Hover; b.FlatAppearance.MouseDownBackColor = p.Border;
                }
            }
            else if (c is TextBox) { c.BackColor = p.Field; c.ForeColor = p.Text; ((TextBox)c).BorderStyle = BorderStyle.FixedSingle; }
            else if (c is ComboBox)
            {
                ComboBox cb = (ComboBox)c; cb.FlatStyle = FlatStyle.Flat; cb.BackColor = p.Field; cb.ForeColor = p.Text;
                // Sistem çizimi koyu temada metni/arka planı kaydırıyor: öğeleri kendimiz çiziyoruz
                cb.DrawMode = DrawMode.OwnerDrawFixed; cb.ItemHeight = 22;
                cb.DrawItem -= DrawComboItem; cb.DrawItem += DrawComboItem;
                NativeTheme(cb, "DarkMode_CFD", "Combobox");
                cb.Invalidate();
            }
            else if (c is NumericUpDown) { c.BackColor = p.Field; c.ForeColor = p.Text; }
            else if (c is DataGridView) StyleGrid((DataGridView)c);
            else if (c is ListView) { c.BackColor = p.Surface; c.ForeColor = p.Text; ((ListView)c).BorderStyle = BorderStyle.None; NativeTheme(c, "DarkMode_Explorer", null); }
            else if (c is Label)
            {
                // saydam değil, ebeveynin rengi: kaydırırken artık/üst üste binme olmasın
                c.BackColor = c.Parent != null ? c.Parent.BackColor : p.Bg;
                c.ForeColor = (role == "dim") ? p.Dim : (role == "accent" ? p.Accent : p.Text);
            }
            else if (c is Panel)
            {
                if (role == "nav") c.BackColor = p.Surface;
                else if (role == "navline") c.BackColor = p.Border;
                else c.BackColor = p.Bg;
                c.ForeColor = p.Text;
                if (((Panel)c).AutoScroll) NativeTheme(c, "DarkMode_Explorer", null);
            }
            foreach (Control ch in c.Controls) Style(ch);
        }

        static void StyleGrid(DataGridView g)
        {
            Palette p = P;
            g.BackgroundColor = p.Surface; g.GridColor = p.Border; g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersHeight = 36; g.RowTemplate.Height = 32;
            foreach (DataGridViewRow r in g.Rows) r.Height = 32;
            g.ColumnHeadersDefaultCellStyle.BackColor = p.Hover; g.ColumnHeadersDefaultCellStyle.ForeColor = p.Dim;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.Hover; g.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.Dim;
            g.ColumnHeadersDefaultCellStyle.Font = Semi;
            g.DefaultCellStyle.BackColor = p.Surface; g.DefaultCellStyle.ForeColor = p.Text;
            g.DefaultCellStyle.SelectionBackColor = Mix(p.Surface, p.Accent, 0.35); g.DefaultCellStyle.SelectionForeColor = p.Text;
            g.DefaultCellStyle.Font = Body;
            NativeTheme(g, "DarkMode_Explorer", null);
            foreach (Control sb in g.Controls) if (sb is ScrollBar) NativeTheme(sb, "DarkMode_Explorer", null);   // tablonun kendi kaydırma çubukları
        }

        // Tepsi / bağlam menüsü
        public static void Menu(ContextMenuStrip m)
        {
            m.Renderer = new ThemedRenderer();
            m.Font = Body; m.BackColor = P.Surface; m.ForeColor = P.Text;
        }
    }

    class ThemedColors : ProfessionalColorTable
    {
        Palette p { get { return Theme.P; } }
        public override Color ToolStripDropDownBackground { get { return p.Surface; } }
        public override Color ImageMarginGradientBegin { get { return p.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return p.Surface; } }
        public override Color ImageMarginGradientEnd { get { return p.Surface; } }
        public override Color MenuBorder { get { return p.Border; } }
        public override Color MenuItemBorder { get { return p.Hover; } }
        public override Color MenuItemSelected { get { return p.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return p.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return p.Hover; } }
        public override Color SeparatorDark { get { return p.Border; } }
        public override Color SeparatorLight { get { return p.Surface; } }
        public override Color CheckBackground { get { return p.Accent; } }
        public override Color CheckSelectedBackground { get { return p.Accent; } }
        public override Color CheckPressedBackground { get { return p.Accent; } }
    }

    class ThemedRenderer : ToolStripProfessionalRenderer
    {
        public ThemedRenderer() : base(new ThemedColors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.P.Text : Theme.P.Dim;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            base.OnRenderItemCheck(e);
        }
    }
}
