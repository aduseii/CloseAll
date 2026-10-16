using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace CloseAllApp
{
    class UpdateInfo
    {
        public Version Version;
        public string Tag;
        public string DownloadUrl;
    }

    // Gets new versions from the GitHub releases of aduseii/CloseAll.
    static class Updater
    {
        public const string Repo = "aduseii/CloseAll";
        public const string AssetName = "CloseAll.exe";
        public static readonly Version Current = typeof(Updater).Assembly.GetName().Version;

        public static string CurrentText { get { return Current.ToString(3); } }

        static WebClient NewClient()
        {
            // .NET Framework defaults to old TLS; GitHub needs TLS 1.2.
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "CloseAll/" + CurrentText;
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }

        // Returns the latest release, or null if there is none. Throws on network errors.
        public static UpdateInfo GetLatest()
        {
            string json;
            using (var wc = NewClient())
                json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");

            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!tag.Success) return null;
            Version v;
            if (!Version.TryParse(tag.Groups[1].Value.TrimStart('v', 'V'), out v)) return null;

            var url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/" + Regex.Escape(AssetName) + ")\"");
            if (!url.Success) return null;

            return new UpdateInfo { Version = Normalize(v), Tag = tag.Groups[1].Value, DownloadUrl = url.Groups[1].Value };
        }

        static Version Normalize(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        }

        public static bool IsNewer(UpdateInfo u)
        {
            return u != null && u.Version > Normalize(Current);
        }

        // Downloads the new exe next to the running one, swaps them and starts the new one.
        // The caller must exit right after this returns.
        public static void InstallAndRestart(UpdateInfo u, bool startHidden)
        {
            string exe = Application.ExecutablePath;
            string dir = Path.GetDirectoryName(exe);
            string fresh = Path.Combine(dir, "CloseAll.new.exe");
            string old = Path.Combine(dir, "CloseAll.old.exe");

            TryDelete(fresh);
            using (var wc = NewClient())
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/octet-stream";
                wc.DownloadFile(u.DownloadUrl, fresh);
            }

            var info = new FileInfo(fresh);
            byte[] head = new byte[2];
            using (var f = File.OpenRead(fresh)) f.Read(head, 0, 2);
            if (info.Length < 20000 || head[0] != 'M' || head[1] != 'Z')
            {
                TryDelete(fresh);
                throw new InvalidDataException("The downloaded file isn't a valid Close All app.");
            }

            // A running exe can't be overwritten, but it can be renamed.
            TryDelete(old);
            File.Move(exe, old);
            try { File.Move(fresh, exe); }
            catch { File.Move(old, exe); throw; }

            Process.Start(new ProcessStartInfo(exe, "/updated" + (startHidden ? " /tray" : "")) { UseShellExecute = false });
        }

        // Called at startup: removes the previous version left behind by an update.
        public static void CleanUpOldVersion()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string old = Path.Combine(dir, "CloseAll.old.exe");
            for (int i = 0; i < 20 && File.Exists(old); i++)
            {
                TryDelete(old);
                if (File.Exists(old)) Thread.Sleep(250);
            }
            TryDelete(Path.Combine(dir, "CloseAll.new.exe"));
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
