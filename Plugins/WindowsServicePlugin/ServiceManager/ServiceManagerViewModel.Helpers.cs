using ColorVision.Common.Utilities;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 辅助方法：管理员提权、Shell命令、文件/文件夹打开、路径设置、安装管理器窗口
    /// </summary>
    public partial class ServiceManagerViewModel
    {
        private bool EnsureElevatedOrRestart(string actionName)
        {
            if (Tool.IsAdministrator()) return true;

            if (MessageBox.Show($"{actionName}需要管理员权限，是否以管理员模式重启并重新打开服务管理器？", "需要管理员权限", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                RestartAsAdministratorToServiceManager();
            }
            return false;
        }

        private void RestartAsAdministratorToServiceManager()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    AddLog("无法获取当前程序路径，不能以管理员模式重开");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-c ServiceManager",
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                Process.Start(psi);
                Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                AddLog($"管理员模式重开失败: {ex.Message}");
            }
        }

        private bool ExecuteShellCommand(string command, bool requireAdmin)
        {
            return requireAdmin
                ? WinServiceHelper.ExecuteCommand(command, true)
                : WinServiceHelper.ExecuteCommand(command, false);
        }

        private void RefreshServiceEntryStatus(ServiceEntry entry)
        {
            Application.Current?.Dispatcher.Invoke(() => entry.RefreshStatus());
        }

        private void OpenLegacyConfigFile()
        {
            string? filePath = GetLegacyAppConfigPath();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AddLog("旧版 App.config 不存在");
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
                AddLog($"目录不存在: {entry.DisplayName}");
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
                AddLog($"无法定位 {entry.DisplayName} 的目录");
                return;
            }

            string filePath = Path.Combine(serviceDir, "cfg", fileName);
            if (!File.Exists(filePath))
            {
                AddLog($"配置文件不存在: {filePath}");
                return;
            }
            PlatformHelper.OpenFolderAndSelectFile(filePath);
        }

        private void OpenServiceLog4Net(ServiceEntry? entry)
        {
            if (entry == null)
                return;

            string? serviceDir = !string.IsNullOrWhiteSpace(entry.ExePath)
                ? Path.GetDirectoryName(entry.ExePath)
                : (!string.IsNullOrWhiteSpace(Config.BaseLocation) ? Path.Combine(Config.BaseLocation, entry.FolderName) : null);
            if (string.IsNullOrWhiteSpace(serviceDir))
            {
                AddLog($"无法定位 {entry.DisplayName} 的目录");
                return;
            }

            string[] candidates =
            [
                Path.Combine(serviceDir, "log4net.config"),
                Path.Combine(serviceDir, "cfg", "log4net.config")
            ];

            string? filePath = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AddLog($"log4net 配置文件不存在: {entry.DisplayName}");
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
                OnPropertyChanged(nameof(LegacyConfigPath));
                OnPropertyChanged(nameof(HasLegacyConfig));
                RefreshAll();
            }
        }

        private void OpenInstallManager()
        {
            EnsureElevatedOrRestart("更新");
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
