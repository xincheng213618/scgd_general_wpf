using ColorVision.Common.MVVM;
using ColorVision.UI;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Xml.Linq;

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

    /// <summary>
    /// 服务管理器配置，从CVWinSMS的App.config读取或从注册表获取
    /// </summary>
    public class ServiceManagerConfig : ViewModelBase, IConfig
    {
        public static ServiceManagerConfig Instance => ConfigService.Instance.GetRequiredService<ServiceManagerConfig>();

        [ConfigSetting(Order = 522)]
        [DisplayName("服务安装根目录")]
        public string BaseLocation { get => _BaseLocation; set { _BaseLocation = value; OnPropertyChanged(); } }
        private string _BaseLocation = string.Empty;

        [ConfigSetting(Order = 523)]
        [DisplayName("MySQL端口")]
        public int MySqlPort { get => _MySqlPort; set { _MySqlPort = value; OnPropertyChanged(); } }
        private int _MySqlPort = 3306;

        [ConfigSetting(Order = 524)]
        [DisplayName("服务更新地址")]
        public string UpdateServerUrl { get => _UpdateServerUrl; set { _UpdateServerUrl = value; OnPropertyChanged(); } }
        private string _UpdateServerUrl = "http://xc213618.ddns.me:9998";

        [JsonIgnore]
        public string LatestReleaseUrl
        {
            get
            {
                string url = UpdateServerUrl?.TrimEnd('/') ?? string.Empty;
                if (url.Contains("/browse/", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Replace("/browse/", "/download/", StringComparison.OrdinalIgnoreCase);
                }
                return url + "/LATEST_RELEASE";
            }
        }

        /// <summary>
        /// 默认的服务定义列表
        /// </summary>
        public static List<ServiceEntry> GetDefaultServiceEntries()
        {
            return
            [
                new ServiceEntry
                {
                    ServiceName = "RegistrationCenterService",
                    DisplayName = "注册中心服务",
                    FolderName = "RegWindowsService",
                    ExecutableName = "RegWindowsService.exe",
                    IsPackaged = true
                },
                new ServiceEntry
                {
                    ServiceName = "CVMainService_x64",
                    DisplayName = "CV主服务(x64)",
                    FolderName = "CVMainWindowsService_x64",
                    IsPackaged = true
                },
                new ServiceEntry
                {
                    ServiceName = "CVMainService_dev",
                    DisplayName = "CV主服务(Dev)",
                    FolderName = "CVMainWindowsService_dev",
                    IsPackaged = true
                },
                new ServiceEntry
                {
                    ServiceName = "CVArchService",
                    DisplayName = "归档服务",
                    FolderName = "RegWindowsService",
                    ExecutableName = "ArchivedWindowsService.exe",
                    IsPackaged = true
                },
                new ServiceEntry
                {
                    ServiceName = "mosquitto",
                    DisplayName = "MQTT服务",
                    FolderName = "mosquitto",
                    ExecutableName = "mosquitto.exe",
                    IsPackaged = false
                },
            ];
        }

        /// <summary>
        /// 尝试从注册表中的RegistrationCenterService确定安装路径
        /// </summary>
        public bool TryDetectInstallPath()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\RegistrationCenterService");
                if (key == null) return false;

                var imagePath = key.GetValue("ImagePath")?.ToString();
                if (string.IsNullOrEmpty(imagePath)) return false;

                imagePath = imagePath.Trim('"');
                imagePath = Environment.ExpandEnvironmentVariables(imagePath);

                if (!File.Exists(imagePath)) return false;

                // RegWindowsService/RegWindowsService.exe → 上两级就是 CVWindowsService
                var parent = Directory.GetParent(imagePath)?.Parent?.FullName;
                if (!string.IsNullOrEmpty(parent))
                {
                    BaseLocation = parent;
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 从CVWinSMS的App.config中读取配置
        /// </summary>
        public bool ReadFromCVWinSMSConfig(string cvWinSMSPath)
        {
            try
            {
                string configDir = Path.Combine(Directory.GetParent(cvWinSMSPath)?.FullName ?? "", "config");
                string configPath = Path.Combine(configDir, "App.config");
                if (!File.Exists(configPath)) return false;

                var doc = XDocument.Load(configPath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null) return false;

                var dic = new Dictionary<string, string>();
                foreach (var setting in settings)
                {
                    var k = setting.Attribute("key")?.Value;
                    var v = setting.Attribute("value")?.Value;
                    if (k != null && v != null)
                        dic[k] = v;
                }

                if (dic.TryGetValue("BaseLocation", out var baseLoc))
                    BaseLocation = baseLoc;
                if (dic.TryGetValue("MysqlPort", out var portStr) && int.TryParse(portStr, out int port))
                    MySqlPort = port;

                return true;
            }
            catch { return false; }
        }
    }
}
