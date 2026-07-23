using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Security;

namespace ColorVision.UI.Desktop.Diagnostics
{
    /// <summary>
    /// Reads and writes the Windows Error Reporting LocalDumps settings for the current executable.
    /// </summary>
    public sealed class CrashDumpConfiguration
    {
        private const string RegistryDefaultKeyPath = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";

        public CrashDumpConfiguration(string? processExecutableName = null)
        {
            ProcessExecutableName = NormalizeExecutableName(processExecutableName ?? ResolveProcessExecutableName());
            Reload();
        }

        public string ProcessExecutableName { get; }

        public string RegistryKeyPath => $@"{RegistryDefaultKeyPath}\{ProcessExecutableName}";

        public string DumpFolder { get; set; } = string.Empty;

        public CrashDumpType DumpType { get; set; }

        public int DumpCount { get; set; }

        public MiniDumpType CustomDumpFlags { get; set; }

        public void Reload()
        {
            DumpFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
            DumpType = CrashDumpType.Mini;
            DumpCount = 10;
            CustomDumpFlags = MiniDumpType.MiniDumpNormal;

            LoadRegistryValues(RegistryDefaultKeyPath);
            LoadRegistryValues(RegistryKeyPath);
        }

        public void Apply()
        {
            EnsureAdministrator();
            Validate();

            DumpFolder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(DumpFolder));
            Directory.CreateDirectory(DumpFolder);

            using RegistryKey key = Registry.LocalMachine.CreateSubKey(RegistryKeyPath, writable: true)
                ?? throw new InvalidOperationException($"无法创建注册表项：{RegistryKeyPath}");

            key.SetValue("DumpFolder", DumpFolder, RegistryValueKind.ExpandString);
            key.SetValue("DumpCount", DumpCount, RegistryValueKind.DWord);
            key.SetValue("DumpType", (int)DumpType, RegistryValueKind.DWord);

            if (DumpType == CrashDumpType.Custom)
                key.SetValue("CustomDumpFlags", (int)CustomDumpFlags, RegistryValueKind.DWord);
            else
                key.DeleteValue("CustomDumpFlags", throwOnMissingValue: false);
        }

        public void Clear()
        {
            EnsureAdministrator();
            Registry.LocalMachine.DeleteSubKeyTree(RegistryKeyPath, throwOnMissingSubKey: false);
            Reload();
        }

        public string SaveCurrentProcessDump()
        {
            ValidateDumpFolder();

            string dumpFolder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(DumpFolder));
            Directory.CreateDirectory(dumpFolder);

            string processName = Path.GetFileNameWithoutExtension(ProcessExecutableName);
            string filePath = Path.Combine(dumpFolder, $"{processName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.dmp");
            DumpHelper.WriteMiniDump(filePath, ResolveMiniDumpFlags());
            return filePath;
        }

        private void LoadRegistryValues(string path)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
                if (key == null) return;

                if (key.GetValue("DumpFolder") is string dumpFolder && !string.IsNullOrWhiteSpace(dumpFolder))
                    DumpFolder = Environment.ExpandEnvironmentVariables(dumpFolder);

                if (key.GetValue("DumpCount") is int dumpCount && dumpCount > 0)
                    DumpCount = dumpCount;

                if (key.GetValue("DumpType") is int dumpType && Enum.IsDefined(typeof(CrashDumpType), dumpType))
                    DumpType = (CrashDumpType)dumpType;

                if (key.GetValue("CustomDumpFlags") is int customDumpFlags)
                    CustomDumpFlags = (MiniDumpType)customDumpFlags;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (SecurityException)
            {
            }
        }

        private void Validate()
        {
            ValidateDumpFolder();
            if (DumpCount is < 1 or > 999)
                throw new ArgumentOutOfRangeException(nameof(DumpCount), "保留数量必须在 1 到 999 之间。");
            if (!Enum.IsDefined(DumpType))
                throw new ArgumentOutOfRangeException(nameof(DumpType), "无效的转储类型。");
            if (((int)CustomDumpFlags & ~(int)MiniDumpType.MiniDumpValidTypeFlags) != 0)
                throw new ArgumentOutOfRangeException(nameof(CustomDumpFlags), "自定义转储标志包含不支持的位。");
        }

        private void ValidateDumpFolder()
        {
            if (string.IsNullOrWhiteSpace(DumpFolder))
                throw new ArgumentException("请选择转储保存目录。", nameof(DumpFolder));

            string expandedPath = Environment.ExpandEnvironmentVariables(DumpFolder);
            if (!Path.IsPathFullyQualified(expandedPath))
                throw new ArgumentException("转储保存目录必须是绝对路径。", nameof(DumpFolder));
        }

        private int ResolveMiniDumpFlags()
        {
            return DumpType switch
            {
                CrashDumpType.Mini => (int)MiniDumpType.MiniDumpNormal,
                CrashDumpType.Full => (int)MiniDumpType.MiniDumpWithFullMemory,
                CrashDumpType.Custom => (int)CustomDumpFlags,
                _ => (int)MiniDumpType.MiniDumpNormal
            };
        }

        private static void EnsureAdministrator()
        {
            if (!Tool.IsAdministrator())
                throw new UnauthorizedAccessException("写入 Windows Error Reporting 的 HKLM 设置需要管理员权限。");
        }

        private static string ResolveProcessExecutableName()
        {
            string? fileName = Path.GetFileName(Environment.ProcessPath);
            if (!string.IsNullOrWhiteSpace(fileName)) return fileName;

            return Assembly.GetEntryAssembly()?.GetName().Name ?? "ColorVision";
        }

        private static string NormalizeExecutableName(string executableName)
        {
            string fileName = Path.GetFileName(executableName.Trim());
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "ColorVision";
            return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? fileName : $"{fileName}.exe";
        }
    }
}
