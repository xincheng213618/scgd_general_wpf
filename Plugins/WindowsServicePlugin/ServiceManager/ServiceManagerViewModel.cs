using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using WindowsServicePlugin.CVWinSMS;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 服务管理器主视图模型：属性、命令定义、初始化、刷新、通用辅助
    /// 具体实现拆分到 partial 文件:
    ///   - ServiceManagerViewModel.OneKey.cs    一键启动/停止
    ///   - ServiceManagerViewModel.Config.cs    配置同步
    ///   - ServiceManagerViewModel.MySql.cs     MySQL 操作
    ///   - ServiceManagerViewModel.Helpers.cs   辅助方法
    /// </summary>
    public partial class ServiceManagerViewModel : ViewModelBase
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
        public RelayCommand MySqlDeleteUserCommand { get; }
        public RelayCommand MySqlGenerateRandomRootPasswordCommand { get; }

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
            MySqlDeleteUserCommand = new RelayCommand(a => _ = Task.Run(() => DoDeleteUser()), a => !IsBusy && IsMySqlRunning);
            MySqlGenerateRandomRootPasswordCommand = new RelayCommand(a => GenerateRandomRootPassword());

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
            MySqlHelper.Port = GetConfiguredMySqlPort();

            var dbCfg = MySqlSetting.Instance.MySqlConfig;
            MySqlAppUser = dbCfg.UserName;
            MySqlAppPassword = dbCfg.UserPwd;
            MySqlDatabaseName = dbCfg.Database;

            var rootCfg = MySqlSetting.Instance.FindProfile(MySqlSetting.RootProfileName);
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
                MySqlHelper.Port = GetConfiguredMySqlPort();
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
    }
}
