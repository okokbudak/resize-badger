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
                Presets.Add(Preset.Make("Triple 5760x1080", 5760, 1080, SystemInformation.VirtualScreen.Left, SystemInformation.VirtualScreen.Top, false, true, "Ctrl+Alt+3"));
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
                    else if (k == "lang") Lang = v;
                    else if (k == "theme") Theme = v;
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
                l.Add("lang=" + Lang);
                l.Add("theme=" + Theme);
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

}
