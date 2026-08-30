using ColorVision.ImageEditor.Cie;
using ColorVision.UI;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Feedback;
using ColorVision.UI.Maintenance;
using ColorVision.Update;
using log4net;
using log4net.Appender;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Settings.Maintenance
{
    internal static class StorageMaintenanceCatalog
    {
        private static readonly string[] UpdateDirectoryPrefixes = ["ColorVisionUpdate-", "ColorVisionPluginsUpdate-", "ColorVisionPackageVersion-"];

        public static IReadOnlyList<MaintenanceFileCleanupRule> CreateRules(int logRetentionDays, int tempRetentionDays, int packageRetentionDays)
        {
            return CreateRulesForPaths(
                logRetentionDays, tempRetentionDays, packageRetentionDays,
                () => LogManager.GetAllRepositories().SelectMany(repository => repository.GetAppenders()).OfType<FileAppender>(),
                Path.GetTempPath(), Environments.DirPackageCache,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "ImageEditor", "CieCache"),
                IsUpdateProtected, HasOpenFeedbackWindow, Aria2cDownloadManager.IsPathProtectedFromCleanup);
        }

        public static string GetProtectionNotice(string id) => id switch
        {
            "temp" when IsUpdateProtected() => MaintenanceText.Get("UpdateTemporaryProtected"),
            "temp" when HasOpenFeedbackWindow() => MaintenanceText.Get("FeedbackProtected"),
            "packages" when IsUpdateProtected() => MaintenanceText.Get("UpdatePackagesProtected"),
            _ => string.Empty,
        };

        // Explicit dependencies keep catalog tests inside their isolated directories, away from real user state.
        internal static IReadOnlyList<MaintenanceFileCleanupRule> CreateRulesForPaths(
            int logRetentionDays, int tempRetentionDays, int packageRetentionDays,
            Func<IEnumerable<FileAppender>> appenders, string temporaryRoot, string packageRoot, string cieCacheRoot,
            Func<bool> updateIsProtected, Func<bool> feedbackIsProtected, Func<string, bool> downloadIsProtected)
        {
            var rules = new List<MaintenanceFileCleanupRule>();
            FileAppender[] fileAppenders = appenders().ToArray();
            foreach (FileAppender appender in fileAppenders)
            {
                if (appender is not RollingFileAppender rolling || string.IsNullOrWhiteSpace(appender.File))
                    continue;
                string activePath = Path.GetFullPath(appender.File);
                string? logDirectory = Path.GetDirectoryName(activePath);
                if (string.IsNullOrEmpty(logDirectory) || !Directory.Exists(logDirectory))
                    continue;
                string activeName = Path.GetFileName(activePath);
                rules.Add(new("logs", logDirectory, RetentionDays: logRetentionDays, IsProtected: path =>
                    appenders().Any(current => IsSamePath(current.File, path)) ||
                    !IsRollingArchiveName(Path.GetFileName(path), activeName, rolling.DatePattern, rolling.StaticLogFileName, rolling.PreserveLogFileNameExtension)));
            }

            if (Directory.Exists(temporaryRoot))
            {
                rules.Add(new("temp", temporaryRoot, "ColorVision_Diagnostics_*.zip", RetentionDays: tempRetentionDays, IsProtected: _ => feedbackIsProtected()));
                rules.Add(new("temp", temporaryRoot, "ColorVision_Screenshot_*.png", RetentionDays: tempRetentionDays, IsProtected: _ => feedbackIsProtected()));
                foreach (string prefix in UpdateDirectoryPrefixes)
                {
                    foreach (string directory in Directory.EnumerateDirectories(temporaryRoot, prefix + "*", SearchOption.TopDirectoryOnly))
                    {
                        if (!IsKnownUpdateDirectoryName(Path.GetFileName(directory)) || IsReparsePoint(directory))
                            continue;
                        string cleanupRoot = directory;
                        rules.Add(new("temp", cleanupRoot, Recursive: true, RetentionDays: tempRetentionDays, IsProtected: _ =>
                            updateIsProtected() || !IsExpiredDirectory(cleanupRoot, tempRetentionDays)));
                    }
                }
            }

            AddPackageRules(Path.Combine(packageRoot, "Application", "Full"), ["ColorVision-*.exe"]);
            AddPackageRules(Path.Combine(packageRoot, "Application", "Incremental"), ["ColorVision-Update-*.cvx"]);
            AddPackageRules(Path.Combine(packageRoot, "Plugins"), ["*.cvxp", "*.zip"]);
            AddPackageRules(Path.Combine(packageRoot, "Tools"), ["*.exe", "*.msi", "*.zip", "*.7z"]);

            if (Directory.Exists(cieCacheRoot))
            {
                foreach (CieDiagramKind kind in Enum.GetValues<CieDiagramKind>())
                    rules.Add(new("cie-cache", cieCacheRoot, $"{kind}_v*.png", RetentionDays: 0, IsProtected: path => !IsCieCacheName(Path.GetFileName(path), kind)));
            }
            return rules;

            void AddPackageRules(string directory, string[] patterns)
            {
                if (!Directory.Exists(directory))
                    return;
                foreach (string pattern in patterns)
                    rules.Add(new("packages", directory, pattern, RetentionDays: packageRetentionDays, IsProtected: path =>
                        updateIsProtected() || File.Exists(path + ".aria2") || downloadIsProtected(path)));
            }
        }

        internal static bool IsKnownUpdateDirectoryName(string name)
        {
            return UpdateDirectoryPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(name[prefix.Length..], "N", out _));
        }

        internal static bool IsRollingArchiveName(string candidate, string activeName, string? datePattern, bool staticFileName, bool preserveExtension)
        {
            if (string.Equals(candidate, activeName, StringComparison.OrdinalIgnoreCase))
                return false;
            string unnumbered = RemoveRollingNumber(candidate, preserveExtension);
            if (staticFileName)
            {
                if (!string.Equals(unnumbered, candidate, StringComparison.Ordinal) && string.Equals(unnumbered, activeName, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.IsNullOrEmpty(datePattern))
                    return false;
                string prefix = preserveExtension ? Path.GetFileNameWithoutExtension(activeName) : activeName;
                string suffix = preserveExtension ? Path.GetExtension(activeName) : string.Empty;
                return MatchesDatedName(unnumbered, prefix, suffix, datePattern);
            }
            if (string.IsNullOrEmpty(datePattern))
                return false;
            // The File property is the actual opened log file, including its current date segment.
            for (int start = 0; start < activeName.Length; start++)
            {
                for (int end = activeName.Length; end > start; end--)
                {
                    if (IsFormattedDate(activeName[start..end], datePattern))
                        return MatchesDatedName(unnumbered, activeName[..start], activeName[end..], datePattern);
                }
            }
            return false;
        }

        private static bool MatchesDatedName(string name, string prefix, string suffix, string pattern)
        {
            return name.Length > prefix.Length + suffix.Length &&
                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                IsFormattedDate(name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length), pattern);
        }

        private static bool IsFormattedDate(string value, string pattern)
        {
            try
            {
                return DateTime.TryParseExact(value, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string RemoveRollingNumber(string name, bool preserveExtension)
        {
            string extension = preserveExtension ? Path.GetExtension(name) : string.Empty;
            string stem = preserveExtension ? Path.GetFileNameWithoutExtension(name) : name;
            int separator = stem.LastIndexOf('.');
            return separator >= 0 && int.TryParse(stem[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number > 0
                ? stem[..separator] + extension : name;
        }

        private static bool IsCieCacheName(string name, CieDiagramKind kind)
        {
            string prefix = $"{kind}_v";
            return name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name[prefix.Length..^4], NumberStyles.None, CultureInfo.InvariantCulture, out int version) && version > 0;
        }

        private static bool IsExpiredDirectory(string directory, int retentionDays)
        {
            try
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
                return Directory.Exists(directory) && !IsReparsePoint(directory) &&
                    Directory.GetCreationTimeUtc(directory) < cutoff;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSamePath(string? left, string right)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(left) && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }

        private static bool IsReparsePoint(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static bool IsUpdateProtected() => CombinedUpdateCoordinator.HasPackageMaintenanceProtection || ExitUpdateHandoff.HasActiveUpdateForCleanup();

        private static bool HasOpenFeedbackWindow()
        {
            Application? application = Application.Current;
            if (application == null)
                return false;
            try
            {
                return application.Dispatcher.CheckAccess()
                    ? application.Windows.OfType<FeedbackWindow>().Any()
                    : application.Dispatcher.Invoke(() => application.Windows.OfType<FeedbackWindow>().Any());
            }
            catch
            {
                return true;
            }
        }
    }
}
