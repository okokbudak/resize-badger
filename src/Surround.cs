// NVIDIA Surround (Mosaic) hızlı yönetimi - NVAPI doğrudan (NVIDIA uygulamasını beklemeden)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ResizeBadger
{
    class SurroundGrid
    {
        public int Rows, Cols, Count, Flags, W, H, Bpp, Hz;
        public int[] Ids;
        public byte[] Raw;      // sürücünün verdiği ham 1832 baytlık NV_MOSAIC_GRID_TOPO (V2)
        public bool IsSurround { get { return Count > 1; } }
    }

    static class Surround
    {
        // NVAPI fonksiyon kimlikleri (nvapi_QueryInterface)
        const uint ID_INIT = 0x0150E828, ID_ENUM = 0xDF2887AF, ID_SET = 0x4D959A89, ID_VALIDATE = 0xCF43903D,
                   ID_ENABLE = 0x5F1AA66C, ID_DISPID = 0xAE457190;
        // NV_MOSAIC_GRID_TOPO_V2 yerleşimi: başlık 20 bayt, 64 x 28 bayt ekran, 20 bayt ayar
        const int GRID = 1832, HDR = 20, DSZ = 28, SET = HDR + 64 * DSZ;
        const uint FLAG_CURRENT_GPU = 1;      // NV_MOSAIC_SETDISPLAYTOPO_FLAG_CURRENT_GPU_TOPOLOGY
        const int GRIDFLAG_DEFAULT = 0x12;    // NVIDIA uygulamasının kurduğu Surround'da görülen bayraklar

        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr QI(uint id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DInit();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DEnable(uint enable);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DEnum(IntPtr topos, ref uint count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DSet(IntPtr topos, uint count, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DValidate(uint flags, IntPtr topos, IntPtr status, uint count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DDispId([MarshalAs(UnmanagedType.LPStr)] string name, out uint id);

        static bool inited;
        static DEnable fEnable; static DEnum fEnum; static DSet fSet; static DValidate fValidate; static DDispId fDispId;

        static string ProfilePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "surround_profile.bin"); } }
        public static bool HasProfile { get { return File.Exists(ProfilePath); } }

        static T Fn<T>(uint id) where T : class
        {
            IntPtr p = QI(id);
            if (p == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        // Sürücü hazır mı? Sorun varsa nedenini döndürür.
        public static string Init()
        {
            if (inited) return null;
            try
            {
                DInit init = Fn<DInit>(ID_INIT);
                if (init == null || init() != 0) return L.T("sur.err.init");
                fEnable = Fn<DEnable>(ID_ENABLE); fEnum = Fn<DEnum>(ID_ENUM); fSet = Fn<DSet>(ID_SET);
                fValidate = Fn<DValidate>(ID_VALIDATE); fDispId = Fn<DDispId>(ID_DISPID);
                if (fEnum == null || fSet == null) return L.T("sur.err.api");
                inited = true; return null;
            }
            catch (DllNotFoundException) { return L.T("sur.err.dll"); }
            catch (Exception e) { return L.F("sur.err.nvapi", e.Message); }
        }

        // ---------------------------------------------------------------- ham yapı yardımcıları
        static byte[] NewGrid(int rows, int cols, int flags, int[] ids, int w, int h, int bpp, int hz)
        {
            byte[] b = new byte[GRID];
            Put(b, 4, rows); Put(b, 8, cols); Put(b, 12, ids.Length); Put(b, 16, flags);
            for (int d = 0; d < ids.Length; d++) Put(b, HDR + d * DSZ + 4, ids[d]);
            Put(b, SET + 4, w); Put(b, SET + 8, h); Put(b, SET + 12, bpp); Put(b, SET + 16, hz);
            return b;
        }
        static void Put(byte[] b, int off, int v) { Array.Copy(BitConverter.GetBytes(v), 0, b, off, 4); }
        static int Get(byte[] b, int off) { return BitConverter.ToInt32(b, off); }

        // Sürümleri doldurup yönetilmeyen belleğe kopyalar (n adet ızgara)
        static IntPtr ToNative(List<byte[]> grids)
        {
            IntPtr p = Marshal.AllocHGlobal(GRID * grids.Count);
            for (int g = 0; g < grids.Count; g++)
            {
                byte[] b = (byte[])grids[g].Clone();
                Put(b, 0, GRID | (2 << 16));
                for (int d = 0; d < 64; d++) Put(b, HDR + d * DSZ, DSZ | (2 << 16));
                Put(b, SET, 20 | (1 << 16));
                Marshal.Copy(b, 0, IntPtr.Add(p, g * GRID), GRID);
            }
            return p;
        }

        // ---------------------------------------------------------------- okuma
        public static List<SurroundGrid> Grids()
        {
            List<SurroundGrid> list = new List<SurroundGrid>();
            uint n = 0;
            if (fEnum(IntPtr.Zero, ref n) != 0 || n == 0) return list;
            List<byte[]> blank = new List<byte[]>();
            for (int i = 0; i < n; i++) blank.Add(new byte[GRID]);
            IntPtr p = ToNative(blank);
            try
            {
                if (fEnum(p, ref n) != 0) return list;
                for (int g = 0; g < n; g++)
                {
                    byte[] raw = new byte[GRID]; Marshal.Copy(IntPtr.Add(p, g * GRID), raw, 0, GRID);
                    SurroundGrid s = new SurroundGrid();
                    s.Raw = raw; s.Rows = Get(raw, 4); s.Cols = Get(raw, 8); s.Count = Get(raw, 12); s.Flags = Get(raw, 16);
                    s.W = Get(raw, SET + 4); s.H = Get(raw, SET + 8); s.Bpp = Get(raw, SET + 12); s.Hz = Get(raw, SET + 16);
                    s.Ids = new int[Math.Min(s.Count, 64)];
                    for (int d = 0; d < s.Ids.Length; d++) s.Ids[d] = Get(raw, HDR + d * DSZ + 4);
                    list.Add(s);
                }
            }
            finally { Marshal.FreeHGlobal(p); }
            return list;
        }

        public static SurroundGrid Active()
        {
            foreach (SurroundGrid g in Grids()) if (g.IsSurround) return g;
            return null;
        }

        public static string StatusText()
        {
            string err = Init(); if (err != null) return err;
            List<SurroundGrid> gs = Grids();
            foreach (SurroundGrid g in gs)
                if (g.IsSurround)
                    return L.F("sur.state.on", g.Cols, g.Rows, g.Count, g.W, g.H, g.Hz, g.W * g.Cols, g.H * g.Rows);
            return L.F("sur.state.off", gs.Count);
        }

        // ---------------------------------------------------------------- yazma
        // Mevcut etkin Surround'u profil olarak kaydet (en sadık geri yükleme için)
        public static string SaveProfile()
        {
            string err = Init(); if (err != null) return err;
            SurroundGrid a = Active();
            if (a == null) return L.T("sur.err.needActive");
            File.WriteAllBytes(ProfilePath, a.Raw);
            return null;
        }

        static bool WaitFor(bool wantSurround)
        {
            for (int i = 0; i < 25; i++)
            {
                Thread.Sleep(400);
                if ((Active() != null) == wantSurround) { Thread.Sleep(600); return true; }
            }
            return false;
        }

        // Kapalıyken Surround ızgarasını kur: kayıtlı profil, yoksa mevcut ekranlardan otomatik
        public static string Debug = "";
        public static bool IsOn() { return Init() == null && Active() != null; }

        static byte[] BuildGrid(out string err)
        {
            err = null;
            if (HasProfile)
            {
                byte[] b = File.ReadAllBytes(ProfilePath);
                if (b.Length == GRID) return b;
            }
            List<SurroundGrid> gs = Grids();
            if (gs.Count < 2) { err = L.T("sur.err.min2"); return null; }
            // soldan sağa: Windows'taki ekran konumuna göre sırala
            List<int> ids = new List<int>();
            foreach (Mon s in Monitors())
            {
                uint id;
                if (fDispId != null && fDispId(s.Name, out id) == 0) ids.Add((int)id);
            }
            if (ids.Count < 2) { err = L.T("sur.err.ids"); return null; }
            SurroundGrid f = gs[0];
            return NewGrid(1, ids.Count, GRIDFLAG_DEFAULT, ids.ToArray(), f.W, f.H, f.Bpp == 0 ? 32 : f.Bpp, f.Hz);
        }

        static int SetGrids(List<byte[]> grids)
        {
            IntPtr p = ToNative(grids);
            try { return fSet(p, (uint)grids.Count, FLAG_CURRENT_GPU); }
            finally { Marshal.FreeHGlobal(p); }
        }

        // true: açık istendi. Hata metni ya da null (başarılı) döner.
        public static string Set(bool on)
        {
            string err = Init(); if (err != null) return err;
            bool isOn = Active() != null;
            if (isOn == on) return null;
            int rc = -1, expect = 0;
            Debug = "";
            // NvAPI_Mosaic_EnableCurrentTopo bu sürücüde ~40 sn bekleyip hata veriyor; ızgaraları doğrudan yazıyoruz.
            if (on)
            {
                byte[] g = BuildGrid(out err); if (g == null) return err;
                rc = SetGrids(new List<byte[]>(new byte[][] { g }));
            }
            else
            {
                SurroundGrid a = Active();
                if (a == null) return null;   // hızlı yol uygulamış, sadece geç görünmüş
                expect = a.Ids.Length;
                List<byte[]> singles = new List<byte[]>();
                foreach (int id in a.Ids) singles.Add(NewGrid(1, 1, 0, new int[] { id }, a.W, a.H, a.Bpp == 0 ? 32 : a.Bpp, a.Hz));
                rc = SetGrids(singles);
            }
            Debug += "setGrids rc=" + rc + "; ";
            if (rc != 0) return L.F("sur.err.set", rc);
            if (!WaitFor(on)) return L.T("sur.err.timeout");
            if (!on) ArrangePrimary(PrimaryAfterOff, expect);
            return null;
        }

        // Surround kapanınca Windows en soldaki ekranı ana ekran yapar; istenen ekranı (soldan sıra, 1 tabanlı) ana ekran yapıp
        // diğerlerini ona göre yerleştirir (ortadaki ana ekran ise sol ekran negatif X'te kalır).
        public static int PrimaryAfterOff = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency,
                       dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string dev, int mode, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ChangeDisplaySettingsEx(string dev, ref DEVMODE dm, IntPtr hwnd, uint flags, IntPtr lp);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ChangeDisplaySettingsEx(string dev, IntPtr dm, IntPtr hwnd, uint flags, IntPtr lp);

        class Mon { public string Name; public Rectangle B; public bool Primary; }
        delegate bool MonEnumProc(IntPtr hMon, IntPtr hdc, IntPtr rc, IntPtr data);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEX
        {
            public int cbSize, L, T, R, B, wL, wT, wR, wB; public uint flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dev;
        }
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonEnumProc p, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr h, ref MONITORINFOEX mi);

        // Windows'un ANLIK ekran listesi, soldan sağa. (System.Windows.Forms.Screen listeyi önbellekte tutar; Surround kapanınca
        // bir süre eski listeyi verebiliyor ve ana ekran ayarı sessizce atlanıyordu.)
        static List<Mon> Monitors()
        {
            List<Mon> l = new List<Mon>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr h, IntPtr dc, IntPtr rc, IntPtr d)
            {
                MONITORINFOEX mi = new MONITORINFOEX(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(h, ref mi))
                {
                    Mon m = new Mon(); m.Name = mi.dev; m.B = Rectangle.FromLTRB(mi.L, mi.T, mi.R, mi.B); m.Primary = (mi.flags & 1) != 0; l.Add(m);
                }
                return true;
            }, IntPtr.Zero);
            l.Sort(delegate(Mon a, Mon b) { return a.B.X.CompareTo(b.B.X); });
            return l;
        }

        // minScreens: Surround kapanınca beklenen ekran sayısı (sürücü hepsini geri getirene kadar beklenir). Sonucu doğrulayıp gerekirse yeniden dener.
        public static void ArrangePrimary(int indexFromLeft, int minScreens)
        {
            try
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    List<Mon> scr = Monitors();
                    if (scr.Count < Math.Max(2, minScreens)) { Thread.Sleep(1000); continue; }   // ekranlar henüz geri gelmedi
                    Mon chosen = scr[Math.Max(0, Math.Min(indexFromLeft - 1, scr.Count - 1))];
                    if (chosen.Primary && chosen.B.X == 0 && chosen.B.Y == 0) return;           // zaten istenen düzen
                    int ox = chosen.B.X, oy = chosen.B.Y;
                    // seçilen ekranı önce (ana ekran + 0,0), sonra diğerlerini
                    scr.Remove(chosen); scr.Insert(0, chosen);
                    foreach (Mon s in scr)
                    {
                        DEVMODE dm = new DEVMODE(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                        if (!EnumDisplaySettings(s.Name, -1, ref dm)) continue;
                        dm.dmFields = 0x20;                       // DM_POSITION
                        dm.dmPositionX = s.B.X - ox; dm.dmPositionY = s.B.Y - oy;
                        uint fl = 0x1 | 0x10000000;               // CDS_UPDATEREGISTRY | CDS_NORESET
                        if (s == chosen) fl |= 0x10;              // CDS_SET_PRIMARY
                        ChangeDisplaySettingsEx(s.Name, ref dm, IntPtr.Zero, fl, IntPtr.Zero);
                    }
                    ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);   // uygula
                    Thread.Sleep(1500);                                                          // oturması için; döngü başında doğrulanır
                }
            }
            catch { }
        }
    }
}
