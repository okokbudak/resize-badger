#!/bin/bash
# Resize Badger derleme betiği (Ubuntu'dan). Derleyici: Windows'un hazır gelen .NET Framework csc.exe dosyası (WSL interop).
# Üretir: ResizeBadger.exe (uygulama) ve ResizeBadger-Setup.exe (kurulum/kaldırma programı).
# Kullanım: ./build.sh [çıktı klasörü]   (varsayılan: Windows teslim klasörü)
set -euo pipefail

ROOT="/home/orhan/projects/resize-badger"
CSC="/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
OUT_DIR="${1:-/mnt/d/OKWorks/Ai Projects/Resize Badger}"

cd "$ROOT"
[ "$(pwd)" = "$ROOT" ] || { echo "HATA: yanlış klasör: $(pwd)"; exit 1; }
[ -x "$CSC" ] || { echo "HATA: csc.exe bulunamadı: $CSC"; exit 1; }
[ -d "$OUT_DIR" ] || { echo "HATA: çıktı klasörü yok: $OUT_DIR"; exit 1; }

w() { wslpath -w "$1"; }          # Linux yolu -> Windows yolu
OUT_WIN="$(w "$OUT_DIR")"
APP_EXE="$OUT_WIN\\ResizeBadger.exe"
SETUP_EXE="$OUT_WIN\\ResizeBadger-Setup.exe"
ICON="$(w "$ROOT/src/app.ico")"
COMMON=(/nologo /target:winexe /optimize+ /codepage:65001 /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/win32icon:$ICON")

# 1) uygulama
APP_FILES=()
for f in Native Config WindowManager Surround App SettingsForm Strings Theme Controls; do APP_FILES+=("$(w "$ROOT/src/$f.cs")"); done
"$CSC" "${COMMON[@]}" "/out:$APP_EXE" "${APP_FILES[@]}"
echo "OK -> $OUT_DIR/ResizeBadger.exe"

# 2) kurulum / kaldırma programı (uygulamayı kaynak olarak gömer)
SETUP_FILES=("$(w "$ROOT/setup/Setup.cs")")
for f in Strings Theme Controls; do SETUP_FILES+=("$(w "$ROOT/src/$f.cs")"); done
"$CSC" "${COMMON[@]}" "/out:$SETUP_EXE" "/resource:$APP_EXE,ResizeBadger.exe" "${SETUP_FILES[@]}"
echo "OK -> $OUT_DIR/ResizeBadger-Setup.exe"
