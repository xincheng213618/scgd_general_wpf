using ColorVision.Database;
using MySqlConnector;
using System.IO;
using System.Windows;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.ServiceManager;

public partial class ServiceManagerViewModel
{
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
            if (ShowUiMessage($"将从注册中心监控列表移除 CVArchService。\n{notice}\n\n是否继续？", caption, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            string rcCode = ColorVision.Engine.Services.RC.RCSetting.Instance.Config.RCName;
            string connectionString = MySqlControl.GetConnectionString(MySqlSetting.Instance.MySqlConfig, timeout: 5);
            await ApplyMonitoringChangeAsync(wasRunning,
                () => ServiceHostWindowsServiceController.ExecuteAsync("RegistrationCenterService", ServiceHostServiceOperation.Stop, log.Info, "注册中心服务"),
                () =>
                {
                    using var connection = new MySqlConnection(connectionString);
                    connection.Open();
                    string services = RegistrationCenterMonitorConfiguration.RemoveArchiveServiceMonitoring(connection, rcCode, configPath);
                    log.Info($"已移除 CVArchService 检测；注册中心 {rcCode} 数据库与本地监控列表已同步: {services}");
                },
                () => ServiceHostWindowsServiceController.ExecuteAsync("RegistrationCenterService", ServiceHostServiceOperation.Start, log.Info, "注册中心服务"));

            ShowUiMessage(wasRunning ? "归档服务检测已移除，注册中心已恢复运行。" : "归档服务检测已移除，注册中心下次启动时生效。", caption, MessageBoxButton.OK, MessageBoxImage.Information);
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

    internal static async Task ApplyMonitoringChangeAsync(bool wasRunning, Func<Task<bool>> stop, Action saveConfiguration, Func<Task<bool>> start)
    {
        if (wasRunning && !await stop().ConfigureAwait(true))
            throw new InvalidOperationException("注册中心停止失败，未修改监控配置，请检查服务状态");

        Exception? saveError = null;
        try
        {
            await Task.Run(saveConfiguration).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            saveError = ex;
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
                throw new InvalidOperationException(saveError == null
                    ? "监控配置已保存，但注册中心恢复启动失败，请检查服务状态并重新启动注册中心"
                    : $"监控配置更新失败：{saveError.Message}；注册中心恢复启动也失败，请检查配置及服务状态",
                    saveError == null ? restartError : new AggregateException(saveError, restartError));
            }
        }

        if (saveError != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(saveError).Throw();
    }
}
