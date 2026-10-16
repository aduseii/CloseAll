@echo off
rem Close All - install or update.
rem Downloads the latest CloseAll.exe from GitHub, installs it to
rem %LOCALAPPDATA%\Programs\CloseAll and starts it.
setlocal
set "DIR=%LOCALAPPDATA%\Programs\CloseAll"
set "URL=https://github.com/aduseii/CloseAll/releases/latest/download/CloseAll.exe"
echo.
echo  Close All - installing the latest version...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop';" ^
  "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12;" ^
  "$dir=$env:DIR; New-Item -ItemType Directory -Force -Path $dir | Out-Null;" ^
  "$tmp=Join-Path $dir 'CloseAll.new.exe';" ^
  "Invoke-WebRequest -UseBasicParsing -Uri $env:URL -OutFile $tmp;" ^
  "if ((Get-Item $tmp).Length -lt 20000) { throw 'Download looks incomplete.' }" ^
  "Get-Process closeall -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800;" ^
  "$exe=Join-Path $dir 'CloseAll.exe'; $fresh=-not (Test-Path $exe); Move-Item -Force $tmp $exe; Unblock-File $exe;" ^
  "$ws=New-Object -ComObject WScript.Shell; $desk=[Environment]::GetFolderPath('Desktop');" ^
  "foreach($n in 'Close All Windows.lnk','Update Close All.lnk'){ $p=Join-Path $desk $n; if(Test-Path $p){ Remove-Item -Force $p } }" ^
  "$old=Join-Path $env:LOCALAPPDATA 'CloseAll'; if(Test-Path (Join-Path $old 'CloseAll.ps1')){ Remove-Item -Recurse -Force $old }" ^
  "$sm=Join-Path ([Environment]::GetFolderPath('Programs')) 'Close All.lnk';" ^
  "if($fresh){ foreach($p in @($sm,(Join-Path $desk 'Close All.lnk'))){ $l=$ws.CreateShortcut($p); $l.TargetPath=$exe; $l.WorkingDirectory=$dir; $l.Description='Close every open window'; $l.Save() } }" ^
  "Start-Process $exe;" ^
  "Write-Host ' Done! Close All' (Get-Item $exe).VersionInfo.FileVersion 'is installed and running.' -ForegroundColor Green"
if errorlevel 1 (
  echo.
  echo  Something went wrong. Check your internet connection and try again.
)
echo.
pause
