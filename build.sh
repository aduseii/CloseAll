#!/bin/sh
# Builds build/CloseAll.exe (needs Mono's mcs; on Windows use build.bat).
set -e
cd "$(dirname "$0")"
mkdir -p build
mcs -nologo -target:winexe -platform:anycpu -optimize+ -sdk:4.5 \
  -out:build/CloseAll.exe -win32icon:src/app.ico \
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll \
  src/*.cs
echo "Built build/CloseAll.exe"
