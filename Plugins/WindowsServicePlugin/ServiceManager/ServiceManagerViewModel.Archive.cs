using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace WindowsServicePlugin.ServiceManager
{
    public partial class ServiceManagerViewModel
    {
        private const string ArchiveServiceName = "CVArchService";
        private const string RegistrationCenterServiceName = "RegistrationCenterService";

        private async Task UnregisterArchiveAsync()
        {
            if (!EnsureElevatedOrRestart("注销归档服务"))
                return;

            var confirm = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                "将停止并注销 CVArchService，并从注册中心 Monitor.Services 中移除，避免被监控任务再次拉起。是否继续？",
                "注销归档服务",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            SetBusy(true, "正在注销归档服务...");
            ArchiveUnregisterResult result;
            try
            {
                result = await Task.Run(UnregisterArchiveCore);
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }

            if (result.ConfigChanged)
            {
                var restart = MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "已从注册中心监控配置中移除 CVArchService。需要重启 RegistrationCenterService 才会立即生效，是否现在重启？",
                    "重启注册中心服务",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (restart == MessageBoxResult.Yes)
                {
                    SetBusy(true, "正在重启注册中心服务...");
                    try
                    {
                        await Task.Run(() =>
                        {
                            ExecuteShellCommand($"net stop {RegistrationCenterServiceName} & net start {RegistrationCenterServiceName}", true);
                            AddLog("注册中心服务已重启");
                        });
                    }
                    finally
                    {
                        SetBusy(false);
                        RefreshAll();
                    }
                }
                else
                {
                    AddLog("已跳过注册中心服务重启，Monitor.Services 修改将在下次重启后生效");
                }
            }
            else if (result.ServiceDeleted)
            {
                AddLog("归档服务已注销，未发现需要修改的注册中心监控配置");
            }
        }

        private ArchiveUnregisterResult UnregisterArchiveCore()
        {
            var result = new ArchiveUnregisterResult();

            try
            {
                if (WinServiceHelper.IsServiceExisted(ArchiveServiceName))
                {
                    AddLog($"正在停止并注销服务: {ArchiveServiceName}");
                    result.ServiceDeleted = WinServiceHelper.UninstallService(ArchiveServiceName);
                    AddLog(result.ServiceDeleted
                        ? $"已注销服务: {ArchiveServiceName}"
                        : $"注销服务失败: {ArchiveServiceName}");
                }
                else
                {
                    AddLog($"服务未安装，跳过注销: {ArchiveServiceName}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"注销服务异常: {ArchiveServiceName}, {ex.Message}");
            }

            foreach (var configPath in GetRegistrationCenterWinServiceConfigPaths())
            {
                try
                {
                    if (RemoveServiceFromMonitorServices(configPath, ArchiveServiceName))
                    {
                        result.ConfigChanged = true;
                        AddLog($"已从 Monitor.Services 移除 {ArchiveServiceName}: {configPath}");
                    }
                    else
                    {
                        AddLog($"Monitor.Services 不包含 {ArchiveServiceName}，无需修改: {configPath}");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"更新注册中心监控配置失败: {configPath}, {ex.Message}");
                }
            }

            if (!result.ConfigChanged)
            {
                AddLog("未找到需要移除 CVArchService 的注册中心监控配置");
            }

            return result;
        }

        private IEnumerable<string> GetRegistrationCenterWinServiceConfigPaths()
        {
            var paths = new List<string>();

            if (!string.IsNullOrWhiteSpace(Config.BaseLocation))
            {
                paths.Add(Path.Combine(Config.BaseLocation, "RegWindowsService", "cfg", "WinService.config"));
            }

            string? rcExePath = WinServiceHelper.GetServiceInstallPath(RegistrationCenterServiceName);
            string? rcDirectory = string.IsNullOrWhiteSpace(rcExePath) ? null : Path.GetDirectoryName(rcExePath);
            if (!string.IsNullOrWhiteSpace(rcDirectory))
            {
                paths.Add(Path.Combine(rcDirectory, "cfg", "WinService.config"));
            }

            return paths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static bool RemoveServiceFromMonitorServices(string configPath, string serviceName)
        {
            var doc = XDocument.Load(configPath);
            var appSettings = doc.Element("configuration")?.Element("appSettings");
            if (appSettings == null)
                return false;

            var setting = appSettings.Elements("add")
                .FirstOrDefault(x => string.Equals(x.Attribute("key")?.Value, "Monitor.Services", StringComparison.OrdinalIgnoreCase));
            if (setting == null)
                return false;

            string currentValue = setting.Attribute("value")?.Value ?? string.Empty;
            var services = currentValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.Equals(x, serviceName, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string newValue = string.Join(",", services);
            if (string.Equals(currentValue, newValue, StringComparison.Ordinal))
                return false;

            setting.SetAttributeValue("value", newValue);
            doc.Save(configPath);
            return true;
        }

        private sealed class ArchiveUnregisterResult
        {
            public bool ServiceDeleted { get; set; }
            public bool ConfigChanged { get; set; }
        }
    }
}
