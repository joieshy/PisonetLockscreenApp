using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;

namespace PisonetLockscreenApp
{
    public class Watchdog
    {
        private const string MainAppName = "PisonetLockscreenApp";
        private const string WatchdogFlag = "watchdog_active.tmp";

        public static void Main(string[] args)
        {
            // Ensure startup task is created for the watchdog as well
            EnsureStartupTask();

            // Ensure only one watchdog is running
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "PisonetWatchdogMutex", out createdNew))
            {
                if (!createdNew) return;

                Console.WriteLine("Pisonet Watchdog Started...");
                
                while (true)
                {
                    try
                    {
                        // Check if the main app is running
                        var processes = Process.GetProcessesByName(MainAppName);
                        
                        // If main app is not running, check if it was a graceful exit
                        if (processes.Length == 0)
                        {
                            if (File.Exists(WatchdogFlag))
                            {
                                string appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, MainAppName + ".exe");
                                if (File.Exists(appPath))
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = appPath,
                                        UseShellExecute = true,
                                        WindowStyle = ProcessWindowStyle.Hidden,
                                        CreateNoWindow = true
                                    });
                                    Console.WriteLine("Main app restarted.");
                                }
                            }
                            else
                            {
                                // If flag is missing, it means admin closed the app
                                Console.WriteLine("Graceful exit detected. Watchdog shutting down.");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }

                    Thread.Sleep(2000); // Check every 2 seconds
                }
            }
        }

        private static void EnsureStartupTask()
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;

                // For non-admin apps, the Registry Run key is the most reliable startup method.
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("PisonetWatchdog", $"\"{exePath}\"");
                        }
                    }
                }
                catch { }

            }
            catch { }
        }
    }
}
