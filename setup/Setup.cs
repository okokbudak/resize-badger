// Resize Badger kurulum + kaldırma programı (tek exe). Yönetici izni gerekmez (kullanıcı başına kurulum).
// Kaldırma, kurulumun oluşturduğu her şeyi siler: klasör (ayarlar dahil), kısayollar, kayıt defteri girdileri.
//
//   ResizeBadger-Setup.exe                      arayüzlü kurulum
//   ResizeBadger-Setup.exe /S [/D=klasör] [/nodesktop] [/startup] [/norun]   sessiz kurulum
//   Uninstall.exe /uninstall [/S]               kaldırma (kurulum klasöründe bulunur)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ResizeBadger
{
    static class SetupApp
    {
        const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ResizeBadger";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string NotifyKey = @"Control Panel\NotifyIconSettings";
        const string AppExe = "ResizeBadger.exe", UnExe = "Uninstall.exe";

        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);

        static bool Has(string[] a, string name) { foreach (string s in a) if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) return true; return false; }
        static string Val(string[] a, string name)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)) return a[i].Substring(name.Length + 1).Trim('"');
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < a.Length) return a[i + 1].Trim('"');
            }
            return null;
        }

        static string StartMenuDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Resize Badger"); } }
        static string DesktopLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Resize Badger.lnk"); } }

        public static string DefaultDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstKey))
                {
                    string d = k == null ? null : k.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(d)) return d;
                }
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Resize Badger");
        }

        // Dolu ve bize ait olmayan bir klasör seçildiyse alt klasör kullan (kaldırırken başkasının dosyası silinmesin)
        public static string ResolveDir(string chosen)
        {
            string full = Path.GetFullPath(chosen.Trim().TrimEnd('\\', '/'));
            bool root = string.Equals(Path.GetPathRoot(full).TrimEnd('\\'), full.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            bool ours = File.Exists(Path.Combine(full, AppExe)) || File.Exists(Path.Combine(full, UnExe));
            bool nonEmpty = Directory.Exists(full) && Directory.GetFileSystemEntries(full).Length > 0;
            if (root || (nonEmpty && !ours)) full = Path.Combine(full, "Resize Badger");
            return full;
        }

        [STAThread]
        static int Main(string[] args)
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            L.Set("auto"); Theme.Mode = "system"; Theme.Reload();
            bool silent = Has(args, "/S") || Has(args, "/silent");

            // Geliştirme: --shot dosya.png [--theme light|dark] [--lang xx] kurulum penceresinin görüntüsünü alır
            string shot = Val(args, "--shot");
            if (shot != null)
            {
                string th = Val(args, "--theme"), lg = Val(args, "--lang");
                if (lg != null) L.Set(lg);
                if (th != null) { Theme.Mode = th; Theme.Reload(); }
                using (SetupForm f = new SetupForm())
                {
                    f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-4000, -4000); f.Show();
                    for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(60); }
                    using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                    {
                        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                        bmp.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                return 0;
            }

            string stage2 = Val(args, "--stage2");
            if (stage2 != null) return UninstallStage2(stage2, silent);
            if (Has(args, "/uninstall") || Has(args, "--uninstall")) return UninstallStage1(silent);

            if (silent)
            {
                string dir = ResolveDir(Val(args, "/D") ?? DefaultDir());
                string err = Install(dir, !Has(args, "/nodesktop") && Has(args, "/desktop"), Has(args, "/startup"), !Has(args, "/norun"));
                return err == null ? 0 : 1;
            }
            Application.Run(new SetupForm());
            return 0;
        }

        // ------------------------------------------------------------ kurulum
        static void KillRunning()
        {
            foreach (Process p in Process.GetProcessesByName("ResizeBadger"))
            {
                try { p.Kill(); p.WaitForExit(4000); } catch { }
            }
        }

        static void CopyRetry(Action a)
        {
            for (int i = 0; ; i++)
            {
                try { a(); return; }
                catch (IOException) { if (i >= 12) throw; System.Threading.Thread.Sleep(250); }
                catch (UnauthorizedAccessException) { if (i >= 12) throw; System.Threading.Thread.Sleep(250); }
            }
        }

        static void Shortcut(string lnkPath, string target, string arguments, string description)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t), lnk = null;
            try
            {
                lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnkPath });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
                lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, lnk, new object[] { arguments });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(target) });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { description });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            finally
            {
                if (lnk != null) Marshal.ReleaseComObject(lnk);
                Marshal.ReleaseComObject(sh);
            }
        }

        // Başarılıysa null, değilse hata metni
        public static string Install(string dir, bool desktop, bool startup, bool launch)
        {
            try
            {
                KillRunning();
                Directory.CreateDirectory(dir);
                string exe = Path.Combine(dir, AppExe), un = Path.Combine(dir, UnExe), self = Application.ExecutablePath;

                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ResizeBadger.exe"))
                {
                    if (s == null) return "ResizeBadger.exe resource missing";
                    byte[] buf = new byte[s.Length]; int off = 0; while (off < buf.Length) off += s.Read(buf, off, buf.Length - off);
                    CopyRetry(delegate { File.WriteAllBytes(exe, buf); });
                }
                if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(un), StringComparison.OrdinalIgnoreCase))
                    CopyRetry(delegate { File.Copy(self, un, true); });

                // Kurulum programının yanında kişisel ayarlar varsa içe aktar (üzerine yazmaz)
                string here = Path.GetDirectoryName(self);
                foreach (string f in new string[] { "settings.ini", "surround_profile.bin" })
                {
                    string src = Path.Combine(here, f), dst = Path.Combine(dir, f);
                    if (File.Exists(src) && !File.Exists(dst) && !string.Equals(Path.GetFullPath(here), Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
                        File.Copy(src, dst);
                }

                Directory.CreateDirectory(StartMenuDir);
                Shortcut(Path.Combine(StartMenuDir, "Resize Badger.lnk"), exe, "", "Resize Badger");
                Shortcut(Path.Combine(StartMenuDir, L.T("setup.shortcut") + ".lnk"), un, "/uninstall", L.T("setup.shortcut"));
                if (desktop) Shortcut(DesktopLnk, exe, "", "Resize Badger");

                long size = new FileInfo(exe).Length + new FileInfo(un).Length;
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstKey))
                {
                    k.SetValue("DisplayName", "Resize Badger");
                    k.SetValue("DisplayVersion", Ver.V);
                    k.SetValue("Publisher", "okokbudak");
                    k.SetValue("InstallLocation", dir);
                    k.SetValue("DisplayIcon", exe);
                    k.SetValue("UninstallString", "\"" + un + "\" /uninstall");
                    k.SetValue("QuietUninstallString", "\"" + un + "\" /uninstall /S");
                    k.SetValue("URLInfoAbout", "https://" + Ver.Repo);
                    k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
                using (RegistryKey r = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (startup) r.SetValue("ResizeBadger", "\"" + exe + "\"");
                    else r.DeleteValue("ResizeBadger", false);
                }
                if (launch) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = dir });
                return null;
            }
            catch (Exception e) { return e.Message; }
        }

        // ------------------------------------------------------------ Windows'un kendi tuttuğu izler
        // Bir exe çalışınca Windows (kullanıcı hesabında) şu kayıtları kendiliğinden oluşturur; kaldırırken bunlar da silinir.
        static readonly string[] TraceKeys = new string[] {
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppSwitched",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppLaunch",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\ShowJumpView",
            @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store",
            @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Persisted",
            @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" };
        const string UserAssist = @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

        static string Rot13(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                char ch = c[i];
                if (ch >= 'a' && ch <= 'z') c[i] = (char)((ch - 'a' + 13) % 26 + 'a');
                else if (ch >= 'A' && ch <= 'Z') c[i] = (char)((ch - 'A' + 13) % 26 + 'A');
            }
            return new string(c);
        }

        static void PurgeValues(string sub, string prefix, bool userAssist)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(sub, true))
                {
                    if (k == null) return;
                    foreach (string n in k.GetValueNames())
                    {
                        string t = userAssist ? Rot13(n) : n;
                        bool hit = t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                                   || (userAssist && (t.IndexOf("ResizeBadger", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("\\Resize Badger\\", StringComparison.OrdinalIgnoreCase) >= 0));
                        if (hit) { try { k.DeleteValue(n, false); } catch { } }
                    }
                }
            }
            catch { }
        }

        static void PurgeTraces(string prefix)
        {
            foreach (string k in TraceKeys) PurgeValues(k, prefix, false);
            try
            {
                using (RegistryKey ua = Registry.CurrentUser.OpenSubKey(UserAssist))
                {
                    if (ua != null) foreach (string g in ua.GetSubKeyNames()) PurgeValues(UserAssist + "\\" + g + "\\Count", prefix, true);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------ kaldırma
        // 1. aşama: kendini TEMP'e kopyalayıp oradan çalıştırır (kurulum klasörünü silebilmek için)
        static int UninstallStage1(bool silent)
        {
            string dir = DefaultDir();
            string here = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(Path.Combine(here, AppExe))) dir = here;
            string tmp = Path.Combine(Path.GetTempPath(), "RB-Uninstall-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".exe");
            File.Copy(Application.ExecutablePath, tmp, true);
            Process.Start(new ProcessStartInfo(tmp, "--stage2 \"" + dir + "\"" + (silent ? " /S" : "")) { UseShellExecute = false });
            return 0;
        }

        static void TryDeleteDir(string dir, List<string> failures)
        {
            for (int i = 0; i < 20; i++)
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); return; }
                catch { System.Threading.Thread.Sleep(300); }
            }
            if (Directory.Exists(dir)) failures.Add(dir);
        }

        static int UninstallStage2(string dir, bool silent)
        {
            string self = Application.ExecutablePath;
            dir = dir.TrimEnd('\\', '/');
            // yalnızca bize ait klasörü sil
            if (!File.Exists(Path.Combine(dir, AppExe)) && !File.Exists(Path.Combine(dir, UnExe)))
            {
                if (!silent) MessageBox.Show("Resize Badger: " + dir, L.T("un.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                SelfDelete(self); return 1;
            }
            if (!silent && MessageBox.Show(L.T("un.confirm"), L.T("un.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            { SelfDelete(self); return 0; }

            List<string> failures = new List<string>();
            KillRunning();

            // kısayollar
            try { if (Directory.Exists(StartMenuDir)) Directory.Delete(StartMenuDir, true); } catch (Exception) { failures.Add(StartMenuDir); }
            try { if (File.Exists(DesktopLnk)) File.Delete(DesktopLnk); } catch (Exception) { failures.Add(DesktopLnk); }

            // kayıt defteri: başlangıç, kaldırma kaydı, Windows 11'in tepsi simgesi hafızası
            try { using (RegistryKey r = Registry.CurrentUser.OpenSubKey(RunKey, true)) { if (r != null) r.DeleteValue("ResizeBadger", false); } } catch (Exception) { failures.Add("HKCU\\" + RunKey); }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstKey, false); } catch (Exception) { failures.Add("HKCU\\" + UninstKey); }
            try
            {
                using (RegistryKey n = Registry.CurrentUser.OpenSubKey(NotifyKey, true))
                {
                    if (n != null)
                        foreach (string sub in n.GetSubKeyNames())
                        {
                            string p = null;
                            using (RegistryKey s = n.OpenSubKey(sub)) { if (s != null) p = s.GetValue("ExecutablePath") as string; }
                            if (p != null && p.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                            {
                                n.DeleteSubKeyTree(sub, false);
                                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\AppUserModelId\NotifyIconGeneratedAumid_" + sub, false);
                            }
                        }
                }
            }
            catch (Exception) { }
            PurgeTraces(dir);

            // klasörün kendisi (ayarlar, profil, exe'ler)
            TryDeleteDir(dir, failures);
            PurgeTraces(dir);   // exe'ler silinirken Windows son bir kayıt daha yazmış olabilir

            SelfDelete(self);
            if (!silent)
            {
                if (failures.Count == 0) MessageBox.Show(L.T("un.done"), L.T("un.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                else MessageBox.Show(L.F("un.fail", string.Join("\n", failures.ToArray())), L.T("un.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return failures.Count == 0 ? 0 : 1;
        }

        // TEMP'teki geçici kopya, süreç bitince kendini siler
        static void SelfDelete(string self)
        {
            try
            {
                // dosyayı sil + Windows'un bu geçici exe için yazdığı izleri temizle (süreç bittikten sonra)
                string cmd = "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + self + "\"";
                foreach (string k in TraceKeys)
                    cmd += " & reg delete \"HKCU\\" + k + "\" /v \"" + self + "\" /f >nul 2>&1";
                foreach (string v in new string[] { ".FriendlyAppName", ".ApplicationCompany" })
                    cmd += " & reg delete \"HKCU\\" + TraceKeys[0] + "\" /v \"" + self + v + "\" /f >nul 2>&1";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd);
                psi.WindowStyle = ProcessWindowStyle.Hidden; psi.CreateNoWindow = true; psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch { }
        }
    }

    // ---------------------------------------------------------------- kurulum arayüzü
    class SetupForm : Form
    {
        TextBox dirBox; Switch desktopSw, startupSw, launchSw;
        Button install, close; Label status; Card card;

        public SetupForm()
        {
            AutoScaleDimensions = new SizeF(96f, 96f); AutoScaleMode = AutoScaleMode.Dpi;
            Text = L.T("setup.title"); Font = Theme.Body; DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(600, 520);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            PictureBox pic = new PictureBox(); pic.SetBounds(28, 26, 56, 56); pic.SizeMode = PictureBoxSizeMode.Zoom;
            try { using (Icon ic = new Icon(Icon, 48, 48)) pic.Image = ic.ToBitmap(); } catch { }
            Controls.Add(pic);
            Label t = new Label(); t.Text = "Resize Badger"; t.Font = Theme.H1; t.AutoSize = true; t.Location = new Point(98, 22); Controls.Add(t);
            Label s = new Label(); s.Text = L.T("setup.sub"); s.Tag = "dim"; s.AutoSize = true; s.Location = new Point(101, 62); Controls.Add(s);

            card = new Card(); card.SetBounds(28, 104, 544, 300); Controls.Add(card);
            Label fl = new Label(); fl.Text = L.T("setup.folder"); fl.Font = Theme.Semi; fl.AutoSize = true; fl.Location = new Point(20, 18); card.Controls.Add(fl);
            dirBox = new TextBox(); dirBox.Text = SetupApp.DefaultDir(); dirBox.SetBounds(20, 46, 400, 28); card.Controls.Add(dirBox);
            Button br = new Button(); br.Text = L.T("setup.browse"); br.SetBounds(430, 44, 94, 32); card.Controls.Add(br);
            br.Click += delegate
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = L.T("setup.folder");
                    if (d.ShowDialog() == DialogResult.OK) dirBox.Text = d.SelectedPath;
                }
            };
            desktopSw = new Switch(); desktopSw.Text = L.T("setup.desktop"); desktopSw.SetBounds(20, 96, 504, 30); card.Controls.Add(desktopSw);
            startupSw = new Switch(); startupSw.Text = L.T("gen.startup"); startupSw.SetBounds(20, 134, 504, 30); card.Controls.Add(startupSw);
            launchSw = new Switch(); launchSw.Text = L.T("setup.launch"); launchSw.Checked = true; launchSw.SetBounds(20, 172, 504, 30); card.Controls.Add(launchSw);
            Label note = new Label(); note.Text = L.T("setup.perUser"); note.Tag = "dim"; note.Font = Theme.Small; note.SetBounds(20, 218, 504, 60); card.Controls.Add(note);

            status = new Label(); status.Tag = "dim"; status.SetBounds(30, 420, 540, 24); Controls.Add(status);
            install = new Button(); install.Text = L.T("setup.install"); install.Tag = "primary"; install.SetBounds(340, 458, 140, 40); Controls.Add(install);
            close = new Button(); close.Text = L.T("btn.close"); close.SetBounds(490, 458, 82, 40); Controls.Add(close);
            close.Click += delegate { Close(); };
            install.Click += DoInstall; AcceptButton = install;
            Theme.Apply(this);
            ActiveControl = install;
        }

        void DoInstall(object s, EventArgs e)
        {
            install.Enabled = false; close.Enabled = false; card.Enabled = false;
            status.Text = L.T("setup.installing"); Application.DoEvents();
            string dir;
            try { dir = SetupApp.ResolveDir(dirBox.Text); }
            catch (Exception ex) { Fail(ex.Message); return; }
            string err = SetupApp.Install(dir, desktopSw.Checked, startupSw.Checked, launchSw.Checked);
            if (err != null) { Fail(err); return; }
            status.Tag = "accent"; status.Text = L.T("setup.done"); status.ForeColor = Theme.P.Accent;
            install.Visible = false; close.Enabled = true; AcceptButton = close;
        }

        void Fail(string msg)
        {
            status.Text = L.F("setup.fail", msg); status.ForeColor = Color.IndianRed;
            install.Enabled = true; close.Enabled = true; card.Enabled = true;
        }
    }
}
