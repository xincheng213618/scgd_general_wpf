using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.UI.ServiceHost;
using Microsoft.Win32;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;

namespace ColorVision.UI.Desktop.Diagnostics
{
    /// <summary>
    /// Reads and writes the Windows Error Reporting LocalDumps settings for the current executable.
    /// </summary>
    public sealed class CrashDumpConfiguration : ViewModelBase
    {
        private const string RegistryDefaultKeyPath = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";

        public static CrashDumpConfiguration Current { get; } = new();

        private string _dumpFolder = string.Empty;
        private CrashDumpType _dumpType;
        private int _dumpCount;
        private MiniDumpType _customDumpFlags;
        private string _customDumpFlagsText = string.Empty;

        public CrashDumpConfiguration(string? processExecutableName = null)
        {
            ProcessExecutableName = NormalizeExecutableName(processExecutableName ?? ResolveProcessExecutableName());
            Reload();
        }

        public string ProcessExecutableName { get; }

        public string RegistryKeyPath => $@"{RegistryDefaultKeyPath}\{ProcessExecutableName}";

        [ConfigSetting(Order = 85, Name = "转储类型", Description = "选择 Windows Error Reporting 在程序崩溃时保存的转储内容。")]
        [PropertyEditorType(typeof(CrashDumpTypePropertiesEditor))]
        public CrashDumpType DumpType
        {
            get => _dumpType;
            set => SetProperty(ref _dumpType, value);
        }

        [ConfigSetting(Order = 86, Name = "最多保留", Description = "最多保留的转储文件数量，允许 1 到 999。")]
        public int DumpCount
        {
            get => _dumpCount;
            set => SetProperty(ref _dumpCount, value);
        }

        [ConfigSetting(Order = 87, Name = "保存目录", Description = "转储文件的保存位置，必须使用绝对路径。", Layout = ConfigSettingLayout.Wide)]
        [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string DumpFolder
        {
            get => _dumpFolder;
            set => SetProperty(ref _dumpFolder, value);
        }

        [ConfigSetting(Order = 88, Name = "自定义标志", Description = "输入十进制整数或以 0x 开头的 MINIDUMP_TYPE 十六进制标志。")]
        [PropertyVisibility(nameof(DumpType), CrashDumpType.Custom)]
        public string CustomDumpFlagsText
        {
            get => _customDumpFlagsText;
            set => SetProperty(ref _customDumpFlagsText, value);
        }

        [Browsable(false)]
        public MiniDumpType CustomDumpFlags
        {
            get => _customDumpFlags;
            set
            {
                SetProperty(ref _customDumpFlags, value);
                CustomDumpFlagsText = FormatCustomDumpFlags(value);
            }
        }

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
            PrepareConfiguration();
            ApplyRegistrySettings();
            Reload();
        }

        public async Task ApplyAsync(IColorVisionServiceHostClient? serviceHostClient = null, CancellationToken cancellationToken = default)
        {
            PrepareConfiguration();
            if (Tool.IsAdministrator())
            {
                ApplyRegistrySettings();
                Reload();
                return;
            }

            serviceHostClient ??= ColorVisionServiceHostClient.Default;
            ServiceHostResponse response;
            try
            {
                var values = new List<ServiceHostRegistryValue>
                {
                    new("DumpFolder", RegistryValueKind.ExpandString, DumpFolder),
                    new("DumpCount", RegistryValueKind.DWord, DumpCount),
                    new("DumpType", RegistryValueKind.DWord, (int)DumpType),
                };
                IReadOnlyCollection<string> deletedValueNames = [];
                if (DumpType == CrashDumpType.Custom)
                    values.Add(new ServiceHostRegistryValue("CustomDumpFlags", RegistryValueKind.DWord, (int)CustomDumpFlags));
                else
                    deletedValueNames = ["CustomDumpFlags"];

                response = await serviceHostClient.SetLocalMachineRegistryValuesAsync(
                    RegistryKeyPath,
                    values,
                    deletedValueNames,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
            {
                throw new InvalidOperationException("无法连接 ColorVision Service Host，请先在“更新”中安装或更新后台特权服务。", ex);
            }

            EnsureServiceHostSucceeded(response, "应用崩溃转储设置");
            Reload();
        }

        public void Clear()
        {
            EnsureAdministrator();
            ClearRegistrySettings();
            Reload();
        }

        public async Task ClearAsync(IColorVisionServiceHostClient? serviceHostClient = null, CancellationToken cancellationToken = default)
        {
            if (Tool.IsAdministrator())
            {
                ClearRegistrySettings();
                Reload();
                return;
            }

            serviceHostClient ??= ColorVisionServiceHostClient.Default;
            ServiceHostResponse response;
            try
            {
                response = await serviceHostClient.DeleteLocalMachineRegistryKeyAsync(
                    RegistryKeyPath,
                    recursive: true,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
            {
                throw new InvalidOperationException("无法连接 ColorVision Service Host，请先在“更新”中安装或更新后台特权服务。", ex);
            }

            EnsureServiceHostSucceeded(response, "清除崩溃转储设置");
            Reload();
        }

        public string SaveCurrentProcessDump()
        {
            PrepareConfiguration();

            string processName = Path.GetFileNameWithoutExtension(ProcessExecutableName);
            string filePath = Path.Combine(DumpFolder, $"{processName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.dmp");
            DumpHelper.WriteMiniDump(filePath, ResolveMiniDumpFlags());
            return filePath;
        }

        public static MiniDumpType ParseCustomDumpFlags(string text)
        {
            string value = text.Trim();
            bool isHex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (isHex) value = value[2..];

            NumberStyles style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;
            if (!int.TryParse(value, style, CultureInfo.InvariantCulture, out int flags))
                throw new FormatException("自定义标志应为十进制整数或 0x 开头的十六进制整数。");
            if ((flags & ~(int)MiniDumpType.MiniDumpValidTypeFlags) != 0)
                throw new ArgumentOutOfRangeException(nameof(text), "自定义转储标志包含不支持的位。");

            return (MiniDumpType)flags;
        }

        private void PrepareConfiguration()
        {
            ValidateDumpFolder();
            if (DumpCount is < 1 or > 999)
                throw new ArgumentOutOfRangeException(nameof(DumpCount), "保留数量必须在 1 到 999 之间。");
            if (!Enum.IsDefined(DumpType))
                throw new ArgumentOutOfRangeException(nameof(DumpType), "无效的转储类型。");
            if (DumpType == CrashDumpType.Custom)
                CustomDumpFlags = ParseCustomDumpFlags(CustomDumpFlagsText);
            if (((int)CustomDumpFlags & ~(int)MiniDumpType.MiniDumpValidTypeFlags) != 0)
                throw new ArgumentOutOfRangeException(nameof(CustomDumpFlags), "自定义转储标志包含不支持的位。");

            DumpFolder = Path.GetFullPath(Environment.ExpandEnvironmentVariables(DumpFolder.Trim()));
            Directory.CreateDirectory(DumpFolder);
        }

        private void ApplyRegistrySettings()
        {
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

        private void ClearRegistrySettings()
        {
            Registry.LocalMachine.DeleteSubKeyTree(RegistryKeyPath, throwOnMissingSubKey: false);
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

        private void ValidateDumpFolder()
        {
            if (string.IsNullOrWhiteSpace(DumpFolder))
                throw new ArgumentException("请选择转储保存目录。", nameof(DumpFolder));

            string expandedPath = Environment.ExpandEnvironmentVariables(DumpFolder.Trim());
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

        private static void EnsureServiceHostSucceeded(ServiceHostResponse response, string operation)
        {
            if (response.Success) return;

            if (response.Message.StartsWith("Unsupported command", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ColorVision Service Host 版本过旧，请先在“更新”中更新后台特权服务。");

            throw new InvalidOperationException($"{operation}失败：{response.Message}");
        }

        private static void EnsureAdministrator()
        {
            if (!Tool.IsAdministrator())
                throw new UnauthorizedAccessException("写入 Windows Error Reporting 的 HKLM 设置需要管理员权限或 ColorVision Service Host。");
        }

        private static string FormatCustomDumpFlags(MiniDumpType flags) => $"0x{(int)flags:X8}";

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
