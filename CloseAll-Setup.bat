@echo off
rem CloseAll-Setup.bat - installs "Close All Windows" (Ctrl+Alt+Q)
rem and "Update Close All". Double-click to run.
rem If CloseAll.ps1 is in the same folder it uses that copy,
rem otherwise it downloads the latest one from GitHub.
setlocal
set "SCRIPT=%~dp0CloseAll.ps1"
set "URL=https://raw.githubusercontent.com/aduseii/CloseAll/main/CloseAll.ps1"
if not exist "%SCRIPT%" (
    echo Downloading the latest Close All from GitHub...
    set "SCRIPT=%TEMP%\CloseAll.ps1"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol='Tls12'; Invoke-WebRequest -UseBasicParsing -Uri '%URL%' -OutFile '%TEMP%\CloseAll.ps1'"
    if not exist "%TEMP%\CloseAll.ps1" (
        echo.
        echo Couldn't download it. Check your internet connection and try again.
        pause
        exit /b 1
    )
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%SCRIPT%' -ErrorAction SilentlyContinue"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -Install
endlocal
