<#
  CloseAll.ps1 - Close every open window with one click.
  https://github.com/aduseii/CloseAll

  It first closes windows the same way clicking the X does. Any app that
  is still open after a second (for example one asking "Save changes?")
  is force-closed - unsaved work in it is LOST.
  It never touches the taskbar, desktop or system background windows.

  SETUP: double-click CloseAll-Setup.bat. It puts two shortcuts on your
  desktop:
    - "Close All Windows"   (keyboard shortcut Ctrl+Alt+Q)
    - "Update Close All"    gets the latest version from GitHub

  YOUR SETTINGS live in %LOCALAPPDATA%\CloseAll\settings.ps1, so updates
  never overwrite them. Open that file in Notepad to change them.
#>

param([switch]$Install, [switch]$Update, [switch]$Quiet)

$Version    = "1.1.0"
$RepoOwner  = "aduseii"
$RepoName   = "CloseAll"
$RawBase    = "https://raw.githubusercontent.com/$RepoOwner/$RepoName/main"
$InstallDir = Join-Path $env:LOCALAPPDATA "CloseAll"
$SettingsFile = Join-Path $InstallDir "settings.ps1"

# ================= DEFAULT SETTINGS =================
# Don't edit these here - edit settings.ps1 in the install folder instead.
$KeepApps    = @()          # apps that should NEVER be closed, e.g. "spotify"
$ForceClose  = $true        # force-close apps stuck on "Save changes?"
$WaitSeconds = 1            # seconds to wait before force-closing
$HotKey      = "Ctrl+Alt+Q" # keyboard shortcut ("" for none)
# ====================================================

$DefaultSettings = @'
# Close All - your settings.
# Updates never overwrite this file. After changing $HotKey, double-click
# "Update Close All" (or run the setup again) so the shortcut picks it up.

# Apps that should NEVER be closed. Use the program's file name without
# ".exe" (see Task Manager > Details tab). Remove the # to turn one on.
$KeepApps = @(
    # "spotify",
    # "discord"
)

# Force-close apps that don't close on their own (e.g. ones asking
# "Save changes?"). WARNING: unsaved work in those apps is lost.
# Set to $false to get the save prompts back.
$ForceClose = $true

# How long (in seconds) apps get to close normally before force-closing
$WaitSeconds = 1

# Keyboard shortcut for "Close All Windows" ("" for none)
$HotKey = "Ctrl+Alt+Q"
'@

if (Test-Path -LiteralPath $SettingsFile) {
    try { . $SettingsFile } catch {}
}

function Show-Message([string]$text, [string]$icon = "Information") {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($text, "Close All", "OK", $icon) | Out-Null
}

function New-Shortcut([string]$name, [string]$arguments, [string]$icon, [string]$desc, [string]$hotkey) {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $shell   = New-Object -ComObject WScript.Shell
    $lnk     = $shell.CreateShortcut((Join-Path $desktop "$name.lnk"))
    $lnk.TargetPath   = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
    $lnk.Arguments    = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass $arguments"
    $lnk.IconLocation = $icon
    $lnk.WindowStyle  = 7
    $lnk.Description  = $desc
    $lnk.WorkingDirectory = $InstallDir
    $lnk.Hotkey = if ($hotkey) { $hotkey } else { "" }
    $lnk.Save()
}

function Install-CloseAll([string]$source) {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $target = Join-Path $InstallDir "CloseAll.ps1"
    if ($source -and ((Resolve-Path -LiteralPath $source).Path -ne $target)) {
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
    try { Unblock-File -LiteralPath $target -ErrorAction SilentlyContinue } catch {}

    if (-not (Test-Path -LiteralPath $SettingsFile)) {
        Set-Content -LiteralPath $SettingsFile -Value $DefaultSettings -Encoding UTF8
    }

    New-Shortcut "Close All Windows" "-File `"$target`"" `
        "$env:WINDIR\System32\shell32.dll,131" "Close every open window" $HotKey
    New-Shortcut "Update Close All" "-File `"$target`" -Update" `
        "$env:WINDIR\System32\shell32.dll,238" "Get the latest Close All from GitHub" ""
}

# ---------------- Install ----------------
if ($Install) {
    try {
        Install-CloseAll $PSCommandPath
        if ($Quiet) { exit }
        Write-Host ""
        Write-Host "Done! Close All $Version is installed." -ForegroundColor Green
        Write-Host "Two shortcuts are on your desktop:"
        Write-Host "  Close All Windows  - closes everything" $(if ($HotKey) { "($HotKey)" })
        Write-Host "  Update Close All   - gets the latest version from GitHub"
        Write-Host ""
        Write-Host "Your settings: $SettingsFile"
        Write-Host "Tip: right-click a shortcut > Show more options > Pin to taskbar."
    } catch {
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
        if ($Quiet) { exit 1 }
    }
    Write-Host ""
    Read-Host "Press Enter to close" | Out-Null
    exit
}

# ---------------- Update from GitHub ----------------
if ($Update) {
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $tmp = Join-Path $env:TEMP "CloseAll-new.ps1"
        $url = "$RawBase/CloseAll.ps1?nocache=$([DateTime]::UtcNow.Ticks)"
        Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing -Headers @{ "Cache-Control" = "no-cache" }

        $text = Get-Content -LiteralPath $tmp -Raw
        if (($text -notmatch 'CloseAllWin') -or ($text -notmatch '\$Version\s*=\s*"([0-9.]+)"')) {
            throw "The file downloaded from GitHub doesn't look like Close All."
        }
        $newVersion = $Matches[1]

        if ([version]$newVersion -le [version]$Version) {
            Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
            Show-Message "You already have the latest version ($Version)."
            exit
        }

        $target = Join-Path $InstallDir "CloseAll.ps1"
        New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
        Copy-Item -LiteralPath $tmp -Destination $target -Force
        Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
        try { Unblock-File -LiteralPath $target -ErrorAction SilentlyContinue } catch {}

        # Let the new version refresh its own shortcuts
        & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass `
            -File $target -Install -Quiet | Out-Null
        Show-Message "Updated Close All from $Version to $newVersion."
    } catch {
        Show-Message ("Couldn't update Close All.`n`n" + $_.Exception.Message +
                      "`n`nCheck your internet connection and try again.") "Warning"
    }
    exit
}

# ---------------- Close the windows ----------------
Add-Type @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class CloseAllWin {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);

    public static List<IntPtr> All() {
        List<IntPtr> list = new List<IntPtr>();
        EnumWindows(delegate(IntPtr h, IntPtr l) { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
    public static string Title(IntPtr h) { StringBuilder s = new StringBuilder(512); GetWindowText(h, s, 512); return s.ToString(); }
    public static string Cls(IntPtr h)   { StringBuilder s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
    public static bool Cloaked(IntPtr h) { int v = 0; DwmGetWindowAttribute(h, 14, out v, 4); return v != 0; }
}
"@

$skipClasses = @(
    "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW",
    "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow"
)
# Never force-kill these (killing explorer would take down the taskbar)
$neverKill = @(
    "explorer", "dwm", "csrss", "winlogon", "sihost", "ShellExperienceHost",
    "StartMenuExperienceHost", "SearchHost", "SearchApp", "TextInputHost",
    "LockApp", "SystemSettingsBroker"
) | ForEach-Object { $_.ToLower() }
$keep  = @($KeepApps | ForEach-Object { "$_".ToLower() -replace '\.exe$', '' })
$myPid = $PID

function Get-OpenWindows {
    $result = @()
    foreach ($h in [CloseAllWin]::All()) {
        if (-not [CloseAllWin]::IsWindowVisible($h)) { continue }
        if ([CloseAllWin]::GetWindow($h, 4) -ne [IntPtr]::Zero) { continue }   # owned popup/dialog
        if ([CloseAllWin]::Cloaked($h)) { continue }                           # hidden UWP/background
        if ([CloseAllWin]::Title($h).Length -eq 0) { continue }
        if ($skipClasses -contains [CloseAllWin]::Cls($h)) { continue }

        $ex = [CloseAllWin]::GetWindowLong($h, -20)
        if ((($ex -band 0x80) -ne 0) -and (($ex -band 0x40000) -eq 0)) { continue }  # not on taskbar

        [uint32]$procId = 0
        [CloseAllWin]::GetWindowThreadProcessId($h, [ref]$procId) | Out-Null
        if ($procId -eq $myPid) { continue }
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if (-not $p) { continue }
        $pname = $p.ProcessName.ToLower()
        if ($keep -contains $pname) { continue }

        $result += [pscustomobject]@{ Handle = $h; ProcessId = [int]$procId; Name = $pname }
    }
    return $result
}

# Step 1: ask every window to close (like clicking X)
foreach ($w in Get-OpenWindows) {
    [CloseAllWin]::PostMessage($w.Handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
}

# Step 2: anything still open (e.g. stuck on "Save changes?") gets force-closed
if ($ForceClose) {
    Start-Sleep -Milliseconds ([int]([double]$WaitSeconds * 1000))
    $leftovers = Get-OpenWindows | Where-Object { $neverKill -notcontains $_.Name }
    $leftovers | Select-Object -ExpandProperty ProcessId -Unique | ForEach-Object {
        Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
    }
}
