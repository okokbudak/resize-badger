// Modern ayar penceresi: yan menü + kartlar. Tüm metinler L (çok dilli), tüm renkler Theme'den gelir.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ResizeBadger
{
    class SettingsForm : Form
    {
        readonly Config cfg; readonly AppContext app;
        const int PAD = 28, CW = 730;           // sayfa kenar boşluğu, kart genişliği
        Panel nav, content, bar;
        readonly List<Panel> pages = new List<Panel>();
        readonly List<NavButton> navBtns = new List<NavButton>();
        bool saved;

        // sayfa denetimleri
        DataGridView grid, ruleGrid; DataGridViewComboBoxColumn ruleCombo;
        readonly TextBox[] hotBoxes = new TextBox[Config.Actions.Length];
        ComboBox autoBox, langBox, themeBox; Switch autoSw, startupSw, surConfirm;
        TextBox excl; NumericUpDown surPrimary; Label surStatus, surMsg;
        const int HotCol = 8;                    // ön ayar tablosunda kısayol sütunu
        const int ContentW = 810, ContentH = 656; // sağ alan (pencere 1040x720 - yan menü 230 - alt çubuk 64)

        public SettingsForm(Config c, AppContext a)
        {
            cfg = c; app = a;
            AutoScaleDimensions = new SizeF(96f, 96f); AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Resize Badger"; Font = Theme.Body; DoubleBuffered = true;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(1040, 720); MinimumSize = new Size(960, 640);
            Icon = a != null ? a.TrayIcon : AppContext.LoadIcon();

            // Dock sırası: önce Fill olan, sonra kenara yaslananlar eklenir
            content = new Panel(); content.Dock = DockStyle.Fill; content.Tag = "page"; Controls.Add(content);
            BuildNav();
            BuildBar();
            pages.Add(BuildWindowsPage()); pages.Add(BuildRulesPage()); pages.Add(BuildSurroundPage()); pages.Add(BuildGeneralPage());
            foreach (Panel p in pages) { content.Controls.Add(p); p.Visible = false; }
            content.Controls.Add(bar);
            Theme.Apply(this);
            FormClosed += delegate { if (!saved) { Theme.Mode = cfg.Theme; Theme.Reload(); } };
            GoPage(0);
        }

        // ---------------------------------------------------------------- iskelet
        void BuildNav()
        {
            nav = new Panel(); nav.Dock = DockStyle.Left; nav.Width = 230; nav.Tag = "nav";
            Label t = new Label(); t.Text = "Resize Badger"; t.Font = Theme.H2; t.AutoSize = true; t.Location = new Point(24, 22); nav.Controls.Add(t);
            Label v = new Label(); v.Text = L.F("gen.version", Ver.V); v.Font = Theme.Small; v.Tag = "dim"; v.AutoSize = true; v.Location = new Point(25, 50); nav.Controls.Add(v);
            string[] keys = { "nav.windows", "nav.rules", "nav.surround", "nav.general" };
            string[] glyphs = { "", "", "", "" };
            for (int i = 0; i < keys.Length; i++)
            {
                NavButton b = new NavButton(); b.Text = L.T(keys[i]); b.Glyph = glyphs[i];
                b.SetBounds(0, 90 + i * 46, 230, 42); int idx = i;
                b.Click += delegate { GoPage(idx); };
                nav.Controls.Add(b); navBtns.Add(b);
            }
            Controls.Add(nav);
        }

        void BuildBar()
        {
            bar = new Panel(); bar.Dock = DockStyle.Bottom; bar.Height = 64; bar.Tag = "page";
            Label h = new Label(); h.Text = L.T("bar.hint"); h.Tag = "dim"; h.Font = Theme.Small; h.AutoSize = true; h.Location = new Point(PAD, 24); bar.Controls.Add(h);
            Button close = new Button(); close.Text = L.T("btn.close"); close.Size = new Size(110, 36); close.Click += delegate { Close(); };
            Button save = new Button(); save.Text = L.T("btn.save"); save.Tag = "primary"; save.Size = new Size(120, 36); save.Click += SaveClick;
            bar.Controls.Add(close); bar.Controls.Add(save);
            EventHandler place = delegate
            {
                close.Location = new Point(bar.Width - PAD - close.Width, 14);
                save.Location = new Point(close.Left - 12 - save.Width, 14);
            };
            bar.Resize += place; bar.Width = ContentW; place(null, EventArgs.Empty);
            AcceptButton = save;
        }

        void GoPage(int i)
        {
            for (int k = 0; k < pages.Count; k++) { pages[k].Visible = (k == i); navBtns[k].Selected = (k == i); navBtns[k].Invalidate(); }
            if (i == 2 && surStatus != null && app != null) RefreshSurround();
        }

        public void ShowPage(int i) { GoPage(i); Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); }
        public void ThemeChanged() { Theme.Apply(this); }

        // ---------------------------------------------------------------- yardımcılar
        Panel NewPage(string title, string desc)
        {
            Panel p = new Panel(); p.Size = new Size(ContentW, ContentH); p.Dock = DockStyle.Fill; p.AutoScroll = true; p.Tag = "page";
            Label h = new Label(); h.Text = title; h.Font = Theme.H1; h.AutoSize = true; h.Location = new Point(PAD, 22); p.Controls.Add(h);
            Label d = new Label(); d.Text = desc; d.Tag = "dim"; d.AutoSize = true; d.Location = new Point(PAD + 2, 66); p.Controls.Add(d);
            return p;
        }
        Card NewCard(Panel page, int y, int h)
        {
            Card c = new Card(); c.SetBounds(PAD, y, CW, h); c.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            page.Controls.Add(c); return c;
        }
        Label Lbl(Control parent, string text, int x, int y, int w, Font f, string role)
        {
            Label l = new Label(); l.Text = text; l.Location = new Point(x, y); if (w > 0) { l.AutoSize = false; l.Size = new Size(w, 24); } else l.AutoSize = true;
            if (f != null) l.Font = f; if (role != null) l.Tag = role; parent.Controls.Add(l); return l;
        }
        TextBox HotBox(Control parent, int actionIndex, int x, int y, int w, string value)
        {
            TextBox b = new TextBox(); b.ReadOnly = true; b.Text = value; b.SetBounds(x, y, w, 28); b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            b.KeyDown += delegate(object s, KeyEventArgs e) { string v = KeyText(e); if (v != null) ((TextBox)s).Text = v; };
            parent.Controls.Add(b); if (actionIndex >= 0) hotBoxes[actionIndex] = b; return b;
        }
        Button Btn(Control parent, string text, int x, int y, int w, bool primary)
        {
            Button b = new Button(); b.Text = text; b.SetBounds(x, y, w, 34); if (primary) b.Tag = "primary"; parent.Controls.Add(b); return b;
        }

        // Basılan tuş birleşimini "Ctrl+Alt+X" olarak verir; Del/Esc/Backspace temizler
        static string KeyText(KeyEventArgs e)
        {
            e.SuppressKeyPress = true; e.Handled = true;
            Keys k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return null;
            if (k == Keys.Delete || k == Keys.Back || k == Keys.Escape) return "";
            if (!e.Control && !e.Alt && !e.Shift) return null;
            string name = k.ToString();
            if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = name.Substring(1);
            return (e.Control ? "Ctrl+" : "") + (e.Alt ? "Alt+" : "") + (e.Shift ? "Shift+" : "") + name;
        }
        static int Num(object o, int def) { int v; return o != null && int.TryParse(o.ToString().Trim(), out v) ? v : def; }
        static bool Chk(object o) { return o != null && (bool)o; }

        DataGridView NewGrid(Control parent, int x, int y, int w, int h)
        {
            DataGridView g = new DataGridView(); g.SetBounds(x, y, w, h); g.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            g.AllowUserToAddRows = false; g.AllowUserToResizeRows = false; g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect; g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.ScrollBars = ScrollBars.Vertical;
            parent.Controls.Add(g); return g;
        }
        void TextCol(DataGridView g, string key, int w, bool fill)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn(); c.HeaderText = L.T(key); c.Width = w;
            c.SortMode = DataGridViewColumnSortMode.NotSortable; if (fill) { c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; c.MinimumWidth = w; }
            g.Columns.Add(c);
        }
        void CheckCol(DataGridView g, string key, int w)
        {
            DataGridViewCheckBoxColumn c = new DataGridViewCheckBoxColumn(); c.HeaderText = L.T(key); c.Width = w; g.Columns.Add(c);
        }

        // ---------------------------------------------------------------- 1. Pencereler
        Panel BuildWindowsPage()
        {
            Panel p = NewPage(L.T("nav.windows"), L.T("win.desc"));
            int y = 108;

            Card c1 = NewCard(p, y, 410);
            Lbl(c1, L.T("win.presets"), 20, 16, 0, Theme.H2, null);
            grid = NewGrid(c1, 20, 52, CW - 40, 236);
            // sütun sırası: Ad, Genişlik, Yükseklik, X, Y, Ortala, Kenarlıksız, İç alan, Kısayol
            TextCol(grid, "col.name", 110, true); TextCol(grid, "col.width", 70, false); TextCol(grid, "col.height", 70, false);
            TextCol(grid, "X", 46, false); TextCol(grid, "Y", 46, false);
            CheckCol(grid, "col.center", 64); CheckCol(grid, "col.borderless", 82); CheckCol(grid, "col.inner", 76); TextCol(grid, "col.hotkey", 120, false);
            grid.EditingControlShowing += GridEditing;
            foreach (Preset pr in cfg.Presets) grid.Rows.Add(pr.Name, pr.W.ToString(), pr.H.ToString(), pr.X.ToString(), pr.Y.ToString(), pr.Center, pr.Borderless, pr.Inner, pr.Hotkey);
            Button add = Btn(c1, L.T("btn.add"), 20, 300, 110, false);
            add.Click += delegate { grid.Rows.Add(L.T("preset.new"), "1920", "1080", "0", "0", true, false, true, ""); };
            Button del = Btn(c1, L.T("btn.delete"), 140, 300, 110, false);
            del.Click += delegate { foreach (DataGridViewRow r in new List<DataGridViewRow>(Sel(grid))) grid.Rows.Remove(r); };
            Label hint = Lbl(c1, L.T("win.presets.hint"), 20, 346, CW - 40, Theme.Small, "dim"); hint.Height = 50;
            y += 410 + 16;

            Card c2 = NewCard(p, y, 56 + 4 * 40);
            Lbl(c2, L.T("win.global"), 20, 16, 0, Theme.H2, null);
            int row = 0;
            for (int i = 0; i < Config.Actions.Length; i++)
            {
                if (Config.Actions[i] == "surround") continue;
                Lbl(c2, Config.ActionName(i), 20, 58 + row * 40, CW - 280, null, null);
                HotBox(c2, i, CW - 240, 54 + row * 40, 220, cfg.Hot[Config.Actions[i]]);
                row++;
            }
            y += c2.Height + 16;

            Card c3 = NewCard(p, y, 246);
            Lbl(c3, L.T("win.auto"), 20, 16, 0, Theme.H2, null);
            Lbl(c3, L.T("win.autoPreset"), 20, 60, CW - 280, null, null);
            autoBox = new ComboBox(); autoBox.DropDownStyle = ComboBoxStyle.DropDownList; autoBox.SetBounds(CW - 280, 56, 260, 28); autoBox.Anchor = AnchorStyles.Top | AnchorStyles.Right; c3.Controls.Add(autoBox);
            FillAutoBox();
            autoSw = new Switch(); autoSw.Text = L.T("win.autoApply"); autoSw.Checked = cfg.Auto; autoSw.SetBounds(20, 98, CW - 40, 30); c3.Controls.Add(autoSw);
            Lbl(c3, L.T("win.exclude"), 20, 146, CW - 40, null, "dim");
            excl = new TextBox(); excl.Text = cfg.Exclude; excl.SetBounds(20, 176, CW - 40, 28); excl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; c3.Controls.Add(excl);
            y += c3.Height + PAD;
            p.AutoScrollMinSize = new Size(0, y);
            return p;
        }

        IEnumerable<DataGridViewRow> Sel(DataGridView g) { foreach (DataGridViewRow r in g.SelectedRows) yield return r; }

        void FillAutoBox()
        {
            autoBox.Items.Clear(); autoBox.Items.Add(L.T("preset.maximize"));
            foreach (Preset pr in cfg.Presets) autoBox.Items.Add(pr.Label);
            autoBox.SelectedIndex = Math.Max(0, Math.Min(cfg.AutoPreset + 1, autoBox.Items.Count - 1));
        }

        void GridEditing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            TextBox tb = e.Control as TextBox;
            if (tb == null) return;
            tb.KeyDown -= HotCellKey;
            if (grid.CurrentCell.ColumnIndex == HotCol) tb.KeyDown += HotCellKey;
        }
        void HotCellKey(object sender, KeyEventArgs e) { string v = KeyText(e); if (v != null) ((TextBox)sender).Text = v; }

        // ---------------------------------------------------------------- 2. Program kuralları
        Panel BuildRulesPage()
        {
            Panel p = NewPage(L.T("nav.rules"), L.T("rules.desc"));
            Card c = NewCard(p, 108, 520);
            ruleGrid = NewGrid(c, 20, 20, CW - 40, 340);
            CheckCol(ruleGrid, "col.enabled", 60);
            TextCol(ruleGrid, "col.program", 280, true);
            ruleCombo = new DataGridViewComboBoxColumn(); ruleCombo.HeaderText = L.T("col.preset"); ruleCombo.Width = 300; ruleCombo.FlatStyle = FlatStyle.Flat;
            ruleGrid.Columns.Add(ruleCombo);
            ruleGrid.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            RefreshRuleCombos();
            foreach (Rule r in cfg.Rules) AddRuleRow(r.Proc, r.Preset, r.Enabled);
            Button pick = Btn(c, L.T("rules.pickRunning"), 20, 374, 250, false);
            pick.Click += delegate { foreach (string n in PickRunning()) AddRuleIfNew(n); };
            Button file = Btn(c, L.T("rules.pickFile"), 282, 374, 210, false);
            file.Click += delegate
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = L.T("rules.fileTitle"); d.Filter = L.T("rules.fileFilter") + " (*.exe)|*.exe";
                    if (d.ShowDialog() == DialogResult.OK) AddRuleIfNew(Path.GetFileNameWithoutExtension(d.FileName));
                }
            };
            Button del = Btn(c, L.T("btn.delete"), 504, 374, 110, false);
            del.Click += delegate { foreach (DataGridViewRow r in new List<DataGridViewRow>(Sel(ruleGrid))) ruleGrid.Rows.Remove(r); };
            Label note = Lbl(c, L.T("rules.note"), 20, 424, CW - 40, Theme.Small, "dim"); note.Height = 84;
            p.AutoScrollMinSize = new Size(0, 108 + 520 + PAD);
            return p;
        }

        void RefreshRuleCombos()
        {
            ruleCombo.Items.Clear(); ruleCombo.Items.Add(L.T("preset.maximize"));
            foreach (DataGridViewRow r in grid.Rows)
            {
                object v = r.Cells[0].Value;
                if (v != null && v.ToString().Trim().Length > 0 && !ruleCombo.Items.Contains(v.ToString())) ruleCombo.Items.Add(v.ToString());
            }
        }
        void AddRuleRow(string proc, string preset, bool enabled)
        {
            string shown = preset.Length == 0 ? L.T("preset.maximize") : preset;
            if (!ruleCombo.Items.Contains(shown)) ruleCombo.Items.Add(shown);   // silinmiş ön ayar da görünsün
            ruleGrid.Rows.Add(enabled, proc, shown);
        }
        void AddRuleIfNew(string proc)
        {
            if (string.IsNullOrEmpty(proc)) return;
            foreach (DataGridViewRow r in ruleGrid.Rows)
                if (string.Equals((r.Cells[1].Value ?? "").ToString(), proc, StringComparison.OrdinalIgnoreCase)) return;
            RefreshRuleCombos();
            string first = ruleCombo.Items.Count > 1 ? ruleCombo.Items[1].ToString() : "";
            AddRuleRow(proc, first, true);
            ruleGrid.CurrentCell = ruleGrid.Rows[ruleGrid.Rows.Count - 1].Cells[1];
        }

        // Çalışan, pencereli programlar (çoklu seçim)
        List<string> PickRunning()
        {
            List<string> res = new List<string>();
            if (app == null) return res;
            using (Form f = new Form())
            {
                f.Text = L.T("rules.pickTitle"); f.StartPosition = FormStartPosition.CenterParent; f.ClientSize = new Size(600, 480);
                f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = false; f.MaximizeBox = false; f.ShowInTaskbar = false; f.Font = Theme.Body;
                ListView lv = new ListView(); lv.View = View.Details; lv.FullRowSelect = true; lv.MultiSelect = true;
                lv.SetBounds(16, 16, 568, 394);
                lv.Columns.Add(L.T("rules.colProgram"), 200); lv.Columns.Add(L.T("rules.colTitle"), 340);
                foreach (string[] a in app.Wm.ListApps()) lv.Items.Add(new ListViewItem(a));
                lv.DoubleClick += delegate { f.DialogResult = DialogResult.OK; };
                Button ok = new Button(); ok.Text = L.T("rules.add"); ok.Tag = "primary"; ok.SetBounds(364, 428, 110, 36); ok.DialogResult = DialogResult.OK;
                Button cancel = new Button(); cancel.Text = L.T("rules.cancel"); cancel.SetBounds(486, 428, 98, 36); cancel.DialogResult = DialogResult.Cancel;
                f.Controls.Add(lv); f.Controls.Add(ok); f.Controls.Add(cancel); f.AcceptButton = ok; f.CancelButton = cancel;
                Theme.Apply(f);
                if (f.ShowDialog(this) == DialogResult.OK)
                    foreach (ListViewItem it in lv.SelectedItems) res.Add(it.Text);
            }
            return res;
        }

        // ---------------------------------------------------------------- 3. NVIDIA Surround
        Panel BuildSurroundPage()
        {
            Panel p = NewPage(L.T("nav.surround"), L.T("sur.desc"));
            int y = 108;
            Card c1 = NewCard(p, y, 150);
            Lbl(c1, L.T("sur.status"), 20, 16, 0, Theme.H2, null);
            surStatus = Lbl(c1, "", 20, 48, CW - 40, null, "accent");
            Button on = Btn(c1, L.T("sur.on"), 20, 90, 190, true); on.Click += delegate { RunSurround(true); };
            Button off = Btn(c1, L.T("sur.off"), 222, 90, 190, false); off.Click += delegate { RunSurround(false); };
            Button rf = Btn(c1, L.T("sur.refresh"), 424, 90, 160, false); rf.Click += delegate { RefreshSurround(); };
            y += 150 + 16;

            Card c2 = NewCard(p, y, 290);
            Lbl(c2, L.T("sur.options"), 20, 16, 0, Theme.H2, null);
            Lbl(c2, L.T("sur.hotkey"), 20, 58, CW - 280, null, null);
            HotBox(c2, Array.IndexOf(Config.Actions, "surround"), CW - 240, 54, 220, cfg.Hot["surround"]);
            surConfirm = new Switch(); surConfirm.Text = L.T("sur.confirm"); surConfirm.Checked = cfg.SurroundConfirm; surConfirm.SetBounds(20, 98, CW - 40, 30); c2.Controls.Add(surConfirm);
            Lbl(c2, L.T("sur.primary"), 20, 148, CW - 130, null, null);
            surPrimary = new NumericUpDown(); surPrimary.Minimum = 1; surPrimary.Maximum = 8; surPrimary.SetBounds(CW - 100, 144, 80, 28); surPrimary.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            surPrimary.Value = Math.Max(1, Math.Min(8, cfg.SurroundPrimary)); c2.Controls.Add(surPrimary);
            Button sp = Btn(c2, L.T("sur.saveProfile"), 20, 194, 330, false);
            sp.Click += delegate { string err = Surround.SaveProfile(); surMsg.Text = err == null ? L.T("sur.profileSaved") : err; };
            surMsg = Lbl(c2, "", 20, 238, CW - 40, Theme.Small, "dim");
            y += 290 + 16;

            Label note = Lbl(p, L.T("sur.note"), PAD + 2, y, CW - 4, Theme.Small, "dim"); note.Height = 80;
            p.AutoScrollMinSize = new Size(0, y + 100);
            return p;
        }

        public void RefreshSurround() { surStatus.Text = Surround.StatusText(); }
        void RunSurround(bool on) { if (app != null) app.SurroundSet(on); RefreshSurround(); }

        // ---------------------------------------------------------------- 4. Genel
        Panel BuildGeneralPage()
        {
            Panel p = NewPage(L.T("nav.general"), L.T("gen.desc"));
            int y = 108;
            Card c1 = NewCard(p, y, 200);
            Lbl(c1, L.T("gen.appearance"), 20, 16, 0, Theme.H2, null);
            Lbl(c1, L.T("gen.language"), 20, 62, 200, null, null);
            langBox = new ComboBox(); langBox.DropDownStyle = ComboBoxStyle.DropDownList; langBox.SetBounds(CW - 300, 58, 280, 28); langBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            langBox.Items.Add(L.T("gen.langAuto")); foreach (string n in L.Names) langBox.Items.Add(n);
            langBox.SelectedIndex = cfg.Lang == "auto" ? 0 : Math.Max(0, Array.IndexOf(L.Codes, cfg.Lang) + 1);
            c1.Controls.Add(langBox);
            Lbl(c1, L.T("gen.theme"), 20, 108, 200, null, null);
            themeBox = new ComboBox(); themeBox.DropDownStyle = ComboBoxStyle.DropDownList; themeBox.SetBounds(CW - 300, 104, 280, 28); themeBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            themeBox.Items.Add(L.T("theme.system")); themeBox.Items.Add(L.T("theme.light")); themeBox.Items.Add(L.T("theme.dark"));
            themeBox.SelectedIndex = cfg.Theme == "light" ? 1 : (cfg.Theme == "dark" ? 2 : 0);
            themeBox.SelectedIndexChanged += delegate   // canlı önizleme
            {
                Theme.Mode = ThemeCode(); Theme.Reload(); ThemeChanged();
            };
            c1.Controls.Add(themeBox);
            Label ah = Lbl(c1, L.T("gen.accent"), 20, 148, CW - 40, Theme.Small, "dim"); ah.Height = 40;
            y += 200 + 16;

            Card c2 = NewCard(p, y, 130);
            Lbl(c2, L.T("gen.start"), 20, 16, 0, Theme.H2, null);
            startupSw = new Switch(); startupSw.Text = L.T("gen.startup"); startupSw.Checked = App.GetStartup(); startupSw.SetBounds(20, 52, CW - 40, 30); c2.Controls.Add(startupSw);
            Lbl(c2, L.T("gen.startupHint"), 20 + 54, 86, CW - 100, Theme.Small, "dim");
            y += 130 + 16;

            Card c3 = NewCard(p, y, 110);
            Lbl(c3, L.T("gen.about"), 20, 16, 0, Theme.H2, null);
            Lbl(c3, "Resize Badger  ·  " + L.F("gen.version", Ver.V), 20, 52, CW - 40, null, null);
            Lbl(c3, Ver.Repo, 20, 76, CW - 40, Theme.Small, "dim");
            y += 110 + PAD;
            p.AutoScrollMinSize = new Size(0, y);
            return p;
        }

        string ThemeCode() { return themeBox.SelectedIndex == 1 ? "light" : (themeBox.SelectedIndex == 2 ? "dark" : "system"); }

        // ---------------------------------------------------------------- kaydet
        void SaveClick(object sender, EventArgs e)
        {
            if (app == null) return;
            grid.EndEdit(); ruleGrid.EndEdit();
            List<Preset> list = new List<Preset>();
            foreach (DataGridViewRow r in grid.Rows)
            {
                Preset p = new Preset();
                p.Name = (r.Cells[0].Value ?? L.T("preset.new")).ToString();
                p.W = Math.Max(100, Num(r.Cells[1].Value, 1920)); p.H = Math.Max(100, Num(r.Cells[2].Value, 1080));
                p.X = Num(r.Cells[3].Value, 0); p.Y = Num(r.Cells[4].Value, 0);
                p.Center = Chk(r.Cells[5].Value); p.Borderless = Chk(r.Cells[6].Value); p.Inner = Chk(r.Cells[7].Value);
                p.Hotkey = (r.Cells[8].Value ?? "").ToString();
                list.Add(p);
            }
            cfg.Presets = list;

            List<Rule> rules = new List<Rule>();
            foreach (DataGridViewRow r in ruleGrid.Rows)
            {
                string proc = (r.Cells[1].Value ?? "").ToString().Trim();
                if (proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) proc = proc.Substring(0, proc.Length - 4);
                if (proc.Length == 0) continue;
                Rule ru = new Rule(); ru.Proc = proc; ru.Enabled = Chk(r.Cells[0].Value);
                string pv = (r.Cells[2].Value ?? "").ToString();
                ru.Preset = (pv == L.T("preset.maximize")) ? "" : pv;
                rules.Add(ru);
            }
            cfg.Rules = rules;

            for (int i = 0; i < hotBoxes.Length; i++) if (hotBoxes[i] != null) cfg.Hot[Config.Actions[i]] = hotBoxes[i].Text;
            cfg.AutoPreset = autoBox.SelectedIndex - 1;
            cfg.Exclude = excl.Text;
            cfg.Auto = autoSw.Checked;
            cfg.SurroundConfirm = surConfirm.Checked;
            cfg.SurroundPrimary = (int)surPrimary.Value;
            cfg.Lang = langBox.SelectedIndex == 0 ? "auto" : L.Codes[langBox.SelectedIndex - 1];
            cfg.Theme = ThemeCode();
            App.SetStartup(startupSw.Checked);
            cfg.Save();
            saved = true;
            app.SettingsSaved();
            Close();
        }
    }
}
