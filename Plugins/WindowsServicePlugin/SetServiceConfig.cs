﻿using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using System.IO;
using System.Windows;
using System.Xml.Linq;
using WindowsServicePlugin.CVWinSMS;

namespace WindowsServicePlugin
{
    public partial class SetServiceConfigStep : WizardStepBase
    {
        public override int Order => 98;

        public override string Header => "替换服务的CFG";
        public override string Description => "如果已经正确配置服务管理工具，使用该命令会中读取配置的文件并应用";

        Dictionary<string, string> dic = new Dictionary<string, string>();
        public override void Execute()
        {
            if (!File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                MessageBox.Show("请先配置服务管理工具");
                return;
            }
            string filePath = Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath) + @"\config\App.config";
            if (!File.Exists(filePath))
            {
                MessageBox.Show("找不到App.config,请先运行服务管理工具");
                return;
            }
            XDocument config = XDocument.Load(filePath);
            var appSettings = config.Element("configuration")?.Element("appSettings")?.Elements("add");
            if (appSettings != null)
            {
                foreach (var setting in appSettings)
                {
                    string key = setting.Attribute("key")?.Value;
                    string value = setting.Attribute("value")?.Value;
                    if (key != null && value != null)
                    {
                        if (!dic.TryAdd(key, value))
                        {
                            dic[key] = value;
                        }
                    }
                }

                string DirPath = dic["BaseLocation"];

                //这两个更新注册中心就可以了，启动时，Reg会先读取
                string serviceDir = Path.Combine(DirPath, "RegWindowsService");
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


                SetServiceConfig(DirPath,"CVMainWindowsService_x86");
                SetServiceConfig(DirPath,"CVMainWindowsService_x64");
                SetServiceConfig(DirPath,"TPAWindowsService");
                SetServiceConfig(DirPath,"TPAWindowsService32");
                SetServiceConfig(DirPath,"CVFlowWindowsService");
                SetServiceConfig(DirPath,"CVMainWindowsService_dev");


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
    



