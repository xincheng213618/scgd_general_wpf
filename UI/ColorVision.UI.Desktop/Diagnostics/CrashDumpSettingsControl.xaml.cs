using ColorVision.Common.Utilities;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.Diagnostics
{
    public partial class CrashDumpSettingsControl : UserControl
    {
        private readonly CrashDumpConfiguration _configuration = new();
        private readonly IReadOnlyList<CrashDumpTypeOption> _dumpTypeOptions =
        [
            new(CrashDumpType.Mini, "小型转储（推荐）"),
            new(CrashDumpType.Full, "完整内存转储"),
            new(CrashDumpType.Custom, "自定义转储")
        ];

        public CrashDumpSettingsControl()
        {
            InitializeComponent();
            DumpTypeComboBox.ItemsSource = _dumpTypeOptions;
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            _configuration.Reload();
            DumpFolderTextBox.Text = _configuration.DumpFolder;
            DumpCountTextBox.Text = _configuration.DumpCount.ToString(CultureInfo.InvariantCulture);
            CustomFlagsTextBox.Text = $"0x{(int)_configuration.CustomDumpFlags:X8}";
            DumpTypeComboBox.SelectedItem = _dumpTypeOptions.First(option => option.Type == _configuration.DumpType);

            string privilege = Tool.IsAdministrator() ? "管理员模式，可应用或清除系统设置。" : "普通用户模式；手动保存可用，应用或清除系统设置需要管理员权限。";
            SetStatus($"当前目标：{_configuration.ProcessExecutableName}。{privilege}");
        }

        private void DumpTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isCustom = DumpTypeComboBox.SelectedItem is CrashDumpTypeOption option && option.Type == CrashDumpType.Custom;
            CustomFlagsPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择崩溃转储保存目录",
                Multiselect = false
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                DumpFolderTextBox.Text = dialog.FolderName;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdministrator()) return;

            try
            {
                UpdateConfigurationFromForm();
                _configuration.Apply();
                SetStatus($"已应用 Windows Error Reporting 设置：{_configuration.RegistryKeyPath}", isSuccess: true);
            }
            catch (Exception ex)
            {
                SetStatus($"应用失败：{ex.Message}", isError: true);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdministrator()) return;

            MessageBoxResult result = MessageBox.Show(
                Window.GetWindow(this),
                $"确定清除 {_configuration.ProcessExecutableName} 的 Windows Error Reporting 转储设置吗？",
                "清除崩溃转储设置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                _configuration.Clear();
                LoadConfiguration();
                SetStatus("已清除当前程序的专用转储设置；界面已回到系统默认值。", isSuccess: true);
            }
            catch (Exception ex)
            {
                SetStatus($"清除失败：{ex.Message}", isError: true);
            }
        }

        private async void SaveDumpButton_Click(object sender, RoutedEventArgs e)
        {
            SaveDumpButton.IsEnabled = false;
            try
            {
                UpdateConfigurationFromForm();
                SetStatus("正在保存当前进程 Dump，请稍候……");
                string filePath = await Task.Run(_configuration.SaveCurrentProcessDump);
                SetStatus($"Dump 已保存：{filePath}", isSuccess: true);
            }
            catch (Exception ex)
            {
                SetStatus($"保存失败：{ex.Message}", isError: true);
            }
            finally
            {
                SaveDumpButton.IsEnabled = true;
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folderPath = GetValidatedDumpFolder();
                Directory.CreateDirectory(folderPath);
                PlatformHelper.OpenFolder(folderPath);
            }
            catch (Exception ex)
            {
                SetStatus($"打开目录失败：{ex.Message}", isError: true);
            }
        }

        private void UpdateConfigurationFromForm()
        {
            if (DumpTypeComboBox.SelectedItem is not CrashDumpTypeOption option)
                throw new InvalidOperationException("请选择转储类型。");

            if (!int.TryParse(DumpCountTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int dumpCount)
                || dumpCount is < 1 or > 999)
            {
                throw new ArgumentOutOfRangeException(nameof(dumpCount), "保留数量必须在 1 到 999 之间。");
            }

            _configuration.DumpFolder = GetValidatedDumpFolder();
            _configuration.DumpCount = dumpCount;
            _configuration.DumpType = option.Type;
            _configuration.CustomDumpFlags = ParseCustomDumpFlags(CustomFlagsTextBox.Text);
        }

        private string GetValidatedDumpFolder()
        {
            string folderPath = Environment.ExpandEnvironmentVariables(DumpFolderTextBox.Text.Trim());
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("请选择转储保存目录。");
            if (!Path.IsPathFullyQualified(folderPath))
                throw new ArgumentException("转储保存目录必须是绝对路径。");
            return Path.GetFullPath(folderPath);
        }

        private static MiniDumpType ParseCustomDumpFlags(string text)
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

        private bool EnsureAdministrator()
        {
            if (Tool.IsAdministrator()) return true;

            MessageBox.Show(
                Window.GetWindow(this),
                "写入或清除 Windows Error Reporting 的 HKLM 设置需要以管理员身份运行 ColorVision。",
                "需要管理员权限",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            SetStatus("当前不是管理员模式，系统设置未更改。", isError: true);
            return false;
        }

        private void SetStatus(string message, bool isError = false, bool isSuccess = false)
        {
            StatusTextBlock.Text = message;
            if (isError)
                StatusTextBlock.Foreground = Brushes.IndianRed;
            else if (isSuccess)
                StatusTextBlock.Foreground = Brushes.SeaGreen;
            else
                StatusTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
        }

        private sealed record CrashDumpTypeOption(CrashDumpType Type, string DisplayName);
    }
}
