using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Flow;
using ColorVision.UI.LogImp;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;

namespace ColorVision.Engine.Services.RC
{
    public class WindowsServiceBase : ViewModelBase
    {
        public RelayCommand OpenLogCommand { get; set; }
        public ServiceInfo ServiceInfo { get; set; }
        public RelayCommand OpenCommand { get; set; }
        public RelayCommand CloseCommand { get; set; }
        public RelayCommand RestartCommand { get; set; }

        ServiceController ServiceController { get; set; }

        public WindowsServiceBase(ServiceInfo serviceInfo)
        {
            ServiceInfo = serviceInfo;
            OpenLogCommand = new RelayCommand(a => OpenLog());
            if(!string.IsNullOrWhiteSpace(serviceInfo.ServiceName))
                ServiceController = new ServiceController(serviceInfo.ServiceName);
            OpenCommand = new RelayCommand(a => Open());
            CloseCommand = new RelayCommand(a => Close());
            RestartCommand = new RelayCommand(a => Restart());
        }

        public ServiceControllerStatus Status { get => ServiceController.Status; }

        public void Open()
        {
            if (Tool.IsAdministrator())
            {
                ServiceController.Start();
            }
            else
            {
                Tool.ExecuteCommandAsAdmin($"net start {ServiceInfo.ServiceName}");
            }
            OnPropertyChanged(nameof(Status));
        }

        public void Restart()
        {
            if (Tool.IsAdministrator())
            {
                ServiceController.Stop();
                ServiceController.Start();
            }
            else
            {
                Tool.ExecuteCommandAsAdmin($"net stop {ServiceInfo.ServiceName}&&net start {ServiceInfo.ServiceName}");
            }
            OnPropertyChanged(nameof(Status));
        }

        public void Close()
        {
            if (Tool.IsAdministrator())
            {
                ServiceController.Stop();
            }
            else
            {
                Tool.ExecuteCommandAsAdmin($"net stop {ServiceInfo.ServiceName}");
            }
            OnPropertyChanged(nameof(Status));
        }

        public void OpenLog()
        {
            string baseDir = Directory.GetParent(ServiceInfo.ExecutablePath).FullName;
            string latestLogPath = LogFileHelper.GetLatestMainLogPath(baseDir);
            if (!string.IsNullOrEmpty(latestLogPath))
            {
                WindowLogLocal windowLogLocal = new WindowLogLocal(latestLogPath, Encoding.GetEncoding("GB2312"));
                windowLogLocal.Show();
            }
        }
    }


    /// <summary>
    /// 服务详细信息
    /// </summary>
    public class ServiceInfo:ViewModelBase
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// 可执行文件路径
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// 文件版本
        /// </summary>
        public string FileVersion { get => _FileVersion; set { _FileVersion = value; OnPropertyChanged(); } }
        private string _FileVersion = string.Empty;

        /// <summary>
        /// 产品版本
        /// </summary>
        public string ProductVersion { get; set; } = string.Empty;

        /// <summary>
        /// 文件描述
        /// </summary>
        public string FileDescription { get; set; } = string.Empty;

        /// <summary>
        /// 产品名称
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// 公司名称
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// 文件创建时间
        /// </summary>
        public DateTime? CreationTime { get; set; }

        /// <summary>
        /// 文件最后修改时间
        /// </summary>
        public DateTime? LastWriteTime { get; set; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 文件是否存在
        /// </summary>
        public bool Exists { get; set; }

        /// <summary>
        /// 格式化的文件大小
        /// </summary>
        public string FileSizeFormatted => FormatFileSize(FileSize);

        /// <summary>
        /// 从服务名称获取服务信息
        /// </summary>
        public static ServiceInfo FromServiceName(string serviceName)
        {
            var info = new ServiceInfo { ServiceName = serviceName };

            string? exePath = ServiceManagerUitl.GetServiceExecutablePath(serviceName);
            if (string.IsNullOrEmpty(exePath))
            {
                info.Exists = false;
                return info;
            }

            info.ExecutablePath = exePath;

            if (!File.Exists(exePath))
            {
                info.Exists = false;
                return info;
            }

            info.Exists = true;

            try
            {
                // 获取文件信息
                var fileInfo = new FileInfo(exePath);
                info.FileSize = fileInfo.Length;
                info.CreationTime = fileInfo.CreationTime;
                info.LastWriteTime = fileInfo.LastWriteTime;

                // 获取版本信息
                var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                info.FileVersion = versionInfo.FileVersion ?? string.Empty;
                info.ProductVersion = versionInfo.ProductVersion ?? string.Empty;
                info.FileDescription = versionInfo.FileDescription ?? string.Empty;
                info.ProductName = versionInfo.ProductName ?? string.Empty;
                info.CompanyName = versionInfo.CompanyName ?? string.Empty;
            }
            catch
            {
                // 忽略获取信息时的异常
            }

            return info;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        public override string ToString()
        {
            if (!Exists)
                return $"{ServiceName}: 未安装";
            return $"{ServiceName}: v{FileVersion} ({LastWriteTime:yyyy-MM-dd HH:mm:ss})";
        }
    }
}
