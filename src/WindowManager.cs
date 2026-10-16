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
            else if (Notify != null) Notify(L.F("msg.rulePresetMissing", r.Proc, r.Preset));
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
                    if (Notify != null) Notify(L.T("msg.elevated"));
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

}
