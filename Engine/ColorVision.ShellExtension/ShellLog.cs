using System;
using System.IO;

namespace ColorVision.ShellExtension
{
    /// <summary>
    /// Simple file-based logger for the shell extension.
    /// Since the COM server runs inside Explorer (not in our app process),
    /// log4net and other app-level loggers are not available.
    /// Logs are written to %APPDATA%\ColorVision\ShellExtension.log.
    /// </summary>
    internal static class ShellLog
    {
        private static readonly string LogFilePath;
        private static readonly object LockObj = new();

        static ShellLog()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logDir = Path.Combine(appData, "ColorVision");
            Directory.CreateDirectory(logDir);
            LogFilePath = Path.Combine(logDir, "ShellExtension.log");
        }

        /// <summary>
        /// Appends a timestamped log entry to the log file.
        /// Thread-safe. Never throws — logging failures are silently ignored.
        /// </summary>
        public static void Log(string message)
        {
            try
            {
                lock (LockObj)
                {
                    File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never cause the shell extension to fail
            }
        }
    }
}
