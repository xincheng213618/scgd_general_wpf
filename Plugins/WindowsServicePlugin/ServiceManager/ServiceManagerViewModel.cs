using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
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

        public MySqlServiceManager MySqlManager { get; } = new MySqlServiceManager();

        public MqttServiceManager MqttManager { get; } = new MqttServiceManager();

        public MySqlServiceHelper MySqlHelper => MySqlManager.Helper;

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
        public RelayCommand CheckDatabaseConfigCommand { get; }

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
            MqttStartCommand = new RelayCommand(a => _ = Task.Run(() => { MqttManager.Start(AddLog); RefreshMqttStatus(); }), a => !IsBusy && MqttManager.Config.IsInstalled && !MqttManager.Config.IsRunning);
            MqttStopCommand = new RelayCommand(a => _ = Task.Run(() => { MqttManager.Stop(AddLog); RefreshMqttStatus(); }), a => !IsBusy && MqttManager.Config.IsRunning);

            MySqlInstallZipCommand = new RelayCommand(a => _ = MySqlInstallZipAsync(), a => !IsBusy);
            MySqlStartCommand = new RelayCommand(a => _ = Task.Run(() => { MySqlManager.Start(AddLog); RefreshMySqlStatus(); }), a => !IsBusy && MySqlManager.Config.IsInstalled && !MySqlManager.Config.IsRunning);
            MySqlStopCommand = new RelayCommand(a => _ = Task.Run(() => { MySqlManager.Stop(AddLog); RefreshMySqlStatus(); }), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlUninstallCommand = new RelayCommand(a => _ = Task.Run(() => { MySqlManager.Uninstall(AddLog); RefreshMySqlStatus(); }), a => !IsBusy && MySqlManager.Config.IsInstalled);
            MySqlBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoMySqlBackup()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlRestoreCommand = new RelayCommand(a => _ = Task.Run(() => DoMySqlRestore()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlRunScriptCommand = new RelayCommand(a => _ = Task.Run(() => DoRunSqlScript()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlBrowseCommand = new RelayCommand(a => BrowseMySqlPath());
            MySqlSetRootPasswordCommand = new RelayCommand(a => _ = Task.Run(() => DoSetRootPassword()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlForceResetRootPasswordCommand = new RelayCommand(a => _ = Task.Run(() => DoForceResetRootPassword()), a => !IsBusy);
            MySqlCreateOrUpdateUserCommand = new RelayCommand(a => _ = Task.Run(() => DoCreateOrUpdateUser()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlDeleteUserCommand = new RelayCommand(a => _ = Task.Run(() => DoDeleteUser()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlGenerateRandomRootPasswordCommand = new RelayCommand(a => GenerateRandomRootPassword());
            CheckDatabaseConfigCommand = new RelayCommand(a => _ = Task.Run(() => DoCheckDatabaseConfig()), a => !IsBusy && MySqlManager.Config.IsRunning);

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

            MySqlManager.Initialize(Config.MySqlPort);
            MqttManager.Initialize();

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
            CommandManager.InvalidateRequerySuggested();

            // 获取当前版本
            var rcService = Services.FirstOrDefault(s => s.ServiceName == "RegistrationCenterService");
            if (rcService != null && !string.IsNullOrEmpty(rcService.VersionText))
                CurrentVersion = rcService.VersionText;
        }

        private void RefreshMySqlStatus()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MySqlManager.RefreshStatus(Services, Config.MySqlPort);
            });
        }

        private void RefreshMqttStatus()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MqttManager.RefreshStatus(Services);
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
