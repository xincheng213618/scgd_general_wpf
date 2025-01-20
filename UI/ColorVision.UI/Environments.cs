using log4net.Appender;
using log4net.Repository.Hierarchy;
using log4net;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI
{
    public static class Environments
    {
        public static string AssemblyCompany { get => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision"; }

        public static string DirAppData { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\{AssemblyCompany}\\";
        public static string DirConfig { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\{AssemblyCompany}\\Config";
        public static string? DirLog { get; set; } = GetLogFilePath();
        private static string? GetLogFilePath()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            var fileAppender = hierarchy.Root.Appenders.OfType<RollingFileAppender>().FirstOrDefault();
            return fileAppender?.File;
        }

        public static string GetExecutablePath()
        {
#if NETCOREAPP
            // For .NET Core
            return Application.ResourceAssembly.Location.Replace(".dll", ".exe");
#else
        // For .NET Framework
        return Assembly.GetExecutingAssembly().Location;
#endif
        }

    }
}
