using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Diagnostics
{
    internal sealed record MvsLogSource(string Name, string Directory, bool SettingsOnly = false);

    public sealed class MvsLogCollector : IFeedbackLogCollector, IFeedbackLogTimeRangeCollector
    {
        internal const long MaxFileBytes = 50L * 1024 * 1024;
        internal const long MaxTotalBytes = 200L * 1024 * 1024;
        public string Name => Properties.Resources.PhyCamera_MvsLog;
        public string Description => "本机海康 SDK、客户端日志及收集状态；无法读取的日志会记录原因";
        public int Order => 21;
        public int RecentDays { get; set; } = 7;
        public string? LogDirectory => GetSources().FirstOrDefault(source => !source.SettingsOnly && Directory.Exists(source.Directory))?.Directory;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles() => CollectFiles(GetSources(),
            MvsDiagnosticsPaths.FindLogViewer(MvsDiagnosticsPaths.GetInstallationDirectories()), RecentDays, DateTime.UtcNow);

        private static IReadOnlyList<MvsLogSource> GetSources()
        {
            var sources = new List<MvsLogSource>();
            foreach (string directory in MvsDiagnosticsPaths.GetLogServiceDirectories())
            {
                sources.Add(new("ServiceSettings", directory, true));
                try
                {
                    string? configured = GetConfiguredSdkLogDirectory(File.ReadAllText(Path.Combine(directory, "Path.ini")));
                    if (configured != null) sources.Add(new("SDK", configured));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            }
            sources.Add(new("SDK", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp", "MvSDKLog")));
            sources.Add(new("SDK", Path.Combine(Path.GetTempPath(), "MvSDKLog")));
            sources.Add(new("SDK", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MvSDKLog")));
            sources.Add(new("Client", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MVS", "Log")));
            sources.Add(new("Client", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MVS", "Log")));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var counts = new Dictionary<string, int>();
            var unique = new List<MvsLogSource>();
            foreach (MvsLogSource source in sources)
            {
                string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.Directory));
                if (!seen.Add(directory)) continue;
                int count = counts.GetValueOrDefault(source.Name) + 1;
                counts[source.Name] = count;
                unique.Add(source with { Name = count == 1 ? source.Name : $"{source.Name}_{count}", Directory = directory });
            }
            return unique;
        }

        internal static string? GetConfiguredSdkLogDirectory(string contents)
        {
            string path = Environment.ExpandEnvironmentVariables(contents.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(path)) return null;
            try { return Path.Combine(Path.GetFullPath(path), "MvSDKLog"); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { return null; }
        }

        internal static IReadOnlyList<(string EntryPath, string FilePath)> CollectFiles(
            IEnumerable<MvsLogSource> sources, string? logViewerPath, int recentDays, DateTime utcNow)
        {
            var results = new List<(string EntryPath, string FilePath)>();
            var sourceReports = new List<object>();
            var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            DateTime cutoffUtc = utcNow.AddDays(-Math.Max(1, recentDays));
            foreach (MvsLogSource source in sources)
            {
                string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.Directory));
                if (!seenDirectories.Add(directory)) continue;
                var fileReports = new List<object>();
                string status = "available";
                string? error = null;
                try
                {
                    FileInfo[] files = new DirectoryInfo(directory).GetFiles("*", SearchOption.TopDirectoryOnly);
                    foreach (FileInfo file in files.OrderByDescending(file => file.LastWriteTimeUtc))
                    {
                        bool settingsFile = file.Name.Equals("Path.ini", StringComparison.OrdinalIgnoreCase)
                            || file.Name.Equals("LogServer.ini", StringComparison.OrdinalIgnoreCase);
                        if (!settingsFile && (source.SettingsOnly || !IsLogFile(file.Name) || file.LastWriteTimeUtc < cutoffUtc)) continue;
                        string? copyPath = null;
                        try
                        {
                            // Snapshot the initial length; growing logs cannot extend the copy indefinitely.
                            using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            long bytes = input.Length;
                            if (bytes > MaxFileBytes || bytes > MaxTotalBytes - totalBytes)
                            {
                                fileReports.Add(new { file.Name, Status = "skippedSizeLimit", Bytes = bytes });
                                continue;
                            }
                            copyPath = Path.Combine(Path.GetTempPath(), $"ColorVision_MVS_{Guid.NewGuid():N}_{file.Name}");
                            using (var output = new FileStream(copyPath, FileMode.CreateNew, FileAccess.Write))
                            {
                                byte[] buffer = new byte[64 * 1024];
                                long remaining = bytes;
                                while (remaining > 0)
                                {
                                    int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                                    if (read == 0) break;
                                    output.Write(buffer, 0, read);
                                    remaining -= read;
                                }
                                totalBytes += output.Length;
                                fileReports.Add(new { file.Name, Status = "collected", Bytes = output.Length, SourceBytes = bytes });
                            }
                            results.Add(($"MVS/{source.Name}/{file.Name}", copyPath));
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                        {
                            fileReports.Add(new { file.Name, Status = "unavailable", Error = $"{ex.GetType().Name}: {ex.Message}" });
                            if (copyPath != null)
                            {
                                try { File.Delete(copyPath); }
                                catch (IOException) { }
                                catch (UnauthorizedAccessException) { }
                            }
                        }
                    }
                    if (fileReports.Count == 0) status = "noMatchingFiles";
                }
                catch (DirectoryNotFoundException) { status = "notFound"; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    status = "unavailable";
                    error = $"{ex.GetType().Name}: {ex.Message}";
                }
                sourceReports.Add(new { source.Name, Directory = directory, Status = status, Error = error, Files = fileReports });
            }

            string reportPath = Path.Combine(Path.GetTempPath(), $"ColorVision_MVS_{Guid.NewGuid():N}_collection.json");
            File.WriteAllText(reportPath, JsonConvert.SerializeObject(new
            {
                CollectedAtUtc = utcNow, RecentDays = Math.Max(1, recentDays), CutoffUtc = cutoffUtc,
                LogViewerPath = logViewerPath, MaxFileBytes, MaxTotalBytes, CollectedBytes = totalBytes,
                Sources = sourceReports,
                Note = "Existing local logs only; no camera connection, SDK log reconfiguration or privileged export. Active logs are best-effort copies.",
            }, Formatting.Indented));
            results.Add(("MVS/collection.json", reportPath));
            return results;
        }

        private static bool IsLogFile(string name)
        {
            if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) return true;
            int suffix = name.LastIndexOf(".log.", StringComparison.OrdinalIgnoreCase);
            return suffix >= 0 && int.TryParse(name[(suffix + 5)..], out int number) && number >= 0;
        }
    }
}
