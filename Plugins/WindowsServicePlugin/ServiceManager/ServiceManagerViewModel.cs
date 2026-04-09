using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using System.Xml.Linq;
using WindowsServicePlugin.CVWinSMS;

namespace WindowsServicePlugin.ServiceManager
{
    public class ServiceManagerViewModel : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceManagerViewModel));

        public static ServiceManagerViewModel Instance { get; } = new ServiceManagerViewModel();

        public ServiceManagerConfig Config => ServiceManagerConfig.Instance;

        public ObservableCollection<ServiceEntry> Services { get; set; } = [];

        public MySqlServiceHelper MySqlHelper { get; set; } = new MySqlServiceHelper();

        public string LogText { get => _LogText; set { _LogText = value; OnPropertyChanged(); } }
        private string _LogText = string.Empty;

        public string CurrentVersion { get => _CurrentVersion; set { _CurrentVersion = value; OnPropertyChanged(); } }
        private string _CurrentVersion = string.Empty;

        public string AvailableVersion { get => _AvailableVersion; set { _AvailableVersion = value; OnPropertyChanged(); } }
        private string _AvailableVersion = string.Empty;

        public bool IsBusy { get => _IsBusy; set { _IsBusy = value; OnPropertyChanged(); } }
        private bool _IsBusy;

        public double Progress { get => _Progress; set { _Progress = value; OnPropertyChanged(); } }
        private double _Progress;

        public string ProgressText { get => _ProgressText; set { _ProgressText = value; OnPropertyChanged(); } }
        private string _ProgressText = string.Empty;

        // MySQL状态
        public string MySqlServiceName { get => _MySqlServiceName; set { _MySqlServiceName = value; OnPropertyChanged(); } }
        private string _MySqlServiceName = "MySQL";
        public string MySqlStatus { get => _MySqlStatus; set { _MySqlStatus = value; OnPropertyChanged(); } }
        private string _MySqlStatus = "未知";
        public bool IsMySqlInstalled { get => _IsMySqlInstalled; set { _IsMySqlInstalled = value; OnPropertyChanged(); } }
        private bool _IsMySqlInstalled;
        public bool IsMySqlRunning { get => _IsMySqlRunning; set { _IsMySqlRunning = value; OnPropertyChanged(); } }
        private bool _IsMySqlRunning;
        public string MySqlVersion { get => _MySqlVersion; set { _MySqlVersion = value; OnPropertyChanged(); } }
        private string _MySqlVersion = string.Empty;
        public string MySqlExePath { get => _MySqlExePath; set { _MySqlExePath = value; OnPropertyChanged(); } }
        private string _MySqlExePath = string.Empty;

        // MQTT状态
        public string MqttServiceName { get => _MqttServiceName; set { _MqttServiceName = value; OnPropertyChanged(); } }
        private string _MqttServiceName = "mosquitto";
        public string MqttStatus { get => _MqttStatus; set { _MqttStatus = value; OnPropertyChanged(); } }
        private string _MqttStatus = "未知";
        public bool IsMqttInstalled { get => _IsMqttInstalled; set { _IsMqttInstalled = value; OnPropertyChanged(); } }
        private bool _IsMqttInstalled;
        public bool IsMqttRunning { get => _IsMqttRunning; set { _IsMqttRunning = value; OnPropertyChanged(); } }
        private bool _IsMqttRunning;
        public string MqttExePath { get => _MqttExePath; set { _MqttExePath = value; OnPropertyChanged(); } }
        private string _MqttExePath = string.Empty;

        public string MySqlRootPassword { get => _MySqlRootPassword; set { _MySqlRootPassword = value; OnPropertyChanged(); } }
        private string _MySqlRootPassword = string.Empty;

        public string MySqlRootNewPassword { get => _MySqlRootNewPassword; set { _MySqlRootNewPassword = value; OnPropertyChanged(); } }
        private string _MySqlRootNewPassword = string.Empty;

        public string MySqlAppUser { get => _MySqlAppUser; set { _MySqlAppUser = value; OnPropertyChanged(); } }
        private string _MySqlAppUser = string.Empty;

        public string MySqlAppPassword { get => _MySqlAppPassword; set { _MySqlAppPassword = value; OnPropertyChanged(); } }
        private string _MySqlAppPassword = string.Empty;

        public string MySqlDatabaseName { get => _MySqlDatabaseName; set { _MySqlDatabaseName = value; OnPropertyChanged(); } }
        private string _MySqlDatabaseName = string.Empty;

        public string LegacyConfigPath => GetLegacyAppConfigPath() ?? string.Empty;
        public bool HasLegacyConfig => !string.IsNullOrWhiteSpace(LegacyConfigPath) && File.Exists(LegacyConfigPath);


        public RelayCommand OneKeyStartCommand { get; }
        public RelayCommand OneKeyStopCommand { get; }
        public RelayCommand UpdateConfigCommand { get; }
        public RelayCommand OpenInstallManagerCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ClearLogCommand { get; }
        public RelayCommand SetBasePathCommand { get; }
        public RelayCommand OpenFolderCommand { get; }
        public RelayCommand OpenWinServiceConfigCommand { get; }
        public RelayCommand OpenMySqlConfigCommand { get; }
        public RelayCommand OpenMqttConfigCommand { get; }
        public RelayCommand OpenLog4NetConfigCommand { get; }
        public RelayCommand OpenLegacyConfigCommand { get; }
        public RelayCommand MqttStartCommand { get; }
        public RelayCommand MqttStopCommand { get; }

        // MySQL commands
        public RelayCommand MySqlInstallZipCommand { get; }
        public RelayCommand MySqlStartCommand { get; }
        public RelayCommand MySqlStopCommand { get; }
        public RelayCommand MySqlUninstallCommand { get; }
        public RelayCommand MySqlBackupCommand { get; }
        public RelayCommand MySqlRestoreCommand { get; }
        public RelayCommand MySqlRunScriptCommand { get; }
        public RelayCommand MySqlBrowseCommand { get; }
        public RelayCommand MySqlSetRootPasswordCommand { get; }
        public RelayCommand MySqlForceResetRootPasswordCommand { get; }
        public RelayCommand MySqlCreateOrUpdateUserCommand { get; }

        public ServiceManagerViewModel()
        {
            // Commands
            OneKeyStartCommand = new RelayCommand(a => _ = OneKeyStartAsync(), a => !IsBusy);
            OneKeyStopCommand = new RelayCommand(a => _ = OneKeyStopAsync(), a => !IsBusy);
            UpdateConfigCommand = new RelayCommand(a => UpdateConfig(), a => !IsBusy);
            OpenInstallManagerCommand = new RelayCommand(a => OpenInstallManager());
            RefreshCommand = new RelayCommand(a => RefreshAll());
            ClearLogCommand = new RelayCommand(a => LogText = string.Empty);
            SetBasePathCommand = new RelayCommand(a => SetBasePath());
            OpenFolderCommand = new RelayCommand(a => OpenServiceFolder(a as ServiceEntry));
            OpenWinServiceConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "WinService.config"));
            OpenMySqlConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "MySql.config"));
            OpenMqttConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "MQTT.config"));
            OpenLog4NetConfigCommand = new RelayCommand(a => OpenServiceLog4Net(a as ServiceEntry));
            OpenLegacyConfigCommand = new RelayCommand(a => OpenLegacyConfigFile(), a => HasLegacyConfig);
            MqttStartCommand = new RelayCommand(a => _ = Task.Run(() => { ExecuteShellCommand("net start mosquitto", true); RefreshMqttStatus(); }), a => !IsBusy && IsMqttInstalled && !IsMqttRunning);
            MqttStopCommand = new RelayCommand(a => _ = Task.Run(() => { ExecuteShellCommand("net stop mosquitto", true); RefreshMqttStatus(); }), a => !IsBusy && IsMqttRunning);

            MySqlInstallZipCommand = new RelayCommand(a => _ = MySqlInstallZipAsync(), a => !IsBusy);
            MySqlStartCommand = new RelayCommand(a => _ = Task.Run(() => { MySqlHelper.Start(AddLog); RefreshMySqlStatus(); }), a => !IsBusy && IsMySqlInstalled && !IsMySqlRunning);
            MySqlStopCommand = new RelayCommand(a => _ = Task.Run(() => { MySqlHelper.Stop(AddLog); RefreshMySqlStatus(); }), a => !IsBusy && IsMySqlRunning);
            MySqlUninstallCommand = new RelayCommand(a => _ = Task.Run(() => { MySqlHelper.Uninstall(AddLog); RefreshMySqlStatus(); }), a => !IsBusy && IsMySqlInstalled);
            MySqlBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoMySqlBackup()), a => !IsBusy && IsMySqlRunning);
            MySqlRestoreCommand = new RelayCommand(a => _ = Task.Run(() => DoMySqlRestore()), a => !IsBusy && IsMySqlRunning);
            MySqlRunScriptCommand = new RelayCommand(a => _ = Task.Run(() => DoRunSqlScript()), a => !IsBusy && IsMySqlRunning);
            MySqlBrowseCommand = new RelayCommand(a => BrowseMySqlPath());
            MySqlSetRootPasswordCommand = new RelayCommand(a => _ = Task.Run(() => DoSetRootPassword()), a => !IsBusy && IsMySqlRunning);
            MySqlForceResetRootPasswordCommand = new RelayCommand(a => _ = Task.Run(() => DoForceResetRootPassword()), a => !IsBusy);
            MySqlCreateOrUpdateUserCommand = new RelayCommand(a => _ = Task.Run(() => DoCreateOrUpdateUser()), a => !IsBusy && IsMySqlRunning);

            Initialize();
        }

        private void Initialize()
        {
            // 加载服务列表
            foreach (var svc in ServiceManagerConfig.GetDefaultServiceEntries())
                Services.Add(svc);

            Services.Add(ServiceManagerConfig.MQTTServiceEntries);
            // 自动检测路径
            if (string.IsNullOrEmpty(Config.BaseLocation))
            {
                Config.TryDetectInstallPath();
            }

            // 尝试从CVWinSMS配置读取
            if (string.IsNullOrEmpty(Config.BaseLocation) && File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                Config.ReadFromCVWinSMSConfig(CVWinSMSConfig.Instance.CVWinSMSPath);
            }

            // 检测MySQL
            MySqlHelper.DetectFromRegistry();

            var dbCfg = MySqlSetting.Instance.MySqlConfig;
            MySqlAppUser = dbCfg.UserName;
            MySqlAppPassword = dbCfg.UserPwd;
            MySqlDatabaseName = dbCfg.Database;

            var rootCfg = MySqlSetting.Instance.MySqlConfigs.FirstOrDefault(a => a.Name == "RootPath");
            if (rootCfg != null)
            {
                MySqlRootPassword = rootCfg.UserPwd;
            }

            RefreshAll();
        }

        public void RefreshAll()
        {
            OnPropertyChanged(nameof(LegacyConfigPath));
            OnPropertyChanged(nameof(HasLegacyConfig));

            foreach (var svc in Services)
            {
                svc.RefreshStatus();
                // 如果有安装路径配置, 更新ExePath
                if (string.IsNullOrEmpty(svc.ExePath) && !string.IsNullOrEmpty(Config.BaseLocation))
                {
                    string exeCandidate = svc.GetExpectedExePath(Config.BaseLocation);
                    if (File.Exists(exeCandidate))
                        svc.ExePath = exeCandidate;
                }
            }
            RefreshMySqlStatus();
            RefreshMqttStatus();

            // 获取当前版本
            var rcService = Services.FirstOrDefault(s => s.ServiceName == "RegistrationCenterService");
            if (rcService != null && !string.IsNullOrEmpty(rcService.VersionText))
                CurrentVersion = rcService.VersionText;
        }

        private void RefreshMySqlStatus()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MySqlServiceName = MySqlHelper.ServiceName;
                IsMySqlInstalled = MySqlHelper.IsInstalled;
                IsMySqlRunning = MySqlHelper.IsRunning;
                MySqlStatus = IsMySqlRunning ? "运行中" : (IsMySqlInstalled ? "已停止" : "未安装");
                MySqlExePath = MySqlHelper.MysqldExePath;
                if (IsMySqlInstalled)
                {
                    var ver = WinServiceHelper.GetFileVersion(MySqlHelper.MysqldExePath);
                    MySqlVersion = ver?.ToString() ?? "";
                }
            });
        }

        private void RefreshMqttStatus()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MqttServiceName = "mosquitto";
                IsMqttInstalled = WinServiceHelper.IsServiceExisted(MqttServiceName);
                IsMqttRunning = IsMqttInstalled && WinServiceHelper.IsServiceRunning(MqttServiceName);
                MqttStatus = IsMqttRunning ? "运行中" : (IsMqttInstalled ? "已停止" : "未安装");

                var mqttEntry = Services.FirstOrDefault(s => s.ServiceName == MqttServiceName);
                MqttExePath = mqttEntry?.ExePath ?? string.Empty;
            });
        }

        public void AddLog(string message)
        {
            string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}=> {message}";
            log.Info(message);
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LogText += entry + Environment.NewLine;
            });
        }

        private void SetBusy(bool busy, string text = "")
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsBusy = busy;
                ProgressText = text;
                if (!busy) Progress = 0;
            });
        }

        private void SetProgress(double value, string text = "")
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Progress = value;
                if (!string.IsNullOrEmpty(text)) ProgressText = text;
            });
        }

        #region One-Key Operations

        /// <summary>
        /// 一键启动所有服务
        /// </summary>
        private async Task OneKeyStartAsync()
        {
            SetBusy(true, "正在启动所有服务...");
            await Task.Run(() =>
            {
                try
                {
                    List<string> commands = [];

                    if (MySqlHelper.IsInstalled && !MySqlHelper.IsRunning)
                    {
                        AddLog("启动 MySQL 服务...");
                        commands.Add($"net start {MySqlHelper.ServiceName}");
                    }

                    var rcService = Services.FirstOrDefault(s => s.ServiceName == "RegistrationCenterService");
                    if (rcService != null && rcService.IsInstalled && !rcService.IsRunning)
                    {
                        AddLog($"启动 {rcService.DisplayName}...");
                        commands.Add($"net start {rcService.ServiceName}");
                    }

                    foreach (var svc in Services)
                    {
                        if (svc.ServiceName == "RegistrationCenterService") continue;
                        if (svc.IsInstalled && !svc.IsRunning)
                        {
                            AddLog($"启动 {svc.DisplayName}...");
                            commands.Add($"net start {svc.ServiceName}");
                        }
                    }

                    if (commands.Count > 0)
                    {
                        ExecuteShellCommand(string.Join(" && ", commands), true);
                    }

                    AddLog("所有服务启动完成");
                    Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                }
                catch (Exception ex)
                {
                    AddLog($"一键启动失败: {ex.Message}");
                }
            });
            SetBusy(false);
        }

        /// <summary>
        /// 一键停止所有服务
        /// </summary>
        private async Task OneKeyStopAsync()
        {
            SetBusy(true, "正在停止所有服务...");
            await Task.Run(() =>
            {
                try
                {
                    List<string> commands = [];

                    foreach (var svc in Services.Reverse())
                    {
                        if (svc.IsInstalled && svc.IsRunning)
                        {
                            AddLog($"停止 {svc.DisplayName}...");
                            commands.Add($"net stop {svc.ServiceName}");
                        }
                    }
                    if (commands.Count > 0)
                    {
                        ExecuteShellCommand(string.Join(" && ", commands), true);
                    }

                    foreach (var svc in Services.Reverse())
                    {
                        if (WinServiceHelper.IsServiceRunning(svc.ServiceName))
                        {
                            string processName = Path.GetFileNameWithoutExtension(svc.ExePath);
                            if (!string.IsNullOrEmpty(processName))
                                WinServiceHelper.KillProcessByName(processName);
                        }
                    }

                    AddLog("所有服务已停止");
                    Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                }
                catch (Exception ex)
                {
                    AddLog($"一键停止失败: {ex.Message}");
                }
            });
            SetBusy(false);
        }

        #endregion

        #region Config Update

        /// <summary>
        /// 从CVWinSMS读取配置并更新到服务的cfg目录
        /// </summary>
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

        private void UpdateMysqlCfgFile(string configPath)
        {
            if (!File.Exists(configPath)) return;
            try
            {
                var doc = XDocument.Load(configPath);
                var settings = doc.Element("configuration")?.Element("appSettings")?.Elements("add");
                if (settings == null) return;

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

                var mqttConfig = ColorVision.Engine.MQTT.MQTTSetting.Instance.MQTTConfig;
                foreach (var setting in settings)
                {
                    var key = setting.Attribute("key")?.Value;
                    if (key == null) continue;
                    string? value = key switch
                    {
                        "Host" => mqttConfig.Host,
                        "Port" => mqttConfig.Port.ToString(),
                        "User" => mqttConfig.UserName,
                        "Password" => mqttConfig.UserPwd,
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

        #endregion

        #region Upgrade

        private sealed record ServicePackageInfo(Version Version, string FileName, string DownloadUrl);

        private async Task<string?> DownloadAndUpgrade(ServicePackageInfo package)
        {
            string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null)
            {
                AddLog("下载服务不可用");
                return null;
            }

            var tcs = new TaskCompletionSource<string?>();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                service.ShowDownloadWindow();
                service.Download(package.DownloadUrl, downloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
                {
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        AddLog($"下载完成: {filePath}");
                        PlatformHelper.OpenFolderAndSelectFile(filePath);
                    }
                    tcs.TrySetResult(filePath);
                });
            });

            return await tcs.Task;
        }

        private async Task<ServicePackageInfo?> FindLatestServicePackageAsync()
        {
            using HttpClient httpClient = new();
            foreach (var apiBaseUrl in GetApiBaseCandidates(Config.UpdateServerUrl))
            {
                try
                {
                    string releasesUrl = apiBaseUrl.TrimEnd('/') + "/api/tool/cvwindowsservice/releases";
                    using var response = await httpClient.GetAsync(releasesUrl);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    string latestVersion = root.TryGetProperty("latestVersion", out var lv) ? lv.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(latestVersion))
                        continue;

                    if (!Version.TryParse(latestVersion, out var version))
                        continue;

                    string fileName = $"FullPackage[{latestVersion}].zip";
                    if (root.TryGetProperty("packages", out var packagesArray))
                    {
                        foreach (var pkg in packagesArray.EnumerateArray())
                        {
                            string pkgVersion = pkg.TryGetProperty("version", out var pv) ? pv.GetString() ?? "" : "";
                            if (pkgVersion == latestVersion)
                            {
                                fileName = pkg.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? fileName : fileName;
                                break;
                            }
                        }
                    }

                    string downloadUrl = apiBaseUrl.TrimEnd('/') + "/api/tool/cvwindowsservice/download/" + latestVersion;
                    return new ServicePackageInfo(version, fileName, downloadUrl);
                }
                catch
                {
                }
            }

            return null;
        }

        private static IEnumerable<string> GetApiBaseCandidates(string configuredUrl)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuredUrl))
            {
                try
                {
                    var uri = new Uri(configuredUrl.TrimEnd('/'));
                    candidates.Add(uri.GetLeftPart(UriPartial.Authority));
                    if (uri.Port == 9999)
                    {
                        candidates.Add($"{uri.Scheme}://{uri.Host}:9998");
                    }
                }
                catch
                {
                }
            }

            candidates.Add("http://xc213618.ddns.me:9998");
            return candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region MySQL Operations

        private async Task MySqlInstallZipAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MySQL ZIP (*.zip)|*.zip",
                Title = "选择 mysql-5.7.37-winx64.zip"
            };
            if (dlg.ShowDialog() != true) return;

            string basePath = Config.BaseLocation;
            if (string.IsNullOrEmpty(basePath))
            {
                MessageBox.Show("请先设置安装根目录", "MySQL安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string targetDir = Path.Combine(Directory.GetParent(basePath)?.FullName ?? basePath, "Mysql");
            SetBusy(true, "正在安装 MySQL...");
            bool result = await MySqlHelper.InstallFromZipAsync(dlg.FileName, targetDir, AddLog);
            if (result)
            {
                AddLog("MySQL 安装成功");
                RefreshMySqlStatus();
            }
            SetBusy(false);
        }

        private void DoMySqlBackup()
        {
            var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
            string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss");
            string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup");
            string bakFile = Path.Combine(backupDir, $"color_vision_{timestamp}.sql");

            MySqlHelper.BackupDatabase(mySqlConfig.UserName, mySqlConfig.UserPwd, mySqlConfig.Database, bakFile, AddLog);
        }

        private void DoMySqlRestore()
        {
            string? filePath = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "SQL 文件 (*.sql)|*.sql",
                    Title = "选择备份文件"
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
            MySqlHelper.RestoreDatabase(mySqlConfig.UserName, mySqlConfig.UserPwd, mySqlConfig.Database, filePath, AddLog);
        }

        private void DoRunSqlScript()
        {
            string? filePath = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "SQL 文件 (*.sql)|*.sql",
                    Title = "选择 SQL 脚本"
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
            MySqlHelper.ExecuteSqlFile(mySqlConfig.UserPwd, mySqlConfig.Database, filePath, AddLog);
        }

        private void DoSetRootPassword()
        {
            if (string.IsNullOrWhiteSpace(MySqlRootNewPassword))
            {
                AddLog("请先输入新 root 密码");
                return;
            }

            bool ok = MySqlHelper.TrySetRootPassword(MySqlRootPassword, MySqlRootNewPassword, AddLog);
            if (ok)
            {
                MySqlRootPassword = MySqlRootNewPassword;
                PersistRootConfig(MySqlRootPassword);
                SyncLegacyAppConfig();
            }
        }

        private void DoForceResetRootPassword()
        {
            if (!Tool.IsAdministrator())
            {
                AddLog("重置 root 密码需要管理员权限，请使用管理员身份启动程序后重试");
                MessageBox.Show("重置 root 密码需要管理员权限，请使用管理员身份启动程序后重试。", "需要管理员权限", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(MySqlRootNewPassword))
            {
                AddLog("请先输入新 root 密码");
                return;
            }

            bool ok = MySqlHelper.ForceResetRootPassword(MySqlRootNewPassword, AddLog);
            if (ok)
            {
                MySqlRootPassword = MySqlRootNewPassword;
                PersistRootConfig(MySqlRootPassword);
                SyncLegacyAppConfig();
                RefreshMySqlStatus();
            }
        }

        private void DoCreateOrUpdateUser()
        {
            if (string.IsNullOrWhiteSpace(MySqlAppUser) || string.IsNullOrWhiteSpace(MySqlAppPassword) || string.IsNullOrWhiteSpace(MySqlDatabaseName))
            {
                AddLog("请填写用户、密码、数据库");
                return;
            }

            bool ok = MySqlHelper.CreateAppUser(MySqlRootPassword, MySqlAppUser, MySqlAppPassword, MySqlDatabaseName, AddLog);
            if (!ok)
            {
                AddLog("创建/更新业务用户失败，请确认 root 密码是否正确");
                return;
            }

            var cfg = MySqlSetting.Instance.MySqlConfig;
            cfg.UserName = MySqlAppUser;
            cfg.UserPwd = MySqlAppPassword;
            cfg.Database = MySqlDatabaseName;
            SyncLegacyAppConfig();
            AddLog("业务用户配置已更新到当前系统配置");
        }

        private static void PersistRootConfig(string rootPassword)
        {
            var rootCfg = MySqlSetting.Instance.MySqlConfigs.FirstOrDefault(a => a.Name == "RootPath");
            if (rootCfg == null)
            {
                rootCfg = new MySqlConfig
                {
                    Name = "RootPath",
                    Host = MySqlSetting.Instance.MySqlConfig.Host,
                    UserName = "root",
                    Database = MySqlSetting.Instance.MySqlConfig.Database,
                    UserPwd = rootPassword
                };
                MySqlSetting.Instance.MySqlConfigs.Add(rootCfg);
            }
            else
            {
                rootCfg.UserPwd = rootPassword;
            }
        }

        private void BrowseMySqlPath()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "mysqld.exe|mysqld.exe",
                Title = "选择 mysqld.exe"
            };
            if (dlg.ShowDialog() == true)
            {
                var dir = Directory.GetParent(dlg.FileName)?.Parent?.FullName;
                if (!string.IsNullOrEmpty(dir))
                {
                    MySqlHelper.BasePath = dir;
                    MySqlExePath = dlg.FileName;
                    RefreshMySqlStatus();
                }
            }
        }

        #endregion

        #region Helpers

        private bool EnsureElevatedOrRestart(string actionName)
        {
            if (Tool.IsAdministrator()) return true;

            if (MessageBox.Show($"{actionName}需要管理员权限，是否以管理员模式重启并重新打开服务管理器？", "需要管理员权限", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                RestartAsAdministratorToServiceManager();
            }
            return false;
        }

        private void RestartAsAdministratorToServiceManager()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    AddLog("无法获取当前程序路径，不能以管理员模式重开");
                    return;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-c ServiceManager",
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                Process.Start(psi);
                Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                AddLog($"管理员模式重开失败: {ex.Message}");
            }
        }
        private bool ExecuteShellCommand(string command, bool requireAdmin)
        {
            return requireAdmin
                ? WinServiceHelper.ExecuteCommand(command, true)
                : WinServiceHelper.ExecuteCommand(command, false);
        }

        private void RefreshServiceEntryStatus(ServiceEntry entry)
        {
            Application.Current?.Dispatcher.Invoke(() => entry.RefreshStatus());
        }


        private void SyncAllConfigs(bool restartRegistrationCenter)
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

                var dbCfg = MySqlSetting.Instance.MySqlConfig;
                var rootCfg = MySqlSetting.Instance.MySqlConfigs.FirstOrDefault(a => a.Name == "RootPath");
                SetSetting("BaseLocation", Config.BaseLocation);
                SetSetting("MysqlHost", dbCfg.Host);
                SetSetting("MysqlPort", dbCfg.Port.ToString());
                SetSetting("MysqlServiceName", MySqlHelper.ServiceName);
                SetSetting("MysqlUser", dbCfg.UserName);
                SetSetting("MysqlPwd", dbCfg.UserPwd);
                SetSetting("MysqlRootPwd", rootCfg?.UserPwd ?? MySqlRootPassword);
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
            if (string.IsNullOrWhiteSpace(CVWinSMSConfig.Instance.CVWinSMSPath))
                return null;

            string? dir = Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath)?.FullName;
            if (string.IsNullOrWhiteSpace(dir))
                return null;

            string filePath = Path.Combine(dir, "config", "App.config");
            return File.Exists(filePath) ? filePath : null;
        }

        private void OpenLegacyConfigFile()
        {
            string? filePath = GetLegacyAppConfigPath();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AddLog("旧版 App.config 不存在");
                return;
            }
            OpenPath(filePath);
        }

        private void OpenServiceFolder(ServiceEntry? entry)
        {
            if (entry == null)
                return;

            string? path = !string.IsNullOrWhiteSpace(entry.ExePath)
                ? Path.GetDirectoryName(entry.ExePath)
                : (!string.IsNullOrWhiteSpace(Config.BaseLocation) ? Path.Combine(Config.BaseLocation, entry.FolderName) : null);
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                AddLog($"目录不存在: {entry.DisplayName}");
                return;
            }
            OpenPath(path);
        }

        private void OpenServiceFile(ServiceEntry? entry, string fileName)
        {
            if (entry == null)
                return;

            string? serviceDir = !string.IsNullOrWhiteSpace(entry.ExePath)
                ? Path.GetDirectoryName(entry.ExePath)
                : (!string.IsNullOrWhiteSpace(Config.BaseLocation) ? Path.Combine(Config.BaseLocation, entry.FolderName) : null);
            if (string.IsNullOrWhiteSpace(serviceDir))
            {
                AddLog($"无法定位 {entry.DisplayName} 的目录");
                return;
            }

            string filePath = Path.Combine(serviceDir, "cfg", fileName);
            if (!File.Exists(filePath))
            {
                AddLog($"配置文件不存在: {filePath}");
                return;
            }
            OpenPath(filePath);
        }

        private void OpenServiceLog4Net(ServiceEntry? entry)
        {
            if (entry == null)
                return;

            string? serviceDir = !string.IsNullOrWhiteSpace(entry.ExePath)
                ? Path.GetDirectoryName(entry.ExePath)
                : (!string.IsNullOrWhiteSpace(Config.BaseLocation) ? Path.Combine(Config.BaseLocation, entry.FolderName) : null);
            if (string.IsNullOrWhiteSpace(serviceDir))
            {
                AddLog($"无法定位 {entry.DisplayName} 的目录");
                return;
            }

            string[] candidates =
            [
                Path.Combine(serviceDir, "log4net.config"),
                Path.Combine(serviceDir, "cfg", "log4net.config")
            ];

            string? filePath = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                AddLog($"log4net 配置文件不存在: {entry.DisplayName}");
                return;
            }
            OpenPath(filePath);
        }

        private void OpenPath(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                AddLog($"打开失败: {path}, {ex.Message}");
            }
        }

        private void SetBasePath()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择服务安装根目录 (CVWindowsService所在目录)",
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Config.BaseLocation = dlg.SelectedPath;
                OnPropertyChanged(nameof(LegacyConfigPath));
                OnPropertyChanged(nameof(HasLegacyConfig));
                RefreshAll();
            }
        }

        private string? FindSqlFile(string basePath)
        {
            string sqlDir = Path.Combine(basePath, "SQL");
            if (Directory.Exists(sqlDir))
            {
                string allSql = Path.Combine(sqlDir, "color_vision_all.sql");
                if (File.Exists(allSql)) return allSql;
            }
            return null;
        }

        private void CopyCommonDllToAllServices(string basePath)
        {
            string commonDir = Path.Combine(basePath, "CommonDll");
            if (!Directory.Exists(commonDir)) return;

            HashSet<string> copiedDirectories = [];

            foreach (var svc in Services)
            {
                string svcDir = Path.Combine(basePath, svc.FolderName);
                if (Directory.Exists(svcDir) && copiedDirectories.Add(svcDir))
                {
                    CopyDirectory(commonDir, svcDir);
                    AddLog($"CommonDll → {svc.FolderName}");
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destDir = Path.Combine(targetDir, Path.GetDirectoryName(dir) is null ? Path.GetFileName(dir) : Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }

        #endregion

        #region Install Manager

        /// <summary>
        /// 打开服务安装管理器窗口
        /// </summary>
        private void OpenInstallManager()
        {
            EnsureElevatedOrRestart("更新");
            var installWindow = new ServiceInstallWindow
            {
                Owner = Application.Current.GetActiveWindow()
            };
            installWindow.Show();

            // 刷新状态
            RefreshAll();
        }


        #endregion
    }
}
