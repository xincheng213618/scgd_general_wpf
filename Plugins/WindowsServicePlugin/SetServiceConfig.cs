using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Xml.Linq;
using WindowsServicePlugin.ServiceManager;

namespace WindowsServicePlugin
{
    public partial class SetServiceConfigStep : WizardStepBase
    {
        private bool _configurationStatus;

        public override int Order => 98;

        public override string Header => Properties.Resources.ReplaceServiceCfg;
        public override string Description => "如果已经正确配置服务管理工具，使用该命令会读取配置文件并应用。";
        public override bool ConfigurationStatus
        {
            get => _configurationStatus;
            set
            {
                if (_configurationStatus == value)
                    return;

                _configurationStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanContinue));
            }
        }

        public override void Execute()
        {
            string? dirPath = GetServiceBaseLocation();
            if (string.IsNullOrWhiteSpace(dirPath))
            {
                MessageBox.Show("请先导入旧版配置，或在服务管理器中设置服务根目录。");
                return;
            }

            if (!Directory.Exists(dirPath))
            {
                MessageBox.Show("服务根目录不存在：" + dirPath);
                return;
            }

            //这两个更新注册中心就可以了，启动时，Reg会先读取
            string serviceDir = Path.Combine(dirPath, "RegWindowsService");
            if (!Directory.Exists(serviceDir)) return;

            string mysqlConfigPath = Path.Combine(serviceDir, "cfg", "MySql.config");
            if (!File.Exists(mysqlConfigPath)) return;

            UpdateMysqlConfig(mysqlConfigPath);

            string mqttConfigPath = Path.Combine(serviceDir, "cfg", "MQTT.config");
            if (!File.Exists(mqttConfigPath)) return;

            UpdateMqttConfig(mqttConfigPath);

            string WinServiceConfigPath = Path.Combine(serviceDir, "cfg", "WinService.config");
            if (!File.Exists(WinServiceConfigPath)) return;

            UpdateRCWinServiceConfig(WinServiceConfigPath);


            SetServiceConfig(dirPath,"CVMainWindowsService_x86");
            SetServiceConfig(dirPath,"CVMainWindowsService_x64");
            SetServiceConfig(dirPath,"TPAWindowsService");
            SetServiceConfig(dirPath,"TPAWindowsService32");
            SetServiceConfig(dirPath,"CVFlowWindowsService");
            SetServiceConfig(dirPath,"CVMainWindowsService_dev");

            ConfigurationStatus = true;
            MessageBox.Show("CFG配置成功，正在重启服务");
            if (Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService&&net start RegistrationCenterService"))
            {
                MessageBox.Show("重启服务完成");
            }
            else
            {
                MessageBox.Show("CFG配置成功，请手动重启服务");
            }
        }

        private static string? GetServiceBaseLocation()
        {
            string baseLocation = ServiceManagerConfig.Instance.BaseLocation;
            if (!string.IsNullOrWhiteSpace(baseLocation))
            {
                return baseLocation;
            }

            if (!LegacyServiceConfig.TryGetAppConfigPath(out string configPath))
            {
                return null;
            }

            Dictionary<string, string> settings = LegacyServiceConfig.ReadAppSettings(configPath);
            if (settings.TryGetValue("BaseLocation", out baseLocation) && !string.IsNullOrWhiteSpace(baseLocation))
            {
                ServiceManagerConfig.Instance.BaseLocation = baseLocation;
                ConfigHandler.GetInstance().Save<ServiceManagerConfig>();
                return baseLocation;
            }

            return null;
        }

        private void SetServiceConfig(string dirPath, string serviceName)
        {
            string serviceDir = Path.Combine(dirPath, serviceName);
            if (!Directory.Exists(serviceDir)) return;

            string WinServiceConfigPath = Path.Combine(serviceDir, "cfg", "WinService.config");
            if (!File.Exists(WinServiceConfigPath)) return;

            UpdateWinServiceConfig(WinServiceConfigPath);
        }

        private void UpdateRCWinServiceConfig(string mysqlConfigPath)
        {
            var mqttConfigXml = XDocument.Load(mysqlConfigPath);
            var mqttAppSettings = mqttConfigXml.Element("configuration")?.Element("appSettings")?.Elements("add");
            if (mqttAppSettings != null)
            {
                var mqttConfig = RCSetting.Instance.Config;
                foreach (var setting in mqttAppSettings)
                {
                    string key = setting.Attribute("key")?.Value;
                    if (key == null) continue;

                    string value = key switch
                    {
                        "NodeName" => mqttConfig.RCName,
                        "RCNodeName" => mqttConfig.RCName,
                        _ => null
                    };
                    if (value != null)
                    {
                        setting.SetAttributeValue("value", value);
                    }
                }

            }
            mqttConfigXml.Save(mysqlConfigPath);
        }

        private void UpdateWinServiceConfig(string mysqlConfigPath)
        {
            var mqttConfigXml = XDocument.Load(mysqlConfigPath);
            var mqttAppSettings = mqttConfigXml.Element("configuration")?.Element("appSettings")?.Elements("add");
            if (mqttAppSettings != null)
            {
                var mqttConfig = RCSetting.Instance.Config;
                foreach (var setting in mqttAppSettings)
                {
                    string key = setting.Attribute("key")?.Value;
                    if (key == null) continue;

                    string value = key switch
                    {
                        "RCNodeName" => mqttConfig.RCName,
                        "NodeAppId" => mqttConfig.AppId,
                        "NodeKey" => mqttConfig.AppSecret,
                        _ => null
                    };
                    if (value != null)
                    {
                        setting.SetAttributeValue("value", value);
                    }
                }

            }
            mqttConfigXml.Save(mysqlConfigPath);
        }

        private void UpdateMqttConfig(string mysqlConfigPath)
        {
            var mqttConfigXml = XDocument.Load(mysqlConfigPath);
            var mqttAppSettings = mqttConfigXml.Element("configuration")?.Element("appSettings")?.Elements("add");
            if (mqttAppSettings != null)
            {
                var mqttConfig = MQTTSetting.Instance.MQTTConfig;
                foreach (var setting in mqttAppSettings)
                {
                    string key = setting.Attribute("key")?.Value;
                    if (key == null) continue;

                    string value = key switch
                    {
                        "Host" => mqttConfig.Host,
                        "Port" => mqttConfig.Port.ToString(),
                        "User" => mqttConfig.UserName,
                        "Password" => mqttConfig.UserPwd,
                        _ => null
                    };
                    if (value != null)
                    {
                        setting.SetAttributeValue("value", value);
                    }
                }

            }
            mqttConfigXml.Save(mysqlConfigPath);

        }



        private void UpdateMysqlConfig(string mysqlConfigPath)
        {
            var mysqlConfigXml = XDocument.Load(mysqlConfigPath);
            var mysqlAppSettings = mysqlConfigXml.Element("configuration")?.Element("appSettings")?.Elements("add");

            if (mysqlAppSettings != null)
            {
                var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
                foreach (var setting in mysqlAppSettings)
                {
                    string key = setting.Attribute("key")?.Value;
                    if (key == null) continue;

                    string value = key switch
                    {
                        "Host" => mySqlConfig.Host,
                        "Port" => mySqlConfig.Port.ToString(),
                        "User" => mySqlConfig.UserName,
                        "Password" => mySqlConfig.UserPwd,
                        "Database" => mySqlConfig.Database,
                        _ => null
                    };

                    if (value != null)
                    {
                        setting.SetAttributeValue("value", value);
                    }
                }
            }

            mysqlConfigXml.Save(mysqlConfigPath);
        }
    }
}
    



