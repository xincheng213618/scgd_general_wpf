using ColorVision.Common.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.Diagnostics
{
    public partial class CrashDumpSettingsControl : UserControl
    {
        private readonly CrashDumpConfiguration _configuration = CrashDumpConfiguration.Current;

        public CrashDumpSettingsControl()
        {
            InitializeComponent();
            _configuration.Reload();

            string privilege = Tool.IsAdministrator()
                ? "当前为管理员模式，系统设置可直接写入。"
                : "当前为普通用户模式，系统设置将由 ColorVision Service Host 代为写入。";
            SetStatus($"当前目标：{_configuration.ProcessExecutableName}。{privilege}");
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            try
            {
                SetStatus(Tool.IsAdministrator() ? "正在应用系统设置……" : "正在通过后台特权服务应用系统设置……");
                await _configuration.ApplyAsync();
                SetStatus($"已应用 Windows Error Reporting 设置：{_configuration.RegistryKeyPath}", isSuccess: true);
            }
            catch (Exception ex)
            {
                SetStatus($"应用失败：{ex.Message}", isError: true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                Window.GetWindow(this),
                $"确定清除 {_configuration.ProcessExecutableName} 的 Windows Error Reporting 转储设置吗？",
                "清除崩溃转储设置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            SetBusy(true);
            try
            {
                SetStatus(Tool.IsAdministrator() ? "正在清除系统设置……" : "正在通过后台特权服务清除系统设置……");
                await _configuration.ClearAsync();
                SetStatus("已清除当前程序的专用转储设置；界面已回到系统默认值。", isSuccess: true);
            }
            catch (Exception ex)
            {
                SetStatus($"清除失败：{ex.Message}", isError: true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void SaveDumpButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            try
            {
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
                SetBusy(false);
            }
        }

        private void SetBusy(bool isBusy)
        {
            ApplyButton.IsEnabled = !isBusy;
            ClearButton.IsEnabled = !isBusy;
            SaveDumpButton.IsEnabled = !isBusy;
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
    }
}
