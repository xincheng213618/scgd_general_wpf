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
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
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

        // Commands
        public RelayCommand OneKeyInstallCommand { get; }
        public RelayCommand OneKeyStartCommand { get; }
        public RelayCommand OneKeyStopCommand { get; }
        public RelayCommand UpdateConfigCommand { get; }
        public RelayCommand IncrementalUpgradeCommand { get; }
        public RelayCommand FreshInstallCommand { get; }
        public RelayCommand CheckUpdateCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ClearLogCommand { get; }
        public RelayCommand SetBasePathCommand { get; }
        public RelayCommand OpenFolderCommand { get; }
        public RelayCommand OpenWinServiceConfigCommand { get; }
        public RelayCommand OpenMySqlConfigCommand { get; }
        public RelayCommand OpenMqttConfigCommand { get; }
        public RelayCommand OpenLog4NetConfigCommand { get; }
        public RelayCommand OpenLegacyConfigCommand { get; }
        public RelayCommand RestartElevatedCommand { get; }

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
            OneKeyInstallCommand = new RelayCommand(a => _ = OneKeyInstallAsync(), a => !IsBusy);
            OneKeyStartCommand = new RelayCommand(a => _ = OneKeyStartAsync(), a => !IsBusy);
            OneKeyStopCommand = new RelayCommand(a => _ = OneKeyStopAsync(), a => !IsBusy);
            UpdateConfigCommand = new RelayCommand(a => UpdateConfig(), a => !IsBusy);
            IncrementalUpgradeCommand = new RelayCommand(a => _ = IncrementalUpgradeAsync(), a => !IsBusy);
            FreshInstallCommand = new RelayCommand(a => _ = FreshInstallAsync(), a => !IsBusy);
            CheckUpdateCommand = new RelayCommand(a => _ = CheckForUpdateAsync(), a => !IsBusy);
            RefreshCommand = new RelayCommand(a => RefreshAll());
            ClearLogCommand = new RelayCommand(a => LogText = string.Empty);
            SetBasePathCommand = new RelayCommand(a => SetBasePath());
            OpenFolderCommand = new RelayCommand(a => OpenServiceFolder(a as ServiceEntry));
            OpenWinServiceConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "WinService.config"));
            OpenMySqlConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "MySql.config"));
            OpenMqttConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "MQTT.config"));
            OpenLog4NetConfigCommand = new RelayCommand(a => OpenServiceLog4Net(a as ServiceEntry));
            OpenLegacyConfigCommand = new RelayCommand(a => OpenLegacyConfigFile(), a => HasLegacyConfig);
            RestartElevatedCommand = new RelayCommand(a => RestartAsAdministratorToServiceManager());

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
        /// 一键安装 - 选择FullPackage.zip，解压并安装所有服务
        /// </summary>
        private async Task OneKeyInstallAsync()
        {
            if (!EnsureElevatedOrRestart("一键安装")) return;

            string? basePath = Config.BaseLocation;
            if (string.IsNullOrEmpty(basePath))
            {
                MessageBox.Show("请先设置服务安装根目录", "一键安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Directory.Exists(basePath) && Directory.GetFileSystemEntries(basePath).Length > 0)
            {
                if (MessageBox.Show($"{basePath} 目录不为空，一键安装需要空目录。是否继续？", "一键安装", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }

            // 选择安装包
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "安装包 (*.zip)|*.zip",
                Title = "选择 FullPackage.zip"
            };
            if (dlg.ShowDialog() != true) return;

            string zipFile = dlg.FileName;

            SetBusy(true, "正在一键安装...");
            await Task.Run(() =>
            {
                try
                {
                    AddLog("开始一键安装...");

                    // 1. 解压到pack子目录
                    string packDir = Path.Combine(basePath, "pack");
                    SetProgress(5, "正在解压安装包...");
                    AddLog($"解压 {zipFile} → {packDir}");
                    ZipFile.ExtractToDirectory(zipFile, packDir, true);
                    SetProgress(20, "解压完成");

                    // 2. 安装MySQL (如果包中有mysql zip)
                    string mysqlZip = Path.Combine(packDir, "mysql-5.7.37-winx64.zip");
                    string mysqlTarget = Path.Combine(basePath, "Mysql");
                    if (File.Exists(mysqlZip))
                    {
                        SetProgress(25, "正在安装 MySQL...");
                        MySqlHelper.InstallFromZipAsync(mysqlZip, mysqlTarget, AddLog).Wait();
                    }

                    // 3. 安装MQTT (如果包中有mosquitto)
                    string mqttInstaller = Path.Combine(packDir, "mosquitto-2.0.18-install-windows-x64.exe");
                    if (File.Exists(mqttInstaller))
                    {
                        SetProgress(50, "正在安装 MQTT...");
                        AddLog("安装 Mosquitto MQTT...");
                        var psi = new ProcessStartInfo
                        {
                            FileName = mqttInstaller,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        var proc = Process.Start(psi);
                        proc?.WaitForExit();
                        Tool.ExecuteCommandAsAdmin("net start mosquitto");
                    }

                    // 4. 解压和安装CV服务
                    string svcZip = Path.Combine(packDir, "CVWindowsService.zip");
                    string svcTarget = Path.Combine(basePath, "CVWindowsService");
                    if (File.Exists(svcZip))
                    {
                        SetProgress(60, "正在安装CV服务...");
                        AddLog($"解压 CVWindowsService → {svcTarget}");
                        ZipFile.ExtractToDirectory(svcZip, svcTarget, true);

                        // Copy CommonDll
                        CopyCommonDllToAllServices(svcTarget);

                        // 安装各服务
                        int idx = 0;
                        foreach (var svc in Services)
                        {
                            string exePath = svc.GetExpectedExePath(svcTarget);
                            if (File.Exists(exePath))
                            {
                                SetProgress(70 + idx * 5, $"安装服务 {svc.DisplayName}...");
                                AddLog($"安装服务 {svc.ServiceName}...");
                                if (WinServiceHelper.InstallService(svc.ServiceName, exePath))
                                    AddLog($"服务 {svc.ServiceName} 安装成功");
                                else
                                    AddLog($"服务 {svc.ServiceName} 安装失败");
                            }
                            idx++;
                        }
                    }

                    // 5. 执行初始化SQL
                    string sqlFile = FindSqlFile(basePath);
                    if (!string.IsNullOrEmpty(sqlFile))
                    {
                        SetProgress(90, "执行初始化 SQL...");
                        var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
                        MySqlHelper.ExecuteSqlFile(mySqlConfig.UserPwd, mySqlConfig.Database, sqlFile, AddLog);
                    }

                    // 6. 启动注册中心
                    SyncAllConfigs(false);
                    SetProgress(95, "启动所有服务...");
                    StartAllServicesAfterInstall();

                    SetProgress(100, "一键安装完成");
                    AddLog("一键安装完成!");

                    Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                }
                catch (Exception ex)
                {
                    AddLog($"一键安装失败: {ex.Message}");
                    log.Error("一键安装失败", ex);
                }
            });
            SetBusy(false);
        }

        /// <summary>
        /// 一键启动所有服务
        /// </summary>
        private async Task OneKeyStartAsync()
        {
            if (!EnsureElevatedOrRestart("一键启动")) return;

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
            if (!EnsureElevatedOrRestart("一键停止")) return;

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

                    if (MySqlHelper.IsInstalled && MySqlHelper.IsRunning)
                    {
                        AddLog("停止 MySQL 服务...");
                        commands.Add($"net stop {MySqlHelper.ServiceName}");
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
                if (!EnsureElevatedOrRestart("更新配置")) return;

                AddLog("开始更新配置...");

                string baseLocation = Config.BaseLocation;
                if (string.IsNullOrEmpty(baseLocation) || !Directory.Exists(baseLocation))
                {
                    MessageBox.Show("安装根目录不存在，请先设置", "更新配置", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SyncAllConfigs(false);

                AddLog("配置更新完成");

                // 重启注册中心
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

        /// <summary>
        /// 增量升级 - 只替换变更文件，不重置数据库
        /// </summary>
        private async Task IncrementalUpgradeAsync()
        {
            if (!EnsureElevatedOrRestart("增量升级")) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "升级包 (*.zip)|*.zip",
                Title = "选择增量升级包"
            };
            if (dlg.ShowDialog() != true) return;

            await IncrementalUpgradeFromZipAsync(dlg.FileName);
        }

        private async Task IncrementalUpgradeFromZipAsync(string zipFile)
        {
            if (string.IsNullOrWhiteSpace(zipFile) || !File.Exists(zipFile))
            {
                AddLog("增量升级包不存在");
                return;
            }

            string basePath = Config.BaseLocation;
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            {
                MessageBox.Show("安装根目录不存在，请先设置", "增量升级", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"确认升级 {Path.GetFileName(zipFile)} 到\n{basePath} 吗?", "增量升级", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
                return;

            SetBusy(true, "正在增量升级...");
            await Task.Run(() =>
            {
                try
                {
                    AddLog("开始增量升级...");
                    string subFolder = Path.GetFileNameWithoutExtension(zipFile);

                    // 1. 停止已打包的服务
                    SetProgress(10, "停止服务...");
                    foreach (var svc in Services)
                    {
                        if (svc.IsPackaged && svc.IsInstalled && svc.IsRunning)
                        {
                            AddLog($"停止 {svc.DisplayName}...");
                            Tool.ExecuteCommandAsAdmin($"net stop {svc.ServiceName}");
                            Thread.Sleep(1000);
                            string processName = Path.GetFileNameWithoutExtension(svc.ExePath);
                            if (!string.IsNullOrEmpty(processName))
                                WinServiceHelper.KillProcessByName(processName);
                        }
                    }

                    // 2. 解压
                    SetProgress(30, "解压升级包...");
                    AddLog($"解压 {zipFile} → {basePath}");
                    ZipFile.ExtractToDirectory(zipFile, basePath, true);

                    // 3. 复制更新文件
                    SetProgress(50, "复制更新文件...");
                    foreach (var svc in Services)
                    {
                        if (!svc.IsPackaged) continue;
                        string svcDir = Path.Combine(basePath, svc.FolderName);
                        if (!Directory.Exists(svcDir)) continue;

                        // 复制 CommonDll
                        string commonDir = Path.Combine(basePath, subFolder, "CommonDll");
                        if (Directory.Exists(commonDir))
                        {
                            CopyDirectory(commonDir, svcDir);
                            AddLog($"CommonDll → {svc.FolderName}");
                        }

                        // 复制服务特定文件
                        string svcUpdateDir = Path.Combine(basePath, subFolder, svc.FolderName);
                        if (Directory.Exists(svcUpdateDir))
                        {
                            CopyDirectory(svcUpdateDir, svcDir);
                            AddLog($"{svc.FolderName} 更新文件已复制");
                        }
                    }

                    // 4. 复制SQL
                    string sqlUpdateDir = Path.Combine(basePath, subFolder, "SQL");
                    string sqlDir = Path.Combine(basePath, "SQL");
                    if (Directory.Exists(sqlUpdateDir))
                    {
                        CopyDirectory(sqlUpdateDir, sqlDir);
                        AddLog("SQL 更新文件已复制");
                    }

                    SyncAllConfigs(false);

                    // 5. 重启服务
                    SetProgress(80, "重启服务...");
                    ExecuteShellCommand("net start RegistrationCenterService", true);
                    Thread.Sleep(2000);

                    foreach (var svc in Services)
                    {
                        if (svc.ServiceName == "RegistrationCenterService") continue;
                        if (svc.IsPackaged && svc.IsInstalled)
                        {
                            ExecuteShellCommand($"net start {svc.ServiceName}", true);
                            Thread.Sleep(1000);
                        }
                    }

                    SetProgress(100, "增量升级完成");
                    AddLog("增量升级完成!");
                    Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                }
                catch (Exception ex)
                {
                    AddLog($"增量升级失败: {ex.Message}");
                    log.Error("增量升级失败", ex);
                }
            });
            SetBusy(false);
        }

        /// <summary>
        /// 全新安装 - 卸载所有服务，备份数据库，重新安装
        /// </summary>
        private async Task FreshInstallAsync()
        {
            if (!EnsureElevatedOrRestart("全新安装")) return;

            var result = MessageBox.Show("全新安装会重置数据库，请确认是否继续？\n注：会自动备份数据库", "全新安装", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "服务安装包 (*.zip)|*.zip",
                Title = "选择 CVWindowsService.zip"
            };
            if (dlg.ShowDialog() != true) return;

            await FreshInstallFromZipAsync(dlg.FileName);
        }

        private async Task FreshInstallFromZipAsync(string zipFile)
        {
            if (string.IsNullOrWhiteSpace(zipFile) || !File.Exists(zipFile))
            {
                AddLog("全新安装包不存在");
                return;
            }

            string basePath = Config.BaseLocation;
            if (string.IsNullOrEmpty(basePath))
            {
                MessageBox.Show("请先设置安装根目录", "全新安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "正在全新安装...");
            await Task.Run(() =>
            {
                try
                {
                    AddLog("开始全新安装...");

                    // 1. 卸载已有服务
                    SetProgress(5, "卸载现有服务...");
                    foreach (var svc in Services)
                    {
                        if (svc.IsPackaged && svc.IsInstalled)
                        {
                            AddLog($"卸载 {svc.DisplayName}...");
                            if (svc.IsRunning)
                            {
                                ExecuteShellCommand($"net stop {svc.ServiceName}", true);
                                RefreshServiceEntryStatus(svc);
                            }
                            Thread.Sleep(1000);
                            WinServiceHelper.UninstallService(svc.ServiceName);
                            RefreshServiceEntryStatus(svc);
                        }
                    }

                    // 2. 备份数据库
                    SetProgress(15, "备份数据库...");
                    if (IsMySqlRunning)
                    {
                        var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
                        string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss");
                        string bakDir = Path.Combine(basePath, "SQL.BAK");
                        string bakFile = Path.Combine(bakDir, $"color_vision.bak_{timestamp}.sql");
                        MySqlHelper.BackupDatabase(mySqlConfig.UserName, mySqlConfig.UserPwd, mySqlConfig.Database, bakFile, AddLog);
                    }

                    // 3. 解压
                    SetProgress(30, "解压安装包...");
                    AddLog($"解压 {zipFile} → {basePath}");
                    ZipFile.ExtractToDirectory(zipFile, basePath, true);

                    // 4. Copy CommonDll
                    CopyCommonDllToAllServices(basePath);

                    // 5. 安装服务
                    SetProgress(50, "安装服务...");
                    int idx = 0;
                    foreach (var svc in Services)
                    {
                        string exePath = svc.GetExpectedExePath(basePath);
                        if (File.Exists(exePath))
                        {
                            SetProgress(50 + idx * 10, $"安装 {svc.DisplayName}...");
                            AddLog($"安装服务 {svc.ServiceName}...");
                            if (WinServiceHelper.InstallService(svc.ServiceName, exePath))
                                AddLog($"服务 {svc.ServiceName} 安装成功");
                            else
                                AddLog($"服务 {svc.ServiceName} 安装失败");
                        }
                        idx++;
                    }

                    // 6. 执行初始化SQL
                    string sqlFile = FindSqlFile(basePath);
                    if (!string.IsNullOrEmpty(sqlFile))
                    {
                        SetProgress(80, "执行初始化 SQL...");
                        var mySqlConfig = MySqlSetting.Instance.MySqlConfig;
                        MySqlHelper.ExecuteSqlFile(mySqlConfig.UserPwd, mySqlConfig.Database, sqlFile, AddLog);
                    }

                    SyncAllConfigs(false);

                    // 7. 自动启动全部服务
                    SetProgress(90, "启动所有服务...");
                    StartAllServicesAfterInstall();

                    SetProgress(100, "全新安装完成");
                    AddLog("全新安装完成!");
                    Application.Current?.Dispatcher.Invoke(() => RefreshAll());
                }
                catch (Exception ex)
                {
                    AddLog($"全新安装失败: {ex.Message}");
                    log.Error("全新安装失败", ex);
                }
            });
            SetBusy(false);
        }

        /// <summary>
        /// 在线检查更新
        /// </summary>
        private async Task CheckForUpdateAsync()
        {
            try
            {
                if (!EnsureElevatedOrRestart("在线更新")) return;

                AddLog("正在检查更新...");

                var latestPackage = await FindLatestServicePackageAsync();
                if (latestPackage == null)
                {
                    AddLog("未在更新目录中找到可用的 CVWindowsService 安装包");
                    MessageBox.Show("未找到可用的服务安装包");
                    return;
                }

                AvailableVersion = latestPackage.Version.ToString();

                Version current = new();
                if (!string.IsNullOrWhiteSpace(CurrentVersion))
                {
                    _ = Version.TryParse(CurrentVersion, out current);
                }

                if (latestPackage.Version <= current)
                {
                    AddLog("当前已是最新版本");
                    MessageBox.Show("当前已是最新版本");
                    return;
                }

                AddLog($"发现新版本: {latestPackage.Version} (当前: {CurrentVersion})");
                if (MessageBox.Show($"发现新版本 {latestPackage.Version}，是否下载并更新？", "在线更新", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }

                string? downloadedPath = await DownloadAndUpgrade(latestPackage);
                if (string.IsNullOrWhiteSpace(downloadedPath) || !File.Exists(downloadedPath))
                {
                    AddLog("下载未完成或已取消");
                    return;
                }

                var doFreshInstallNow = MessageBox.Show(
                    $"安装包已下载完成:\n{downloadedPath}\n\n是否立即执行全新安装并自动启动服务？",
                    "在线更新",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (doFreshInstallNow == MessageBoxResult.Yes)
                {
                    await FreshInstallFromZipAsync(downloadedPath);
                }
                else
                {
                    AddLog("已下载完成，可稍后手动执行全新安装或增量升级");
                }
            }
            catch (Exception ex)
            {
                AddLog($"检查更新失败: {ex.Message}");
            }
        }

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
            string browseUrl = GetBrowseUrl(Config.UpdateServerUrl);
            if (string.IsNullOrWhiteSpace(browseUrl))
                return null;

            using HttpClient httpClient = CreateAuthorizedHttpClient();
            string html = await httpClient.GetStringAsync(browseUrl);

            var hrefRegex = new Regex(@"href\s*=\s*[""'](?<href>/download/Tool/CVWindowsService/(?<file>[^""'#?<>]+\.zip))[""']", RegexOptions.IgnoreCase);
            var fileRegex = new Regex(@"^CVWindowsService\[(?<version>\d+\.\d+\.\d+\.\d+)\](?:-(?<suffix>\d+))?\.zip$", RegexOptions.IgnoreCase);

            var candidates = new List<ServicePackageInfo>();
            foreach (Match match in hrefRegex.Matches(html))
            {
                string href = match.Groups["href"].Value;
                string fileName = Uri.UnescapeDataString(match.Groups["file"].Value);

                Match fileMatch = fileRegex.Match(fileName);
                if (!fileMatch.Success)
                    continue;

                if (!Version.TryParse(fileMatch.Groups["version"].Value, out var version))
                    continue;

                string downloadUrl = new Uri(new Uri(browseUrl), href).ToString();
                candidates.Add(new ServicePackageInfo(version, fileName, downloadUrl));
            }

            return candidates
                .OrderByDescending(c => c.Version)
                .ThenByDescending(c => c.FileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static HttpClient CreateAuthorizedHttpClient()
        {
            HttpClient httpClient = new();
            var byteArray = Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return httpClient;
        }

        private static string GetBrowseUrl(string configuredUrl)
        {
            if (string.IsNullOrWhiteSpace(configuredUrl))
                return string.Empty;

            string url = configuredUrl.TrimEnd('/');
            if (url.Contains("/download/", StringComparison.OrdinalIgnoreCase))
            {
                return url.Replace("/download/", "/browse/", StringComparison.OrdinalIgnoreCase);
            }

            if (url.Contains("/browse/", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            return url + "/browse/Tool/CVWindowsService";
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
                AddLog("当前不是管理员，正在以管理员模式重开服务管理器...");
                RestartAsAdministratorToServiceManager();
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

        private void StartAllServicesAfterInstall()
        {
            AddLog("安装完成，正在自动启动所有服务...");

            List<string> commands = [];
            if (WinServiceHelper.IsServiceExisted(MySqlHelper.ServiceName))
            {
                commands.Add($"net start {MySqlHelper.ServiceName}");
            }

            if (WinServiceHelper.IsServiceExisted("mosquitto"))
            {
                commands.Add("net start mosquitto");
            }

            if (WinServiceHelper.IsServiceExisted("RegistrationCenterService"))
            {
                commands.Add("net start RegistrationCenterService");
            }

            foreach (var svc in Services)
            {
                if (svc.ServiceName == "RegistrationCenterService") continue;
                if (WinServiceHelper.IsServiceExisted(svc.ServiceName))
                {
                    commands.Add($"net start {svc.ServiceName}");
                }
            }

            if (commands.Count > 0)
            {
                ExecuteShellCommand(string.Join(" && ", commands.Distinct(StringComparer.OrdinalIgnoreCase)), true);
            }

            Application.Current?.Dispatcher.Invoke(() => RefreshAll());
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
    }
}
