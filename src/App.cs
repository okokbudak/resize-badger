using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ResizeBadger
{
    // ------------------------------------------------------------------ Mesaj penceresi (kısayollar)
    class HotWindow : NativeWindow
    {
        public event Action<int> Pressed;
        public HotWindow() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && Pressed != null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    // ------------------------------------------------------------------ Uygulama
    class AppContext : ApplicationContext
    {
        public Config Cfg = new Config();
        public WindowManager Wm;
        public Icon TrayIcon;
        NotifyIcon tray; HotWindow hw; SettingsForm form;
        ToolStripMenuItem autoItem, startupItem;
        int hotCount;
        readonly SynchronizationContext ui = SynchronizationContext.Current;

        public AppContext()
        {
            L.Set(Cfg.Lang);
            Theme.Mode = Cfg.Theme; Theme.Reload();
            Wm = new WindowManager(Cfg);
            Surround.PrimaryAfterOff = Cfg.SurroundPrimary;
            TrayIcon = LoadIcon();
            hw = new HotWindow(); hw.Pressed += OnHotkey;

            tray = new NotifyIcon(); tray.Icon = TrayIcon; tray.Text = "Resize Badger"; tray.Visible = true;
            Wm.Notify += delegate(string msg) { tray.ShowBalloonTip(4000, "Resize Badger", msg, ToolTipIcon.Warning); };
            tray.DoubleClick += delegate { ShowSettings(0); };
            SystemEvents.UserPreferenceChanged += OnSystemTheme;
            Rebuild();
        }

        // Windows teması / vurgu rengi değişince (tema "Windows teması" ise) uygulamayı da güncelle
        void OnSystemTheme(object s, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color && e.Category != UserPreferenceCategory.VisualStyle) return;
            ui.Post(delegate
            {
                if (Theme.Mode != "system") return;
                Theme.Reload(); Rebuild();
                if (form != null && !form.IsDisposed) form.ThemeChanged();
            }, null);
        }

        public void ShowSettings(int page)
        {
            if (form == null || form.IsDisposed)
            {
                form = new SettingsForm(Cfg, this);
                form.FormClosed += delegate { form = null; };
            }
            form.ShowPage(page);
        }

        // Ayarlar kaydedilince: dil, tema, menü, kısayollar, kurallar
        public void SettingsSaved()
        {
            L.Set(Cfg.Lang);
            Theme.Mode = Cfg.Theme; Theme.Reload();
            Surround.PrimaryAfterOff = Cfg.SurroundPrimary;
            Wm.ReloadExclude();
            Rebuild();
            Wm.ApplyRulesToExisting();
            Info(L.T("msg.saved"));
        }

        // Menü + kısayolları ayarlardan yeniden kur
        public void Rebuild()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            for (int i = 0; i < Cfg.Presets.Count; i++)
            {
                Preset p = Cfg.Presets[i];
                string hk = string.IsNullOrEmpty(p.Hotkey) ? "" : "    [" + p.Hotkey + "]";
                ToolStripMenuItem it = new ToolStripMenuItem(p.Label + hk);
                it.Tag = p; it.Click += delegate(object s, EventArgs e) { Wm.Apply(Wm.Target(true), (Preset)((ToolStripMenuItem)s).Tag); };
                m.Items.Add(it);
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(L.T("menu.maximize"), null, delegate { Wm.Maximize(Wm.Target(true)); });
            m.Items.Add(L.T("menu.restore"), null, delegate { Wm.Restore(Wm.Target(true)); });
            m.Items.Add(L.T("menu.applyAll"), null, delegate { Wm.ApplyAll(); });
            m.Items.Add(L.T("menu.restoreAll"), null, delegate { Wm.RestoreAll(); });
            autoItem = new ToolStripMenuItem(L.T("menu.auto"), null, delegate { ToggleAuto(); });
            autoItem.Checked = Cfg.Auto;
            m.Items.Add(autoItem);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(L.T("act.surround"), null, delegate { SurroundToggle(); });
            startupItem = new ToolStripMenuItem(L.T("menu.startup"), null, delegate { App.SetStartup(!App.GetStartup()); startupItem.Checked = App.GetStartup(); });
            startupItem.Checked = App.GetStartup();
            m.Items.Add(startupItem);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(L.T("menu.settings"), null, delegate { ShowSettings(0); });
            m.Items.Add(L.T("menu.exit"), null, delegate { Exit(); });
            Theme.Menu(m);
            ContextMenuStrip old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = m;
            if (old != null) old.Dispose();
            RegisterHotkeys();
        }

        void ToggleAuto()
        {
            Cfg.Auto = !Cfg.Auto; Cfg.Save(); autoItem.Checked = Cfg.Auto;
            tray.ShowBalloonTip(1200, "Resize Badger", L.T(Cfg.Auto ? "msg.autoOn" : "msg.autoOff"), ToolTipIcon.None);
        }

        public void Info(string msg) { tray.ShowBalloonTip(1500, "Resize Badger", msg, ToolTipIcon.None); }

        // ---- NVIDIA Surround
        public void SurroundToggle() { SurroundSet(!Surround.IsOn()); }

        public void SurroundSet(bool on)
        {
            string err;
            Cursor old = Cursor.Current; Cursor.Current = Cursors.WaitCursor;
            try { err = Surround.Set(on); } finally { Cursor.Current = old; }
            if (err != null)
            {
                tray.ShowBalloonTip(4000, "NVIDIA Surround", err, ToolTipIcon.Warning);
                return;
            }
            if (Cfg.SurroundConfirm && !ConfirmSurround(on))
            {
                string back = Surround.Set(!on);
                tray.ShowBalloonTip(3000, "NVIDIA Surround", back == null ? L.T("msg.sur.reverted") : L.F("msg.sur.revertFail", back), ToolTipIcon.Info);
                return;
            }
            tray.ShowBalloonTip(1500, "NVIDIA Surround", L.T(on ? "msg.sur.on" : "msg.sur.off"), ToolTipIcon.None);
        }

        // 15 sn içinde "Tut" denmezse false (=geri al). Siyah ekranda kilitlenmeyi önler.
        static bool ConfirmSurround(bool on)
        {
            using (Form f = new Form())
            {
                int left = 15;
                f.Text = "NVIDIA Surround"; f.TopMost = true; f.StartPosition = FormStartPosition.CenterScreen;
                f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = false; f.MaximizeBox = false; f.ShowInTaskbar = false;
                f.ClientSize = new Size(480, 150); f.Font = Theme.Body;
                Label l = new Label(); l.Location = new Point(20, 18); l.Size = new Size(440, 60); f.Controls.Add(l);
                Button keep = new Button(); keep.Text = L.T("dlg.keep"); keep.Tag = "primary"; keep.Location = new Point(20, 94); keep.Size = new Size(210, 38); keep.DialogResult = DialogResult.Yes; f.Controls.Add(keep);
                Button undo = new Button(); undo.Text = L.T("dlg.undo"); undo.Location = new Point(250, 94); undo.Size = new Size(210, 38); undo.DialogResult = DialogResult.No; f.Controls.Add(undo);
                f.AcceptButton = keep;
                Theme.Apply(f);
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 1000;
                Action upd = delegate { l.Text = L.F("dlg.sur.msg", L.T(on ? "msg.sur.on" : "msg.sur.off"), left); };
                upd();
                t.Tick += delegate { left--; if (left <= 0) { t.Stop(); f.DialogResult = DialogResult.No; } else upd(); };
                t.Start();
                DialogResult r = f.ShowDialog();
                t.Stop(); t.Dispose();
                return r == DialogResult.Yes;
            }
        }

        void RegisterHotkeys()
        {
            for (int i = 1; i <= hotCount; i++) N.UnregisterHotKey(hw.Handle, i);
            List<string> failed = new List<string>();
            int id = 0;
            for (int i = 0; i < Config.Actions.Length; i++) Reg(++id, Cfg.Hot[Config.Actions[i]], failed);
            for (int i = 0; i < Cfg.Presets.Count; i++) Reg(++id, Cfg.Presets[i].Hotkey, failed);
            hotCount = id;
            if (failed.Count > 0)
                tray.ShowBalloonTip(3000, "Resize Badger", L.F("msg.hotkeyTaken", string.Join(", ", failed.ToArray())), ToolTipIcon.Warning);
        }

        void Reg(int id, string hotkey, List<string> failed)
        {
            uint mod, vk;
            if (!Config.ParseHotkey(hotkey, out mod, out vk)) return;
            if (!N.RegisterHotKey(hw.Handle, id, mod, vk)) failed.Add(hotkey);
        }

        void OnHotkey(int id)
        {
            IntPtr h = Wm.Target(false);
            if (id <= Config.Actions.Length)
            {
                switch (Config.Actions[id - 1])
                {
                    case "fill": Wm.Maximize(h); break;
                    case "restore": Wm.Restore(h); break;
                    case "all": Wm.ApplyAll(); break;
                    case "auto": ToggleAuto(); break;
                    case "surround": SurroundToggle(); break;
                }
            }
            else
            {
                int pi = id - Config.Actions.Length - 1;
                if (pi < Cfg.Presets.Count) Wm.Apply(h, Cfg.Presets[pi]);
            }
        }

        // exe'ye gömülü simge; yoksa basit bir yedek çizilir
        public static Icon LoadIcon()
        {
            try { Icon ic = Icon.ExtractAssociatedIcon(Application.ExecutablePath); if (ic != null) return ic; } catch { }
            Bitmap b = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(Color.FromArgb(0, 168, 107)), 1, 1, 30, 30);
                g.DrawRectangle(new Pen(Color.White, 2), 8, 9, 16, 14);
            }
            IntPtr hi = b.GetHicon(); Icon res = (Icon)Icon.FromHandle(hi).Clone(); N.DestroyIcon(hi); return res;
        }

        void Exit()
        {
            SystemEvents.UserPreferenceChanged -= OnSystemTheme;
            Wm.StopHooks();
            for (int i = 1; i <= hotCount; i++) N.UnregisterHotKey(hw.Handle, i);
            tray.Visible = false; tray.Dispose();
            ExitThread();
        }
    }

    static class App
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool GetStartup()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey)) { return k != null && k.GetValue("ResizeBadger") != null; }
        }
        public static void SetStartup(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue("ResizeBadger", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("ResizeBadger", false);
            }
        }

        static string Arg(string[] a, string name)
        {
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        // Geliştirme: --shot dosya.png [--page N] [--theme light|dark|system] [--lang tr|en|de|es|fr]
        // Ayar penceresinin görüntüsünü kaydedip çıkar (tepsiye / kısayola dokunmaz).
        static void Shot(string[] a)
        {
            Config c = new Config();
            string v;
            if ((v = Arg(a, "--theme")) != null) c.Theme = v;
            if ((v = Arg(a, "--lang")) != null) c.Lang = v;
            L.Set(c.Lang); Theme.Mode = c.Theme; Theme.Reload();
            int page = 0; if ((v = Arg(a, "--page")) != null) int.TryParse(v, out page);
            using (SettingsForm f = new SettingsForm(c, null))
            {
                f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, -4000);
                int then = -1, scroll = 0;
                if ((v = Arg(a, "--then")) != null) int.TryParse(v, out then);      // önce başka sayfaya geçip geri dön (hata yeniden üretimi)
                if ((v = Arg(a, "--scroll")) != null) int.TryParse(v, out scroll);  // sayfayı piksel kaydır
                f.ShowPage(page);
                for (int i = 0; i < 4; i++) { Application.DoEvents(); Thread.Sleep(60); }
                if (scroll > 0) { f.ScrollCurrent(scroll); for (int i = 0; i < 3; i++) { Application.DoEvents(); Thread.Sleep(60); } }
                if (then >= 0) { f.ShowPage(then); for (int i = 0; i < 3; i++) { Application.DoEvents(); Thread.Sleep(60); } f.ShowPage(page); }
                for (int i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(60); }
                using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(Arg(a, "--shot"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            N.SetProcessDpiAwarenessContext(new IntPtr(-4)); // per-monitor v2
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (Arg(args, "--shot") != null) { Shot(args); return; }
            bool created;
            using (Mutex mx = new Mutex(true, "ResizeBadger.SingleInstance", out created))
            {
                if (!created) return;
                Application.Run(new AppContext());
            }
        }
    }
}
