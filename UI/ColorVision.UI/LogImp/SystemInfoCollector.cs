using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// Collects system diagnostic information (OS, hardware, app version, etc.) for the feedback system.
    /// </summary>
    public class SystemInfoCollector : IFeedbackLogCollector
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SystemInfoCollector));

        public string Name => "System Information";
        public int Order => 10;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            string? tempPath = null;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== ColorVision System Information ===");
                sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                // Application info
                sb.AppendLine("--- Application ---");
                var entryAssembly = Assembly.GetEntryAssembly();
                sb.AppendLine($"Name: {entryAssembly?.GetName().Name}");
                sb.AppendLine($"Version: {entryAssembly?.GetName().Version}");
                sb.AppendLine($"Location: {entryAssembly?.Location}");
                sb.AppendLine($"Runtime: {Environment.Version}");
                sb.AppendLine($"Is64BitProcess: {Environment.Is64BitProcess}");
                sb.AppendLine();

                // OS info
                sb.AppendLine("--- Operating System ---");
                sb.AppendLine($"OS: {Environment.OSVersion}");
                sb.AppendLine($"Is64BitOS: {Environment.Is64BitOperatingSystem}");
                sb.AppendLine($"MachineName: {Environment.MachineName}");
                sb.AppendLine($"UserName: {Environment.UserName}");
                sb.AppendLine($"UserDomainName: {Environment.UserDomainName}");
                sb.AppendLine($"SystemDirectory: {Environment.SystemDirectory}");
                sb.AppendLine();

                // Hardware info
                sb.AppendLine("--- Hardware ---");
                sb.AppendLine($"ProcessorCount: {Environment.ProcessorCount}");
                try
                {
                    var process = Process.GetCurrentProcess();
                    sb.AppendLine($"WorkingSet64: {process.WorkingSet64 / 1024 / 1024} MB");
                    sb.AppendLine($"PrivateMemorySize64: {process.PrivateMemorySize64 / 1024 / 1024} MB");
                }
                catch { }
                sb.AppendLine();

                // Environment
                sb.AppendLine("--- Environment ---");
                sb.AppendLine($"CurrentDirectory: {Environment.CurrentDirectory}");
                sb.AppendLine($"CommandLine: {Environment.CommandLine}");
                sb.AppendLine($"TickCount: {Environment.TickCount64 / 1000 / 60} minutes uptime");
                sb.AppendLine();

                // Loaded assemblies summary
                sb.AppendLine("--- Loaded Assemblies (summary) ---");
                try
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var asm in assemblies)
                    {
                        try
                        {
                            sb.AppendLine($"  {asm.GetName().Name} v{asm.GetName().Version}");
                        }
                        catch { }
                    }
                }
                catch { }

                tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_SystemInfo_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(tempPath, sb.ToString(), Encoding.UTF8);

                return new[] { ("SystemInfo.txt", tempPath) };
            }
            catch (Exception ex)
            {
                log.Debug($"SystemInfoCollector failed: {ex.Message}");
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                return Array.Empty<(string, string)>();
            }
        }
    }
}
