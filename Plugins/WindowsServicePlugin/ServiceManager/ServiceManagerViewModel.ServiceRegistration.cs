using System.IO;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    public partial class ServiceManagerViewModel
    {
        private async Task RegisterPackagedServicesAsync()
        {
            if (!EnsureElevatedOrRestart("直接安装服务"))
                return;

            string? installRoot = ResolveManagedServiceInstallRoot();
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "未找到服务安装目录，请先设置安装根目录。", "直接安装服务", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"将从现有文件直接安装并启动基础服务：\n{installRoot}\n\n归档服务不会自动安装。是否继续？",
                "直接安装服务",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            Config.BaseLocation = installRoot;
            SaveServiceManagerConfig();

            SetBusy(true, "正在直接安装服务...");
            try
            {
                SyncManagedServiceConfigs();
                await Task.Run(() => RegisterPackagedServicesCore(installRoot));
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private void RegisterPackagedServicesCore(string installRoot)
        {
            foreach (var svc in ServiceManagerConfig.GetDefaultServiceEntries())
            {
                if (!svc.IsPackaged)
                    continue;

                if (string.Equals(svc.ServiceName, ArchiveServiceName, StringComparison.OrdinalIgnoreCase))
                {
                    AddLog($"跳过归档服务: {svc.ServiceName}");
                    continue;
                }

                string exePath = Path.Combine(installRoot, svc.FolderName, svc.GetExecutableName());
                if (!File.Exists(exePath))
                {
                    AddLog($"跳过服务（未找到可执行文件）: {svc.ServiceName}, {exePath}");
                    continue;
                }

                try
                {
                    bool exists = WinServiceHelper.IsServiceExisted(svc.ServiceName);
                    string? registeredPath = WinServiceHelper.GetServiceInstallPath(svc.ServiceName);
                    bool shouldReinstall = !exists || !IsSamePath(registeredPath, exePath);

                    if (exists && shouldReinstall)
                    {
                        AddLog($"服务路径变化，重新安装: {svc.ServiceName}");
                        WinServiceHelper.StopService(svc.ServiceName, 20);
                        WinServiceHelper.UninstallService(svc.ServiceName);
                    }

                    if (shouldReinstall)
                    {
                        AddLog($"安装服务: {svc.ServiceName} -> {exePath}");
                        if (!WinServiceHelper.InstallService(svc.ServiceName, exePath))
                        {
                            AddLog($"服务安装失败: {svc.ServiceName}");
                            continue;
                        }
                    }
                    else
                    {
                        AddLog($"服务已安装: {svc.ServiceName}");
                    }

                    AddLog($"启动服务: {svc.ServiceName}");
                    bool started = WinServiceHelper.StartService(svc.ServiceName, 30);
                    AddLog(started ? $"服务已启动: {svc.ServiceName}" : $"服务启动失败: {svc.ServiceName}");
                }
                catch (Exception ex)
                {
                    AddLog($"安装服务异常: {svc.ServiceName}, {ex.Message}");
                }
            }
        }

        private string? ResolveManagedServiceInstallRoot()
        {
            var candidates = new List<string>();

            void AddCandidate(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (Directory.Exists(fullPath) && !candidates.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                    {
                        candidates.Add(fullPath);
                    }
                }
                catch
                {
                }
            }

            AddCandidate(Config.BaseLocation);
            if (!string.IsNullOrWhiteSpace(Config.BaseLocation))
            {
                AddCandidate(Path.Combine(Config.BaseLocation, "CVWindowsService"));
            }

            foreach (string serviceName in new[] { "RegistrationCenterService", "CVMainService_x64", "CVMainService_dev" })
            {
                string? exePath = WinServiceHelper.GetServiceInstallPath(serviceName);
                string? root = string.IsNullOrWhiteSpace(exePath) ? null : Directory.GetParent(exePath)?.Parent?.FullName;
                AddCandidate(root);
            }

            AddCandidate(@"D:\CVService");

            return candidates.FirstOrDefault(IsManagedServiceRoot);
        }

        private static bool IsManagedServiceRoot(string path)
        {
            return Directory.Exists(Path.Combine(path, "RegWindowsService"))
                || Directory.Exists(Path.Combine(path, "CVMainWindowsService_x64"))
                || Directory.Exists(Path.Combine(path, "CVMainWindowsService_dev"));
        }

        private static bool IsSamePath(string? left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
                return false;

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left.Trim('"'), right.Trim('"'), StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
