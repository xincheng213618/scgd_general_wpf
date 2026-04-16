using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WindowsServicePlugin.ServiceManager
{
    public class MqttServiceManager
    {
        public MqttServiceConfig Config { get; } = MqttServiceConfig.Instance;

        public MqttServiceManager()
        {
            MigrateFromLegacySettings();
        }

        public void Initialize()
        {
            MigrateFromLegacySettings();
        }

        public void RefreshStatus(IEnumerable<ServiceEntry> services)
        {
            Config.ServiceName = "mosquitto";
            Config.IsInstalled = WinServiceHelper.IsServiceExisted(Config.ServiceName);
            Config.IsRunning = Config.IsInstalled && WinServiceHelper.IsServiceRunning(Config.ServiceName);
            Config.Status = Config.IsRunning ? "运行中" : (Config.IsInstalled ? "已停止" : "未安装");

            Config.ExePath = services.FirstOrDefault(s => string.Equals(s.ServiceName, Config.ServiceName, StringComparison.OrdinalIgnoreCase))?.ExePath
                ?? WinServiceHelper.GetServiceInstallPath(Config.ServiceName)
                ?? string.Empty;
        }

        public bool Start(Action<string> logCallback)
        {
            logCallback($"正在启动 MQTT 服务 ({Config.ServiceName})...");
            bool result = Tool.ExecuteCommandAsAdmin($"net start {Config.ServiceName}");
            logCallback(result ? "MQTT 服务已启动" : "MQTT 服务启动失败");
            return result;
        }

        public bool Stop(Action<string> logCallback)
        {
            logCallback($"正在停止 MQTT 服务 ({Config.ServiceName})...");
            bool result = Tool.ExecuteCommandAsAdmin($"net stop {Config.ServiceName}");
            logCallback(result ? "MQTT 服务已停止" : "MQTT 服务停止失败");
            return result;
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
            Start(logCallback);
        }

        private void MigrateFromLegacySettings()
        {
            bool changed = false;
            var legacy = MQTTSetting.Instance.MQTTConfig;

            if (string.IsNullOrWhiteSpace(Config.Host) && !string.IsNullOrWhiteSpace(legacy.Host))
            {
                Config.Host = legacy.Host;
                changed = true;
            }
            if (Config.Port <= 0 && legacy.Port > 0)
            {
                Config.Port = legacy.Port;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(Config.UserName) && !string.IsNullOrWhiteSpace(legacy.UserName))
            {
                Config.UserName = legacy.UserName;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(Config.Password) && !string.IsNullOrWhiteSpace(legacy.UserPwd))
            {
                Config.Password = legacy.UserPwd;
                changed = true;
            }

            if (changed)
            {
                SaveConfig();
            }
        }

        private static void SaveConfig()
        {
            ConfigHandler.GetInstance().Save<MqttServiceConfig>();
        }
    }
}