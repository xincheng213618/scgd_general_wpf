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
            await Task.Run(() =>
            {
                try
                {
                    List<string> commands = [];

                    if (MySqlHelper.IsInstalled && !MySqlHelper.IsRunning)
                    {
                        AddLog("启动 MySQL 服务...");
                        commands.Add($"net start {MySqlHelper.ServiceName}");
                    }

                    var rcService = Services.FirstOrDefault(s => s.ServiceName == "RegistrationCenterService");
                    if (rcService != null && rcService.IsInstalled && !rcService.IsRunning)
                    {
                        AddLog($"启动 {rcService.DisplayName}...");
                        commands.Add($"net start {rcService.ServiceName}");
                    }

                    foreach (var svc in Services)
                    {
                        if (svc.ServiceName == "RegistrationCenterService") continue;
                        if (svc.IsInstalled && !svc.IsRunning)
                        {
                            AddLog($"启动 {svc.DisplayName}...");
                            commands.Add($"net start {svc.ServiceName}");
                        }
                    }

                    if (commands.Count > 0)
                    {
                        ExecuteShellCommand(string.Join(" && ", commands), true);
                    }

                    AddLog("所有服务启动完成");
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                }
                catch (Exception ex)
                {
                    AddLog($"一键启动失败: {ex.Message}");
                }
            });
            SetBusy(false);
        }

        private async Task OneKeyStopAsync()
        {
            SetBusy(true, "正在停止所有服务...");
            await Task.Run(() =>
            {
                try
                {
                    List<string> commands = [];

                    foreach (var svc in Services.Reverse())
                    {
                        if (svc.IsInstalled && svc.IsRunning)
                        {
                            AddLog($"停止 {svc.DisplayName}...");
                            commands.Add($"net stop {svc.ServiceName}");
                        }
                    }
                    if (commands.Count > 0)
                    {
                        ExecuteShellCommand(string.Join(" && ", commands), true);
                    }

                    foreach (var svc in Services.Reverse())
                    {
                        if (WinServiceHelper.IsServiceRunning(svc.ServiceName))
                        {
                            string processName = System.IO.Path.GetFileNameWithoutExtension(svc.ExePath);
                            if (!string.IsNullOrEmpty(processName))
                                WinServiceHelper.KillProcessByName(processName);
                        }
                    }

                    AddLog("所有服务已停止");
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                }
                catch (Exception ex)
                {
                    AddLog($"一键停止失败: {ex.Message}");
                }
            });
            SetBusy(false);
        }
    }
}
