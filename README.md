# Close All

Close every open window on Windows with one click or **Ctrl+Alt+Q**.

It closes each window the way clicking **X** does. Anything still open after a second (for example an app asking "Save changes?") is **force-closed, and unsaved work in it is lost**. The taskbar, desktop and Windows' own background processes are never touched.

## Install
1. Download **CloseAll-Setup.bat** (open it above, then click the download button), or download the whole repo as a ZIP.
2. Double-click **CloseAll-Setup.bat**. If `CloseAll.ps1` isn't next to it, it downloads the latest one from here.
3. Two shortcuts appear on your desktop:
   - **Close All Windows** – closes everything. Hotkey **Ctrl+Alt+Q**.
   - **Update Close All** – gets the latest version from this repo and installs it.

Right-click either one → Show more options → Pin to taskbar to keep it handy. If SmartScreen warns you, click **More info → Run anyway**.

## Settings
Your settings live in `%LOCALAPPDATA%\CloseAll\settings.ps1`. Updates never overwrite it. Open it in Notepad to change:
- `$KeepApps` – apps that should never be closed, e.g. `"spotify"`, `"discord"`
- `$ForceClose` – `$false` brings back the "Save changes?" prompts
- `$WaitSeconds` – how long apps get to close normally
- `$HotKey` – the keyboard shortcut (`""` for none). Click **Update Close All** afterwards so the shortcut picks it up.

## Releasing an update
Change `$Version` at the top of `CloseAll.ps1` (e.g. `1.1.0` → `1.2.0`) and push. **Update Close All** only installs versions newer than the one you have.

## Uninstall
Delete the two desktop shortcuts and the folder `%LOCALAPPDATA%\CloseAll`.
