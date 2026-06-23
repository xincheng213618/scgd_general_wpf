using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Marketplace;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Xml.Linq;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 服务管理器配置，从CVWinSMS的App.config读取或从注册表获取
    /// </summary>
    public class ServiceManagerConfig : ViewModelBase, IConfig
    {
        public static ServiceManagerConfig Instance => ConfigService.Instance.GetRequiredService<ServiceManagerConfig>();

        [DisplayName("ServiceManagerBaseLocation")]
        [Description("ServiceManagerBaseLocationDescription")]
        public string BaseLocation { get => _BaseLocation; set { _BaseLocation = value; OnPropertyChanged(); } }
        private string _BaseLocation = string.Empty;

        [DisplayName("ServiceManagerMySqlPort")]
        [Description("ServiceManagerMySqlPortDescription")]
        public int MySqlPort { get => _MySqlPort; set { _MySqlPort = value; OnPropertyChanged(); } }
        private int _MySqlPort = 3306;

        [DisplayName("ServiceManagerUpdateServerUrl")]
        [Description("ServiceManagerUpdateServerUrlDescription")]
        public string UpdateServerUrl { get => _UpdateServerUrl; set { _UpdateServerUrl = value; OnPropertyChanged(); } }
        private string _UpdateServerUrl = MarketplaceConfig.DefaultServiceBaseUrl;

        [DisplayName("ServiceManagerDownloadLocation")]
        [Description("ServiceManagerDownloadLocationDescription")]
        public string DownloadLocation { get => _DownloadLocation; set { _DownloadLocation = value; OnPropertyChanged(); } }
        private string _DownloadLocation = Path.Combine(Environments.DirToolPackageCache, "CVWindowsService");

        [DisplayName("ServiceManagerInstallServiceChecked")]
        [Description("ServiceManagerInstallServiceCheckedDescription")]
        public bool InstallServiceChecked { get => _InstallServiceChecked; set { _InstallServiceChecked = value; OnPropertyChanged(); } }
        private bool _InstallServiceChecked = true;

        [DisplayName("ServiceManagerInstallMySqlChecked")]
        [Description("ServiceManagerInstallMySqlCheckedDescription")]
        public bool InstallMySqlChecked { get => _InstallMySqlChecked; set { _InstallMySqlChecked = value; OnPropertyChanged(); } }
        private bool _InstallMySqlChecked;

        [DisplayName("ServiceManagerInstallMqttChecked")]
        [Description("ServiceManagerInstallMqttCheckedDescription")]
        public bool InstallMqttChecked { get => _InstallMqttChecked; set { _InstallMqttChecked = value; OnPropertyChanged(); } }
        private bool _InstallMqttChecked;

        public static ServiceEntry MQTTServiceEntries { get; set; } = new ServiceEntry
        {
            ServiceName = "mosquitto",
            DisplayName = Resources.MqttServiceDisplayName,
            FolderName = "mosquitto",
            ExecutableName = "mosquitto.exe",
            IsPackaged = false
        };


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
                    DisplayName = Resources.RegistrationCenterServiceDisplayName,
                    FolderName = "RegWindowsService",
                    ExecutableName = "RegWindowsService.exe",
                    IsPackaged = true
                },
                new ServiceEntry
                {
                    ServiceName = "CVMainService_x64",
                    DisplayName = Resources.CvMainServiceX64DisplayName,
                    FolderName = "CVMainWindowsService_x64",
                    IsPackaged = true
                },
                new ServiceEntry
                {
                    ServiceName = "CVMainService_dev",
                    DisplayName = Resources.CvMainServiceDevDisplayName,
                    FolderName = "CVMainWindowsService_dev",
                    IsPackaged = true
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
