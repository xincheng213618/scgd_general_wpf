using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.Services
{
    /// <summary>
    /// Collects Engine backend service logs (WindowsServiceX64, WindowsServiceDev, RegistrationCenterService)
    /// for the feedback system.
    /// </summary>
    public class ServiceLogCollector : IFeedbackLogCollector
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceLogCollector));

        public string Name => "Engine Service Logs";
        public int Order => 20;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();

            var services = new[]
            {
                ("WindowsServiceX64", ServiceConfig.Instance.CVMainService_x64Info),
                ("WindowsServiceDev", ServiceConfig.Instance.CVMainService_devInfo),
                ("RegistrationCenter", ServiceConfig.Instance.RegistrationCenterServiceInfo),
                ("CVArchService", ServiceConfig.Instance.CVArchServiceInfo),
            };

            foreach (var (name, info) in services)
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

                // Collect the most recent log files (up to 10 per service)
                int count = 0;
                try
                {
                    foreach (var file in Directory.GetFiles(logDir, "*.log", SearchOption.AllDirectories))
                    {
                        if (count >= 10) break;

                        try
                        {
                            var fi = new FileInfo(file);
                            // Only collect recent files (last 3 days) and size < 50MB
                            if (fi.LastWriteTime < DateTime.Now.AddDays(-3)) continue;
                            if (fi.Length > 50 * 1024 * 1024) continue;

                            string tempCopy = Path.Combine(Path.GetTempPath(), $"svclog_{name}_{Path.GetFileName(file)}");
                            File.Copy(file, tempCopy, true);

                            // Preserve subdirectory structure in zip
                            string relativePath = Path.GetRelativePath(logDir, file);
                            results.Add(($"ServiceLogs/{name}/{relativePath}", tempCopy));
                            count++;
                        }
                        catch (Exception ex)
                        {
                            log.Debug($"Could not collect service log {file}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Debug($"Error enumerating log dir for {name}: {ex.Message}");
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
    }
}
