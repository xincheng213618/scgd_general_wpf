using System;
using System.IO;

namespace ColorVisionSetup
{
    internal static class SetupFiles
    {
        private static readonly object LogLock = new object();
        internal static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Setup");
        internal static string LogPath => Path.Combine(Root, "setup.log");
        internal static string CreatePackagePath(Version version)
        {
            string directory = Path.Combine(Root, "Packages", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "ColorVision-" + version + ".exe");
        }
        internal static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Root);
                lock (LogLock) File.AppendAllText(LogPath, DateTimeOffset.Now.ToString("o") + " " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
