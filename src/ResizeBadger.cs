// Resize Badger - özel çözünürlüğe pencere boyutlandırıcı (adım 1) + NVIDIA Surround menüsü (adım 2, yakında)
// Derleme: build.ps1  (Windows'ta hazır gelen .NET Framework csc ile, ek kurulum gerekmez)
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
    // ------------------------------------------------------------------ Win32
    static class N
    {
        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const int WS_CAPTION = 0xC00000, WS_THICKFRAME = 0x40000;
        public const int WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        public const int SW_MAXIMIZE = 3, SW_RESTORE = 9;
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;
        public const uint MONITOR_NEAREST = 2;

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; public int W { get { return R - L; } } public int H { get { return B - T; } } }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct WINDOWPLACEMENT { public int length, flags, showCmd; public POINT ptMin, ptMax; public RECT rcNormal; }
        [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

        public delegate bool EnumProc(IntPtr h, IntPtr l);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObj, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int idx, int v);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT p);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc cb, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("advapi32.dll")] public static extern bool OpenProcessToken(IntPtr p, uint access, out IntPtr tok);
        [DllImport("advapi32.dll")] public static extern bool GetTokenInformation(IntPtr tok, int cls, out int info, int len, out int ret);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
    }

    // ------------------------------------------------------------------ Ayarlar
    class Preset
    {
        public string Name = "Yeni";
        public int W = 1920, H = 1080, X, Y;
        public bool Center = true, Borderless, Inner = true;
        public string Hotkey = "";
        public string Label { get { return Name + "  (" + W + "x" + H + ")"; } }

        public string Serialize()
        {
            return string.Join("|", new string[] { Name.Replace("|", "/"), W.ToString(), H.ToString(), X.ToString(), Y.ToString(),
                Center ? "1" : "0", Borderless ? "1" : "0", Inner ? "1" : "0", Hotkey });
        }
        public static Preset Parse(string s)
        {
            string[] p = s.Split('|');
            if (p.Length < 9) return null;
            Preset r = new Preset(); r.Name = p[0];
            int.TryParse(p[1], out r.W); int.TryParse(p[2], out r.H); int.TryParse(p[3], out r.X); int.TryParse(p[4], out r.Y);
            r.Center = p[5] == "1"; r.Borderless = p[6] == "1"; r.Inner = p[7] == "1"; r.Hotkey = p[8];
            return r.W > 0 && r.H > 0 ? r : null;
        }
        public static Preset Make(string n, int w, int h, int x, int y, bool center, bool bl, string hk)
        {
            Preset p = new Preset(); p.Name = n; p.W = w; p.H = h; p.X = x; p.Y = y; p.Center = center; p.Borderless = bl; p.Hotkey = hk; return p;
        }
    }

    // Program kuralı: bu programın penceresi açılınca ön ayar uygulanır
    class Rule
    {
        public string Proc = "";          // exe adı, uzantısız (örn. "notepad")
        public string Preset = "";        // ön ayar adı; boşsa tam ekran (maximize)
        public bool Enabled = true;
        public string Serialize() { return string.Join("|", new string[] { Proc.Replace("|", ""), Preset.Replace("|", "/"), Enabled ? "1" : "0" }); }
        public static Rule Parse(string s)
        {
            string[] p = s.Split('|');
            if (p.Length < 3 || p[0].Trim().Length == 0) return null;
            Rule r = new Rule(); r.Proc = p[0].Trim(); r.Preset = p[1]; r.Enabled = p[2] == "1"; return r;
        }
    }

    class Config
    {
        public List<Rule> Rules = new List<Rule>();
        public static readonly string[] Actions = { "fill", "restore", "all", "auto", "surround" };
        public static readonly string[] ActionNames = {
            "Aktif pencereyi tam ekran yap (maximize)", "Aktif pencereyi eski haline getir",
            "Tüm pencerelere otomatik ayarı uygula", "Otomatik uygulamayı aç/kapat", "NVIDIA Surround aç/kapat" };
        public bool SurroundConfirm = true;
        public int SurroundPrimary = 2;   // Surround kapanınca ana ekran olacak ekran (soldan sıra)

        public Dictionary<string, string> Hot = new Dictionary<string, string>();
        public List<Preset> Presets = new List<Preset>();
        public int AutoPreset = -1;   // -1 = maximize, >=0 = preset sırası
        public bool Auto;
        public string Exclude = "explorer;SearchHost;ShellExperienceHost;StartMenuExperienceHost;TextInputHost;LockApp;ResizeBadger";
        readonly string path;

        public Config()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            Hot["fill"] = "Ctrl+Alt+Up";
            Hot["restore"] = "Ctrl+Alt+Down";
            Hot["all"] = "Ctrl+Alt+A";
            Hot["auto"] = "Ctrl+Alt+Z";
            Hot["surround"] = "Ctrl+Alt+S";
            Load();
            if (Presets.Count == 0)
            {
                Presets.Add(Preset.Make("Full HD", 1920, 1080, 0, 0, true, false, "Ctrl+Alt+1"));
                Presets.Add(Preset.Make("2K", 2560, 1440, 0, 0, true, false, "Ctrl+Alt+2"));
                Presets.Add(Preset.Make("3 ekran birden (5760x1080)", 5760, 1080, SystemInformation.VirtualScreen.Left, SystemInformation.VirtualScreen.Top, false, true, "Ctrl+Alt+3"));
                Presets.Add(Preset.Make("720p", 1280, 720, 0, 0, true, false, "Ctrl+Alt+4"));
            }
        }

        void Load()
        {
            try
            {
                if (!File.Exists(path)) return;
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int i = raw.IndexOf('=');
                    if (i < 1) continue;
                    string k = raw.Substring(0, i).Trim(), v = raw.Substring(i + 1).Trim();
                    if (k == "autopreset") int.TryParse(v, out AutoPreset);
                    else if (k == "auto") Auto = v == "1";
                    else if (k == "exclude") Exclude = v;
                    else if (k == "sconfirm") SurroundConfirm = v == "1";
                    else if (k == "sprimary") int.TryParse(v, out SurroundPrimary);
                    else if (k == "rule") { Rule r = Rule.Parse(v); if (r != null) Rules.Add(r); }
                    else if (k == "preset") { Preset p = Preset.Parse(v); if (p != null) Presets.Add(p); }
                    else if (k.StartsWith("hk_")) Hot[k.Substring(3)] = v;
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                List<string> l = new List<string>();
                l.Add("autopreset=" + AutoPreset);
                l.Add("auto=" + (Auto ? "1" : "0"));
                l.Add("exclude=" + Exclude);
                l.Add("sconfirm=" + (SurroundConfirm ? "1" : "0"));
                l.Add("sprimary=" + SurroundPrimary);
                foreach (Rule r in Rules) l.Add("rule=" + r.Serialize());
                foreach (string a in Actions) l.Add("hk_" + a + "=" + Hot[a]);
                foreach (Preset p in Presets) l.Add("preset=" + p.Serialize());
                File.WriteAllLines(path, l.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        public static bool ParseHotkey(string s, out uint mod, out uint vk)
        {
            mod = 0x4000; vk = 0; // MOD_NOREPEAT
            if (string.IsNullOrEmpty(s)) return false;
            string[] parts = s.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (i < parts.Length - 1)
                {
                    if (p == "Ctrl") mod |= 2; else if (p == "Alt") mod |= 1;
                    else if (p == "Shift") mod |= 4; else if (p == "Win") mod |= 8;
                    else return false;
                }
                else
                {
                    Keys k;
                    if (p.Length == 1 && char.IsDigit(p[0])) p = "D" + p;   // "1" -> D1 (yoksa sayı olarak ayrışır)
                    if (p.Length > 0 && char.IsDigit(p[0])) return false;
                    if (!Enum.TryParse(p, out k)) return false;
                    vk = (uint)k;
                }
            }
            return vk != 0 && (mod & 0xF) != 0;
        }
    }

    // ------------------------------------------------------------------ Pencere yöneticisi
    class SavedState { public N.RECT Normal; public int Style; public bool WasZoomed, StyleChanged; }

    class WindowManager
    {
        readonly Config cfg;
        readonly Dictionary<IntPtr, SavedState> saved = new Dictionary<IntPtr, SavedState>();
        readonly HashSet<IntPtr> autoDone = new HashSet<IntPtr>();
        readonly HashSet<string> exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly uint myPid = (uint)Process.GetCurrentProcess().Id;
        N.WinEventProc cb;
        IntPtr hookFg, hookShow;
        public IntPtr LastWindow;   // tepsi menüsünden işlem yaparken hedef pencere

        public WindowManager(Config c) { cfg = c; ReloadExclude(); StartHooks(); }

        public void ReloadExclude()
        {
            exclude.Clear();
            foreach (string s in cfg.Exclude.Split(';')) { string t = s.Trim(); if (t.Length > 0) exclude.Add(t); }
        }

        // ---- olay kancaları: son aktif pencereyi hatırla + otomatik mod
        void StartHooks()
        {
            cb = OnEvent;
            hookFg = N.SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, cb, 0, 0, 0);   // FOREGROUND
            hookShow = N.SetWinEventHook(0x8002, 0x8002, IntPtr.Zero, cb, 0, 0, 0); // OBJECT_SHOW
        }
        public void StopHooks() { N.UnhookWinEvent(hookFg); N.UnhookWinEvent(hookShow); }

        void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObj, int idChild, uint thread, uint time)
        {
            if (hwnd == IntPtr.Zero || idObj != 0 || idChild != 0) return;
            if (ev == 0x0003 && IsManageable(hwnd)) LastWindow = hwnd;
            bool wantRule = cfg.Rules.Count > 0 && !ruleDone.Contains(hwnd);
            bool wantAuto = cfg.Auto && !autoDone.Contains(hwnd);
            if (!wantRule && !wantAuto) return;
            if (N.GetAncestor(hwnd, 2) != hwnd) return;   // yalnızca üst düzey pencereler
            After(180, delegate { Consider(hwnd); });     // pencere kendi boyutunu oturtsun
        }

        static void After(int ms, Action a)
        {
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = ms;
            t.Tick += delegate { t.Stop(); t.Dispose(); a(); };
            t.Start();
        }

        // ---- program kuralları
        readonly HashSet<IntPtr> ruleDone = new HashSet<IntPtr>();
        readonly Dictionary<uint, string> pidNames = new Dictionary<uint, string>();

        string ProcName(IntPtr h)
        {
            uint pid; N.GetWindowThreadProcessId(h, out pid);
            string n;
            if (pidNames.TryGetValue(pid, out n)) return n;
            try { n = Process.GetProcessById((int)pid).ProcessName; } catch { n = ""; }
            if (pidNames.Count > 300) pidNames.Clear();
            pidNames[pid] = n;
            return n;
        }

        Rule FindRule(IntPtr h)
        {
            if (cfg.Rules.Count == 0) return null;
            string n = ProcName(h);
            foreach (Rule r in cfg.Rules)
                if (r.Enabled && string.Equals(r.Proc, n, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        // Kural pencereleri için gevşek ama diyalog/araç penceresi dışlayan kontrol
        bool IsRuleCandidate(IntPtr h)
        {
            if (!IsManageable(h) || N.GetWindow(h, 4) != IntPtr.Zero) return false;
            if ((N.GetWindowLong(h, N.GWL_EXSTYLE) & (N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE)) != 0) return false;
            N.RECT r; N.GetWindowRect(h, out r);
            return r.W >= 200 && r.H >= 150;
        }

        void ApplyRule(IntPtr h, Rule r)
        {
            Preset p = null;
            foreach (Preset x in cfg.Presets) if (x.Name.Replace("|", "/") == r.Preset) { p = x; break; }
            if (p != null) Apply(h, p);
            else if (r.Preset.Length == 0) Maximize(h);
            else if (Notify != null) Notify("'" + r.Proc + "' kuralındaki ön ayar bulunamadı: " + r.Preset);
        }

        // Kural bu pencereye uygulandıysa true döner
        bool TryRule(IntPtr h)
        {
            Rule r = FindRule(h);
            if (r == null) return false;
            if (ruleDone.Contains(h) || !IsRuleCandidate(h)) return true;   // kurala ait ama şimdilik dokunma
            if (ruleDone.Count > 500) ruleDone.RemoveWhere(delegate(IntPtr x) { return !N.IsWindow(x); });
            ruleDone.Add(h);
            ApplyRule(h, r);
            After(1500, delegate { if (N.IsWindow(h)) ApplyRule(h, r); });   // oyunlar açılışta kendi boyutuna döner
            return true;
        }

        // Kuralları kaydettikten sonra şu an açık olan pencerelere de uygula
        public void ApplyRulesToExisting()
        {
            if (cfg.Rules.Count == 0) return;
            List<IntPtr> list = new List<IntPtr>();
            N.EnumWindows(delegate(IntPtr h, IntPtr l) { if (FindRule(h) != null) list.Add(h); return true; }, IntPtr.Zero);
            foreach (IntPtr h in list) { ruleDone.Remove(h); TryRule(h); }
        }

        // Çalışan, pencereli programlar (program seçici için)
        public List<string[]> ListApps()
        {
            Dictionary<string, string> seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            N.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (IsManageable(h) && N.GetWindow(h, 4) == IntPtr.Zero)
                {
                    string n = ProcName(h);
                    if (n.Length > 0 && !seen.ContainsKey(n)) seen[n] = WindowTitle(h);
                }
                return true;
            }, IntPtr.Zero);
            List<string[]> res = new List<string[]>();
            foreach (KeyValuePair<string, string> kv in seen) res.Add(new string[] { kv.Key, kv.Value });
            res.Sort(delegate(string[] a, string[] b) { return string.Compare(a[0], b[0], StringComparison.OrdinalIgnoreCase); });
            return res;
        }

        static string WindowTitle(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(256); N.GetWindowText(h, sb, 256); return sb.ToString();
        }

        void Consider(IntPtr h)
        {
            if (TryRule(h)) return;     // programa özel kural, genel otomatik moddan önce gelir
            if (!cfg.Auto || autoDone.Contains(h) || !IsCandidate(h)) return;
            N.RECT r; N.GetWindowRect(h, out r);
            if (r.W < 300 || r.H < 200) return; // küçük yardımcı pencereler
            if (autoDone.Count > 500) autoDone.RemoveWhere(delegate(IntPtr x) { return !N.IsWindow(x); });
            autoDone.Add(h);
            ApplyAuto(h);
        }

        // ---- pencere uygunluğu
        // Elle (kısayol/menü) işlem için gevşek kontrol
        public bool IsManageable(IntPtr h)
        {
            if (h == IntPtr.Zero || !N.IsWindow(h) || !N.IsWindowVisible(h) || N.IsIconic(h)) return false;
            if (N.GetWindowTextLength(h) == 0) return false;
            int cloaked; N.DwmGetWindowAttribute(h, 14, out cloaked, 4);
            if (cloaked != 0) return false;
            StringBuilder cn = new StringBuilder(64); N.GetClassName(h, cn, 64);
            string c = cn.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return false;
            uint pid; N.GetWindowThreadProcessId(h, out pid);
            return pid != myPid;
        }

        // Otomatik/toplu işlem için sıkı kontrol
        public bool IsCandidate(IntPtr h)
        {
            if (!IsManageable(h)) return false;
            int st = N.GetWindowLong(h, N.GWL_STYLE), ex = N.GetWindowLong(h, N.GWL_EXSTYLE);
            if ((st & N.WS_CAPTION) != N.WS_CAPTION || (st & N.WS_THICKFRAME) == 0) return false;
            if ((ex & (N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE)) != 0) return false;
            if (N.GetWindow(h, 4) != IntPtr.Zero) return false;           // sahibi olan (diyalog) pencere
            uint pid; N.GetWindowThreadProcessId(h, out pid);
            try { if (exclude.Contains(Process.GetProcessById((int)pid).ProcessName)) return false; } catch { }
            return true;
        }

        N.MONITORINFO Monitor(IntPtr h)
        {
            N.MONITORINFO mi = new N.MONITORINFO(); mi.cbSize = Marshal.SizeOf(typeof(N.MONITORINFO));
            N.GetMonitorInfo(N.MonitorFromWindow(h, N.MONITOR_NEAREST), ref mi);
            return mi;
        }

        void Save(IntPtr h)
        {
            if (saved.ContainsKey(h)) return;
            N.WINDOWPLACEMENT p = new N.WINDOWPLACEMENT(); p.length = Marshal.SizeOf(typeof(N.WINDOWPLACEMENT));
            N.GetWindowPlacement(h, ref p);
            SavedState s = new SavedState();
            s.Normal = p.rcNormal; s.Style = N.GetWindowLong(h, N.GWL_STYLE); s.WasZoomed = N.IsZoomed(h);
            saved[h] = s;
        }

        // ---- eylemler
        public void ApplyAuto(IntPtr h)
        {
            if (cfg.AutoPreset >= 0 && cfg.AutoPreset < cfg.Presets.Count) Apply(h, cfg.Presets[cfg.AutoPreset]);
            else Maximize(h);
        }

        public event Action<string> Notify;
        readonly bool selfElevated = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

        // Yönetici olarak çalışan pencereye normal yetkiyle dokunulamaz (UIPI); sessizce başarısız olmasın
        bool BlockedByElevation(IntPtr h)
        {
            if (selfElevated) return false;
            uint pid; N.GetWindowThreadProcessId(h, out pid);
            IntPtr proc = N.OpenProcess(0x1000, false, pid), tok = IntPtr.Zero;
            if (proc == IntPtr.Zero) return false;
            try
            {
                int el, ret;
                if (N.OpenProcessToken(proc, 8, out tok) && N.GetTokenInformation(tok, 20, out el, 4, out ret) && el != 0)
                {
                    if (Notify != null) Notify("Bu pencere yönetici olarak çalışıyor. Resize Badger'ı da 'Yönetici olarak çalıştır' ile açın.");
                    return true;
                }
            }
            finally { if (tok != IntPtr.Zero) N.CloseHandle(tok); N.CloseHandle(proc); }
            return false;
        }

        public void Maximize(IntPtr h)
        {
            if (!IsManageable(h) || BlockedByElevation(h)) return;
            Save(h);
            N.ShowWindow(h, N.SW_MAXIMIZE);
        }

        // Pencereyi preset'teki çözünürlüğe getirir (ekrandan büyük olabilir)
        public void Apply(IntPtr h, Preset p)
        {
            if (!IsManageable(h) || BlockedByElevation(h)) return;
            Save(h);
            SavedState s = saved[h];
            if (N.IsZoomed(h)) N.ShowWindow(h, N.SW_RESTORE);

            if (p.Borderless)
            {
                N.SetWindowLong(h, N.GWL_STYLE, s.Style & ~(N.WS_CAPTION | N.WS_THICKFRAME));
                N.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, N.SWP_NOSIZE | N.SWP_NOMOVE | N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_FRAMECHANGED);
                s.StyleChanged = true;
            }
            else if (s.StyleChanged)
            {
                N.SetWindowLong(h, N.GWL_STYLE, s.Style);
                N.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, N.SWP_NOSIZE | N.SWP_NOMOVE | N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_FRAMECHANGED);
                s.StyleChanged = false;
            }

            N.RECT w, c, f;
            N.GetWindowRect(h, out w); N.GetClientRect(h, out c);
            int ncW = (p.Inner && !p.Borderless) ? w.W - c.W : 0;
            int ncH = (p.Inner && !p.Borderless) ? w.H - c.H : 0;
            int winW = p.W + ncW, winH = p.H + ncH;

            int lS = 0, tS = 0, rS = 0, bS = 0;     // Win10/11 görünmez gölge payı
            if (!p.Borderless && N.DwmGetWindowAttribute(h, 9, out f, Marshal.SizeOf(typeof(N.RECT))) == 0)
            { lS = f.L - w.L; tS = f.T - w.T; rS = w.R - f.R; bS = w.B - f.B; }

            int x = p.X, y = p.Y;
            if (p.Center)
            {
                N.MONITORINFO mi = Monitor(h);
                N.RECT area = p.Borderless ? mi.rcMonitor : mi.rcWork;
                x = area.L + (area.W - (winW - lS - rS)) / 2;
                y = area.T + (area.H - (winH - tS - bS)) / 2;
            }
            N.SetWindowPos(h, IntPtr.Zero, x - lS, y - tS, winW, winH, N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_FRAMECHANGED);
        }

        public void Restore(IntPtr h)
        {
            if (!N.IsWindow(h)) return;
            SavedState s;
            if (!saved.TryGetValue(h, out s)) { if (N.IsZoomed(h)) N.ShowWindow(h, N.SW_RESTORE); return; }
            saved.Remove(h);
            if (s.StyleChanged) N.SetWindowLong(h, N.GWL_STYLE, s.Style);
            if (N.IsZoomed(h)) N.ShowWindow(h, N.SW_RESTORE);
            N.SetWindowPos(h, IntPtr.Zero, s.Normal.L, s.Normal.T, s.Normal.W, s.Normal.H,
                N.SWP_NOZORDER | N.SWP_NOACTIVATE | N.SWP_FRAMECHANGED);
            if (s.WasZoomed) N.ShowWindow(h, N.SW_MAXIMIZE);
        }

        public void ApplyAll()
        {
            List<IntPtr> list = new List<IntPtr>();
            N.EnumWindows(delegate(IntPtr h, IntPtr l) { if (IsCandidate(h)) list.Add(h); return true; }, IntPtr.Zero);
            foreach (IntPtr h in list) { autoDone.Add(h); ApplyAuto(h); }
        }

        public void RestoreAll()
        {
            foreach (IntPtr h in new List<IntPtr>(saved.Keys)) Restore(h);
        }

        // Hedef: kısayolda aktif pencere, menüde son aktif pencere
        public IntPtr Target(bool fromMenu) { return fromMenu ? LastWindow : N.GetForegroundWindow(); }
    }

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

    // ------------------------------------------------------------------ Ayar penceresi
    class SettingsForm : Form
    {
        readonly Config cfg; readonly AppContext app;
        readonly TextBox[] boxes = new TextBox[Config.Actions.Length];
        DataGridView grid; ComboBox autoBox; CheckBox auto, startup; TextBox excl; TabControl tabs;
        const int HotCol = 8;

        public SettingsForm(Config c, AppContext a)
        {
            cfg = c; app = a;
            Text = "Resize Badger"; Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(820, 700);
            Icon = a.TrayIcon;

            tabs = new TabControl(); tabs.Dock = DockStyle.Fill;
            TabPage t1 = new TabPage("1 · Pencere Boyutlandırıcı"), t2 = new TabPage("2 · NVIDIA Surround"), t3 = new TabPage("3 · Program Kuralları");
            tabs.TabPages.Add(t1); tabs.TabPages.Add(t2); tabs.TabPages.Add(t3); Controls.Add(tabs);

            int y = 12;
            Label gl = new Label(); gl.AutoSize = true; gl.Location = new Point(12, y); gl.Font = new Font(Font, FontStyle.Bold);
            gl.Text = "Çözünürlük ön ayarları — kısayol hücresine tıklayıp tuşlara basın"; t1.Controls.Add(gl); y += 26;

            grid = new DataGridView();
            grid.Location = new Point(12, y); grid.Size = new Size(786, 230);
            grid.AllowUserToAddRows = false; grid.AllowUserToResizeRows = false; grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.BackgroundColor = SystemColors.Window;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            AddText("Ad", 150); AddText("Genişlik", 70); AddText("Yükseklik", 70); AddText("X", 55); AddText("Y", 55);
            AddCheck("Ortala", 55); AddCheck("Kenarlıksız", 80); AddCheck("İç alan", 60); AddText("Kısayol", 140);
            grid.EditingControlShowing += GridEditing;
            foreach (Preset p in cfg.Presets) AddRow(p);
            t1.Controls.Add(grid); y += 238;

            Button add = new Button(); add.Text = "+ Ekle"; add.Location = new Point(12, y); add.Width = 90;
            add.Click += delegate { AddRow(new Preset()); };
            Button del = new Button(); del.Text = "Sil"; del.Location = new Point(110, y); del.Width = 90;
            del.Click += delegate { foreach (DataGridViewRow r in new List<DataGridViewRow>(SelRows())) grid.Rows.Remove(r); };
            Label hint = new Label(); hint.AutoSize = true; hint.Location = new Point(215, y + 4); hint.ForeColor = SystemColors.GrayText;
            hint.Text = "Ortala işaretliyse X/Y yok sayılır. İç alan: ölçü pencere kenarlığı hariç. Kenarlıksız: başlık çubuğu kalkar.";
            t1.Controls.Add(add); t1.Controls.Add(del); t1.Controls.Add(hint); y += 44;

            Label hl = new Label(); hl.Text = "Genel kısayollar"; hl.AutoSize = true; hl.Location = new Point(12, y); hl.Font = new Font(Font, FontStyle.Bold); t1.Controls.Add(hl); y += 26;
            for (int i = 0; i < Config.Actions.Length; i++)
            {
                if (Config.Actions[i] == "surround") continue;   // 2. sekmede
                Label l = new Label(); l.Text = Config.ActionNames[i]; l.Location = new Point(12, y + 3); l.Width = 340; t1.Controls.Add(l);
                TextBox b = new TextBox(); b.ReadOnly = true; b.Location = new Point(360, y); b.Width = 180; b.Text = cfg.Hot[Config.Actions[i]];
                b.KeyDown += delegate(object s, KeyEventArgs e) { string v = KeyText(e); if (v != null) ((TextBox)s).Text = v; };
                t1.Controls.Add(b); boxes[i] = b; y += 30;
            }
            y += 6;
            Label al = new Label(); al.Text = "Otomatik / toplu modda uygulanacak ayar"; al.Location = new Point(12, y + 3); al.Width = 340; t1.Controls.Add(al);
            autoBox = new ComboBox(); autoBox.DropDownStyle = ComboBoxStyle.DropDownList; autoBox.Location = new Point(360, y); autoBox.Width = 240;
            t1.Controls.Add(autoBox); y += 30;
            auto = new CheckBox(); auto.Text = "Yeni açılan her pencereye otomatik uygula"; auto.Checked = cfg.Auto; auto.AutoSize = true; auto.Location = new Point(12, y); t1.Controls.Add(auto);
            startup = new CheckBox(); startup.Text = "Windows ile birlikte başlat"; startup.Checked = App.GetStartup(); startup.AutoSize = true; startup.Location = new Point(360, y); t1.Controls.Add(startup); y += 30;
            Label el = new Label(); el.Text = "Otomatik modda dokunulmayacak programlar (; ile ayırın)"; el.Location = new Point(12, y + 3); el.Width = 340; t1.Controls.Add(el);
            excl = new TextBox(); excl.Location = new Point(360, y); excl.Width = 438; excl.Text = cfg.Exclude; t1.Controls.Add(excl); y += 38;

            // Kaydet/Kapat her iki sekmede de görünsün diye pencerenin altında
            Panel bar = new Panel(); bar.Dock = DockStyle.Bottom; bar.Height = 52;
            Button ok = new Button(); ok.Text = "Kaydet"; ok.Location = new Point(608, 10); ok.Size = new Size(90, 32); ok.Click += SaveClick; bar.Controls.Add(ok);
            Button cancel = new Button(); cancel.Text = "Kapat"; cancel.Location = new Point(708, 10); cancel.Size = new Size(90, 32); cancel.Click += delegate { Hide(); }; bar.Controls.Add(cancel);
            Label bh = new Label(); bh.Text = "Kısayol ve ayar değişiklikleri 'Kaydet' ile geçerli olur."; bh.AutoSize = true; bh.Location = new Point(14, 18); bh.ForeColor = SystemColors.GrayText; bar.Controls.Add(bh);
            Controls.Add(bar);

            BuildSurroundTab(t2);
            BuildRulesTab(t3);
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedIndex == 2) RefreshRuleCombos(); };

            FillAutoBox();
            FormClosing += delegate(object o, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
        }

        Label surStatus; CheckBox surConfirm; Label surMsg; NumericUpDown surPrimary;

        void BuildSurroundTab(TabPage t)
        {
            int y = 16;
            Label hd = new Label(); hd.Text = "NVIDIA Surround — NVIDIA uygulamasını beklemeden"; hd.AutoSize = true; hd.Location = new Point(14, y);
            hd.Font = new Font(Font, FontStyle.Bold); t.Controls.Add(hd); y += 34;
            surStatus = new Label(); surStatus.Location = new Point(14, y); surStatus.Size = new Size(780, 24); t.Controls.Add(surStatus); y += 40;

            Button on = new Button(); on.Text = "Surround'u AÇ"; on.Location = new Point(14, y); on.Size = new Size(180, 34);
            on.Click += delegate { RunSurround(true); };
            Button off = new Button(); off.Text = "Surround'u KAPAT"; off.Location = new Point(204, y); off.Size = new Size(180, 34);
            off.Click += delegate { RunSurround(false); };
            Button refresh = new Button(); refresh.Text = "Durumu yenile"; refresh.Location = new Point(394, y); refresh.Size = new Size(150, 34);
            refresh.Click += delegate { RefreshSurround(); };
            t.Controls.Add(on); t.Controls.Add(off); t.Controls.Add(refresh); y += 54;

            Label kl = new Label(); kl.Text = "Aç/kapat kısayolu (kutuya tıklayıp tuşlara basın)"; kl.Location = new Point(14, y + 3); kl.Width = 340; t.Controls.Add(kl);
            TextBox kb = new TextBox(); kb.ReadOnly = true; kb.Location = new Point(360, y); kb.Width = 180; kb.Text = cfg.Hot["surround"];
            kb.KeyDown += delegate(object s, KeyEventArgs e) { string v = KeyText(e); if (v != null) ((TextBox)s).Text = v; };
            t.Controls.Add(kb); boxes[Array.IndexOf(Config.Actions, "surround")] = kb; y += 34;

            surConfirm = new CheckBox(); surConfirm.AutoSize = true; surConfirm.Location = new Point(14, y); surConfirm.Checked = cfg.SurroundConfirm;
            surConfirm.Text = "Değişiklikten sonra onay iste (15 sn içinde onaylamazsam otomatik geri al — siyah ekranda kilitlenmemek için önerilir)"; t.Controls.Add(surConfirm); y += 34;

            Label pl = new Label(); pl.Text = "Surround kapanınca ana ekran (soldan kaçıncı ekran)"; pl.Location = new Point(14, y + 3); pl.Width = 340; t.Controls.Add(pl);
            surPrimary = new NumericUpDown(); surPrimary.Minimum = 1; surPrimary.Maximum = 8; surPrimary.Location = new Point(360, y); surPrimary.Width = 60;
            surPrimary.Value = Math.Max(1, Math.Min(8, cfg.SurroundPrimary)); t.Controls.Add(surPrimary); y += 38;

            Button save = new Button(); save.Text = "Şu anki Surround'u profil olarak kaydet"; save.Location = new Point(14, y); save.Size = new Size(300, 30);
            save.Click += delegate
            {
                string err = Surround.SaveProfile();
                surMsg.Text = err == null ? "Profil kaydedildi. Bundan sonra 'AÇ' bu düzeni birebir kullanır." : err;
            };
            t.Controls.Add(save); y += 44;
            surMsg = new Label(); surMsg.Location = new Point(14, y); surMsg.Size = new Size(780, 40); surMsg.ForeColor = SystemColors.GrayText; t.Controls.Add(surMsg); y += 50;

            Label note = new Label(); note.Location = new Point(14, y); note.Size = new Size(780, 90); note.ForeColor = SystemColors.GrayText;
            note.Text = "Not: İlk kullanımda Surround'u bir kez NVIDIA uygulamasından kurup 'profil olarak kaydet'e basmanız en sağlam yoldur. " +
                        "Profil yoksa program, ekranlarınızı Windows'taki soldan sağa sıraya göre tek satırlık Surround olarak kurmayı dener.\r\n" +
                        "Surround açılıp kapanırken ekranlar birkaç saniye yanıp söner; açık pencereler yer değiştirebilir.";
            t.Controls.Add(note);
        }

        // ---- 3. sekme: program kuralları
        const string MaxLabel = "Tam ekran (maximize)";
        DataGridView ruleGrid; DataGridViewComboBoxColumn ruleCombo;

        void BuildRulesTab(TabPage t)
        {
            int y = 14;
            Label hd = new Label(); hd.Text = "Program kuralları — seçtiğiniz programın penceresi açılınca ön ayar otomatik uygulanır"; hd.AutoSize = true;
            hd.Location = new Point(14, y); hd.Font = new Font(Font, FontStyle.Bold); t.Controls.Add(hd); y += 32;

            ruleGrid = new DataGridView(); ruleGrid.Location = new Point(12, y); ruleGrid.Size = new Size(786, 380);
            ruleGrid.AllowUserToAddRows = false; ruleGrid.AllowUserToResizeRows = false; ruleGrid.RowHeadersVisible = false;
            ruleGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; ruleGrid.BackgroundColor = SystemColors.Window;
            ruleGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            DataGridViewCheckBoxColumn c0 = new DataGridViewCheckBoxColumn(); c0.HeaderText = "Etkin"; c0.Width = 55; ruleGrid.Columns.Add(c0);
            DataGridViewTextBoxColumn c1 = new DataGridViewTextBoxColumn(); c1.HeaderText = "Program (exe adı)"; c1.Width = 300; c1.SortMode = DataGridViewColumnSortMode.NotSortable; ruleGrid.Columns.Add(c1);
            ruleCombo = new DataGridViewComboBoxColumn(); ruleCombo.HeaderText = "Uygulanacak ön ayar"; ruleCombo.Width = 360; ruleCombo.FlatStyle = FlatStyle.Flat; ruleGrid.Columns.Add(ruleCombo);
            ruleGrid.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            RefreshRuleCombos();
            foreach (Rule r in cfg.Rules) AddRuleRow(r.Proc, r.Preset, r.Enabled);
            t.Controls.Add(ruleGrid); y += 390;

            Button pick = new Button(); pick.Text = "Çalışan programlardan seç…"; pick.Location = new Point(12, y); pick.Size = new Size(210, 32);
            pick.Click += delegate { foreach (string n in PickRunning()) AddRuleIfNew(n); };
            Button file = new Button(); file.Text = "Dosyadan seç (.exe)…"; file.Location = new Point(232, y); file.Size = new Size(180, 32);
            file.Click += delegate
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = "Boyutlandırılacak programı seçin"; d.Filter = "Program (*.exe)|*.exe";
                    if (d.ShowDialog() == DialogResult.OK) AddRuleIfNew(Path.GetFileNameWithoutExtension(d.FileName));
                }
            };
            Button del = new Button(); del.Text = "Sil"; del.Location = new Point(422, y); del.Size = new Size(90, 32);
            del.Click += delegate { foreach (DataGridViewRow r in new List<DataGridViewRow>(RuleSel())) ruleGrid.Rows.Remove(r); };
            t.Controls.Add(pick); t.Controls.Add(file); t.Controls.Add(del); y += 44;

            Label note = new Label(); note.Location = new Point(14, y); note.Size = new Size(780, 60); note.ForeColor = SystemColors.GrayText;
            note.Text = "Ön ayarlar 1. sekmedeki listeden gelir. Kural, genel 'otomatik uygula' ayarından önce gelir. " +
                        "Program zaten açıksa 'Kaydet' dediğinizde mevcut penceresine de uygulanır. Program adı hücresini elle de yazabilirsiniz (ör. notepad).";
            t.Controls.Add(note);
        }

        IEnumerable<DataGridViewRow> RuleSel() { foreach (DataGridViewRow r in ruleGrid.SelectedRows) yield return r; }

        void RefreshRuleCombos()
        {
            ruleCombo.Items.Clear(); ruleCombo.Items.Add(MaxLabel);
            foreach (DataGridViewRow r in grid.Rows)
            {
                object v = r.Cells[0].Value;
                if (v != null && v.ToString().Trim().Length > 0 && !ruleCombo.Items.Contains(v.ToString())) ruleCombo.Items.Add(v.ToString());
            }
        }

        void AddRuleRow(string proc, string preset, bool enabled)
        {
            string shown = preset.Length == 0 ? MaxLabel : preset;
            if (!ruleCombo.Items.Contains(shown)) ruleCombo.Items.Add(shown);   // silinmiş ön ayar da görünsün
            ruleGrid.Rows.Add(enabled, proc, shown);
        }

        void AddRuleIfNew(string proc)
        {
            if (string.IsNullOrEmpty(proc)) return;
            foreach (DataGridViewRow r in ruleGrid.Rows)
                if (string.Equals((r.Cells[1].Value ?? "").ToString(), proc, StringComparison.OrdinalIgnoreCase)) return;
            string first = ruleCombo.Items.Count > 1 ? ruleCombo.Items[1].ToString() : "";
            AddRuleRow(proc, first, true);
            ruleGrid.CurrentCell = ruleGrid.Rows[ruleGrid.Rows.Count - 1].Cells[1];
        }

        // Çalışan, pencereli programların listesi (çoklu seçim)
        List<string> PickRunning()
        {
            List<string> res = new List<string>();
            using (Form f = new Form())
            {
                f.Text = "Çalışan programlar"; f.StartPosition = FormStartPosition.CenterParent; f.ClientSize = new Size(560, 440);
                f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = false; f.MaximizeBox = false; f.ShowInTaskbar = false; f.Font = Font;
                ListView lv = new ListView(); lv.View = View.Details; lv.FullRowSelect = true; lv.MultiSelect = true;
                lv.Location = new Point(10, 10); lv.Size = new Size(540, 380);
                lv.Columns.Add("Program", 200); lv.Columns.Add("Pencere başlığı", 320);
                foreach (string[] a in app.Wm.ListApps()) lv.Items.Add(new ListViewItem(a));
                lv.DoubleClick += delegate { f.DialogResult = DialogResult.OK; };
                Button ok = new Button(); ok.Text = "Ekle"; ok.Location = new Point(360, 400); ok.Size = new Size(90, 30); ok.DialogResult = DialogResult.OK;
                Button cancel = new Button(); cancel.Text = "İptal"; cancel.Location = new Point(460, 400); cancel.Size = new Size(90, 30); cancel.DialogResult = DialogResult.Cancel;
                f.Controls.Add(lv); f.Controls.Add(ok); f.Controls.Add(cancel); f.AcceptButton = ok; f.CancelButton = cancel;
                if (f.ShowDialog(this) == DialogResult.OK)
                    foreach (ListViewItem it in lv.SelectedItems) res.Add(it.Text);
            }
            return res;
        }

        public void RefreshSurround() { surStatus.Text = Surround.StatusText(); }

        void RunSurround(bool on) { app.SurroundSet(on); RefreshSurround(); }

        IEnumerable<DataGridViewRow> SelRows() { foreach (DataGridViewRow r in grid.SelectedRows) yield return r; }

        void AddText(string h, int w) { DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn(); c.HeaderText = h; c.Width = w; c.SortMode = DataGridViewColumnSortMode.NotSortable; grid.Columns.Add(c); }
        void AddCheck(string h, int w) { DataGridViewCheckBoxColumn c = new DataGridViewCheckBoxColumn(); c.HeaderText = h; c.Width = w; grid.Columns.Add(c); }
        void AddRow(Preset p) { grid.Rows.Add(p.Name, p.W.ToString(), p.H.ToString(), p.X.ToString(), p.Y.ToString(), p.Center, p.Borderless, p.Inner, p.Hotkey); }

        void FillAutoBox()
        {
            autoBox.Items.Clear(); autoBox.Items.Add("Tam ekran (maximize)");
            foreach (Preset p in cfg.Presets) autoBox.Items.Add(p.Label);
            autoBox.SelectedIndex = Math.Max(0, Math.Min(cfg.AutoPreset + 1, autoBox.Items.Count - 1));
        }

        void GridEditing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            TextBox tb = e.Control as TextBox;
            if (tb == null) return;
            tb.KeyDown -= HotCellKey;
            if (grid.CurrentCell.ColumnIndex == HotCol) tb.KeyDown += HotCellKey;
        }

        void HotCellKey(object sender, KeyEventArgs e)
        {
            string v = KeyText(e);
            if (v != null) ((TextBox)sender).Text = v;
        }

        // Basılan tuş birleşimini "Ctrl+Alt+X" biçiminde verir; Esc/Delete temizler
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

        public void ShowTab(int i)
        {
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit); tabs.SelectedIndex = i; Show(); Activate();
            if (i == 1) RefreshSurround();
        }

        void SaveClick(object sender, EventArgs e)
        {
            grid.EndEdit();
            List<Preset> list = new List<Preset>();
            foreach (DataGridViewRow r in grid.Rows)
            {
                Preset p = new Preset();
                p.Name = (r.Cells[0].Value ?? "Ön ayar").ToString();
                p.W = Math.Max(100, Num(r.Cells[1].Value, 1920)); p.H = Math.Max(100, Num(r.Cells[2].Value, 1080));
                p.X = Num(r.Cells[3].Value, 0); p.Y = Num(r.Cells[4].Value, 0);
                p.Center = Chk(r.Cells[5].Value); p.Borderless = Chk(r.Cells[6].Value); p.Inner = Chk(r.Cells[7].Value);
                p.Hotkey = (r.Cells[HotCol].Value ?? "").ToString();
                list.Add(p);
            }
            cfg.Presets = list;
            ruleGrid.EndEdit();
            List<Rule> rules = new List<Rule>();
            foreach (DataGridViewRow r in ruleGrid.Rows)
            {
                string proc = (r.Cells[1].Value ?? "").ToString().Trim();
                if (proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) proc = proc.Substring(0, proc.Length - 4);
                if (proc.Length == 0) continue;
                Rule ru = new Rule(); ru.Proc = proc; ru.Enabled = Chk(r.Cells[0].Value);
                string pv = (r.Cells[2].Value ?? "").ToString();
                ru.Preset = (pv == MaxLabel) ? "" : pv;
                rules.Add(ru);
            }
            cfg.Rules = rules;
            for (int i = 0; i < boxes.Length; i++) cfg.Hot[Config.Actions[i]] = boxes[i].Text;
            cfg.AutoPreset = autoBox.SelectedIndex - 1;
            cfg.Exclude = excl.Text;
            cfg.SurroundConfirm = surConfirm.Checked;
            cfg.SurroundPrimary = (int)surPrimary.Value; Surround.PrimaryAfterOff = cfg.SurroundPrimary;
            cfg.Auto = auto.Checked;
            App.SetStartup(startup.Checked);
            cfg.Save();
            app.Wm.ReloadExclude();
            app.Rebuild();
            FillAutoBox();
            RefreshRuleCombos();
            Hide();
            app.Wm.ApplyRulesToExisting();
            app.Info("Ayarlar kaydedildi.");
        }
    }

    // ------------------------------------------------------------------ Uygulama
    class AppContext : ApplicationContext
    {
        public Config Cfg = new Config();
        public WindowManager Wm;
        public Icon TrayIcon;
        NotifyIcon tray; HotWindow hw; SettingsForm form;
        ToolStripMenuItem autoItem;
        int hotCount;

        public AppContext()
        {
            Wm = new WindowManager(Cfg);
            Surround.PrimaryAfterOff = Cfg.SurroundPrimary;
            TrayIcon = MakeIcon();
            hw = new HotWindow(); hw.Pressed += OnHotkey;
            form = new SettingsForm(Cfg, this);

            tray = new NotifyIcon(); tray.Icon = TrayIcon; tray.Text = "Resize Badger"; tray.Visible = true;
            Wm.Notify += delegate(string msg) { tray.ShowBalloonTip(4000, "Resize Badger", msg, ToolTipIcon.Warning); };
            tray.DoubleClick += delegate { form.ShowTab(0); };
            Rebuild();
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
            m.Items.Add("Tam ekran yap", null, delegate { Wm.Maximize(Wm.Target(true)); });
            m.Items.Add("Eski haline getir", null, delegate { Wm.Restore(Wm.Target(true)); });
            m.Items.Add("Tümüne uygula / hepsini geri al", null, delegate { Wm.ApplyAll(); });
            m.Items.Add("Hepsini eski haline getir", null, delegate { Wm.RestoreAll(); });
            autoItem = new ToolStripMenuItem("Otomatik uygula", null, delegate { ToggleAuto(); });
            autoItem.Checked = Cfg.Auto;
            m.Items.Add(autoItem);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("1 · Ön ayarlar / ayarlar…", null, delegate { form.ShowTab(0); });
            m.Items.Add("2 · NVIDIA Surround aç/kapat", null, delegate { SurroundToggle(); });
            m.Items.Add("2 · NVIDIA Surround ayarları…", null, delegate { form.ShowTab(1); });
            m.Items.Add("3 · Program kuralları…", null, delegate { form.ShowTab(2); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Çıkış", null, delegate { Exit(); });
            ContextMenuStrip old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = m;
            if (old != null) old.Dispose();
            RegisterHotkeys();
        }

        void ToggleAuto()
        {
            Cfg.Auto = !Cfg.Auto; Cfg.Save(); autoItem.Checked = Cfg.Auto;
            tray.ShowBalloonTip(1200, "Resize Badger", "Otomatik uygulama " + (Cfg.Auto ? "AÇIK" : "KAPALI"), ToolTipIcon.None);
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
                tray.ShowBalloonTip(3000, "NVIDIA Surround", back == null ? "Geri alındı." : "Geri alınamadı: " + back, ToolTipIcon.Info);
                return;
            }
            tray.ShowBalloonTip(1500, "NVIDIA Surround", on ? "Surround AÇILDI" : "Surround KAPATILDI", ToolTipIcon.None);
        }

        // 15 sn içinde "Tut" denmezse false (=geri al). Siyah ekranda kilitlenmeyi önler.
        static bool ConfirmSurround(bool on)
        {
            using (Form f = new Form())
            {
                int left = 15;
                f.Text = "NVIDIA Surround"; f.TopMost = true; f.StartPosition = FormStartPosition.CenterScreen;
                f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = false; f.MaximizeBox = false; f.ShowInTaskbar = false;
                f.ClientSize = new Size(420, 130); f.Font = new Font("Segoe UI", 10f);
                Label l = new Label(); l.Location = new Point(14, 14); l.Size = new Size(392, 60); f.Controls.Add(l);
                Button keep = new Button(); keep.Text = "Tut"; keep.Location = new Point(14, 84); keep.Size = new Size(190, 34); keep.DialogResult = DialogResult.Yes; f.Controls.Add(keep);
                Button undo = new Button(); undo.Text = "Geri al"; undo.Location = new Point(216, 84); undo.Size = new Size(190, 34); undo.DialogResult = DialogResult.No; f.Controls.Add(undo);
                f.AcceptButton = keep;
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 1000;
                Action upd = delegate
                {
                    l.Text = (on ? "Surround AÇILDI." : "Surround KAPATILDI.") + " Ekran düzgünse 'Tut'a basın.\r\n" +
                             left + " sn içinde yanıt vermezseniz otomatik geri alınır.";
                };
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
                tray.ShowBalloonTip(3000, "Resize Badger", "Kısayol başka program tarafından kullanılıyor: " + string.Join(", ", failed.ToArray()), ToolTipIcon.Warning);
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

        static Icon MakeIcon()
        {
            Bitmap b = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(Color.FromArgb(45, 45, 52)), 1, 1, 30, 30);
                g.FillRectangle(Brushes.White, 14, 2, 4, 28);       // badger şeridi
                g.DrawRectangle(new Pen(Color.FromArgb(0, 200, 120), 2), 6, 9, 20, 14);
            }
            IntPtr hi = b.GetHicon(); Icon ic = (Icon)Icon.FromHandle(hi).Clone(); N.DestroyIcon(hi); return ic;
        }

        void Exit()
        {
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

        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex mx = new Mutex(true, "ResizeBadger.SingleInstance", out created))
            {
                if (!created) return;
                N.SetProcessDpiAwarenessContext(new IntPtr(-4)); // per-monitor v2
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppContext());
            }
        }
    }
}
