using log4net;
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

                AppendApplicationInfo(sb);
                AppendOperatingSystemInfo(sb);
                AppendHardwareInfo(sb);
                AppendEnvironmentInfo(sb);
                AppendLoadedAssemblies(sb);

                tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_SystemInfo_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.txt");
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

        private static void AppendApplicationInfo(StringBuilder sb)
        {
            sb.AppendLine("--- Application ---");
            var entryAssembly = Assembly.GetEntryAssembly();
            var assemblyName = entryAssembly?.GetName();
            sb.AppendLine($"Name: {assemblyName?.Name}");
            sb.AppendLine($"Version: {assemblyName?.Version}");
            sb.AppendLine($"Location: {entryAssembly?.Location}");
            sb.AppendLine($"Runtime: {Environment.Version}");
            sb.AppendLine($"Is64BitProcess: {Environment.Is64BitProcess}");
            sb.AppendLine();
        }

        private static void AppendOperatingSystemInfo(StringBuilder sb)
        {
            sb.AppendLine("--- Operating System ---");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"Is64BitOS: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"MachineName: {Environment.MachineName}");
            sb.AppendLine($"UserName: {Environment.UserName}");
            sb.AppendLine($"UserDomainName: {Environment.UserDomainName}");
            sb.AppendLine($"SystemDirectory: {Environment.SystemDirectory}");
            sb.AppendLine();
        }

        private static void AppendHardwareInfo(StringBuilder sb)
        {
            sb.AppendLine("--- Hardware ---");
            sb.AppendLine($"ProcessorCount: {Environment.ProcessorCount}");
            try
            {
                using var process = Process.GetCurrentProcess();
                sb.AppendLine($"WorkingSet64: {process.WorkingSet64 / 1024 / 1024} MB");
                sb.AppendLine($"PrivateMemorySize64: {process.PrivateMemorySize64 / 1024 / 1024} MB");
            }
            catch (Exception ex)
            {
                log.Debug($"Could not collect process memory info: {ex.Message}");
            }
            sb.AppendLine();
        }

        private static void AppendEnvironmentInfo(StringBuilder sb)
        {
            sb.AppendLine("--- Environment ---");
            sb.AppendLine($"CurrentDirectory: {Environment.CurrentDirectory}");
            sb.AppendLine($"CommandLine: {Environment.CommandLine}");
            sb.AppendLine($"TickCount: {Environment.TickCount64 / 1000 / 60} minutes uptime");
            sb.AppendLine();
        }

        private static void AppendLoadedAssemblies(StringBuilder sb)
        {
            sb.AppendLine("--- Loaded Assemblies (summary) ---");
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in assemblies)
                {
                    AppendAssemblyInfo(sb, asm);
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Could not enumerate loaded assemblies: {ex.Message}");
            }
        }

        private static void AppendAssemblyInfo(StringBuilder sb, Assembly assembly)
        {
            try
            {
                var assemblyName = assembly.GetName();
                sb.AppendLine($"  {assemblyName.Name} v{assemblyName.Version}");
            }
            catch (Exception ex)
            {
                log.Debug($"Could not read assembly name: {ex.Message}");
            }
        }
    }
}
