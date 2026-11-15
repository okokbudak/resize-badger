using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ResizeBadger
{
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
        public static string ActionName(int i) { return L.T("act." + Actions[i]); }
        public string Lang = "auto";      // auto | tr | en | de | es | fr
        public string Theme = "system";   // system | light | dark
        public bool SurroundConfirm = true;
        public List<Rule> SurroundGames = new List<Rule>();   // bu programlar çalışınca Surround otomatik açılır (Rule.Preset kullanılmaz)
        public bool SurroundGameOff = true;                    // oyun(lar) kapanınca, Surround'u biz açtıysak geri kapat
        public int SurroundPrimary = 2;   // Surround kapanınca ana ekran olacak ekran (soldan sıra)
        public int[] Bezel = null;        // Surround bezel düzeltme: soldan sağa her ekran çifti arasındaki piksel boşluğu (0 = kapalı).
                                          // null = uygulamada hiç ayarlanmadı: NVIDIA uygulamasındaki / profildeki mevcut değerlere dokunulmaz.

        public Dictionary<string, string> Hot = new Dictionary<string, string>();
        public List<Preset> Presets = new List<Preset>();
        public int AutoPreset = -1;   // -1 = maximize, >=0 = preset sırası
        public bool Auto;
        public string Exclude = "explorer;SearchHost;ShellExperienceHost;StartMenuExperienceHost;TextInputHost;LockApp;ResizeBadger";
        readonly string path;

        public Config()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            // Ctrl+Alt = AltGr (Türkçe/Almanca/İspanyolca... klavyelerde #, $, @, ß gibi karakterler): varsayılanlar Ctrl+Alt kullanmaz
            Hot["fill"] = "Ctrl+Shift+F5";
            Hot["restore"] = "Ctrl+Shift+F6";
            Hot["all"] = "Ctrl+Shift+F7";
            Hot["auto"] = "Ctrl+Shift+F8";
            Hot["surround"] = "Ctrl+Shift+F9";
            Load();
            if (Presets.Count == 0)
            {
                Presets.Add(Preset.Make("Full HD", 1920, 1080, 0, 0, true, false, "Ctrl+Shift+F1"));
                Presets.Add(Preset.Make("2K", 2560, 1440, 0, 0, true, false, "Ctrl+Shift+F2"));
                Presets.Add(Preset.Make("Triple 5760x1080", 5760, 1080, SystemInformation.VirtualScreen.Left, SystemInformation.VirtualScreen.Top, false, true, "Ctrl+Shift+F3"));
                Presets.Add(Preset.Make("720p", 1280, 720, 0, 0, true, false, "Ctrl+Shift+F4"));
            }
            // Eski sürümlerde kaydedilmiş Ctrl+Alt kısayollarını AltGr ile çakışmayanlara çevir
            foreach (string a in new List<string>(Hot.Keys)) { string n = NoAltGr(Hot[a]); if (n != Hot[a]) { Hot[a] = n; Migrated = true; } }
            foreach (Preset pr in Presets) { string n = NoAltGr(pr.Hotkey); if (n != pr.Hotkey) { pr.Hotkey = n; Migrated = true; } }
            if (Migrated) Save();
        }

        // ---- Yedekleme: ayarlar (+ varsa Surround profili) tek dosyada. Profil "sprofile=" satırında base64 tutulur.
        public string Export(string file)
        {
            try
            {
                Save();
                List<string> l = new List<string>(File.ReadAllLines(path, Encoding.UTF8));
                byte[] prof = Surround.ReadProfile();
                if (prof != null) l.Add("sprofile=" + Convert.ToBase64String(prof));
                File.WriteAllLines(file, l.ToArray(), Encoding.UTF8);
                return null;
            }
            catch (Exception e) { return e.Message; }
        }

        // Yedek dosyasından geri yükler (mevcut ayarların yerine geçer). Başarılıysa null, değilse hata metni.
        public string Import(string file)
        {
            try
            {
                List<string> keep = new List<string>(); byte[] prof = null; bool known = false;
                foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
                {
                    int i = raw.IndexOf('=');
                    if (i < 1) continue;
                    string k = raw.Substring(0, i).Trim();
                    if (k == "sprofile") { try { prof = Convert.FromBase64String(raw.Substring(i + 1).Trim()); } catch { } continue; }
                    if (k == "preset" || k == "lang" || k == "theme" || k == "autopreset" || k.StartsWith("hk_")) known = true;
                    keep.Add(raw);
                }
                if (!known) return L.T("gen.importBad");
                File.WriteAllLines(path, keep.ToArray(), Encoding.UTF8);
                if (prof != null) Surround.WriteProfile(prof);
                Reload();
                return null;
            }
            catch (Exception e) { return e.Message; }
        }

        // Tüm alanları varsayılana döndürüp dosyadan yeniden okur
        void Reload()
        {
            Rules = new List<Rule>(); SurroundGames = new List<Rule>(); Presets = new List<Preset>();
            Lang = "auto"; Theme = "system"; SurroundConfirm = true; SurroundGameOff = true; SurroundPrimary = 2; Bezel = null;
            AutoPreset = -1; Auto = false;
            Exclude = "explorer;SearchHost;ShellExperienceHost;StartMenuExperienceHost;TextInputHost;LockApp;ResizeBadger";
            Hot["fill"] = "Ctrl+Shift+F5"; Hot["restore"] = "Ctrl+Shift+F6"; Hot["all"] = "Ctrl+Shift+F7"; Hot["auto"] = "Ctrl+Shift+F8"; Hot["surround"] = "Ctrl+Shift+F9";
            Load();
            foreach (string a in new List<string>(Hot.Keys)) Hot[a] = NoAltGr(Hot[a]);
            foreach (Preset pr in Presets) pr.Hotkey = NoAltGr(pr.Hotkey);
            if (Presets.Count == 0) Presets.Add(Preset.Make("Full HD", 1920, 1080, 0, 0, true, false, "Ctrl+Shift+F1"));
            Save();
        }

        public bool Migrated;   // true: eski Ctrl+Alt kısayolları otomatik değiştirildi (kullanıcıya haber verilir)

        static readonly Dictionary<string, string> OldDefaults = new Dictionary<string, string> {
            { "Ctrl+Alt+Up", "Ctrl+Shift+F5" }, { "Ctrl+Alt+Down", "Ctrl+Shift+F6" }, { "Ctrl+Alt+A", "Ctrl+Shift+F7" },
            { "Ctrl+Alt+Z", "Ctrl+Shift+F8" }, { "Ctrl+Alt+S", "Ctrl+Shift+F9" },
            { "Ctrl+Alt+1", "Ctrl+Shift+F1" }, { "Ctrl+Alt+2", "Ctrl+Shift+F2" }, { "Ctrl+Alt+3", "Ctrl+Shift+F3" }, { "Ctrl+Alt+4", "Ctrl+Shift+F4" } };

        // Ctrl+Alt (Win yok) birleşimi AltGr ile aynıdır: bu tuşla yazılan karakterler çalışmaz
        public static bool IsAltGr(string hk)
        {
            if (string.IsNullOrEmpty(hk)) return false;
            string[] t = hk.Split('+'); bool ctrl = false, alt = false, win = false;
            for (int i = 0; i < t.Length - 1; i++) { string s = t[i].Trim(); if (s == "Ctrl") ctrl = true; else if (s == "Alt") alt = true; else if (s == "Win") win = true; }
            return ctrl && alt && !win;
        }

        public static string NoAltGr(string hk)
        {
            if (!IsAltGr(hk)) return hk;
            string m; if (OldDefaults.TryGetValue(hk, out m)) return m;
            string[] t = hk.Split('+');
            return "Ctrl+Shift+" + t[t.Length - 1].Trim();     // özel Ctrl+Alt+X -> Ctrl+Shift+X
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
                    else if (k == "lang") Lang = v;
                    else if (k == "theme") Theme = v;
                    else if (k == "sconfirm") SurroundConfirm = v == "1";
                    else if (k == "sprimary") int.TryParse(v, out SurroundPrimary);
                    else if (k == "sgame") { Rule g = Rule.Parse(v); if (g != null) SurroundGames.Add(g); }
                    else if (k == "sgameoff") SurroundGameOff = v == "1";
                    else if (k == "bezel")
                    {
                        List<int> g = new List<int>();
                        foreach (string part in v.Split(',')) { int n; if (int.TryParse(part.Trim(), out n)) g.Add(Math.Max(0, n)); }
                        Bezel = g.ToArray();
                    }
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
                l.Add("lang=" + Lang);
                l.Add("theme=" + Theme);
                l.Add("sconfirm=" + (SurroundConfirm ? "1" : "0"));
                l.Add("sprimary=" + SurroundPrimary);
                l.Add("sgameoff=" + (SurroundGameOff ? "1" : "0"));
                foreach (Rule g in SurroundGames) l.Add("sgame=" + g.Serialize());
                if (Bezel != null)
                {
                    string[] bz = new string[Bezel.Length]; for (int i = 0; i < bz.Length; i++) bz[i] = Bezel[i].ToString();
                    l.Add("bezel=" + string.Join(",", bz));
                }
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

    // Güncelleme denetimi: yalnızca kullanıcı düğmeye basınca çalışır; GitHub'dan herkese açık "en son sürüm" bilgisini okur,
    // kullanıcıya ait hiçbir bilgi göndermez (genel bir User-Agent kullanılır).
    static class Updater
    {
        // Sonuç: (hata, yeni sürüm ya da null, sürüm sayfası adresi)
        public static void Check(Action<string, string, string> done)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null, ver = null, url = null;
                try
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;     // TLS 1.2 (.NET 4'te varsayılan değil)
                    HttpWebRequest rq = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Ver.Repo.Replace("github.com/", "") + "/releases/latest");
                    rq.UserAgent = "ResizeBadger-UpdateCheck"; rq.Timeout = 10000; rq.Accept = "application/vnd.github+json";
                    using (WebResponse rs = rq.GetResponse())
                    using (StreamReader sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();
                        Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"");
                        Match u = Regex.Match(json, "\"html_url\"\\s*:\\s*\"([^\"]+/releases/tag/[^\"]+)\"");
                        Version latest, mine;
                        if (!t.Success || !Version.TryParse(t.Groups[1].Value, out latest) || !Version.TryParse(Ver.V, out mine)) err = L.T("gen.updFail");
                        else if (latest > mine) { ver = t.Groups[1].Value; url = u.Success ? u.Groups[1].Value : "https://" + Ver.Repo + "/releases"; }
                    }
                }
                catch (Exception e) { err = e.Message; }
                done(err, ver, url);
            });
        }
    }

}
