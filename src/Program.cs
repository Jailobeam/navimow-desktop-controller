using System;
using System.IO;
using System.Windows.Forms;

namespace NavimowDesktopController
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (sender, args) => LogException("ThreadException", args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => LogException("UnhandledException", args.ExceptionObject as Exception);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogException("Main", ex);
                throw;
            }
        }

        private static void LogException(string source, Exception exception)
        {
            try
            {
                var baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NavimowDesktopController");

                Directory.CreateDirectory(baseDirectory);
                var logFile = Path.Combine(baseDirectory, "startup-error.log");
                var message =
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + source + Environment.NewLine +
                    (exception != null ? exception.ToString() : "(null)") + Environment.NewLine + Environment.NewLine;
                File.AppendAllText(logFile, message);
            }
            catch
            {
            }
        }
    }
}
