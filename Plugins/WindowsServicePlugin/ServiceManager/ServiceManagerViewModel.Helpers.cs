using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 辅助方法：提权、Shell命令、文件/文件夹打开、路径设置、安装管理器窗口
    /// </summary>
    public partial class ServiceManagerViewModel
    {
        private void OpenLegacyConfigFile()
        {
            string? filePath = GetLegacyAppConfigPath();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                log.Info("旧版 App.config 不存在");
                return;
            }
            PlatformHelper.OpenFolderAndSelectFile(filePath);
        }

        private void OpenServiceFolder(ServiceEntry? entry)
        {
            if (entry == null)
                return;

            string? path = !string.IsNullOrWhiteSpace(entry.ExePath)
                ? Path.GetDirectoryName(entry.ExePath)
                : (!string.IsNullOrWhiteSpace(Config.BaseLocation) ? Path.Combine(Config.BaseLocation, entry.FolderName) : null);
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                log.Info($"目录不存在: {entry.DisplayName}");
                return;
            }
            PlatformHelper.OpenFolder(path);
        }

        private void OpenServiceFile(ServiceEntry? entry, string fileName)
        {
            if (entry == null)
                return;

            string? serviceDir = !string.IsNullOrWhiteSpace(entry.ExePath)
                ? Path.GetDirectoryName(entry.ExePath)
                : (!string.IsNullOrWhiteSpace(Config.BaseLocation) ? Path.Combine(Config.BaseLocation, entry.FolderName) : null);
            if (string.IsNullOrWhiteSpace(serviceDir))
            {
                log.Info($"无法定位 {entry.DisplayName} 的目录");
                return;
            }

            string filePath = Path.Combine(serviceDir, "cfg", fileName);
            if (!File.Exists(filePath))
            {
                log.Info($"配置文件不存在: {filePath}");
                return;
            }
            PlatformHelper.OpenFolderAndSelectFile(filePath);
        }

        private void SetBasePath()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择服务安装根目录 (CVWindowsService所在目录)",
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Config.BaseLocation = dlg.SelectedPath;
                SaveServiceManagerConfig();
                OnPropertyChanged(nameof(LegacyConfigPath));
                OnPropertyChanged(nameof(HasLegacyConfig));
                RefreshAll();
            }
        }

        private static void SaveServiceManagerConfig()
        {
            ConfigHandler.GetInstance().Save<ServiceManagerConfig>();
        }

        private void OpenInstallManager()
        {
            var installWindow = new ServiceInstallWindow
            {
                Owner = Application.Current.GetActiveWindow()
            };
            installWindow.Show();

            // 刷新状态
            RefreshAll();
        }
    }
}
