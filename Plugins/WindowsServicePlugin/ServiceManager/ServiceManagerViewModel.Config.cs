using System.Diagnostics;
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
                AddLog("开始更新配置...");
                string baseLocation = Config.BaseLocation;
                if (string.IsNullOrEmpty(baseLocation) || !Directory.Exists(baseLocation))
                {
                    MessageBox.Show("安装根目录不存在，请先设置", "更新配置", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SyncAllConfigs(false);

                AddLog("配置更新完成");

                if (MessageBox.Show("配置已更新，是否重启注册中心服务？", "更新配置", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Task.Run(() =>
                    {
                        ExecuteShellCommand("net stop RegistrationCenterService && net start RegistrationCenterService", true);
                        AddLog("注册中心服务已重启");
                        Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                    });
                }
            }
            catch (Exception ex)
            {
                AddLog($"配置更新失败: {ex.Message}");
                log.Error("配置更新失败", ex);
            }
        }

        public void ApplyConfigAndRefreshAfterInstall()
        {
            try
            {
                string baseLocation = Config.BaseLocation;
                if (string.IsNullOrEmpty(baseLocation) || !Directory.Exists(baseLocation))
                    return;

                SyncAllConfigs(false);
                AddLog("已执行安装后配置同步(UpdateConfig)");
                Application.Current?.Dispatcher.Invoke(() => RefreshAll());
            }
            catch (Exception ex)
            {
                AddLog($"安装后配置同步失败: {ex.Message}");
            }
        }

        private void SyncManagedServiceConfigs()
        {
            string baseLocation = Config.BaseLocation;
            if (string.IsNullOrWhiteSpace(baseLocation) || !Directory.Exists(baseLocation))
                return;

            string regDir = Path.Combine(baseLocation, "RegWindowsService");
            if (Directory.Exists(regDir))
            {
                UpdateMysqlCfgFile(Path.Combine(regDir, "cfg", "MySql.config"));
                UpdateMqttCfgFile(Path.Combine(regDir, "cfg", "MQTT.config"));
                UpdateWinServiceCfgFile(Path.Combine(regDir, "cfg", "WinService.config"), isRC: true);
            }

            string[] serviceFolders = ["CVMainWindowsService_x64", "CVMainWindowsService_dev", "TPAWindowsService", "TPAWindowsService32", "CVFlowWindowsService"];
            foreach (var folderName in serviceFolders)
            {
                string svcDir = Path.Combine(baseLocation, folderName);
                if (!Directory.Exists(svcDir)) continue;

                UpdateMysqlCfgFile(Path.Combine(svcDir, "cfg", "MySql.config"));
                UpdateMqttCfgFile(Path.Combine(svcDir, "cfg", "MQTT.config"));
                UpdateWinServiceCfgFile(Path.Combine(svcDir, "cfg", "WinService.config"), isRC: false);
            }
        }

        private void UpdateMysqlCfgFile(string configPath)
        {
            if (!File.Exists(configPath)) return;
            try
            {
                var doc = XDocument.Load(configPath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null) return;

                var mySqlConfig = MySqlManager.Config;
                foreach (var setting in settings)
                {
                    var key = setting.Attribute("key")?.Value;
                    if (key == null) continue;
                    string? value = key switch
                    {
                        "Host" => mySqlConfig.Host,
                        "Port" => mySqlConfig.Port.ToString(),
                        "User" => mySqlConfig.AppUser,
                        "Password" => mySqlConfig.AppPassword,
                        "Database" => mySqlConfig.Database,
                        _ => null
                    };
                    if (value != null)
                        setting.SetAttributeValue("value", value);
                }
                doc.Save(configPath);
                AddLog($"更新 MySQL 配置: {configPath}");
            }
            catch (Exception ex)
            {
                AddLog($"更新 MySQL 配置失败: {ex.Message}");
            }
        }

        private void UpdateMqttCfgFile(string configPath)
        {
            if (!File.Exists(configPath)) return;
            try
            {
                var doc = XDocument.Load(configPath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null) return;

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
                AddLog($"更新 MQTT 配置: {configPath}");
            }
            catch (Exception ex)
            {
                AddLog($"更新 MQTT 配置失败: {ex.Message}");
            }
        }

        private void UpdateWinServiceCfgFile(string configPath, bool isRC)
        {
            if (!File.Exists(configPath)) return;
            try
            {
                var doc = XDocument.Load(configPath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null) return;

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
                        _ => null
                    };
                    if (value != null)
                        setting.SetAttributeValue("value", value);
                }
                doc.Save(configPath);
                AddLog($"更新 WinService 配置: {configPath}");
            }
            catch (Exception ex)
            {
                AddLog($"更新 WinService 配置失败: {ex.Message}");
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
                ExecuteShellCommand("net stop RegistrationCenterService && net start RegistrationCenterService", true);
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
                AddLog($"已同步旧版配置: {filePath}");
            }
            catch (Exception ex)
            {
                AddLog($"同步旧版配置失败: {ex.Message}");
            }
        }

        private string? GetLegacyAppConfigPath()
        {
            if (string.IsNullOrWhiteSpace(CVWinSMS.CVWinSMSConfig.Instance.CVWinSMSPath))
                return null;

            string? dir = Directory.GetParent(CVWinSMS.CVWinSMSConfig.Instance.CVWinSMSPath)?.FullName;
            if (string.IsNullOrWhiteSpace(dir))
                return null;

            string filePath = Path.Combine(dir, "config", "App.config");
            return File.Exists(filePath) ? filePath : null;
        }

        private LegacyMySqlProfile? ReadLegacyMySqlProfile()
        {
            string? filePath = GetLegacyAppConfigPath();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                var doc = XDocument.Load(filePath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null)
                    return null;

                string? GetValue(string key)
                {
                    return settings.FirstOrDefault(x => x.Attribute("key")?.Value == key)?.Attribute("value")?.Value;
                }

                int port = GetConfiguredMySqlPort();
                if (int.TryParse(GetValue("MysqlPort"), out int parsedPort) && parsedPort > 0)
                {
                    port = parsedPort;
                }

                return new LegacyMySqlProfile
                {
                    Host = string.IsNullOrWhiteSpace(GetValue("MysqlHost")) ? "127.0.0.1" : GetValue("MysqlHost")!,
                    Port = port,
                    AppUser = GetValue("MysqlUser") ?? string.Empty,
                    AppPassword = GetValue("MysqlPwd") ?? string.Empty,
                    RootPassword = GetValue("MysqlRootPwd") ?? string.Empty,
                    Database = string.IsNullOrWhiteSpace(GetValue("MysqlDatabase")) ? MySqlManager.Config.Database : GetValue("MysqlDatabase")!
                };
            }
            catch (Exception ex)
            {
                AddLog($"读取旧版数据库配置失败: {ex.Message}");
                return null;
            }
        }

        private bool SyncLegacyMySqlProfileSafely(LegacyMySqlProfile targetProfile, bool restartIfRunning)
        {
            string? filePath = GetLegacyAppConfigPath();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;

            bool wasRunning = IsCVWinSMSRunning();
            string? exePath = wasRunning ? ResolveCVWinSMSExecutablePath() : null;

            try
            {
                if (wasRunning)
                {
                    StopCVWinSMSProcesses();
                }

                var doc = XDocument.Load(filePath);
                var appSettings = doc.Element("configuration")?.Element("appSettings");
                if (appSettings == null)
                    return false;

                void SetSetting(string key, string value)
                {
                    var element = appSettings.Elements("add").FirstOrDefault(x => x.Attribute("key")?.Value == key);
                    if (element == null)
                    {
                        appSettings.Add(new XElement("add", new XAttribute("key", key), new XAttribute("value", value)));
                    }
                    else
                    {
                        element.SetAttributeValue("value", value);
                    }
                }

                SetSetting("MysqlHost", targetProfile.Host);
                SetSetting("MysqlPort", targetProfile.Port.ToString());
                SetSetting("MysqlUser", targetProfile.AppUser);
                SetSetting("MysqlPwd", targetProfile.AppPassword);
                SetSetting("MysqlRootPwd", targetProfile.RootPassword);
                SetSetting("MysqlDatabase", targetProfile.Database);

                doc.Save(filePath);
                AddLog($"已更新旧版 App.config: {filePath}");
                OnPropertyChanged(nameof(LegacyConfigPath));
                OnPropertyChanged(nameof(HasLegacyConfig));
                return true;
            }
            catch (Exception ex)
            {
                AddLog($"更新旧版 App.config 失败: {ex.Message}");
                return false;
            }
            finally
            {
                if (wasRunning && restartIfRunning)
                {
                    RestartCVWinSMS(exePath);
                }
            }
        }

        private static bool IsCVWinSMSRunning()
        {
            return Process.GetProcessesByName("CVWinSMS").Length > 0;
        }

        private string? ResolveCVWinSMSExecutablePath()
        {
            if (!string.IsNullOrWhiteSpace(CVWinSMS.CVWinSMSConfig.Instance.CVWinSMSPath) && File.Exists(CVWinSMS.CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                return CVWinSMS.CVWinSMSConfig.Instance.CVWinSMSPath;
            }

            foreach (var process in Process.GetProcessesByName("CVWinSMS"))
            {
                try
                {
                    return process.MainModule?.FileName;
                }
                catch
                {
                }
            }

            return null;
        }

        private void StopCVWinSMSProcesses()
        {
            foreach (var process in Process.GetProcessesByName("CVWinSMS"))
            {
                try
                {
                    if (!process.CloseMainWindow())
                    {
                        process.Kill();
                    }
                    else if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                    }
                }
                catch
                {
                }
            }
        }

        private void RestartCVWinSMS(string? exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                AddLog("未找到 CVWinSMS.exe，跳过重启旧版管理工具");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                AddLog("已重新启动 CVWinSMS");
            }
            catch (Exception ex)
            {
                AddLog($"重新启动 CVWinSMS 失败: {ex.Message}");
            }
        }

        private sealed class LegacyMySqlProfile
        {
            public string Host { get; init; } = "127.0.0.1";
            public int Port { get; init; } = 3306;
            public string AppUser { get; init; } = string.Empty;
            public string AppPassword { get; init; } = string.Empty;
            public string RootPassword { get; init; } = string.Empty;
            public string Database { get; init; } = "color_vision";
        }
    }
}
