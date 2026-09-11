using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services
{
    /// <summary>
    /// Collects Engine backend service logs (WindowsServiceX64, WindowsServiceDev, RegistrationCenterService)
    /// for the feedback system.
    /// </summary>
    public class ServiceLogCollector : IFeedbackLogCollector, IFeedbackLogTimeRangeCollector, IFeedbackDiagnosticCleanupSource
    {
        private const long MaxFileBytes = 50L * 1024 * 1024;
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceLogCollector));

        public string Name => "Engine Service Logs";
        public string Description => "Recent logs and service metadata from Engine services";
        public int Order => 20;
        public int RecentDays { get; set; } = 7;
        public string? LogDirectory => GetPrimaryLogDirectory();

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();

            foreach (var (name, info) in GetServices())
            {
                if (info == null || !info.Exists || string.IsNullOrEmpty(info.ExecutablePath))
                    continue;

                string? baseDir = null;
                try
                {
                    baseDir = Directory.GetParent(info.ExecutablePath)?.FullName;
                }
                catch (Exception ex)
                {
                    log.Debug($"Could not get parent dir for {name}: {ex.Message}");
                    continue;
                }

                if (string.IsNullOrEmpty(baseDir))
                    continue;

                string logDir = Path.Combine(baseDir, "log");
                if (!Directory.Exists(logDir))
                    continue;

                foreach (var file in GetRecentLogFiles(logDir, RecentDays, DateTime.UtcNow))
                {
                    try
                    {
                        string tempCopy = Path.Combine(Path.GetTempPath(), $"svclog_{name}_{Guid.NewGuid():N}_{file.Name}");
                        file.CopyTo(tempCopy, true);

                        // Preserve subdirectory structure in zip
                        string relativePath = Path.GetRelativePath(logDir, file.FullName);
                        results.Add(($"ServiceLogs/{name}/{relativePath}", tempCopy));
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"Could not collect service log {file.FullName}: {ex.Message}");
                    }
                }

                // Also include service info as a text file
                try
                {
                    string infoTempPath = Path.Combine(Path.GetTempPath(), $"svcinfo_{name}.txt");
                    File.WriteAllText(infoTempPath, info.ToString());
                    results.Add(($"ServiceLogs/{name}/_ServiceInfo.txt", infoTempPath));
                }
                catch (Exception ex)
                {
                    log.Debug($"Could not write service info for {name}: {ex.Message}");
                }
            }

            return results;
        }

        public IEnumerable<string> GetHistoricalDiagnosticFiles(DateTime preserveFromUtc)
        {
            var results = new List<string>();
            foreach ((string name, ServiceInfo? info) in GetServices())
            {
                if (info == null || !info.Exists || string.IsNullOrEmpty(info.ExecutablePath))
                    continue;

                try
                {
                    string? baseDirectory = Directory.GetParent(info.ExecutablePath)?.FullName;
                    if (baseDirectory == null)
                        continue;

                    string logDirectory = Path.Combine(baseDirectory, "log");
                    if (!Directory.Exists(logDirectory))
                        continue;

                    IEnumerable<FileInfo> files = new DirectoryInfo(logDirectory)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .GroupBy(file => file.DirectoryName, StringComparer.OrdinalIgnoreCase)
                        .SelectMany(group => group
                            .OrderByDescending(file => file.LastWriteTimeUtc)
                            .Skip(1)
                            .Where(file => file.LastWriteTimeUtc < preserveFromUtc));
                    results.AddRange(files.Select(file => file.FullName));
                }
                catch (Exception ex)
                {
                    log.Debug($"Could not enumerate historical service logs for {name}: {ex.Message}");
                }
            }

            return results;
        }

        internal static IReadOnlyList<FileInfo> GetRecentLogFiles(string logDir, int recentDays, DateTime utcNow)
        {
            try
            {
                DateTime cutoffUtc = utcNow.AddDays(-Math.Max(1, recentDays));
                return new DirectoryInfo(logDir)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(file => file.Length <= MaxFileBytes && file.LastWriteTimeUtc >= cutoffUtc)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Debug($"Could not enumerate service log directory {logDir}: {ex.Message}");
                return Array.Empty<FileInfo>();
            }
        }

        private static string? GetPrimaryLogDirectory()
        {
            try
            {
                foreach (ServiceInfo? info in GetServices().Select(service => service.Info))
                {
                    if (info == null || !info.Exists || string.IsNullOrEmpty(info.ExecutablePath))
                        continue;

                    string? baseDir = Directory.GetParent(info.ExecutablePath)?.FullName;
                    if (baseDir == null)
                        continue;

                    string logDir = Path.Combine(baseDir, "log");
                    if (Directory.Exists(logDir))
                        return logDir;
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Could not resolve primary service log directory: {ex.Message}");
            }

            return null;
        }

        private static IReadOnlyList<(string Name, ServiceInfo? Info)> GetServices() =>
        [
            ("WindowsServiceX64", ServiceConfig.Instance.CVMainService_x64Info),
            ("WindowsServiceDev", ServiceConfig.Instance.CVMainService_devInfo),
            ("RegistrationCenter", ServiceConfig.Instance.RegistrationCenterServiceInfo),
            ("CVArchService", ServiceConfig.Instance.CVArchServiceInfo),
        ];
    }
}
