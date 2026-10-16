# Resize Badger

Windows için hızlı pencere boyutlandırıcı ve NVIDIA Surround yöneticisi (tepsi uygulaması).

## Özellikler
- **Özel çözünürlük ön ayarları:** Pencereyi istediğiniz genişlik × yüksekliğe getirir (ekrandan büyük olabilir). Konum, ortalama, kenarlıksız ve "iç alan" seçenekleri vardır. Her ön ayara kısayol atanır.
- **Program kuralları:** Seçtiğiniz bir programın penceresi açılınca ön ayar otomatik uygulanır.
- **Otomatik mod, toplu uygulama, geri alma.**
- **NVIDIA Surround:** NVAPI ile tek kısayolla aç/kapat, 15 sn onay ve otomatik geri alma, kapanınca ana ekranı seçme.

## Kullanım
`ResizeBadger.exe` çalıştırılınca saatin yanında tepsi simgesi çıkar. Çift tık ayar penceresini açar. Ayarlar exe'nin yanındaki `settings.ini` dosyasında tutulur.

Varsayılan kısayollar: `Ctrl+Alt+1..4` ön ayarlar, `Ctrl+Alt+↑` tam ekran, `Ctrl+Alt+↓` geri al, `Ctrl+Alt+A` tümüne uygula, `Ctrl+Alt+Z` otomatik mod, `Ctrl+Alt+S` Surround.

## Derleme
Ek kurulum gerekmez; Windows'un hazır gelen .NET Framework 4 derleyicisi (`csc.exe`) kullanılır.

```bash
./build.sh [çıktı klasörü]      # WSL/Ubuntu'dan, csc.exe'yi interop ile çağırır
```

Windows'ta doğrudan derlemek için:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /optimize+ /codepage:65001 /platform:x64 `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:ResizeBadger.exe src\ResizeBadger.cs src\Surround.cs
```

Kod C# 5 uyumludur (`?.`, string interpolation vb. kullanılmaz).

## Notlar
- NVIDIA Surround için NVIDIA ekran kartı ve sürücüsü gerekir; ilk kullanımda Surround'u NVIDIA uygulamasından bir kez kurup "profil olarak kaydet"e basmak en sağlam yoldur.
- Yönetici olarak çalışan pencereleri boyutlandırmak için Resize Badger'ı da yönetici olarak çalıştırın.
