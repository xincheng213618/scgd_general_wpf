using ColorVision.Common.MVVM;
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
        private readonly ILog log = LogManager.GetLogger(typeof(ServiceManagerViewModel));

        public static ServiceManagerViewModel Instance { get; } = new ServiceManagerViewModel();

        private readonly ServiceManagerConfig _config = ServiceManagerConfig.Instance;
        public ServiceManagerConfig Config => _config;

        public ObservableCollection<ServiceEntry> Services { get; set; } = [];

        public MySqlServiceManager MySqlManager { get; } = new MySqlServiceManager();

        public MqttServiceManager MqttManager { get; } = new MqttServiceManager();

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as an instance property for change notification and command binding.")]
        public string LegacyConfigPath => GetLegacyAppConfigPath() ?? string.Empty;
        public bool HasLegacyConfig => !string.IsNullOrWhiteSpace(LegacyConfigPath) && File.Exists(LegacyConfigPath);


        public RelayCommand OneKeyStartCommand { get; }
        public RelayCommand OneKeyStopCommand { get; }
        public RelayCommand UpdateConfigCommand { get; }
        public RelayCommand OpenInstallManagerCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand SetBasePathCommand { get; }
        public RelayCommand OpenFolderCommand { get; }
        public RelayCommand OpenWinServiceConfigCommand { get; }
        public RelayCommand OpenMySqlConfigCommand { get; }
        public RelayCommand OpenMqttConfigCommand { get; }
        public RelayCommand OpenLegacyConfigCommand { get; }
        public RelayCommand ServiceInstallCommand { get; }
        public RelayCommand ServiceUninstallCommand { get; }
        public RelayCommand ServiceStartCommand { get; }
        public RelayCommand ServiceStopCommand { get; }
        public RelayCommand ServiceRestartCommand { get; }
        public RelayCommand ServiceTerminateCommand { get; }
        public RelayCommand MqttStartCommand { get; }
        public RelayCommand MqttStopCommand { get; }

        // MySQL commands
        public RelayCommand MySqlInstallZipCommand { get; }
        public RelayCommand MySqlRepairServiceCommand { get; }
        public RelayCommand MySqlRegisterExistingCommand { get; }
        public RelayCommand MySqlStartCommand { get; }
        public RelayCommand MySqlStopCommand { get; }
        public RelayCommand MySqlUninstallCommand { get; }
        public RelayCommand MySqlBackupCommand { get; }
        public RelayCommand MySqlRestoreCommand { get; }
        public RelayCommand MySqlRunScriptCommand { get; }
        public RelayCommand MySqlResetDatabaseCommand { get; }
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
            SetBasePathCommand = new RelayCommand(a => SetBasePath());
            OpenFolderCommand = new RelayCommand(a => OpenServiceFolder(a as ServiceEntry));
            OpenWinServiceConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "WinService.config"));
            OpenMySqlConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "MySql.config"));
            OpenMqttConfigCommand = new RelayCommand(a => OpenServiceFile(a as ServiceEntry, "MQTT.config"));
            OpenLegacyConfigCommand = new RelayCommand(a => OpenLegacyConfigFile(), a => HasLegacyConfig);
            ServiceInstallCommand = new RelayCommand(a => _ = InstallManagedServiceAsync(a as ServiceEntry), a => !IsBusy && a is ServiceEntry entry && !entry.IsInstalled && HasResolvableServiceExecutable(entry));
            ServiceUninstallCommand = new RelayCommand(a => _ = UninstallManagedServiceAsync(a as ServiceEntry), a => !IsBusy && a is ServiceEntry { IsInstalled: true });
            ServiceStartCommand = new RelayCommand(a => _ = ControlManagedServiceAsync(a as ServiceEntry, ServiceHostServiceOperation.Start), a => !IsBusy && a is ServiceEntry { IsInstalled: true, IsRunning: false });
            ServiceStopCommand = new RelayCommand(a => _ = ControlManagedServiceAsync(a as ServiceEntry, ServiceHostServiceOperation.Stop), a => !IsBusy && a is ServiceEntry { IsInstalled: true, IsRunning: true });
            ServiceRestartCommand = new RelayCommand(a => _ = ControlManagedServiceAsync(a as ServiceEntry, ServiceHostServiceOperation.Restart), a => !IsBusy && a is ServiceEntry { IsInstalled: true });
            ServiceTerminateCommand = new RelayCommand(a => _ = ControlManagedServiceAsync(a as ServiceEntry, ServiceHostServiceOperation.Terminate), a => !IsBusy && a is ServiceEntry entry && (entry.IsInstalled || !string.IsNullOrWhiteSpace(entry.ExePath)));
            MqttStartCommand = new RelayCommand(a => _ = StartMqttServiceAsync(), a => !IsBusy && MqttManager.Config.IsInstalled && !MqttManager.Config.IsRunning);
            MqttStopCommand = new RelayCommand(a => _ = StopMqttServiceAsync(), a => !IsBusy && MqttManager.Config.IsRunning);

            MySqlInstallZipCommand = new RelayCommand(a => _ = MySqlInstallZipAsync(), a => !IsBusy);
            MySqlRepairServiceCommand = new RelayCommand(a => _ = RepairMySqlServicePreferServiceHostAsync(), a => !IsBusy);
            MySqlRegisterExistingCommand = new RelayCommand(a => _ = RegisterExistingMySqlServiceAsync(), a => !IsBusy);
            MySqlStartCommand = new RelayCommand(a => _ = StartMySqlAsync(), a => !IsBusy && MySqlManager.Config.IsInstalled && !MySqlManager.Config.IsRunning);
            MySqlStopCommand = new RelayCommand(a => _ = StopMySqlAsync(), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlUninstallCommand = new RelayCommand(a => _ = UninstallMySqlAsync(), a => !IsBusy && MySqlManager.Config.IsInstalled);
            MySqlBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoMySqlBackup()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlRestoreCommand = new RelayCommand(a => _ = Task.Run(() => DoMySqlRestore()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlRunScriptCommand = new RelayCommand(a => _ = Task.Run(() => DoRunSqlScript()), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlResetDatabaseCommand = new RelayCommand(a => _ = ResetDatabaseAsync(), a => !IsBusy && MySqlManager.Config.IsRunning);
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
                if (Config.TryDetectInstallPath())
                {
                    SaveServiceManagerConfig();
                }
            }

            // 尝试从CVWinSMS配置读取
            if (string.IsNullOrEmpty(Config.BaseLocation) && File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                if (Config.ReadFromCVWinSMSConfig(CVWinSMSConfig.Instance.CVWinSMSPath))
                {
                    SaveServiceManagerConfig();
                }
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
