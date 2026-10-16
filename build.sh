#!/bin/bash
# Resize Badger derleme betiği (Ubuntu'dan). Derleyici: Windows'un hazır gelen .NET Framework csc.exe dosyası (WSL interop).
# Kullanım: ./build.sh [çıktı klasörü]   (varsayılan: Windows teslim klasörü)
set -euo pipefail

ROOT="/home/orhan/projects/resize-badger"
CSC="/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
OUT_DIR="${1:-/mnt/d/OKWorks/Ai Projects/Resize Badger}"

cd "$ROOT"
[ "$(pwd)" = "$ROOT" ] || { echo "HATA: yanlış klasör: $(pwd)"; exit 1; }
[ -x "$CSC" ] || { echo "HATA: csc.exe bulunamadı: $CSC"; exit 1; }
[ -d "$OUT_DIR" ] || { echo "HATA: çıktı klasörü yok: $OUT_DIR"; exit 1; }

OUT_WIN="$(wslpath -w "$OUT_DIR")\\ResizeBadger.exe"
"$CSC" /nologo /target:winexe /optimize+ /codepage:65001 /platform:x64 \
  /r:System.Windows.Forms.dll /r:System.Drawing.dll \
  "/out:$OUT_WIN" \
  "$(wslpath -w "$ROOT/src/ResizeBadger.cs")" "$(wslpath -w "$ROOT/src/Surround.cs")"
echo "OK -> $OUT_DIR/ResizeBadger.exe"
