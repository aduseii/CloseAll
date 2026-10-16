using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Close All")]
[assembly: AssemblyProduct("Close All")]
[assembly: AssemblyDescription("Close every open window with one click")]
// Bump this for every release. The update button compares it with the latest GitHub release.
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]

namespace CloseAllApp
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var flags = new System.Collections.Generic.HashSet<string>(
                args.Select(a => a.Trim().TrimStart('/', '-').ToLowerInvariant()));
            var settings = Settings.Load();

            // "CloseAll.exe /closeall" closes everything with no window
            if (flags.Contains("closeall"))
            {
                WindowCloser.CloseAll(settings.Keep, settings.Force, (double)settings.WaitSeconds);
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, "CloseAllApp_SingleInstance_v1", out createdNew))
            {
                if (!createdNew && flags.Contains("updated"))
                {
                    // We were started by the previous version during an update; wait for it to exit.
                    try { createdNew = mutex.WaitOne(15000); }
                    catch (AbandonedMutexException) { createdNew = true; }
                }

                var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "CloseAllApp_Show_v1");
                if (!createdNew)
                {
                    showEvent.Set();   // already running: bring that window up instead
                    return;
                }

                if (flags.Contains("updated")) Updater.CleanUpOldVersion();

                try { Native.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Log unexpected errors instead of showing the "Unhandled exception" box.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => LogError(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => LogError(e.ExceptionObject as Exception);

                var form = new MainForm(settings, flags.Contains("tray"));
                var waiter = new Thread(() =>
                {
                    while (true)
                    {
                        showEvent.WaitOne();
                        try { form.BeginInvoke((MethodInvoker)form.ShowMain); }
                        catch { return; }
                    }
                }) { IsBackground = true };
                waiter.Start();

                Application.Run(form);
                try { mutex.ReleaseMutex(); } catch { }
            }
        }

        static void LogError(Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CloseAll");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  v" + Updater.CurrentText + Environment.NewLine +
                    (ex == null ? "(unknown error)" : ex.ToString()) + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
    }
}
