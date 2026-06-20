using System.IO;

namespace WindowsServicePlugin.ServiceManager
{
    public partial class ServiceManagerViewModel
    {
        private async Task InstallManagedServiceAsync(ServiceEntry? entry)
        {
            if (entry == null)
                return;

            SetBusy(true, $"正在安装 {entry.DisplayName}...");
            try
            {
                await InstallManagedServiceCoreAsync(entry, startAfterInstall: false).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AddLog($"{entry.DisplayName} 安装失败: {ex.Message}");
            }
            finally
            {
                RefreshAll();
                SetBusy(false);
            }
        }

        private async Task UninstallManagedServiceAsync(ServiceEntry? entry)
        {
            if (entry == null)
                return;

            SetBusy(true, $"正在卸载 {entry.DisplayName}...");
            try
            {
                await ServiceHostWindowsServiceController
                    .UninstallAsync(entry.ServiceName, AddLog, entry.DisplayName)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AddLog($"{entry.DisplayName} 卸载失败: {ex.Message}");
            }
            finally
            {
                RefreshAll();
                SetBusy(false);
            }
        }

        private async Task<bool> InstallManagedServiceCoreAsync(ServiceEntry entry, bool startAfterInstall)
        {
            if (!TryResolveServiceExecutable(entry, out string executablePath))
            {
                AddLog($"找不到 {entry.DisplayName} 可执行文件，无法安装服务");
                return false;
            }

            entry.ExePath = executablePath;
            return await ServiceHostWindowsServiceController
                .InstallAsync(entry.ServiceName, executablePath, AddLog, entry.DisplayName, startAfterInstall)
                .ConfigureAwait(true);
        }

        private bool HasResolvableServiceExecutable(ServiceEntry entry)
        {
            return TryResolveServiceExecutable(entry, out _);
        }

        private bool TryResolveServiceExecutable(ServiceEntry entry, out string executablePath)
        {
            executablePath = string.Empty;
            if (!string.IsNullOrWhiteSpace(entry.ExePath) && File.Exists(entry.ExePath))
            {
                executablePath = entry.ExePath;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Config.BaseLocation))
            {
                string candidate = entry.GetExpectedExePath(Config.BaseLocation);
                if (File.Exists(candidate))
                {
                    executablePath = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
