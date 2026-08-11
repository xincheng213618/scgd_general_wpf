using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
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
    public partial class ServiceManagerViewModel : ViewModelBase, IConfigReloadParticipant, IDisposable
    {
        private readonly ILog log = LogManager.GetLogger(typeof(ServiceManagerViewModel));

        public static ServiceManagerViewModel Instance { get; } = new ServiceManagerViewModel();

        private readonly ServiceConfigurationLeaseGate configurationGate;
        private ServiceManagerConfig _config;
        private long nextConfigurationGeneration;
        private ServiceManagerOperationLease? mainOperationLease;
        private ServiceConfigurationSnapshot MainOperationSnapshot => mainOperationLease?.Snapshot
            ?? throw new InvalidOperationException("The service operation has not captured its configuration snapshot.");
        public ServiceManagerConfig Config => _config;

        public ObservableCollection<ServiceEntry> Services { get; set; } = [];

        public MySqlServiceManager MySqlManager { get; }

        public MqttServiceManager MqttManager { get; }

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

        public RelayCommand OneKeyStartCommand { get; }
        public RelayCommand OneKeyStopCommand { get; }
        public RelayCommand UpdateConfigCommand { get; }
        public RelayCommand OpenInstallManagerCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand SetBasePathCommand { get; }
        public RelayCommand OpenBaseLocationCommand { get; }
        public RelayCommand OpenFolderCommand { get; }
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
        public RelayCommand MySqlRegisterExistingCommand { get; }
        public RelayCommand MySqlStartCommand { get; }
        public RelayCommand MySqlStopCommand { get; }
        public RelayCommand MySqlUninstallCommand { get; }
        public RelayCommand MySqlRunScriptCommand { get; }
        public RelayCommand MySqlBrowseSqlScriptCommand { get; }
        public RelayCommand MySqlResetDatabaseCommand { get; }
        public RelayCommand MySqlBrowseCommand { get; }
        public RelayCommand MySqlApplyRootPasswordCommand { get; }
        public RelayCommand MySqlCreateOrUpdateUserCommand { get; }
        public RelayCommand MySqlGenerateRandomRootPasswordCommand { get; }

        public ServiceManagerViewModel()
        {
            ServiceConfigurationGeneration initial = ServiceConfigurationGeneration.Capture(ConfigService.Instance, 0);
            _config = initial.ServiceManager;
            configurationGate = new ServiceConfigurationLeaseGate(initial);
            MySqlManager = new MySqlServiceManager(_config, initial.MySql, initial.MySqlLocal, initial.MySqlSetting);
            MqttManager = new MqttServiceManager(initial.Mqtt, initial.MQTTSetting);

            // Commands
            OneKeyStartCommand = new RelayCommand(a => _ = OneKeyStartAsync(), a => !IsBusy);
            OneKeyStopCommand = new RelayCommand(a => _ = OneKeyStopAsync(), a => !IsBusy);
            UpdateConfigCommand = new RelayCommand(a => UpdateConfig(), a => !IsBusy);
            OpenInstallManagerCommand = new RelayCommand(a => OpenInstallManager());
            RefreshCommand = new RelayCommand(a => RefreshAll());
            SetBasePathCommand = new RelayCommand(a => SetBasePath());
            OpenBaseLocationCommand = new RelayCommand(a => OpenBaseLocation());
            OpenFolderCommand = new RelayCommand(a => OpenServiceFolder(a as ServiceEntry));
            ServiceInstallCommand = new RelayCommand(a => _ = InstallManagedServiceAsync(a as ServiceEntry), a => !IsBusy && a is ServiceEntry entry && !entry.IsInstalled && HasResolvableServiceExecutable(entry));
            ServiceUninstallCommand = new RelayCommand(a => _ = UninstallManagedServiceAsync(a as ServiceEntry), a => !IsBusy && a is ServiceEntry { IsInstalled: true });
            ServiceStartCommand = new RelayCommand(a => _ = ControlManagedServiceAsync(a as ServiceEntry, ServiceHostServiceOperation.Start), a => !IsBusy && a is ServiceEntry { IsInstalled: true, IsRunning: false });
            ServiceStopCommand = new RelayCommand(a => _ = ControlManagedServiceAsync(a as ServiceEntry, ServiceHostServiceOperation.Stop), a => !IsBusy && a is ServiceEntry { IsInstalled: true, IsRunning: true });
            ServiceRestartCommand = new RelayCommand(a => _ = ControlManagedServiceAsync(a as ServiceEntry, ServiceHostServiceOperation.Restart), a => !IsBusy && a is ServiceEntry { IsInstalled: true });
            ServiceTerminateCommand = new RelayCommand(a => _ = ControlManagedServiceAsync(a as ServiceEntry, ServiceHostServiceOperation.Terminate), a => !IsBusy && a is ServiceEntry entry && (entry.IsInstalled || !string.IsNullOrWhiteSpace(entry.ExePath)));
            MqttStartCommand = new RelayCommand(a => _ = StartMqttServiceAsync(), a => !IsBusy && MqttManager.Config.IsInstalled && !MqttManager.Config.IsRunning);
            MqttStopCommand = new RelayCommand(a => _ = StopMqttServiceAsync(), a => !IsBusy && MqttManager.Config.IsRunning);

            MySqlInstallZipCommand = new RelayCommand(a => _ = MySqlInstallZipAsync(), a => !IsBusy);
            MySqlRegisterExistingCommand = new RelayCommand(a => _ = RegisterExistingMySqlServiceAsync(), a => !IsBusy);
            MySqlStartCommand = new RelayCommand(a => _ = StartMySqlAsync(), a => !IsBusy && MySqlManager.Config.IsInstalled && !MySqlManager.Config.IsRunning);
            MySqlStopCommand = new RelayCommand(a => _ = StopMySqlAsync(), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlUninstallCommand = new RelayCommand(a => _ = UninstallMySqlAsync(), a => !IsBusy && MySqlManager.Config.IsInstalled);
            MySqlRunScriptCommand = new RelayCommand(a => _ = RunSqlScriptAsync(), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlBrowseSqlScriptCommand = new RelayCommand(a => BrowseSqlScriptPath());
            MySqlResetDatabaseCommand = new RelayCommand(a => _ = ResetDatabaseAsync(), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlBrowseCommand = new RelayCommand(a => BrowseMySqlPath());
            MySqlApplyRootPasswordCommand = new RelayCommand(a => _ = RunBackgroundOperationAsync("正在应用 MySQL root 密码...", DoApplyRootPassword), a => !IsBusy);
            MySqlCreateOrUpdateUserCommand = new RelayCommand(a => _ = RunBackgroundOperationAsync("正在创建或更新 MySQL 业务用户...", DoCreateOrUpdateUser), a => !IsBusy && MySqlManager.Config.IsRunning);
            MySqlGenerateRandomRootPasswordCommand = new RelayCommand(a => GenerateRandomRootPassword());

            Initialize();
        }

        public string ConfigReloadName => nameof(ServiceManagerViewModel);

        public int ConfigReloadOrder => 400;

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            ArgumentNullException.ThrowIfNull(currentConfig);
            long generation = Interlocked.Increment(ref nextConfigurationGeneration);
            ServiceConfigurationGeneration prepared = ServiceConfigurationGeneration.Capture(currentConfig, generation);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                PublishConfiguration(prepared, propagateFailure: true);
            else
                dispatcher.Invoke(() => PublishConfiguration(prepared, propagateFailure: true));
        }

        private void PublishConfiguration(ServiceConfigurationGeneration candidate, bool propagateFailure)
        {
            ServiceConfigurationGeneration? transition = configurationGate.QueueOrBeginTransition(candidate);
            if (transition != null)
                ApplyStartedTransitions(transition, propagateFailure);
        }

        private void ApplyStartedTransitions(ServiceConfigurationGeneration candidate, bool propagateFailure = false)
        {
            ServiceConfigurationGeneration? transition = candidate;
            while (transition != null)
            {
                bool applied = false;
                try
                {
                    ApplyConfiguration(transition);
                    applied = true;
                }
                catch (Exception ex)
                {
                    log.Error("应用服务管理器配置失败，保留上一代运行态", ex);
                    if (propagateFailure)
                    {
                        transition = configurationGate.CompleteTransition(transition, applied: false);
                        throw;
                    }
                }

                transition = configurationGate.CompleteTransition(transition, applied);
            }
        }

        private void ApplyConfiguration(ServiceConfigurationGeneration candidate)
        {
            ServiceConfigurationGeneration previous = configurationGate.Active;
            try
            {
                MySqlManager.RebindConfiguration(candidate.ServiceManager, candidate.MySql, candidate.MySqlLocal, candidate.MySqlSetting);
                MqttManager.RebindConfiguration(candidate.Mqtt, candidate.MQTTSetting);
                _config = candidate.ServiceManager;
                OnPropertyChanged(nameof(Config));
                RefreshAll();
            }
            catch
            {
                MySqlManager.RebindConfiguration(previous.ServiceManager, previous.MySql, previous.MySqlLocal, previous.MySqlSetting);
                MqttManager.RebindConfiguration(previous.Mqtt, previous.MQTTSetting);
                _config = previous.ServiceManager;
                OnPropertyChanged(nameof(Config));
                throw;
            }
        }

        internal ServiceManagerOperationLease BeginOperation()
        {
            return new ServiceManagerOperationLease(this, configurationGate.BeginOperation());
        }

        internal bool TryPersistOperationConfiguration(ServiceConfigurationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ServiceConfigurationGeneration active = configurationGate.Active;
            IConfigService currentConfig = ConfigService.Instance;
            if (active.Generation != snapshot.Generation
                || !ReferenceEquals(currentConfig.GetRequiredService<ServiceManagerConfig>(), active.ServiceManager)
                || !ReferenceEquals(currentConfig.GetRequiredService<MySqlServiceConfig>(), active.MySql)
                || !ReferenceEquals(currentConfig.GetRequiredService<MqttServiceConfig>(), active.Mqtt)
                || !ReferenceEquals(currentConfig.GetRequiredService<RCSetting>(), active.RCSetting)
                || !ReferenceEquals(currentConfig.GetRequiredService<CVWinSMSConfig>(), active.CVWinSMS)
                || !ReferenceEquals(currentConfig.GetRequiredService<MySqlLocalConfig>(), active.MySqlLocal)
                || !ReferenceEquals(currentConfig.GetRequiredService<MySqlSetting>(), active.MySqlSetting)
                || !ReferenceEquals(currentConfig.GetRequiredService<MQTTSetting>(), active.MQTTSetting))
            {
                log.Info($"配置代 {snapshot.Generation} 已不是当前代，跳过回写但继续使用该快照完成本次操作");
                return false;
            }

            Populate(snapshot.ServiceManager, active.ServiceManager);
            Populate(snapshot.MySql, active.MySql);
            Populate(snapshot.Mqtt, active.Mqtt);
            Populate(snapshot.RCSetting, active.RCSetting);
            Populate(snapshot.CVWinSMS, active.CVWinSMS);
            Populate(snapshot.MySqlLocal, active.MySqlLocal);
            Populate(snapshot.MySqlSetting, active.MySqlSetting);
            Populate(snapshot.MQTTSetting, active.MQTTSetting);
            ConfigHandler.GetInstance().SaveConfigs();
            return true;
        }

        private static void Populate<T>(T source, T destination) where T : class
        {
            JsonConvert.PopulateObject(JsonConvert.SerializeObject(source), destination);
        }

        internal void ReleaseOperation()
        {
            ServiceConfigurationGeneration? next = configurationGate.ReleaseOperation();

            if (next == null)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                ApplyStartedTransitions(next);
            else
                dispatcher.Invoke(() => ApplyStartedTransitions(next));
        }

        private async Task RunBackgroundOperationAsync(string text, Action action)
        {
            SetBusy(true, text);
            try
            {
                await Task.Run(action);
            }
            finally
            {
                SetBusy(false);
            }
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
            CVWinSMSConfig cvWinSmsConfig = configurationGate.Active.CVWinSMS;
            if (string.IsNullOrEmpty(Config.BaseLocation) && File.Exists(cvWinSmsConfig.CVWinSMSPath))
            {
                if (Config.ReadFromCVWinSMSConfig(cvWinSmsConfig.CVWinSMSPath))
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
            try
            {
                ColorVision.Engine.Services.RC.ServiceConfig.Instance.RefreshInstalledServices();
            }
            catch (Exception ex)
            {
                log.Warn("刷新 Engine 服务版本信息失败", ex);
            }
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
            void SetBusyCore()
            {
                if (busy && mainOperationLease == null)
                    mainOperationLease = BeginOperation();

                IsBusy = busy;
                ProgressText = text;
                if (!busy) Progress = 0;

                if (!busy)
                {
                    ServiceManagerOperationLease? completedLease = mainOperationLease;
                    mainOperationLease = null;
                    completedLease?.Dispose();
                }
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                SetBusyCore();
            else
                dispatcher.Invoke(SetBusyCore);
        }

        private void SetProgress(double value, string text = "")
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Progress = value;
                if (!string.IsNullOrEmpty(text)) ProgressText = text;
            });
        }

        public void Dispose()
        {
            mainOperationLease?.Dispose();
            mainOperationLease = null;
            GC.SuppressFinalize(this);
        }

    }
}
