using System.ServiceProcess;

namespace WindowsServicePlugin.ServiceManager
{
    internal static class ServiceStatusText
    {
        public static string Unknown => Get("ServiceStatusUnknown", "未知");

        public static string NotInstalled => Get("ServiceStatusNotInstalled", "未安装");

        public static string FromInstallationState(bool isInstalled, bool isRunning)
        {
            return isRunning
                ? Get("ServiceStatusRunning", "运行中")
                : isInstalled
                    ? Get("ServiceStatusStopped", "已停止")
                    : NotInstalled;
        }

        public static string FromServiceControllerStatus(ServiceControllerStatus status)
        {
            return status switch
            {
                ServiceControllerStatus.Running => Get("ServiceStatusRunning", "运行中"),
                ServiceControllerStatus.Stopped => Get("ServiceStatusStopped", "已停止"),
                ServiceControllerStatus.StartPending => Get("ServiceStatusStarting", "正在启动"),
                ServiceControllerStatus.StopPending => Get("ServiceStatusStopping", "正在停止"),
                ServiceControllerStatus.Paused => Get("ServiceStatusPaused", "已暂停"),
                ServiceControllerStatus.PausePending => Get("ServiceStatusPausing", "正在暂停"),
                ServiceControllerStatus.ContinuePending => Get("ServiceStatusResuming", "正在继续"),
                _ => Unknown
            };
        }

        private static string Get(string key, string fallback)
        {
            return Properties.Resources.ResourceManager.GetString(key, Properties.Resources.Culture) ?? fallback;
        }
    }
}
