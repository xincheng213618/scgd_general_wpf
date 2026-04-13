using ColorVision.Common.MVVM;
using System.IO;
using System.ServiceProcess;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 单个Windows服务的信息和状态
    /// </summary>
    public class ServiceEntry : ViewModelBase
    {
        public string ServiceName { get => _ServiceName; set { _ServiceName = value; OnPropertyChanged(); } }
        private string _ServiceName = string.Empty;

        public string DisplayName { get => _DisplayName; set { _DisplayName = value; OnPropertyChanged(); } }
        private string _DisplayName = string.Empty;

        public string ExePath { get => _ExePath; set { _ExePath = value; OnPropertyChanged(); } }
        private string _ExePath = string.Empty;

        public string FolderName { get => _FolderName; set { _FolderName = value; OnPropertyChanged(); } }
        private string _FolderName = string.Empty;

        public string ExecutableName { get => _ExecutableName; set { _ExecutableName = value; OnPropertyChanged(); } }
        private string _ExecutableName = string.Empty;

        public bool IsPackaged { get; set; } = true;

        public string StatusText { get => _StatusText; set { _StatusText = value; OnPropertyChanged(); } }
        private string _StatusText = "未知";

        public string VersionText { get => _VersionText; set { _VersionText = value; OnPropertyChanged(); } }
        private string _VersionText = string.Empty;

        public bool IsInstalled { get => _IsInstalled; set { _IsInstalled = value; OnPropertyChanged(); } }
        private bool _IsInstalled;

        public bool IsRunning { get => _IsRunning; set { _IsRunning = value; OnPropertyChanged(); } }
        private bool _IsRunning;

        public string GetExecutableName()
        {
            return string.IsNullOrWhiteSpace(ExecutableName) ? FolderName + ".exe" : ExecutableName;
        }

        public string GetExpectedExePath(string basePath)
        {
            return Path.Combine(basePath, FolderName, GetExecutableName());
        }

        public void RefreshStatus()
        {
            IsInstalled = WinServiceHelper.IsServiceExisted(ServiceName);
            if (IsInstalled)
            {
                var status = WinServiceHelper.GetServiceStatus(ServiceName);
                IsRunning = status == ServiceControllerStatus.Running;
                StatusText = status switch
                {
                    ServiceControllerStatus.Running => "运行中",
                    ServiceControllerStatus.Stopped => "已停止",
                    ServiceControllerStatus.StartPending => "正在启动",
                    ServiceControllerStatus.StopPending => "正在停止",
                    ServiceControllerStatus.Paused => "已暂停",
                    _ => status.ToString()
                };

                var svcPath = WinServiceHelper.GetServiceInstallPath(ServiceName);
                if (!string.IsNullOrEmpty(svcPath) && File.Exists(svcPath))
                {
                    ExePath = svcPath;
                    var ver = WinServiceHelper.GetFileVersion(svcPath);
                    VersionText = ver?.ToString() ?? "";
                }
            }
            else
            {
                IsRunning = false;
                StatusText = "未安装";
                VersionText = "";
            }
        }
    }
}
