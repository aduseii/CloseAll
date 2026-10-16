using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CloseAllApp
{
    class Settings
    {
        public bool Force = true;
        public decimal WaitSeconds = 1;
        public bool Hotkey = true;
        public HashSet<string> Keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CloseAll"); } }
        static string FilePath { get { return Path.Combine(Dir, "settings.txt"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "force") s.Force = val == "1";
                    else if (key == "wait")
                    {
                        decimal d;
                        if (decimal.TryParse(val, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
                            s.WaitSeconds = Math.Min(30, Math.Max(0, d));
                    }
                    else if (key == "hotkey") s.Hotkey = val == "1";
                    else if (key == "keep" && val.Length > 0) s.Keep.Add(val.ToLowerInvariant());
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var lines = new List<string> {
                    "force=" + (Force ? "1" : "0"),
                    "wait=" + WaitSeconds.ToString(CultureInfo.InvariantCulture),
                    "hotkey=" + (Hotkey ? "1" : "0")
                };
                foreach (string k in Keep.OrderBy(x => x)) lines.Add("keep=" + k);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }

        public static bool StartupEnabled
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue("CloseAll") != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue("CloseAll", "\"" + Application.ExecutablePath + "\" /tray");
                        else k.DeleteValue("CloseAll", false);
                    }
                }
                catch { }
            }
        }
    }
}
