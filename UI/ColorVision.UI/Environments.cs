using log4net.Appender;
using log4net.Repository.Hierarchy;
using log4net;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI
{
    public static class Environments
    {
        public static string AssemblyCompany { get => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "ColorVision"; }

        public static string DirAppData { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\{AssemblyCompany}\\";

        public static string DirLocalAppData { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + $"\\{AssemblyCompany}\\";

        public static string? DirLog { get; set; } = GetLogFilePath();

        public static string DirState => Path.Combine(DirAppData, "State");

        public static string DirStateDesktop => Path.Combine(DirState, "Desktop");

        public static string DirStateLayout => Path.Combine(DirState, "Layout");

        public static string DirStateScheduler => Path.Combine(DirState, "Scheduler");

        public static string DirStateTerminal => Path.Combine(DirState, "Terminal");

        public static string DirDownloads => Path.Combine(DirAppData, "Downloads");

        public static string DirPackageCache => Path.Combine(DirAppData, "PackageCache");

        public static string DirApplicationPackageCache => Path.Combine(DirPackageCache, "Application");

        public static string DirApplicationFullPackageCache => Path.Combine(DirApplicationPackageCache, "Full");

        public static string DirApplicationIncrementalPackageCache => Path.Combine(DirApplicationPackageCache, "Incremental");

        public static string DirPluginPackageCache => Path.Combine(DirPackageCache, "Plugins");

        public static string DirToolPackageCache => Path.Combine(DirPackageCache, "Tools");

        public static string DirTools => Path.Combine(DirAppData, "Tools");

        public static string DirSnapshots => Path.Combine(DirAppData, "Snapshots");

        public static string DirApplicationSnapshots => Path.Combine(DirSnapshots, "Application");

        public static string DirUpdateState => Path.Combine(DirAppData, "UpdateState");

        public static string DirUpdateBackup => Path.Combine(DirAppData, "UpdateBackup");

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
