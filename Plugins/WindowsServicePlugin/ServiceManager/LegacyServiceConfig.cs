using ColorVision.Database;
using ColorVision.Engine.MQTT;
using ColorVision.UI;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Xml.Linq;
using WindowsServicePlugin.CVWinSMS;

namespace WindowsServicePlugin.ServiceManager
{
    internal static class LegacyServiceConfig
    {
        public static bool TryGetAppConfigPath(out string configPath)
        {
            configPath = string.Empty;

            string cvWinSmsPath = CVWinSMSConfig.Instance.CVWinSMSPath;
            if (string.IsNullOrWhiteSpace(cvWinSmsPath) || !File.Exists(cvWinSmsPath))
            {
                return false;
            }

            string? directory = Directory.GetParent(cvWinSmsPath)?.FullName;
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            string candidate = Path.Combine(directory, "config", "App.config");
            if (!File.Exists(candidate))
            {
                return false;
            }

            configPath = candidate;
            return true;
        }

        public static bool EnsureAppConfigPath(Window owner, out string configPath)
        {
            if (TryGetAppConfigPath(out configPath))
            {
                return true;
            }

            OpenFileDialog openFileDialog = new()
            {
                Title = "选择旧版 CVWinSMS.exe",
                Filter = "CVWinSMS.exe|CVWinSMS.exe|Executable files (*.exe)|*.exe",
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog(owner) != true)
            {
                return false;
            }

            CVWinSMSConfig.Instance.CVWinSMSPath = openFileDialog.FileName;
            ConfigHandler.GetInstance().Save<CVWinSMSConfig>();

            return TryGetAppConfigPath(out configPath);
        }

        public static bool Import(string configPath, out string message)
        {
            try
            {
                Dictionary<string, string> settings = ReadAppSettings(configPath);
                if (settings.Count == 0)
                {
                    message = "旧版 App.config 中没有读取到 appSettings 配置。";
                    return false;
                }

                ApplyServiceManagerConfig(settings);
                ApplyMySqlConfig(settings);
                ApplyMqttConfig(settings);
                SaveImportedConfigs();

                ServiceManagerViewModel.Instance.RefreshAll();
                message = "已导入旧版服务配置。请继续确认 MySQL、MQTT 和服务 CFG。";
                return true;
            }
            catch (Exception ex)
            {
                message = "导入旧版配置失败：" + ex.Message;
                return false;
            }
        }

        public static Dictionary<string, string> ReadAppSettings(string configPath)
        {
            XDocument config = XDocument.Load(configPath);
            IEnumerable<XElement>? appSettings = config.Element("configuration")?.Element("appSettings")?.Elements("add");
            Dictionary<string, string> settings = new(StringComparer.OrdinalIgnoreCase);

            if (appSettings == null)
            {
                return settings;
            }

            foreach (XElement setting in appSettings)
            {
                string? key = setting.Attribute("key")?.Value;
                string? value = setting.Attribute("value")?.Value;
                if (!string.IsNullOrWhiteSpace(key) && value != null)
                {
                    settings[key] = value;
                }
            }

            return settings;
        }

        private static void ApplyServiceManagerConfig(IReadOnlyDictionary<string, string> settings)
        {
            ServiceManagerConfig config = ServiceManagerConfig.Instance;

            string? baseLocation = GetSetting(settings, "BaseLocation");
            if (!string.IsNullOrWhiteSpace(baseLocation))
            {
                config.BaseLocation = baseLocation;
            }

            if (TryGetPort(settings, out int mysqlPort, "MysqlPort", "MySqlPort"))
            {
                config.MySqlPort = mysqlPort;
            }
        }

        private static void ApplyMySqlConfig(IReadOnlyDictionary<string, string> settings)
        {
            MySqlServiceConfig serviceConfig = MySqlServiceConfig.Instance;
            MySqlSetting databaseSetting = MySqlSetting.Instance;

            string host = GetSetting(settings, "MysqlHost", "MySqlHost") ?? serviceConfig.Host;
            int port = TryGetPort(settings, out int parsedPort, "MysqlPort", "MySqlPort") ? parsedPort : serviceConfig.Port;
            string appUser = GetSetting(settings, "MysqlUser", "MySqlUser") ?? serviceConfig.AppUser;
            string appPassword = GetSetting(settings, "MysqlPwd", "MysqlPassword", "MySqlPwd", "MySqlPassword") ?? serviceConfig.AppPassword;
            string rootPassword = GetSetting(settings, "MysqlRootPwd", "MysqlRootPassword", "MySqlRootPwd", "MySqlRootPassword") ?? serviceConfig.RootPassword;
            string database = GetSetting(settings, "MysqlDatabase", "MySqlDatabase") ?? serviceConfig.Database;

            serviceConfig.Host = host;
            serviceConfig.Port = port;
            serviceConfig.AppUser = appUser;
            serviceConfig.AppPassword = appPassword;
            serviceConfig.RootPassword = rootPassword;
            serviceConfig.Database = database;

            databaseSetting.MySqlConfig.Host = host;
            databaseSetting.MySqlConfig.Port = port;
            databaseSetting.MySqlConfig.UserName = appUser;
            databaseSetting.MySqlConfig.UserPwd = appPassword;
            databaseSetting.MySqlConfig.Database = database;

            if (!string.IsNullOrWhiteSpace(rootPassword))
            {
                MySqlConfig rootConfig = new()
                {
                    Name = MySqlServiceConfig.RootProfileName,
                    Host = host,
                    Port = port,
                    UserName = "root",
                    UserPwd = rootPassword,
                    Database = database
                };

                MySqlConfig? oldRootConfig = databaseSetting.MySqlConfigs.FirstOrDefault(item =>
                    string.Equals(item.Name, MySqlServiceConfig.RootProfileName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.UserName, "root", StringComparison.OrdinalIgnoreCase));

                if (oldRootConfig != null)
                {
                    databaseSetting.MySqlConfigs.Remove(oldRootConfig);
                }

                databaseSetting.MySqlConfigs.Add(rootConfig);
            }
        }

        private static void ApplyMqttConfig(IReadOnlyDictionary<string, string> settings)
        {
            MqttServiceConfig serviceConfig = MqttServiceConfig.Instance;
            MQTTConfig mqttConfig = MQTTSetting.Instance.MQTTConfig;

            string? host = GetSetting(settings, "MqttHost", "MQTTHost", "MQTT.Host");
            if (!string.IsNullOrWhiteSpace(host))
            {
                serviceConfig.Host = host;
                mqttConfig.Host = host;
            }

            if (TryGetPort(settings, out int port, "MqttPort", "MQTTPort", "MQTT.Port"))
            {
                serviceConfig.Port = port;
                mqttConfig.Port = port;
            }

            string? user = GetSetting(settings, "MqttUser", "MQTTUser", "MqttUserName", "MQTTUserName", "MQTT.User");
            if (user != null)
            {
                serviceConfig.UserName = user;
                mqttConfig.UserName = user;
            }

            string? password = GetSetting(settings, "MqttPwd", "MQTTPwd", "MqttPassword", "MQTTPassword", "MQTT.Password");
            if (password != null)
            {
                serviceConfig.Password = password;
                mqttConfig.UserPwd = password;
            }
        }

        private static void SaveImportedConfigs()
        {
            ConfigHandler configHandler = ConfigHandler.GetInstance();
            configHandler.Save<CVWinSMSConfig>();
            configHandler.Save<ServiceManagerConfig>();
            configHandler.Save<MySqlServiceConfig>();
            configHandler.Save<MqttServiceConfig>();
            configHandler.Save<MySqlSetting>();
            configHandler.Save<MQTTSetting>();
        }

        private static string? GetSetting(IReadOnlyDictionary<string, string> settings, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (settings.TryGetValue(key, out string? value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool TryGetPort(IReadOnlyDictionary<string, string> settings, out int port, params string[] keys)
        {
            port = 0;
            string? value = GetSetting(settings, keys);
            return int.TryParse(value, out port) && port > 0;
        }
    }
}
