using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using log4net;
using System;
using System.IO;
using System.Text;
using System.Windows.Controls;

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

        public int Order => 100;

        /// <summary>
        /// Service log definitions: (panelId, panelTitle, logFilePrefix or null for main log).
        /// When logFilePrefix is null, the main daily log is used.
        /// When logFilePrefix is specified, LogFileHelper.GetMostRecentLogFile is used.
        /// </summary>
        private static readonly (string PanelId, string Title, string? LogFilePrefix)[] ServiceLogs = new[]
        {
            ("ServiceLog_x64",          "x64服务日志",         (string?)null),
            ("ServiceLog_Camera",       "Camera服务日志",      "CVMainWindowsService_x64_camera"),
            ("ServiceLog_Algorithm",    "Algorithm服务日志",   "CVMainWindowsService_x64_Algorithm"),
            ("ServiceLog_CVOLED",       "CVOLED服务日志",      "CVMainWindowsService_x64_CVOLED"),
            ("ServiceLog_Spectrum",     "Spectrum服务日志",     "CVMainWindowsService_x64_Spectrum_"),
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

                // Try to get the service base directory from registry (fast, no async wait needed)
                string? serviceBaseDir = GetServiceBaseDir();

                if (string.IsNullOrEmpty(serviceBaseDir))
                {
                    log.Debug("CVMainService_x64 not installed or path unavailable, skipping service log panels");
                    return;
                }

                string logDir = Path.Combine(serviceBaseDir, "log");

                foreach (var (panelId, title, logFilePrefix) in ServiceLogs)
                {
                    try
                    {
                        string? logPath = GetLogFilePath(serviceBaseDir, logDir, logFilePrefix);

                        if (string.IsNullOrEmpty(logPath))
                        {
                            log.Debug($"No log file found for {panelId}, skipping");
                            continue;
                        }

                        var logOutput = new LogLocalOutput(logPath, Encoding.GetEncoding("GB2312"));

                        layoutManager.RegisterPanel(panelId, logOutput, title, PanelPosition.Bottom);
                        log.Info($"Registered service log panel: {title} -> {logPath}");
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"Failed to register service log panel {panelId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug($"ServiceLogPanelProvider failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the base directory of the x64 service from ServiceInfo or registry.
        /// </summary>
        private static string? GetServiceBaseDir()
        {
            // First try from the already-populated ServiceConfig (may be populated from persisted config)
            try
            {
                string? x64Path = ServiceConfig.Instance?.CVMainService_x64;
                if (!string.IsNullOrEmpty(x64Path) && File.Exists(x64Path))
                {
                    return Directory.GetParent(x64Path)?.FullName;
                }
            }
            catch
            {
                // Fall through
            }

            // Fallback: check the registry directly
            string? exePath = ServiceManagerUitl.GetServiceExecutablePath("CVMainService_x64");
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
                // Device/module-specific log: most recent file matching prefix
                if (Directory.Exists(logDir))
                {
                    string? logPath = LogFileHelper.GetMostRecentLogFile(logDir, logFilePrefix);
                    if (!string.IsNullOrEmpty(logPath))
                        return logPath;
                }

                // Fallback: construct expected path (file may not exist yet, LogLocalOutput handles this)
                return Path.Combine(logDir, $"{logFilePrefix}{DateTime.Now:yyyyMMdd}.log");
            }
        }
    }
}
