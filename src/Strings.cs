// Çok dilli metinler: Türkçe, İngilizce, Almanca, İspanyolca, Fransızca. Uygulama ve kurulum programı ortak kullanır.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ResizeBadger
{
    static class Ver
    {
        public const string V = "1.1.0";
        public const string Repo = "github.com/okokbudak/resize-badger";
    }

    static class L
    {
        public static readonly string[] Codes = { "tr", "en", "de", "es", "fr" };
        public static readonly string[] Names = { "Türkçe", "English", "Deutsch", "Español", "Français" };
        static readonly Dictionary<string, string[]> D = new Dictionary<string, string[]>();
        static int idx = 1;

        // "auto" ise Windows görüntü dili; desteklenmiyorsa İngilizce
        public static void Set(string code)
        {
            if (string.IsNullOrEmpty(code) || code == "auto")
                code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            int i = Array.IndexOf(Codes, code);
            idx = i < 0 ? 1 : i;
        }
        public static string Code { get { return Codes[idx]; } }

        public static string T(string key)
        {
            string[] v;
            return D.TryGetValue(key, out v) ? v[idx] : key;
        }
        public static string F(string key, params object[] a) { return string.Format(T(key), a); }

        static void A(string k, string tr, string en, string de, string es, string fr) { D[k] = new string[] { tr, en, de, es, fr }; }

        static L()
        {
            // ---- gezinme
            A("nav.windows", "Pencereler", "Windows", "Fenster", "Ventanas", "Fenêtres");
            A("nav.rules", "Program kuralları", "App rules", "Programmregeln", "Reglas de programas", "Règles par programme");
            A("nav.surround", "NVIDIA Surround", "NVIDIA Surround", "NVIDIA Surround", "NVIDIA Surround", "NVIDIA Surround");
            A("nav.general", "Genel", "General", "Allgemein", "General", "Général");

            // ---- eylemler (kısayol listesi)
            A("act.fill", "Aktif pencereyi tam ekran yap (maximize)", "Maximize the active window", "Aktives Fenster maximieren", "Maximizar la ventana activa", "Agrandir la fenêtre active");
            A("act.restore", "Aktif pencereyi eski haline getir", "Restore the active window", "Aktives Fenster wiederherstellen", "Restaurar la ventana activa", "Restaurer la fenêtre active");
            A("act.all", "Tüm pencerelere otomatik ayarı uygula", "Apply the auto setting to all windows", "Auto-Einstellung auf alle Fenster anwenden", "Aplicar el ajuste automático a todas las ventanas", "Appliquer le réglage auto à toutes les fenêtres");
            A("act.auto", "Otomatik uygulamayı aç/kapat", "Toggle auto-apply", "Automatisches Anwenden ein/aus", "Activar/desactivar aplicación automática", "Activer/désactiver l'application auto");
            A("act.surround", "NVIDIA Surround aç/kapat", "Toggle NVIDIA Surround", "NVIDIA Surround ein/aus", "Activar/desactivar NVIDIA Surround", "Activer/désactiver NVIDIA Surround");

            // ---- ortak
            A("btn.save", "Kaydet", "Save", "Speichern", "Guardar", "Enregistrer");
            A("btn.close", "Kapat", "Close", "Schließen", "Cerrar", "Fermer");
            A("btn.add", "+ Ekle", "+ Add", "+ Hinzufügen", "+ Añadir", "+ Ajouter");
            A("btn.delete", "Sil", "Delete", "Löschen", "Eliminar", "Supprimer");
            A("bar.hint", "Değişiklikler 'Kaydet' ile geçerli olur.", "Changes apply when you click Save.", "Änderungen gelten nach „Speichern“.", "Los cambios se aplican al pulsar Guardar.", "Les modifications s'appliquent après Enregistrer.");
            A("msg.saved", "Ayarlar kaydedildi.", "Settings saved.", "Einstellungen gespeichert.", "Ajustes guardados.", "Paramètres enregistrés.");

            // ---- Pencereler sayfası
            A("win.desc", "Pencereleri istediğiniz çözünürlüğe getirin.", "Resize windows to the exact resolution you want.", "Fenster auf genau die gewünschte Auflösung bringen.", "Redimensione las ventanas a la resolución exacta que quiera.", "Redimensionnez les fenêtres à la résolution exacte souhaitée.");
            A("win.presets", "Çözünürlük ön ayarları", "Resolution presets", "Auflösungs-Voreinstellungen", "Ajustes de resolución", "Préréglages de résolution");
            A("win.presets.hint", "Kısayol hücresine tıklayıp tuşlara basın (Del temizler). Ortala işaretliyse X/Y yok sayılır. İç alan: ölçü pencere kenarlığı hariç.", "Click a shortcut cell and press the keys (Del clears). If Center is on, X/Y are ignored. Client area: size excludes the window frame.", "Kürzel-Zelle anklicken und Tasten drücken (Entf löscht). Bei „Zentrieren“ werden X/Y ignoriert. Innenbereich: Größe ohne Fensterrahmen.", "Haga clic en el atajo y pulse las teclas (Supr borra). Con Centrar, X/Y se ignoran. Área interior: tamaño sin el marco.", "Cliquez sur un raccourci et appuyez sur les touches (Suppr efface). Avec Centrer, X/Y sont ignorés. Zone client : taille sans le cadre.");
            A("win.global", "Genel kısayollar", "Global shortcuts", "Globale Tastenkürzel", "Atajos globales", "Raccourcis globaux");
            A("win.auto", "Otomatik uygulama", "Auto-apply", "Automatisch anwenden", "Aplicación automática", "Application automatique");
            A("win.autoPreset", "Otomatik / toplu modda uygulanacak ayar", "Setting used by auto / bulk mode", "Einstellung für Auto-/Sammelmodus", "Ajuste para el modo automático / masivo", "Réglage du mode auto / groupé");
            A("win.autoApply", "Yeni açılan her pencereye otomatik uygula", "Automatically apply to every new window", "Auf jedes neue Fenster automatisch anwenden", "Aplicar automáticamente a cada ventana nueva", "Appliquer automatiquement à chaque nouvelle fenêtre");
            A("win.exclude", "Otomatik modda dokunulmayacak programlar (; ile ayırın)", "Programs left alone in auto mode (separate with ;)", "Programme, die im Auto-Modus ausgelassen werden (mit ; trennen)", "Programas que se ignoran en modo automático (separar con ;)", "Programmes ignorés en mode auto (séparer par ;)");
            A("preset.maximize", "Tam ekran (maximize)", "Maximize", "Maximieren", "Maximizar", "Plein écran (agrandir)");
            A("col.name", "Ad", "Name", "Name", "Nombre", "Nom");
            A("col.width", "Genişlik", "Width", "Breite", "Ancho", "Largeur");
            A("col.height", "Yükseklik", "Height", "Höhe", "Alto", "Hauteur");
            A("col.center", "Ortala", "Center", "Mitte", "Centrar", "Centrer");
            A("col.borderless", "Kenarlıksız", "Borderless", "Rahmenlos", "Sin bordes", "Sans cadre");
            A("col.inner", "İç alan", "Client area", "Innen", "Interior", "Intérieur");
            A("col.hotkey", "Kısayol", "Shortcut", "Tastenkürzel", "Atajo", "Raccourci");
            A("preset.new", "Yeni", "New", "Neu", "Nuevo", "Nouveau");

            // ---- Program kuralları
            A("rules.desc", "Seçtiğiniz programın penceresi açılınca ön ayar otomatik uygulanır.", "When a window of the chosen program opens, a preset is applied automatically.", "Öffnet sich ein Fenster des gewählten Programms, wird die Voreinstellung automatisch angewendet.", "Cuando se abre una ventana del programa elegido, se aplica el ajuste automáticamente.", "Quand une fenêtre du programme choisi s'ouvre, le préréglage est appliqué automatiquement.");
            A("col.enabled", "Etkin", "On", "Aktiv", "Activa", "Actif");
            A("col.program", "Program (exe adı)", "Program (exe name)", "Programm (exe-Name)", "Programa (nombre del exe)", "Programme (nom de l'exe)");
            A("col.preset", "Uygulanacak ön ayar", "Preset to apply", "Anzuwendende Voreinstellung", "Ajuste a aplicar", "Préréglage à appliquer");
            A("rules.pickRunning", "Çalışan programlardan seç…", "Pick from running programs…", "Aus laufenden Programmen wählen…", "Elegir de los programas en ejecución…", "Choisir parmi les programmes en cours…");
            A("rules.pickFile", "Dosyadan seç (.exe)…", "Pick a file (.exe)…", "Datei wählen (.exe)…", "Elegir archivo (.exe)…", "Choisir un fichier (.exe)…");
            A("rules.note", "Ön ayarlar Pencereler sayfasındaki listeden gelir. Kural, genel otomatik moddan önce gelir. Program zaten açıksa 'Kaydet' dediğinizde mevcut penceresine de uygulanır. Program adını hücreye elle de yazabilirsiniz (ör. notepad).", "Presets come from the list on the Windows page. A rule takes precedence over the global auto mode. If the program is already open, Save also applies it to its current window. You can also type the program name in the cell (e.g. notepad).", "Voreinstellungen stammen aus der Liste auf der Seite „Fenster“. Eine Regel hat Vorrang vor dem globalen Auto-Modus. Ist das Programm schon offen, wird sie beim Speichern auch auf dessen Fenster angewendet. Den Programmnamen können Sie auch direkt eintippen (z. B. notepad).", "Los ajustes provienen de la lista de la página Ventanas. Una regla tiene prioridad sobre el modo automático global. Si el programa ya está abierto, Guardar también la aplica a su ventana. También puede escribir el nombre del programa en la celda (p. ej. notepad).", "Les préréglages viennent de la liste de la page Fenêtres. Une règle prime sur le mode auto global. Si le programme est déjà ouvert, Enregistrer l'applique aussi à sa fenêtre. Vous pouvez aussi saisir le nom du programme dans la cellule (ex. notepad).");
            A("rules.pickTitle", "Çalışan programlar", "Running programs", "Laufende Programme", "Programas en ejecución", "Programmes en cours");
            A("rules.colProgram", "Program", "Program", "Programm", "Programa", "Programme");
            A("rules.colTitle", "Pencere başlığı", "Window title", "Fenstertitel", "Título de la ventana", "Titre de la fenêtre");
            A("rules.add", "Ekle", "Add", "Hinzufügen", "Añadir", "Ajouter");
            A("rules.cancel", "İptal", "Cancel", "Abbrechen", "Cancelar", "Annuler");
            A("rules.fileTitle", "Boyutlandırılacak programı seçin", "Choose the program to resize", "Programm zum Anpassen wählen", "Elija el programa a redimensionar", "Choisissez le programme à redimensionner");
            A("rules.fileFilter", "Program", "Program", "Programm", "Programa", "Programme");

            // ---- Surround sayfası
            A("sur.desc", "NVIDIA uygulamasını beklemeden Surround'u aç/kapat.", "Turn Surround on/off without waiting for the NVIDIA app.", "Surround ein-/ausschalten, ohne auf die NVIDIA-App zu warten.", "Active/desactive Surround sin esperar a la app de NVIDIA.", "Activez/désactivez Surround sans attendre l'application NVIDIA.");
            A("sur.status", "Durum", "Status", "Status", "Estado", "État");
            A("sur.on", "Surround'u AÇ", "Turn Surround ON", "Surround EINSCHALTEN", "ACTIVAR Surround", "ACTIVER Surround");
            A("sur.off", "Surround'u KAPAT", "Turn Surround OFF", "Surround AUSSCHALTEN", "DESACTIVAR Surround", "DÉSACTIVER Surround");
            A("sur.refresh", "Durumu yenile", "Refresh status", "Status aktualisieren", "Actualizar estado", "Actualiser l'état");
            A("sur.options", "Seçenekler", "Options", "Optionen", "Opciones", "Options");
            A("sur.hotkey", "Aç/kapat kısayolu", "On/off shortcut", "Ein/Aus-Tastenkürzel", "Atajo de activar/desactivar", "Raccourci activer/désactiver");
            A("sur.confirm", "Değişiklikten sonra onay iste (15 sn içinde onaylamazsam otomatik geri al)", "Ask for confirmation after changing (reverts automatically after 15 s)", "Nach der Änderung bestätigen lassen (nach 15 s automatisch rückgängig)", "Pedir confirmación tras el cambio (se revierte solo a los 15 s)", "Demander confirmation après le changement (annulation auto après 15 s)");
            A("sur.primary", "Surround kapanınca ana ekran (soldan kaçıncı ekran)", "Primary display after Surround turns off (Nth from left)", "Hauptbildschirm nach dem Ausschalten (n-ter von links)", "Pantalla principal al desactivar Surround (n.º desde la izquierda)", "Écran principal après désactivation (n-ième depuis la gauche)");
            A("sur.saveProfile", "Şu anki Surround'u profil olarak kaydet", "Save the current Surround as a profile", "Aktuelles Surround als Profil speichern", "Guardar el Surround actual como perfil", "Enregistrer le Surround actuel comme profil");
            A("sur.profileSaved", "Profil kaydedildi. Bundan sonra 'AÇ' bu düzeni birebir kullanır.", "Profile saved. 'Turn ON' will now use this exact layout.", "Profil gespeichert. „Einschalten“ verwendet nun genau dieses Layout.", "Perfil guardado. «Activar» usará este diseño exacto.", "Profil enregistré. « Activer » utilisera désormais cette disposition exacte.");
            A("sur.note", "İpucu: Surround'u bir kez NVIDIA uygulamasından kurup profil olarak kaydetmek en sağlam yoldur. Profil yoksa ekranlar, Windows'taki soldan sağa sıraya göre tek satırlık Surround olarak kurulmaya çalışılır. Geçiş sırasında ekranlar birkaç saniye yanıp söner.", "Tip: the most reliable way is to set up Surround once in the NVIDIA app and save it as a profile. Without a profile, displays are arranged as a single row, left to right as in Windows. Screens flicker for a few seconds while switching.", "Tipp: Am zuverlässigsten richten Sie Surround einmal in der NVIDIA-App ein und speichern es als Profil. Ohne Profil werden die Bildschirme als eine Reihe von links nach rechts wie in Windows angeordnet. Beim Umschalten flackern die Bildschirme kurz.", "Consejo: lo más fiable es configurar Surround una vez en la app de NVIDIA y guardarlo como perfil. Sin perfil, las pantallas se disponen en una fila, de izquierda a derecha como en Windows. Las pantallas parpadean unos segundos al cambiar.", "Astuce : le plus fiable est de configurer Surround une fois dans l'application NVIDIA et de l'enregistrer comme profil. Sans profil, les écrans sont disposés en une rangée, de gauche à droite comme dans Windows. Les écrans clignotent quelques secondes pendant le changement.");
            A("sur.state.on", "Surround AÇIK: {0}x{1} ızgara, {2} ekran, ekran başına {3}x{4} @ {5} Hz (toplam {6}x{7})", "Surround is ON: {0}x{1} grid, {2} displays, {3}x{4} @ {5} Hz each (total {6}x{7})", "Surround ist AN: {0}x{1}-Raster, {2} Bildschirme, je {3}x{4} @ {5} Hz (gesamt {6}x{7})", "Surround ACTIVADO: cuadrícula {0}x{1}, {2} pantallas, {3}x{4} @ {5} Hz cada una (total {6}x{7})", "Surround ACTIVÉ : grille {0}x{1}, {2} écrans, {3}x{4} @ {5} Hz chacun (total {6}x{7})");
            A("sur.state.off", "Surround KAPALI ({0} ayrı ekran görünüyor)", "Surround is OFF ({0} separate displays)", "Surround ist AUS ({0} einzelne Bildschirme)", "Surround DESACTIVADO ({0} pantallas independientes)", "Surround DÉSACTIVÉ ({0} écrans distincts)");
            A("sur.err.init", "NVAPI başlatılamadı (NVIDIA ekran kartı/sürücüsü yok mu?)", "NVAPI could not start (no NVIDIA GPU/driver?)", "NVAPI konnte nicht gestartet werden (keine NVIDIA-GPU/-Treiber?)", "No se pudo iniciar NVAPI (¿sin GPU/controlador NVIDIA?)", "Impossible de démarrer NVAPI (pas de GPU/pilote NVIDIA ?)");
            A("sur.err.dll", "nvapi64.dll bulunamadı (NVIDIA sürücüsü kurulu değil).", "nvapi64.dll not found (NVIDIA driver not installed).", "nvapi64.dll nicht gefunden (NVIDIA-Treiber nicht installiert).", "No se encontró nvapi64.dll (controlador NVIDIA no instalado).", "nvapi64.dll introuvable (pilote NVIDIA non installé).");
            A("sur.err.api", "Bu sürücü Surround (Mosaic) API'sini sunmuyor.", "This driver does not provide the Surround (Mosaic) API.", "Dieser Treiber bietet die Surround-(Mosaic-)API nicht an.", "Este controlador no ofrece la API de Surround (Mosaic).", "Ce pilote ne fournit pas l'API Surround (Mosaic).");
            A("sur.err.nvapi", "NVAPI hatası: {0}", "NVAPI error: {0}", "NVAPI-Fehler: {0}", "Error de NVAPI: {0}", "Erreur NVAPI : {0}");
            A("sur.err.needActive", "Önce Surround'u etkinleştirin (NVIDIA uygulamasıyla), sonra kaydedin.", "Enable Surround first (with the NVIDIA app), then save.", "Aktivieren Sie zuerst Surround (mit der NVIDIA-App) und speichern Sie dann.", "Active Surround primero (con la app de NVIDIA) y luego guarde.", "Activez d'abord Surround (avec l'application NVIDIA), puis enregistrez.");
            A("sur.err.min2", "Surround için en az 2 ekran gerekli.", "Surround needs at least 2 displays.", "Für Surround sind mindestens 2 Bildschirme nötig.", "Surround necesita al menos 2 pantallas.", "Surround nécessite au moins 2 écrans.");
            A("sur.err.ids", "Ekranların NVIDIA kimlikleri bulunamadı.", "Could not find the NVIDIA IDs of the displays.", "NVIDIA-IDs der Bildschirme nicht gefunden.", "No se encontraron los ID NVIDIA de las pantallas.", "Identifiants NVIDIA des écrans introuvables.");
            A("sur.err.set", "NVAPI SetDisplayGrids hata kodu: {0}", "NVAPI SetDisplayGrids error code: {0}", "NVAPI-SetDisplayGrids-Fehlercode: {0}", "Código de error de NVAPI SetDisplayGrids: {0}", "Code d'erreur NVAPI SetDisplayGrids : {0}");
            A("sur.err.timeout", "Sürücü değişikliği zamanında uygulamadı.", "The driver did not apply the change in time.", "Der Treiber hat die Änderung nicht rechtzeitig übernommen.", "El controlador no aplicó el cambio a tiempo.", "Le pilote n'a pas appliqué le changement à temps.");
            A("msg.sur.on", "Surround AÇILDI", "Surround turned ON", "Surround eingeschaltet", "Surround ACTIVADO", "Surround ACTIVÉ");
            A("msg.sur.off", "Surround KAPATILDI", "Surround turned OFF", "Surround ausgeschaltet", "Surround DESACTIVADO", "Surround DÉSACTIVÉ");
            A("msg.sur.reverted", "Geri alındı.", "Reverted.", "Rückgängig gemacht.", "Revertido.", "Annulé.");
            A("msg.sur.revertFail", "Geri alınamadı: {0}", "Could not revert: {0}", "Rückgängig fehlgeschlagen: {0}", "No se pudo revertir: {0}", "Annulation impossible : {0}");
            A("dlg.sur.msg", "{0}. Ekran düzgünse 'Tut'a basın.\n{1} sn içinde yanıt vermezseniz otomatik geri alınır.", "{0}. If the screen looks right, click Keep.\nIt reverts automatically in {1} s if you do not respond.", "{0}. Wenn der Bildschirm in Ordnung ist, klicken Sie auf „Behalten“.\nOhne Antwort wird in {1} s automatisch zurückgesetzt.", "{0}. Si la pantalla se ve bien, pulse Mantener.\nSi no responde, se revierte automáticamente en {1} s.", "{0}. Si l'écran est correct, cliquez sur Garder.\nSans réponse, annulation automatique dans {1} s.");
            A("dlg.keep", "Tut", "Keep", "Behalten", "Mantener", "Garder");
            A("dlg.undo", "Geri al", "Undo", "Rückgängig", "Deshacer", "Annuler");

            // ---- Genel sayfa
            A("gen.desc", "Görünüm, dil ve başlangıç ayarları.", "Appearance, language and startup.", "Darstellung, Sprache und Autostart.", "Apariencia, idioma e inicio.", "Apparence, langue et démarrage.");
            A("gen.language", "Dil", "Language", "Sprache", "Idioma", "Langue");
            A("gen.langAuto", "Windows diliyle aynı", "Same as Windows", "Wie Windows", "Igual que Windows", "Comme Windows");
            A("gen.theme", "Tema", "Theme", "Design", "Tema", "Thème");
            A("theme.system", "Windows teması (otomatik)", "Windows theme (automatic)", "Windows-Design (automatisch)", "Tema de Windows (automático)", "Thème Windows (automatique)");
            A("theme.light", "Açık", "Light", "Hell", "Claro", "Clair");
            A("theme.dark", "Koyu", "Dark", "Dunkel", "Oscuro", "Sombre");
            A("gen.accent", "Vurgu rengi ve açık/koyu seçimi Windows'tan alınır; Windows teması değişince uygulama da değişir.", "The accent color and light/dark choice come from Windows and follow it when it changes.", "Akzentfarbe und Hell/Dunkel stammen von Windows und folgen Änderungen.", "El color de énfasis y el modo claro/oscuro vienen de Windows y lo siguen cuando cambia.", "La couleur d'accentuation et le mode clair/sombre viennent de Windows et le suivent.");
            A("gen.startup", "Windows ile birlikte başlat", "Start with Windows", "Mit Windows starten", "Iniciar con Windows", "Démarrer avec Windows");
            A("gen.startupHint", "Oturum açıldığında Resize Badger sessizce tepsiye yerleşir.", "Resize Badger starts quietly in the tray when you sign in.", "Resize Badger startet beim Anmelden still im Infobereich.", "Resize Badger se inicia en silencio en la bandeja al iniciar sesión.", "Resize Badger démarre discrètement dans la zone de notification à l'ouverture de session.");
            A("gen.appearance", "Görünüm ve dil", "Appearance and language", "Darstellung und Sprache", "Apariencia e idioma", "Apparence et langue");
            A("gen.start", "Başlangıç", "Startup", "Autostart", "Inicio", "Démarrage");
            A("gen.about", "Hakkında", "About", "Info", "Acerca de", "À propos");
            A("gen.version", "Sürüm {0}", "Version {0}", "Version {0}", "Versión {0}", "Version {0}");

            // ---- tepsi menüsü ve bildirimler
            A("menu.maximize", "Tam ekran yap", "Maximize", "Maximieren", "Maximizar", "Agrandir");
            A("menu.restore", "Eski haline getir", "Restore", "Wiederherstellen", "Restaurar", "Restaurer");
            A("menu.applyAll", "Tümüne uygula", "Apply to all", "Auf alle anwenden", "Aplicar a todas", "Appliquer à toutes");
            A("menu.restoreAll", "Hepsini eski haline getir", "Restore all", "Alle wiederherstellen", "Restaurar todas", "Tout restaurer");
            A("menu.auto", "Otomatik uygula", "Auto-apply", "Automatisch anwenden", "Aplicación automática", "Application automatique");
            A("menu.startup", "Windows ile başlat", "Start with Windows", "Mit Windows starten", "Iniciar con Windows", "Démarrer avec Windows");
            A("menu.settings", "Ayarlar…", "Settings…", "Einstellungen…", "Ajustes…", "Paramètres…");
            A("menu.exit", "Çıkış", "Exit", "Beenden", "Salir", "Quitter");
            A("msg.autoOn", "Otomatik uygulama AÇIK", "Auto-apply is ON", "Automatisches Anwenden ist AN", "Aplicación automática ACTIVADA", "Application automatique ACTIVÉE");
            A("msg.autoOff", "Otomatik uygulama KAPALI", "Auto-apply is OFF", "Automatisches Anwenden ist AUS", "Aplicación automática DESACTIVADA", "Application automatique DÉSACTIVÉE");
            A("msg.hotkeyTaken", "Kısayol başka program tarafından kullanılıyor: {0}", "Shortcut already used by another program: {0}", "Tastenkürzel wird von einem anderen Programm verwendet: {0}", "Atajo en uso por otro programa: {0}", "Raccourci déjà utilisé par un autre programme : {0}");
            A("hk.altgr", "Ctrl+Alt, AltGr tuşuyla aynıdır ve #, $, @ gibi karakterlerin yazılmasını engeller. Ctrl+Shift, Win ya da F tuşları ile bir birleşim seçin.", "Ctrl+Alt is the same as AltGr and would block typing characters such as #, $, @. Choose a combination with Ctrl+Shift, Win or F-keys instead.", "Strg+Alt entspricht AltGr und würde Zeichen wie #, $, @ blockieren. Wählen Sie eine Kombination mit Strg+Umschalt, Win oder F-Tasten.", "Ctrl+Alt equivale a AltGr y impediría escribir caracteres como #, $, @. Elija una combinación con Ctrl+Mayús, Win o teclas F.", "Ctrl+Alt équivaut à AltGr et empêcherait de saisir des caractères comme #, $, @. Choisissez une combinaison avec Ctrl+Maj, Win ou les touches F.");
            A("msg.hotkeysMigrated", "Ctrl+Alt kısayolları AltGr ile çakıştığı için Ctrl+Shift+F1…F9 olarak değiştirildi (Ayarlar'dan düzenleyebilirsiniz).", "Ctrl+Alt shortcuts clashed with AltGr and were changed to Ctrl+Shift+F1…F9 (editable in Settings).", "Strg+Alt-Kürzel kollidierten mit AltGr und wurden auf Strg+Umschalt+F1…F9 geändert (in den Einstellungen anpassbar).", "Los atajos Ctrl+Alt chocaban con AltGr y se cambiaron a Ctrl+Mayús+F1…F9 (editables en Ajustes).", "Les raccourcis Ctrl+Alt entraient en conflit avec AltGr et sont devenus Ctrl+Maj+F1…F9 (modifiables dans les Paramètres).");
            A("msg.elevated", "Bu pencere yönetici olarak çalışıyor. Resize Badger'ı da 'Yönetici olarak çalıştır' ile açın.", "This window runs as administrator. Start Resize Badger with 'Run as administrator' too.", "Dieses Fenster läuft als Administrator. Starten Sie Resize Badger ebenfalls „Als Administrator“.", "Esta ventana se ejecuta como administrador. Inicie también Resize Badger como administrador.", "Cette fenêtre s'exécute en administrateur. Lancez aussi Resize Badger en tant qu'administrateur.");
            A("msg.rulePresetMissing", "'{0}' kuralındaki ön ayar bulunamadı: {1}", "The preset of the '{0}' rule was not found: {1}", "Die Voreinstellung der Regel „{0}“ wurde nicht gefunden: {1}", "No se encontró el ajuste de la regla «{0}»: {1}", "Le préréglage de la règle « {0} » est introuvable : {1}");

            // ---- kurulum / kaldırma
            A("setup.title", "Resize Badger kurulumu", "Resize Badger Setup", "Resize Badger Setup", "Instalación de Resize Badger", "Installation de Resize Badger");
            A("setup.sub", "Hızlı pencere boyutlandırıcı ve NVIDIA Surround yöneticisi", "Fast window resizer and NVIDIA Surround manager", "Schneller Fenster-Resizer und NVIDIA-Surround-Manager", "Redimensionador rápido de ventanas y gestor de NVIDIA Surround", "Redimensionneur de fenêtres rapide et gestionnaire NVIDIA Surround");
            A("setup.folder", "Kurulum klasörü", "Install folder", "Installationsordner", "Carpeta de instalación", "Dossier d'installation");
            A("setup.browse", "Gözat…", "Browse…", "Durchsuchen…", "Examinar…", "Parcourir…");
            A("setup.desktop", "Masaüstü kısayolu oluştur", "Create a desktop shortcut", "Desktop-Verknüpfung erstellen", "Crear acceso directo en el escritorio", "Créer un raccourci sur le bureau");
            A("setup.launch", "Kurulumdan sonra çalıştır", "Launch after installing", "Nach der Installation starten", "Ejecutar tras instalar", "Lancer après l'installation");
            A("setup.install", "Kur", "Install", "Installieren", "Instalar", "Installer");
            A("setup.installing", "Kuruluyor…", "Installing…", "Wird installiert…", "Instalando…", "Installation…");
            A("setup.done", "Kurulum tamamlandı.", "Installation complete.", "Installation abgeschlossen.", "Instalación completada.", "Installation terminée.");
            A("setup.fail", "Kurulum başarısız: {0}", "Installation failed: {0}", "Installation fehlgeschlagen: {0}", "La instalación falló: {0}", "Échec de l'installation : {0}");
            A("setup.perUser", "Yönetici izni gerekmez; yalnızca bu kullanıcı için kurulur. Kaldırma, oluşturulan her şeyi siler.", "No administrator rights needed; installs for the current user only. Uninstalling removes everything it created.", "Keine Administratorrechte nötig; nur für den aktuellen Benutzer. Beim Deinstallieren wird alles Erstellte entfernt.", "No requiere permisos de administrador; solo para el usuario actual. Al desinstalar se elimina todo lo creado.", "Aucun droit administrateur requis ; pour l'utilisateur actuel uniquement. La désinstallation supprime tout ce qui a été créé.");
            A("setup.shortcut", "Resize Badger'ı kaldır", "Uninstall Resize Badger", "Resize Badger deinstallieren", "Desinstalar Resize Badger", "Désinstaller Resize Badger");
            A("un.title", "Resize Badger'ı kaldır", "Uninstall Resize Badger", "Resize Badger deinstallieren", "Desinstalar Resize Badger", "Désinstaller Resize Badger");
            A("un.confirm", "Resize Badger; ayarları, kısayolları ve kayıt defteri girdileriyle birlikte bilgisayarınızdan tamamen kaldırılacak. Devam edilsin mi?", "Resize Badger will be completely removed from this computer, including its settings, shortcuts and registry entries. Continue?", "Resize Badger wird samt Einstellungen, Verknüpfungen und Registrierungseinträgen vollständig von diesem Computer entfernt. Fortfahren?", "Resize Badger se eliminará por completo de este equipo, incluidos sus ajustes, accesos directos y entradas del registro. ¿Continuar?", "Resize Badger sera entièrement supprimé de cet ordinateur, avec ses paramètres, raccourcis et entrées de registre. Continuer ?");
            A("un.done", "Resize Badger tamamen kaldırıldı.", "Resize Badger has been completely removed.", "Resize Badger wurde vollständig entfernt.", "Resize Badger se ha eliminado por completo.", "Resize Badger a été entièrement supprimé.");
            A("un.fail", "Bazı öğeler silinemedi:\n{0}", "Some items could not be removed:\n{0}", "Einige Elemente konnten nicht entfernt werden:\n{0}", "No se pudieron eliminar algunos elementos:\n{0}", "Certains éléments n'ont pas pu être supprimés :\n{0}");
        }
    }
}
