using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CloseAllApp
{
    static class Native
    {
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder s, ref int size);
    }

    class WinInfo
    {
        public IntPtr Handle;
        public int Pid;
        public string ProcName = "";
        public string Title = "";
        public string ExePath = "";
        public string AppName = "";
    }

    class CloseResult
    {
        public int Asked;
        public int ForceClosed;
        public int StillOpen;
    }

    static class WindowCloser
    {
        static readonly HashSet<string> SkipClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW",
            "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow"
        };
        // Never force-kill these (killing explorer would take down the taskbar)
        static readonly HashSet<string> NeverKill = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "explorer", "dwm", "csrss", "winlogon", "sihost", "shellexperiencehost",
            "startmenuexperiencehost", "searchhost", "searchapp", "textinputhost",
            "lockapp", "applicationframehost", "systemsettingsbroker", "closeall"
        };
        static readonly int MyPid = Process.GetCurrentProcess().Id;
        static readonly Dictionary<int, string> PathCache = new Dictionary<int, string>();
        static readonly Dictionary<string, string> NameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static string Text(IntPtr h) { var s = new StringBuilder(512); Native.GetWindowText(h, s, 512); return s.ToString(); }
        static string Cls(IntPtr h) { var s = new StringBuilder(256); Native.GetClassName(h, s, 256); return s.ToString(); }

        public static string GetExePath(int pid)
        {
            string path;
            lock (PathCache) { if (PathCache.TryGetValue(pid, out path)) return path; }
            path = "";
            IntPtr hp = Native.OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (hp != IntPtr.Zero)
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (Native.QueryFullProcessImageName(hp, 0, sb, ref size)) path = sb.ToString();
                Native.CloseHandle(hp);
            }
            lock (PathCache) { PathCache[pid] = path; }
            return path;
        }

        static string FriendlyName(string exePath, string procName)
        {
            if (string.IsNullOrEmpty(exePath)) return procName ?? "";
            string name;
            lock (NameCache) { if (NameCache.TryGetValue(exePath, out name)) return name; }
            name = procName ?? "";
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(vi.FileDescription)) name = vi.FileDescription.Trim();
            }
            catch { }
            lock (NameCache) { NameCache[exePath] = name; }
            return name;
        }

        public static List<WinInfo> GetOpenWindows()
        {
            var handles = new List<IntPtr>();
            Native.EnumWindows((h, l) => { handles.Add(h); return true; }, IntPtr.Zero);

            var result = new List<WinInfo>();
            foreach (IntPtr h in handles)
            {
                try
                {
                    if (!Native.IsWindowVisible(h)) continue;
                    if (Native.GetWindow(h, 4) != IntPtr.Zero) continue;        // owned popup/dialog
                    int cloaked = 0;
                    Native.DwmGetWindowAttribute(h, 14, out cloaked, 4);
                    if (cloaked != 0) continue;                                   // hidden UWP/background
                    string title = Text(h);
                    if (title.Length == 0) continue;
                    if (SkipClasses.Contains(Cls(h))) continue;

                    int ex = Native.GetWindowLong(h, -20);
                    bool toolWindow = (ex & 0x80) != 0, appWindow = (ex & 0x40000) != 0;
                    if (toolWindow && !appWindow) continue;                      // not on taskbar

                    uint upid;
                    Native.GetWindowThreadProcessId(h, out upid);
                    int pid = (int)upid;
                    if (pid == MyPid) continue;

                    string exe = GetExePath(pid) ?? "";
                    string proc = exe.Length > 0 ? Path.GetFileNameWithoutExtension(exe).ToLowerInvariant() : "";
                    if (proc.Length == 0)
                    {
                        try { proc = Process.GetProcessById(pid).ProcessName.ToLowerInvariant(); }
                        catch { continue; }
                    }
                    if (proc == "closeall") continue;

                    result.Add(new WinInfo {
                        Handle = h, Pid = pid, ProcName = proc, Title = title,
                        ExePath = exe, AppName = FriendlyName(exe, proc)
                    });
                }
                catch { }
            }
            return result;
        }

        public static CloseResult CloseAll(ICollection<string> keep, bool force, double waitSeconds)
        {
            var r = new CloseResult();
            var targets = GetOpenWindows().Where(w => !keep.Contains(w.ProcName)).ToList();
            foreach (var w in targets)
            {
                Native.PostMessage(w.Handle, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE, like clicking X
                r.Asked++;
            }
            if (targets.Count == 0) return r;

            int waitMs = (int)(Math.Max(0.2, waitSeconds) * 1000);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < waitMs)
            {
                Thread.Sleep(100);
                if (!targets.Any(w => Native.IsWindow(w.Handle) && Native.IsWindowVisible(w.Handle))) break;
            }

            if (force)
            {
                var pids = GetOpenWindows()
                    .Where(w => !keep.Contains(w.ProcName) && !NeverKill.Contains(w.ProcName))
                    .Select(w => w.Pid).Distinct().ToList();
                foreach (int pid in pids)
                {
                    try { Process.GetProcessById(pid).Kill(); r.ForceClosed++; } catch { }
                }
                if (pids.Count > 0) Thread.Sleep(400);
            }

            r.StillOpen = GetOpenWindows().Count(w => !keep.Contains(w.ProcName));
            lock (PathCache) { PathCache.Clear(); }
            return r;
        }
    }
}
