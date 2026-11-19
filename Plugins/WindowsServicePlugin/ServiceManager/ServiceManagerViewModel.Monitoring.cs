using ColorVision.Database;
using ColorVision.Engine.Services.RC;
using MySqlConnector;
using System.IO;
using System.Windows;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.ServiceManager;

public partial class ServiceManagerViewModel
{
    private const string ArchiveServiceName = "CVArchService";

    private async Task RemoveArchiveServiceMonitoringAsync()
    {
        string caption = Resources.RemoveArchiveServiceMonitoring;
        SetBusy(true, "正在移除归档服务检测...");
        try
        {
            string? executable = WinServiceHelper.GetServiceInstallPath("RegistrationCenterService");
            string? serviceDirectory = !string.IsNullOrWhiteSpace(executable)
                ? Path.GetDirectoryName(executable)
                : Path.Combine(ResolveManagedServiceInstallRoot() ?? Config.BaseLocation, "RegWindowsService");
            if (string.IsNullOrWhiteSpace(serviceDirectory))
                throw new DirectoryNotFoundException("未找到注册中心服务目录，请先设置安装目录");
            string configPath = Path.Combine(serviceDirectory, "cfg", "WinService.config");
            if (!File.Exists(configPath))
                throw new FileNotFoundException("未找到注册中心 cfg/WinService.config，请核对安装目录", configPath);

            bool wasRunning = WinServiceHelper.IsServiceRunning("RegistrationCenterService");
            string notice = wasRunning ? "注册中心将短暂停止，保存配置后恢复运行。" : "配置将在注册中心下次启动时生效。";
            if (ShowUiMessage($"将从注册中心监控列表移除 {ArchiveServiceName}，并卸载它（如已安装）。\n{notice}\n\n是否继续？", caption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            string rcCode = MqttRCService.RCNodeName;
            string connectionString = MySqlControl.GetConnectionString(MySqlSetting.Instance.MySqlConfig, timeout: 5);
            bool archiveUninstalled = false;
            await ApplyMonitoringChangeAsync(wasRunning,
                () => ServiceHostWindowsServiceController.ExecuteAsync("RegistrationCenterService", ServiceHostServiceOperation.Stop, log.Info, "注册中心服务"),
                () =>
                {
                    using var connection = new MySqlConnection(connectionString);
                    connection.Open();
                    string services = RegistrationCenterMonitorConfiguration.RemoveArchiveServiceMonitoring(connection, rcCode, configPath);
                    log.Info($"已移除 CVArchService 检测；注册中心 {rcCode} 数据库与本地监控列表已同步: {services}");
                },
                async () =>
                {
                    if (!WinServiceHelper.IsServiceExisted(ArchiveServiceName))
                        return;

                    archiveUninstalled = await ServiceHostWindowsServiceController.UninstallAsync(ArchiveServiceName, log.Info);
                    if (!archiveUninstalled)
                        throw new InvalidOperationException($"归档服务检测已移除，但卸载 {ArchiveServiceName} 失败，请查看日志后重试。");
                },
                () => ServiceHostWindowsServiceController.ExecuteAsync("RegistrationCenterService", ServiceHostServiceOperation.Start, log.Info, "注册中心服务"));

            string archiveStatus = archiveUninstalled ? $"已卸载 {ArchiveServiceName}。" : string.Empty;
            string registrationStatus = wasRunning ? "注册中心已恢复运行。" : "监控配置将在注册中心下次启动时生效。";
            ShowUiMessage($"归档服务检测已移除。{archiveStatus}{registrationStatus}", caption, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            log.Error("移除归档服务检测未完成", ex);
            ShowUiMessage(ex.Message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshAll();
            SetBusy(false);
        }
    }

    internal static async Task ApplyMonitoringChangeAsync(bool wasRunning, Func<Task<bool>> stop, Action saveConfiguration, Func<Task> uninstallArchive, Func<Task<bool>> start)
    {
        if (wasRunning && !await stop().ConfigureAwait(true))
            throw new InvalidOperationException("注册中心停止失败，未修改监控配置，请检查服务状态");

        bool monitoringSaved = false;
        Exception? operationError = null;
        try
        {
            await Task.Run(saveConfiguration).ConfigureAwait(true);
            monitoringSaved = true;
            await uninstallArchive().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            operationError = ex;
        }

        if (wasRunning)
        {
            try
            {
                if (!await start().ConfigureAwait(true))
                    throw new InvalidOperationException("后台服务返回启动失败");
            }
            catch (Exception restartError)
            {
                string detail = operationError == null ? "监控配置已保存。"
                    : monitoringSaved ? operationError.Message
                    : $"监控配置更新失败。{operationError.Message}。";
                throw new InvalidOperationException($"{detail}注册中心恢复启动失败，请检查服务状态并重新启动注册中心。",
                    operationError == null ? restartError : new AggregateException(operationError, restartError));
            }
        }

        if (operationError != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationError).Throw();
    }
}
