# Close All

Close every open window on Windows with one click or **Ctrl+Alt+Q**.

Close All shows the windows that are open, lets you untick apps you want to keep, and closes the rest the way clicking **X** does. Apps that are still open after a short wait (for example ones asking "Save changes?") can be force-closed — unsaved work in them is lost. The taskbar, desktop and Windows' own processes are never touched. It keeps running in the system tray so **Ctrl+Alt+Q** works any time.

## Install
Download **[CloseAll-Setup.bat](https://github.com/aduseii/CloseAll/raw/main/CloseAll-Setup.bat)** and double-click it. It downloads the latest `CloseAll.exe` from the releases, installs it to `%LOCALAPPDATA%\Programs\CloseAll` and starts it. Running it again updates an existing install.

If SmartScreen warns you, click **More info → Run anyway**.

## Updates
Click **Check for updates** in the window, or in the tray icon's right-click menu. Close All also checks quietly each time it starts and changes the button to **Update to x.y.z** when a newer version is out. Updating downloads the new version, swaps it in and restarts — your settings stay.

## Settings
Everything is in the window: which apps to keep, force-closing and the wait time, the hotkey, and starting with Windows. Settings are saved in `%APPDATA%\CloseAll\settings.txt`. Unexpected errors are logged to `%APPDATA%\CloseAll\error.log`.

Run `CloseAll.exe /closeall` to close everything without opening the window.

## Releasing a new version
1. Change `AssemblyVersion` and `AssemblyFileVersion` in `src/Program.cs`.
2. (Optional) build locally with `./build.sh` (Mono) to check it compiles.
3. Write what changed in `release-notes.md` and push. GitHub Actions builds `CloseAll.exe` and publishes the `vX.Y.Z` release; the update button picks it up.
