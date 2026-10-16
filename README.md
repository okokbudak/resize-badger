# Resize Badger

Windows için hızlı pencere boyutlandırıcı ve NVIDIA Surround yöneticisi (tepsi uygulaması).
*A fast window resizer and NVIDIA Surround manager for Windows (tray app).*

## Özellikler
- **Özel çözünürlük ön ayarları:** Pencereyi istediğiniz genişlik × yüksekliğe getirir (ekrandan büyük olabilir). Konum, ortalama, kenarlıksız ve "iç alan" seçenekleri; her ön ayara kısayol.
- **Program kuralları:** Seçtiğiniz programın penceresi açılınca ön ayar otomatik uygulanır.
- **Otomatik mod, toplu uygulama, geri alma.**
- **NVIDIA Surround:** NVAPI ile tek kısayolla aç/kapat, 15 sn onay ve otomatik geri alma, kapanınca ana ekranı seçme.
- **Modern arayüz:** Açık / Koyu / **Windows teması** (açık-koyu ve vurgu rengini Windows'tan alır, değişince kendisi de değişir).
- **Dil:** Türkçe, English, Deutsch, Español, Français (varsayılan: Windows dili).
- **Windows ile başlat** seçeneği (ayarlardan, tepsi menüsünden ya da kurulum sırasında).

## Kurulum
`ResizeBadger-Setup.exe` dosyasını çalıştırın. Yönetici izni gerekmez (kullanıcı başına kurulum).

Kaldırma (Ayarlar → Uygulamalar ya da Başlat menüsündeki kısayol) oluşturulan **her şeyi** siler: klasör (ayarlar dahil), kısayollar, başlangıç kaydı, "Uygulamalar" girdisi ve Windows'un bu program için tuttuğu kullanım izleri.

Sessiz kurulum / kaldırma:
```
ResizeBadger-Setup.exe /S [/D=klasör] [/desktop] [/startup] [/norun]
"<kurulum klasörü>\Uninstall.exe" /uninstall /S
```
Taşınabilir kullanım için `ResizeBadger.exe` tek başına da çalışır; ayarlar exe'nin yanındaki `settings.ini` dosyasında tutulur.

Varsayılan kısayollar: `Ctrl+Alt+1..4` ön ayarlar, `Ctrl+Alt+↑` tam ekran, `Ctrl+Alt+↓` geri al, `Ctrl+Alt+A` tümüne uygula, `Ctrl+Alt+Z` otomatik mod, `Ctrl+Alt+S` Surround.

## Derleme
Ek kurulum gerekmez; Windows'un hazır gelen .NET Framework 4 derleyicisi (`csc.exe`) kullanılır. Kod C# 5 uyumludur (`?.`, string interpolation vb. yok).

```bash
bash build.sh [çıktı klasörü]    # WSL/Ubuntu'dan: ResizeBadger.exe + ResizeBadger-Setup.exe
```

Simgeyi yeniden üretmek için `tools/MakeIcon.cs` (çıktı: `src/app.ico`).

## Notlar
- NVIDIA Surround için NVIDIA ekran kartı ve sürücüsü gerekir; ilk kullanımda Surround'u NVIDIA uygulamasından bir kez kurup "profil olarak kaydet"e basmak en sağlam yoldur.
- Yönetici olarak çalışan pencereleri boyutlandırmak için Resize Badger'ı da yönetici olarak çalıştırın.
