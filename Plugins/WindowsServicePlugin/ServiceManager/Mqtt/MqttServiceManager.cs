using System.Diagnostics;

namespace WindowsServicePlugin.ServiceManager
{
    public class MqttServiceManager
    {
        public MqttServiceConfig Config { get; } = new();

        public void RefreshStatus(IEnumerable<ServiceEntry> services)
        {
            Config.ServiceName = "mosquitto";
            Config.IsInstalled = WinServiceHelper.IsServiceExisted(Config.ServiceName);
            Config.IsRunning = Config.IsInstalled && WinServiceHelper.IsServiceRunning(Config.ServiceName);
            Config.Status = ServiceStatusText.FromInstallationState(Config.IsInstalled, Config.IsRunning);

            Config.ExePath = services.FirstOrDefault(s => string.Equals(s.ServiceName, Config.ServiceName, StringComparison.OrdinalIgnoreCase))?.ExePath
                ?? WinServiceHelper.GetServiceInstallPath(Config.ServiceName)
                ?? string.Empty;
        }

        public async Task<bool> StartViaServiceHostAsync(Action<string> logCallback)
        {
            return await ServiceHostWindowsServiceController.ExecuteAsync(Config.ServiceName, ServiceHostServiceOperation.Start, logCallback, "MQTT").ConfigureAwait(true);
        }

        public async Task<bool> StopViaServiceHostAsync(Action<string> logCallback)
        {
            return await ServiceHostWindowsServiceController.ExecuteAsync(Config.ServiceName, ServiceHostServiceOperation.Stop, logCallback, "MQTT").ConfigureAwait(true);
        }

        public void InstallFromExe(string exeFile, Action<string> logCallback)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exeFile,
                Verb = "runas",
                UseShellExecute = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            StartViaServiceHostAsync(logCallback).GetAwaiter().GetResult();
        }

    }
}
