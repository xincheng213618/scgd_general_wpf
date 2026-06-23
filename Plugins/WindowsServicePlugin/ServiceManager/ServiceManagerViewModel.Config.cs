using ColorVision.Database;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 配置同步：从当前系统配置写入服务 cfg 目录、旧版 App.config 同步
    /// </summary>
    public partial class ServiceManagerViewModel
    {
        private void UpdateConfig()
        {
            try
            {
                log.Info("开始更新配置...");
                string baseLocation = Config.BaseLocation;
                if (string.IsNullOrEmpty(baseLocation) || !Directory.Exists(baseLocation))
                {
                    MessageBox.Show("安装根目录不存在，请先设置", "更新配置", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SyncAllConfigs(false);

                log.Info("配置更新完成");

                if (MessageBox.Show("配置已更新，是否重启注册中心服务？", "更新配置", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _ = Task.Run(async () =>
                    {
                        bool restarted = await ServiceHostWindowsServiceController
                            .ExecuteAsync("RegistrationCenterService", ServiceHostServiceOperation.Restart, log.Info, "注册中心服务")
                            .ConfigureAwait(true);
                        if (restarted)
                            log.Info("注册中心服务已重启");
                        Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                    });
                }
            }
            catch (Exception ex)
            {
                log.Info($"配置更新失败: {ex.Message}");
                log.Error("配置更新失败", ex);
            }
        }

        public void ApplyConfigAndRefreshAfterInstall()
        {
            try
            {
                string baseLocation = Config.BaseLocation;
                if (string.IsNullOrEmpty(baseLocation) || !Directory.Exists(baseLocation))
                    throw new DirectoryNotFoundException($"安装根目录不存在: {baseLocation}");

                SyncAllConfigs(false);
                log.Info("已执行安装后配置同步(UpdateConfig)");
                Application.Current?.Dispatcher.Invoke(() => RefreshAll());
            }
            catch (Exception ex)
            {
                log.Info($"安装后配置同步失败: {ex.Message}");
                throw;
            }
        }

        private void SyncManagedServiceConfigs()
        {
            string baseLocation = ResolveManagedServiceInstallRoot() ?? Config.BaseLocation;
            if (string.IsNullOrWhiteSpace(baseLocation) || !Directory.Exists(baseLocation))
                return;

            string regDir = Path.Combine(baseLocation, "RegWindowsService");
            if (Directory.Exists(regDir)) UpdateServiceConfigFiles(regDir, isRC: true);

            string[] serviceFolders = ["CVMainWindowsService_x64", "CVMainWindowsService_dev", "TPAWindowsService", "TPAWindowsService32", "CVFlowWindowsService"];
            foreach (var folderName in serviceFolders)
            {
                string svcDir = Path.Combine(baseLocation, folderName);
                if (!Directory.Exists(svcDir)) continue;

                UpdateServiceConfigFiles(svcDir, isRC: false);
            }
        }

        private void UpdateServiceConfigFiles(string serviceDir, bool isRC)
        {
            string cfgDir = Path.Combine(serviceDir, "cfg");
            UpdateMysqlCfgFile(Path.Combine(cfgDir, "MySql.config"));
            UpdateMqttCfgFile(Path.Combine(cfgDir, "MQTT.config"));
            UpdateWinServiceCfgFile(Path.Combine(cfgDir, "WinService.config"), isRC);
            UpdateLog4NetConfigFiles(serviceDir);
        }

        private string? ResolveManagedServiceInstallRoot()
        {
            var candidates = new List<string>();

            void AddCandidate(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (Directory.Exists(fullPath) && !candidates.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                    {
                        candidates.Add(fullPath);
                    }
                }
                catch
                {
                }
            }

            AddCandidate(Config.BaseLocation);
            if (!string.IsNullOrWhiteSpace(Config.BaseLocation))
            {
                AddCandidate(Path.Combine(Config.BaseLocation, "CVWindowsService"));
            }

            foreach (string serviceName in new[] { "RegistrationCenterService", "CVMainService_x64", "CVMainService_dev" })
            {
                string? exePath = WinServiceHelper.GetServiceInstallPath(serviceName);
                string? root = string.IsNullOrWhiteSpace(exePath) ? null : Directory.GetParent(exePath)?.Parent?.FullName;
                AddCandidate(root);
            }

            return candidates.FirstOrDefault(IsManagedServiceRoot);
        }

        private static bool IsManagedServiceRoot(string path)
        {
            return Directory.Exists(Path.Combine(path, "RegWindowsService"))
                || Directory.Exists(Path.Combine(path, "CVMainWindowsService_x64"))
                || Directory.Exists(Path.Combine(path, "CVMainWindowsService_dev"));
        }

        private void UpdateMysqlCfgFile(string configPath)
        {
            MySqlSetting.Instance.MySqlConfig.Host = MySqlServiceConfig.Instance.Host;
            MySqlSetting.Instance.MySqlConfig.Port = MySqlServiceConfig.Instance.Port;
            MySqlSetting.Instance.MySqlConfig.UserName = MySqlServiceConfig.Instance.AppUser;
            MySqlSetting.Instance.MySqlConfig.UserPwd = MySqlServiceConfig.Instance.AppPassword;
            MySqlSetting.Instance.MySqlConfig.Database = MySqlServiceConfig.Instance.Database;

            if (!File.Exists(configPath)) return;
            try
            {
                var doc = XDocument.Load(configPath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null)
                    throw new InvalidDataException($"MySql.config 缺少 appSettings: {configPath}");

                var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
                foreach (var setting in settings)
                {
                    var key = setting.Attribute("key")?.Value;
                    if (key == null) continue;
                    string? value = key switch
                    {
                        "Host" => mySqlConfig.Host,
                        "Port" => mySqlConfig.Port.ToString(),
                        "User" => mySqlConfig.UserName,
                        "Password" => mySqlConfig.UserPwd,
                        "Database" => mySqlConfig.Database,
                        _ => null
                    };
                    if (value != null)
                        setting.SetAttributeValue("value", value);
                }
                doc.Save(configPath);
                log.Info($"更新 MySQL 配置: {configPath}");
            }
            catch (Exception ex)
            {
                log.Info($"更新 MySQL 配置失败: {ex.Message}");
                throw;
            }
        }

        private void UpdateLog4NetConfigFiles(string serviceDir)
        {
            foreach (string configPath in EnumerateServiceConfigFiles(serviceDir))
            {
                UpdateLog4NetConfigFile(configPath);
            }
        }

        private static IEnumerable<string> EnumerateServiceConfigFiles(string serviceDir)
        {
            foreach (string dir in new[] { serviceDir, Path.Combine(serviceDir, "cfg") })
            {
                if (!Directory.Exists(dir)) continue;

                foreach (string filePath in Directory.EnumerateFiles(dir, "*log4net*.config", SearchOption.TopDirectoryOnly))
                    yield return filePath;
            }
        }

        private void UpdateLog4NetConfigFile(string configPath)
        {
            try
            {
                var doc = XDocument.Load(configPath);
                var log4net = doc.Root?.Name.LocalName == "log4net" ? doc.Root : doc.Descendants().FirstOrDefault(element => element.Name.LocalName == "log4net");
                if (log4net == null) return;

                bool changed = false;
                foreach (var level in log4net.Descendants().Where(IsLoggerLevelElement))
                {
                    if (string.Equals(level.Attribute("value")?.Value, "ALL", StringComparison.OrdinalIgnoreCase)) continue;

                    level.SetAttributeValue("value", "ALL");
                    changed = true;
                }

                if (!changed) return;

                doc.Save(configPath);
                log.Info($"更新 log4net 配置: {configPath}");
            }
            catch (Exception ex)
            {
                log.Info($"更新 log4net 配置失败: {configPath}, {ex.Message}");
                throw;
            }
        }

        private static bool IsLoggerLevelElement(XElement element)
        {
            string parentName = element.Parent?.Name.LocalName ?? string.Empty;
            return element.Name.LocalName == "level" && (parentName == "root" || parentName == "logger");
        }

        private void UpdateMqttCfgFile(string configPath)
        {
            if (!File.Exists(configPath)) return;
            try
            {
                var doc = XDocument.Load(configPath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null)
                    throw new InvalidDataException($"MQTT.config 缺少 appSettings: {configPath}");

                var mqttConfig = MqttManager.Config;
                foreach (var setting in settings)
                {
                    var key = setting.Attribute("key")?.Value;
                    if (key == null) continue;
                    string? value = key switch
                    {
                        "Host" => mqttConfig.Host,
                        "Port" => mqttConfig.Port.ToString(),
                        "User" => mqttConfig.UserName,
                        "Password" => mqttConfig.Password,
                        _ => null
                    };
                    if (value != null)
                        setting.SetAttributeValue("value", value);
                }
                doc.Save(configPath);
                log.Info($"更新 MQTT 配置: {configPath}");
            }
            catch (Exception ex)
            {
                log.Info($"更新 MQTT 配置失败: {ex.Message}");
                throw;
            }
        }

        private void UpdateWinServiceCfgFile(string configPath, bool isRC)
        {
            if (!File.Exists(configPath)) return;
            try
            {
                var doc = XDocument.Load(configPath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null)
                    throw new InvalidDataException($"WinService.config 缺少 appSettings: {configPath}");

                var rcConfig = ColorVision.Engine.Services.RC.RCSetting.Instance.Config;
                foreach (var setting in settings)
                {
                    var key = setting.Attribute("key")?.Value;
                    if (key == null) continue;
                    string? value = key switch
                    {
                        "RCNodeName" => rcConfig.RCName,
                        "NodeName" when isRC => rcConfig.RCName,
                        "NodeAppId" => rcConfig.AppId,
                        "NodeKey" => rcConfig.AppSecret,
                        "Monitor.Services" when isRC => "MySQL,CVMainService_x64,CVMainService_dev",
                        "Monitor.UIServices" when isRC => string.Empty,
                        _ => null
                    };
                    if (value != null)
                        setting.SetAttributeValue("value", value);
                }
                doc.Save(configPath);
                log.Info($"更新 WinService 配置: {configPath}");
            }
            catch (Exception ex)
            {
                log.Info($"更新 WinService 配置失败: {ex.Message}");
                throw;
            }
        }

        private void SyncAllConfigs(bool restartRegistrationCenter)
        {
            string baseLocation = Config.BaseLocation;
            if (string.IsNullOrWhiteSpace(baseLocation) || !Directory.Exists(baseLocation))
                return;

            SyncManagedServiceConfigs();

            SyncLegacyAppConfig();

            if (restartRegistrationCenter)
            {
                ServiceHostWindowsServiceController
                    .ExecuteAsync("RegistrationCenterService", ServiceHostServiceOperation.Restart, log.Info, "注册中心服务")
                    .GetAwaiter()
                    .GetResult();
            }
        }

        private void SyncLegacyAppConfig()
        {
            string? filePath = GetLegacyAppConfigPath();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                XDocument config = XDocument.Load(filePath);
                XElement? appSettings = config.Element("configuration")?.Element("appSettings");
                if (appSettings == null)
                    return;

                void SetSetting(string key, string? value)
                {
                    if (value == null) return;
                    XElement? element = appSettings.Elements("add").FirstOrDefault(x => x.Attribute("key")?.Value == key);
                    if (element == null)
                    {
                        appSettings.Add(new XElement("add", new XAttribute("key", key), new XAttribute("value", value)));
                    }
                    else
                    {
                        element.SetAttributeValue("value", value);
                    }
                }

                var dbCfg = MySqlManager.Config;
                SetSetting("BaseLocation", Config.BaseLocation);
                SetSetting("MysqlHost", dbCfg.Host);
                SetSetting("MysqlPort", dbCfg.Port.ToString());
                SetSetting("MysqlServiceName", MySqlManager.Helper.ServiceName);
                SetSetting("MysqlUser", dbCfg.AppUser);
                SetSetting("MysqlPwd", dbCfg.AppPassword);
                SetSetting("MysqlRootPwd", dbCfg.RootPassword);
                SetSetting("MysqlDatabase", dbCfg.Database);
                SetSetting("RCName", ColorVision.Engine.Services.RC.RCSetting.Instance.Config.RCName);

                config.Save(filePath);
                OnPropertyChanged(nameof(LegacyConfigPath));
                OnPropertyChanged(nameof(HasLegacyConfig));
                log.Info($"已同步旧版配置: {filePath}");
            }
            catch (Exception ex)
            {
                log.Info($"同步旧版配置失败: {ex.Message}");
            }
        }

        private static string? GetLegacyAppConfigPath()
        {
            if (string.IsNullOrWhiteSpace(CVWinSMS.CVWinSMSConfig.Instance.CVWinSMSPath))
                return null;

            string? dir = Directory.GetParent(CVWinSMS.CVWinSMSConfig.Instance.CVWinSMSPath)?.FullName;
            if (string.IsNullOrWhiteSpace(dir))
                return null;

            string filePath = Path.Combine(dir, "config", "App.config");
            return File.Exists(filePath) ? filePath : null;
        }

    }
}
