namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 一键启动/停止所有服务
    /// </summary>
    public partial class ServiceManagerViewModel
    {
        private async Task OneKeyStartAsync()
        {
            SetBusy(true, "正在启动所有服务...");
            try
            {
                RefreshAll();

                if (MySqlManager.Config.IsInstalled && !MySqlManager.Config.IsRunning)
                {
                    await MySqlManager.StartViaServiceHostAsync(AddLog).ConfigureAwait(true);
                }

                if (MqttManager.Config.IsInstalled && !MqttManager.Config.IsRunning)
                {
                    await MqttManager.StartViaServiceHostAsync(AddLog).ConfigureAwait(true);
                }

                var rcService = Services.FirstOrDefault(s => s.ServiceName == "RegistrationCenterService");
                if (rcService != null && rcService.IsInstalled && !rcService.IsRunning)
                {
                    await ServiceHostWindowsServiceController
                        .ExecuteAsync(rcService.ServiceName, ServiceHostServiceOperation.Start, AddLog, rcService.DisplayName, rcService.ExePath)
                        .ConfigureAwait(true);
                }

                foreach (var svc in Services)
                {
                    if (svc.ServiceName == "RegistrationCenterService")
                        continue;

                    if (svc.IsInstalled && !svc.IsRunning)
                    {
                        await ServiceHostWindowsServiceController
                            .ExecuteAsync(svc.ServiceName, ServiceHostServiceOperation.Start, AddLog, svc.DisplayName, svc.ExePath)
                            .ConfigureAwait(true);
                    }
                }

                AddLog("所有服务启动完成");
            }
            catch (Exception ex)
            {
                AddLog($"一键启动失败: {ex.Message}");
            }
            finally
            {
                RefreshAll();
                SetBusy(false);
            }
        }

        private async Task OneKeyStopAsync()
        {
            SetBusy(true, "正在停止所有服务...");
            try
            {
                RefreshAll();

                foreach (var svc in Services.Reverse())
                {
                    if (!svc.IsInstalled || !svc.IsRunning)
                        continue;

                    await ServiceHostWindowsServiceController
                        .ExecuteAsync(svc.ServiceName, ServiceHostServiceOperation.Stop, AddLog, svc.DisplayName, svc.ExePath)
                        .ConfigureAwait(true);
                }

                if (MqttManager.Config.IsRunning)
                {
                    await MqttManager.StopViaServiceHostAsync(AddLog).ConfigureAwait(true);
                }

                if (MySqlManager.Config.IsRunning)
                {
                    await MySqlManager.StopViaServiceHostAsync(AddLog).ConfigureAwait(true);
                }

                foreach (var svc in Services.Reverse())
                {
                    if (WinServiceHelper.IsServiceRunning(svc.ServiceName))
                    {
                        await ServiceHostWindowsServiceController
                            .ExecuteAsync(svc.ServiceName, ServiceHostServiceOperation.Terminate, AddLog, svc.DisplayName, svc.ExePath)
                            .ConfigureAwait(true);
                    }
                }

                AddLog("所有服务已停止");
            }
            catch (Exception ex)
            {
                AddLog($"一键停止失败: {ex.Message}");
            }
            finally
            {
                RefreshAll();
                SetBusy(false);
            }
        }
    }
}
