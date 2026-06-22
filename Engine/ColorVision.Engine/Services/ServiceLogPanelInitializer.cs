using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using log4net;
using System;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services
{
    /// <summary>
    /// Registers service log panels in the ColorVision MainWindow DockingManager.
    /// Discovers installed Windows services (WindowsServiceX64, WindowsServiceDev, etc.)
    /// and registers their log files as embeddable panels using LogLocalOutput.
    /// Called before LoadLayout to ensure proper layout persistence.
    /// </summary>
    public class ServiceLogPanelProvider : IDockPanelProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceLogPanelProvider));
        private static readonly Encoding ServiceLogEncoding = Encoding.GetEncoding("GB2312");

        public int Order => 100;

        /// <summary>
        /// Service log definitions: (panelId, panelTitle, logFilePrefix or null for main log).
        /// When logFilePrefix is null, the main daily log is used.
        /// When logFilePrefix is specified, LogFileHelper.GetMostRecentLogFile is used.
        /// </summary>
        private static readonly (string PanelId, string Title, string? LogFilePrefix)[] X64ServiceLogs = new[]
        {
            ("ServiceLog_x64",          Properties.Resources.ServiceLogX64,         (string?)null),
            ("ServiceLog_Camera",       Properties.Resources.ServiceLogCamera,      "CVMainWindowsService_x64_camera"),
            ("ServiceLog_Algorithm",    Properties.Resources.ServiceLogAlgorithm,   "CVMainWindowsService_x64_Algorithm"),
            ("ServiceLog_CVOLED",       Properties.Resources.ServiceLogCVOLED,      "CVMainWindowsService_x64_CVOLED"),
            ("ServiceLog_Spectrum",     Properties.Resources.ServiceLogSpectrum,     "CVMainWindowsService_x64_Spectrum"),
        };

        private static readonly (string PanelId, string Title, string? LogFilePrefix)[] DevServiceLogs = new[]
        {
            ("ServiceLog_dev",          Properties.Resources.ServiceLogDev,         (string?)null),
            ("ServiceLog_SMU",          Properties.Resources.ServiceLogSMU,         "CVMainWindowsService_dev_SMU"),
        };

        private static readonly (string ServiceName, Func<string?> ConfiguredPathAccessor, (string PanelId, string Title, string? LogFilePrefix)[] Logs)[] ServiceGroups =
        {
            ("CVMainService_x64", () => ServiceConfig.Instance?.CVMainService_x64, X64ServiceLogs),
            ("CVMainService_dev", () => ServiceConfig.Instance?.CVMainService_dev, DevServiceLogs),
        };


        public void RegisterPanels()
        {
            try
            {
                var layoutManager = WorkspaceManager.LayoutManager;
                if (layoutManager == null)
                {
                    log.Debug("LayoutManager not available, skipping service log panel registration");
                    return;
                }

                foreach (var (serviceName, configuredPathAccessor, logs) in ServiceGroups)
                {
                    RegisterServicePanels(layoutManager, serviceName, configuredPathAccessor, logs);
                }

            }
            catch (Exception ex)
            {
                log.Debug($"ServiceLogPanelProvider failed: {ex.Message}");
            }
        }

        public static string? GetServiceLogPath(string panelId)
        {
            if (string.IsNullOrWhiteSpace(panelId))
                return null;

            foreach (var (serviceName, configuredPathAccessor, logs) in ServiceGroups)
            {
                foreach (var (logPanelId, _, logFilePrefix) in logs)
                {
                    if (!string.Equals(logPanelId, panelId, StringComparison.Ordinal))
                        continue;

                    string? serviceBaseDir = GetServiceBaseDir(configuredPathAccessor, serviceName);
                    if (string.IsNullOrEmpty(serviceBaseDir))
                        return null;

                    return GetLogFilePath(serviceBaseDir, Path.Combine(serviceBaseDir, "log"), logFilePrefix);
                }
            }

            return null;
        }

        /// <summary>
        /// Register all configured log panels for a single Windows service.
        /// </summary>
        private static void RegisterServicePanels(
            DockLayoutManager layoutManager,
            string serviceName,
            Func<string?> configuredPathAccessor,
            (string PanelId, string Title, string? LogFilePrefix)[] serviceLogs)
        {
            string? serviceBaseDir = GetServiceBaseDir(configuredPathAccessor, serviceName);
            if (string.IsNullOrEmpty(serviceBaseDir))
            {
                log.Debug($"{serviceName} not installed or path unavailable, skipping service log panels");
                return;
            }

            string logDir = Path.Combine(serviceBaseDir, "log");

            foreach (var (panelId, title, logFilePrefix) in serviceLogs)
            {
                try
                {
                    string? logPath = GetLogFilePath(serviceBaseDir, logDir, logFilePrefix);
                    if (string.IsNullOrEmpty(logPath))
                    {
                        log.Debug($"No log file found for {panelId} under {serviceName}, skipping");
                        continue;
                    }

                    var logOutput = new LogLocalOutput(logPath, ServiceLogEncoding);
                    layoutManager.RegisterPanel(panelId, logOutput, title, PanelPosition.Bottom, isDefaultVisible: false);
                    log.Info($"Registered service log panel: {title} -> {logPath}");
                }
                catch (Exception ex)
                {
                    log.Debug($"Failed to register service log panel {panelId}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get the base directory of a service from persisted config or registry.
        /// </summary>
        private static string? GetServiceBaseDir(Func<string?> configuredPathAccessor, string serviceName)
        {
            // First try from the already-populated ServiceConfig (may be populated from persisted config)
            try
            {
                string? configuredPath = configuredPathAccessor();
                if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
                {
                    return Directory.GetParent(configuredPath)?.FullName;
                }
            }
            catch
            {
                // Fall through
            }

            // Fallback: check the registry directly
            string? exePath = ServiceManagerUitl.GetServiceExecutablePath(serviceName);
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    return Directory.GetParent(exePath)?.FullName;
                }
                catch
                {
                    // Fall through
                }
            }

            return null;
        }

        /// <summary>
        /// Get the appropriate log file path for a service log.
        /// Dynamic module logs include timestamp and pid segments, so they can only be resolved
        /// from an existing file and should not fall back to a fabricated path.
        /// </summary>
        private static string? GetLogFilePath(string serviceBaseDir, string logDir, string? logFilePrefix)
        {
            if (logFilePrefix == null)
            {
                // Main daily log: {baseDir}/log/{yyyyMMdd}.log
                return LogFileHelper.GetLatestMainLogPath(serviceBaseDir);
            }
            else
            {
                if (!Directory.Exists(logDir))
                    return null;

                return LogFileHelper.GetMostRecentLogFile(logDir, logFilePrefix);
            }
        }
    }
}
