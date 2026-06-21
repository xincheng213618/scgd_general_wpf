namespace WindowsServicePlugin.ServiceManager
{
    public partial class ServiceManagerViewModel
    {
        private async Task ControlManagedServiceAsync(ServiceEntry? entry, ServiceHostServiceOperation operation)
        {
            if (entry == null)
                return;

            string label = ServiceHostWindowsServiceController.GetOperationLabel(operation);
            SetBusy(true, $"正在通过后台服务{label} {entry.ServiceName}...");
            try
            {
                await ServiceHostWindowsServiceController.ExecuteAsync(entry.ServiceName, operation, log.Info, entry.DisplayName, entry.ExePath).ConfigureAwait(true);
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private async Task StartMqttServiceAsync()
        {
            SetBusy(true, "正在通过后台服务启动 MQTT...");
            try
            {
                bool ok = await MqttManager.StartViaServiceHostAsync(log.Info).ConfigureAwait(true);
                log.Info(ok ? "MQTT 服务启动完成" : "MQTT 服务启动失败");
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

        private async Task StopMqttServiceAsync()
        {
            SetBusy(true, "正在通过后台服务停止 MQTT...");
            try
            {
                bool ok = await MqttManager.StopViaServiceHostAsync(log.Info).ConfigureAwait(true);
                log.Info(ok ? "MQTT 服务停止完成" : "MQTT 服务停止失败");
            }
            finally
            {
                SetBusy(false);
                RefreshAll();
            }
        }

    }
}
