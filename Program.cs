using System;
using System.Windows.Forms;
using System.IO;
using PisonetLockscreenApp.Forms;

namespace PisonetLockscreenApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogError(e.ExceptionObject as Exception);
            Application.ThreadException += (s, e) => LogError(e.Exception);

            try
            {
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                LogError(ex);
                MessageBox.Show($"Application Error: {ex.Message}\n\nCheck error_log.txt for details.", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void LogError(Exception? ex)
        {
            if (ex == null) return;
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {ex.ToString()}\n\n");
            }
            catch { }
        }
    }
}
