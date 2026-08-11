#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;

namespace ColorVision.SocketProtocol
{

    /// <summary>
    /// Socket连接管理器
    /// 负责管理TCP服务器、客户端连接和消息分发
    /// </summary>
    public class SocketManager:ViewModelBase, IConfigReloadParticipant
    {
        private static ILog log = LogManager.GetLogger(typeof(SocketManager));
        private static readonly SocketManagerApplicationLifetime ApplicationLifetime = new();

        /// <summary>
        /// 获取SocketManager单例实例
        /// </summary>
        /// <returns>SocketManager实例</returns>
        public static SocketManager GetInstance() => ApplicationLifetime.GetOrCreate(static () => new SocketManager());

        public static bool ShutdownExisting(TimeSpan timeout) => ApplicationLifetime.ShutdownExisting(timeout);

        private readonly SocketWorkerTracker _workerTracker;
        private readonly SocketServerLifecycle _serverLifecycle;
        private readonly object _configBindingLock = new();
        private SocketConfig _config = null!;
        private volatile bool _hasUsableConfig = true;
        private bool _serverInitialized;
        private long _appliedTransitionSequence;
        private int _firewallRefreshVersion;
        private int _shutdownStarted;

        /// <summary>
        /// Socket配置信息
        /// </summary>
        public SocketConfig Config => Volatile.Read(ref _config);

        public string ConfigReloadName => nameof(SocketManager);

        public int ConfigReloadOrder => 350;

        internal bool HasUsableConfig => _hasUsableConfig;

        /// <summary>
        /// 编辑配置命令
        /// </summary>
        public RelayCommand EditCommand { get; set; }

        /// <summary>
        /// 添加当前程序防火墙允许规则命令
        /// </summary>
        public RelayCommand AllowFirewallRuleCommand { get; set; }

        /// <summary>
        /// JSON消息分发器
        /// </summary>
        public SocketJsonDispatcher JsonDispatcher { get; set; }

        /// <summary>
        /// 文本消息分发器
        /// </summary>
        public SocketTextDispatcher TextDispatcher { get;set; }

        /// <summary>
        /// 消息管理器(用于持久化)
        /// </summary>
        public SocketMessageManager MessageManager { get; set; }

        public SocketManager()
            : this(
                SocketConfig.Instance,
                TcpSocketServerListenerFactory.Instance,
                action => _ = Task.Run(action),
                new SocketWorkerTracker(),
                new SocketJsonDispatcher(),
                new SocketTextDispatcher(),
                SocketMessageManager.GetInstance(),
                refreshNetworkAccessStatus: true)
        {
        }

        internal SocketManager(
            SocketConfig config,
            ISocketServerListenerFactory listenerFactory,
            Action<Action> queueWork,
            SocketWorkerTracker workerTracker,
            SocketJsonDispatcher jsonDispatcher,
            SocketTextDispatcher textDispatcher,
            SocketMessageManager messageManager,
            bool refreshNetworkAccessStatus,
            Action<Action>? queueShutdownWork = null)
        {
            ArgumentNullException.ThrowIfNull(config);
            SetConfigReference(config);
            _workerTracker = workerTracker;
            JsonDispatcher = jsonDispatcher;
            TextDispatcher = textDispatcher;
            MessageManager = messageManager;
            _serverLifecycle = new SocketServerLifecycle(
                Config.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled,
                listenerFactory,
                queueWork,
                _workerTracker,
                ApplyServerTransition,
                AcceptClient,
                CloseClient,
                queueShutdownWork);
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            AllowFirewallRuleCommand = new RelayCommand(a => _ = AllowFirewallRuleAsync(a?.ToString()));
            AttachConfig(config);
            TcpClients.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ClientCountText));
            ServerState = Config.IsServerEnabled ? SocketServerState.Stopped : SocketServerState.Disabled;
            if (refreshNetworkAccessStatus)
                RefreshNetworkAccessStatus();
        }

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            ArgumentNullException.ThrowIfNull(currentConfig);

            SocketConfig nextConfig;
            try
            {
                nextConfig = currentConfig.GetRequiredService<SocketConfig>();
            }
            catch (Exception exception)
            {
                FailClosedAfterConfigResolutionFailure(exception);
                return;
            }

            RunOnUiThread(() => BindCurrentConfigCore(nextConfig));
        }

        private void BindCurrentConfigCore(SocketConfig nextConfig)
        {
            var failures = new List<Exception>();
            bool shouldTransition;
            bool sameConfig;
            lock (_configBindingLock)
            {
                if (Volatile.Read(ref _shutdownStarted) != 0)
                    throw new InvalidOperationException("SocketManager cannot bind configuration after application shutdown has started.");

                SocketConfig previousConfig = Config;
                sameConfig = ReferenceEquals(previousConfig, nextConfig);
                if (sameConfig && _hasUsableConfig)
                    return;

                if (!sameConfig)
                    DetachConfig(previousConfig);

                SetConfigReference(nextConfig);
                _hasUsableConfig = true;
                AttachConfig(nextConfig);
                shouldTransition = _serverInitialized;
            }

            if (shouldTransition && Volatile.Read(ref _shutdownStarted) == 0)
            {
                if (!sameConfig)
                    TryConfigTransition(() => _serverLifecycle.Stop(nextConfig.IsServerEnabled), failures);

                bool shouldStart;
                lock (_configBindingLock)
                {
                    shouldStart = Volatile.Read(ref _shutdownStarted) == 0
                        && _hasUsableConfig
                        && ReferenceEquals(Config, nextConfig)
                        && nextConfig.IsServerEnabled;
                }
                if (shouldStart)
                {
                    TryConfigTransition(
                        () => _serverLifecycle.Start(SocketServerSettings.Capture(nextConfig)),
                        failures);
                }
            }

            TryConfigTransition(PublishConfigChanged, failures);
            ThrowConfigTransitionFailures(failures);
        }

        private void FailClosedAfterConfigResolutionFailure(Exception resolutionFailure)
        {
            var failures = new List<Exception> { resolutionFailure };
            RunOnUiThread(() =>
            {
                bool shouldStop = false;
                lock (_configBindingLock)
                {
                    if (Volatile.Read(ref _shutdownStarted) == 0)
                    {
                        if (_hasUsableConfig)
                            DetachConfig(Config);
                        _hasUsableConfig = false;
                        shouldStop = _serverInitialized;
                    }
                }

                if (shouldStop && Volatile.Read(ref _shutdownStarted) == 0)
                    TryConfigTransition(() => _serverLifecycle.Stop(isServerEnabled: false), failures);

                TryConfigTransition(PublishConfigChanged, failures);
            });
            ThrowConfigTransitionFailures(failures);
        }

        private static void TryConfigTransition(Action action, List<Exception> failures)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        private static void TryConfigTransition(Func<bool> transition, List<Exception> failures) =>
            TryConfigTransition(() => _ = transition(), failures);

        private static void ThrowConfigTransitionFailures(List<Exception> failures)
        {
            if (failures.Count != 0)
            {
                throw new AggregateException(
                    "Socket runtime configuration could not be fully rebound.",
                    failures);
            }
        }

        private void AttachConfig(SocketConfig config)
        {
            config.PropertyChanged += CurrentConfig_PropertyChanged;
            config.ServerEnabledChanged += CurrentConfig_ServerEnabledChanged;
        }

        private void DetachConfig(SocketConfig config)
        {
            config.PropertyChanged -= CurrentConfig_PropertyChanged;
            config.ServerEnabledChanged -= CurrentConfig_ServerEnabledChanged;
        }

        private void CurrentConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                bool shouldNotify;
                lock (_configBindingLock)
                {
                    shouldNotify = Volatile.Read(ref _shutdownStarted) == 0
                        && _hasUsableConfig
                        && ReferenceEquals(sender, Config);
                }
                if (shouldNotify)
                    NotifyServerStatusChanged();
            });
        }

        private void CurrentConfig_ServerEnabledChanged(object? sender, bool isEnabled)
        {
            RunOnUiThread(() => CurrentConfig_ServerEnabledChangedCore(sender, isEnabled));
        }

        private void CurrentConfig_ServerEnabledChangedCore(object? sender, bool isEnabled)
        {
            bool transitionAccepted = true;
            SocketConfig currentConfig;
            lock (_configBindingLock)
            {
                if (Volatile.Read(ref _shutdownStarted) != 0
                    || !_hasUsableConfig
                    || !_serverInitialized
                    || !ReferenceEquals(sender, Config))
                {
                    return;
                }
                currentConfig = Config;
            }

            if (isEnabled)
                transitionAccepted = _serverLifecycle.Start(SocketServerSettings.Capture(currentConfig));
            else
                transitionAccepted = _serverLifecycle.Stop(isServerEnabled: false);
            if (!transitionAccepted)
                NotifyServerStatusChanged();
        }

        private void PublishConfigChanged()
        {
            OnPropertyChanged(nameof(Config));
            OnPropertyChanged(nameof(HasUsableConfig));
            NotifyServerStatusChanged();
        }

        internal void SetConfigReference(SocketConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            Volatile.Write(ref _config, config);
        }

        /// <summary>
        /// Socket连接状态改变事件
        /// </summary>
        public event EventHandler<bool> SocketConnectChanged;

        /// <summary>
        /// 获取或设置当前连接状态
        /// </summary>
        public bool IsConnect => ServerState == SocketServerState.Running;

        public SocketServerState ServerState
        {
            get => _ServerState;
            private set
            {
                if (_ServerState == value)
                    return;

                _ServerState = value;
                OnPropertyChanged();
                NotifyServerStatusChanged();
            }
        }
        private SocketServerState _ServerState;

        public string ServerStateText
        {
            get
            {
                if (ServerState == SocketServerState.Error)
                    return Properties.Resources.OpenFailed;
                if (!_hasUsableConfig || !Config.IsServerEnabled)
                    return Properties.Resources.Disabled;

                return ServerState switch
                {
                    SocketServerState.Starting => Properties.Resources.Starting,
                    SocketServerState.Running => Properties.Resources.Running,
                    SocketServerState.Stopping => Properties.Resources.Stopping,
                    SocketServerState.Error => Properties.Resources.OpenFailed,
                    _ => Properties.Resources.Stopped
                };
            }
        }

        public string EnabledStatusText => _hasUsableConfig && Config.IsServerEnabled
            ? Properties.Resources.Enabled
            : Properties.Resources.Disabled;

        public string OpenStatusText
        {
            get
            {
                if (ServerState == SocketServerState.Error)
                    return Properties.Resources.OpenFailed;
                if (!_hasUsableConfig || !Config.IsServerEnabled)
                    return Properties.Resources.Stopped;

                return IsConnect
                    ? Properties.Resources.Running
                    : Properties.Resources.Stopped;
            }
        }

        public string ListenAddress => $"{Config.IPAddress}:{Config.ServerPort}";

        public string PrivateFirewallStatusText
        {
            get => _PrivateFirewallStatusText;
            private set
            {
                if (_PrivateFirewallStatusText == value)
                    return;

                _PrivateFirewallStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrivateFirewallTooltip));
            }
        }
        private string _PrivateFirewallStatusText = string.Empty;

        public string PrivateFirewallStatusDetail
        {
            get => _PrivateFirewallStatusDetail;
            private set
            {
                if (_PrivateFirewallStatusDetail == value)
                    return;

                _PrivateFirewallStatusDetail = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrivateFirewallTooltip));
            }
        }
        private string _PrivateFirewallStatusDetail = string.Empty;

        public bool CanAllowPrivateFirewall
        {
            get => _CanAllowPrivateFirewall;
            private set
            {
                if (_CanAllowPrivateFirewall == value)
                    return;

                _CanAllowPrivateFirewall = value;
                OnPropertyChanged();
            }
        }
        private bool _CanAllowPrivateFirewall;

        public string PrivateFirewallTooltip => PrivateFirewallStatusDetail;

        public string PublicFirewallStatusText
        {
            get => _PublicFirewallStatusText;
            private set
            {
                if (_PublicFirewallStatusText == value)
                    return;

                _PublicFirewallStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PublicFirewallTooltip));
            }
        }
        private string _PublicFirewallStatusText = string.Empty;

        public string PublicFirewallStatusDetail
        {
            get => _PublicFirewallStatusDetail;
            private set
            {
                if (_PublicFirewallStatusDetail == value)
                    return;

                _PublicFirewallStatusDetail = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PublicFirewallTooltip));
            }
        }
        private string _PublicFirewallStatusDetail = string.Empty;

        public bool CanAllowPublicFirewall
        {
            get => _CanAllowPublicFirewall;
            private set
            {
                if (_CanAllowPublicFirewall == value)
                    return;

                _CanAllowPublicFirewall = value;
                OnPropertyChanged();
            }
        }
        private bool _CanAllowPublicFirewall;

        public string PublicFirewallTooltip => PublicFirewallStatusDetail;

        public string ClientCountText => FormatResource(Properties.Resources.ClientCountFormat, TcpClients.Count);

        public string LastErrorMessage
        {
            get => _LastErrorMessage;
            private set
            {
                if (_LastErrorMessage == value)
                    return;

                _LastErrorMessage = value;
                OnPropertyChanged();
                NotifyServerStatusChanged();
            }
        }
        private string _LastErrorMessage = string.Empty;

        public DateTime? LastStatusChangedTime
        {
            get => _LastStatusChangedTime;
            private set
            {
                if (_LastStatusChangedTime == value)
                    return;

                _LastStatusChangedTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastStatusChangedText));
            }
        }
        private DateTime? _LastStatusChangedTime;

        public string LastStatusChangedText => LastStatusChangedTime.HasValue
            ? FormatResource(Properties.Resources.UpdatedAtFormat, LastStatusChangedTime.Value)
            : string.Empty;

        public bool HasLastError => ServerState == SocketServerState.Error && !string.IsNullOrWhiteSpace(LastErrorMessage);

        public string LastErrorDisplay => HasLastError ? LastErrorMessage : Properties.Resources.NoError;

        public string ServerSummary => $"{EnabledStatusText} / {OpenStatusText} / {ListenAddress}";

        private static string FormatResource(string format, params object?[] args)
        {
#pragma warning disable CA1863
            return string.Format(CultureInfo.CurrentUICulture, format, args);
#pragma warning restore CA1863
        }

        private void NotifyServerStatusChanged()
        {
            OnPropertyChanged(nameof(ServerStateText));
            OnPropertyChanged(nameof(EnabledStatusText));
            OnPropertyChanged(nameof(OpenStatusText));
            OnPropertyChanged(nameof(ListenAddress));
            OnPropertyChanged(nameof(PrivateFirewallStatusText));
            OnPropertyChanged(nameof(PrivateFirewallStatusDetail));
            OnPropertyChanged(nameof(CanAllowPrivateFirewall));
            OnPropertyChanged(nameof(PrivateFirewallTooltip));
            OnPropertyChanged(nameof(PublicFirewallStatusText));
            OnPropertyChanged(nameof(PublicFirewallStatusDetail));
            OnPropertyChanged(nameof(CanAllowPublicFirewall));
            OnPropertyChanged(nameof(PublicFirewallTooltip));
            OnPropertyChanged(nameof(ClientCountText));
            OnPropertyChanged(nameof(LastStatusChangedText));
            OnPropertyChanged(nameof(HasLastError));
            OnPropertyChanged(nameof(LastErrorDisplay));
            OnPropertyChanged(nameof(ServerSummary));
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        private static void PostOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                _ = dispatcher.BeginInvoke(action);
            }
        }

        private void ApplyServerTransition(SocketServerTransition transition)
        {
            if (Volatile.Read(ref _shutdownStarted) != 0)
                return;

            PostOnUiThread(() => ApplyServerTransitionCore(transition));
        }

        internal void ApplyServerTransitionCore(SocketServerTransition transition)
        {
            if (Volatile.Read(ref _shutdownStarted) != 0)
                return;
            if (transition.Sequence <= _appliedTransitionSequence)
                return;

            bool wasConnected = IsConnect;
            _appliedTransitionSequence = transition.Sequence;
            ServerState = transition.State;
            LastStatusChangedTime = DateTime.Now;
            LastErrorMessage = transition.State == SocketServerState.Error && transition.Exception != null
                ? BuildFailureMessage(transition)
                : string.Empty;

            if (transition.State == SocketServerState.Running)
                log.Info("Server started. Listening on port: " + transition.Settings!.ServerPort);
            else if (transition.State is SocketServerState.Stopped or SocketServerState.Disabled)
                log.Info("Server stopped.");
            else if (transition.State == SocketServerState.Error)
                log.Error(LastErrorMessage, transition.Exception);

            if (wasConnected != IsConnect)
            {
                OnPropertyChanged(nameof(IsConnect));
                SocketConnectChanged?.Invoke(this, IsConnect);
            }
        }

        private string BuildFailureMessage(SocketServerTransition transition)
        {
            if (transition.FailureStage == SocketServerFailureStage.Stop)
                return FormatResource(Properties.Resources.StopServerFailedFormat, transition.Exception!.Message);

            return BuildOpenFailureMessage(transition.Settings!, transition.Exception!);
        }

        private void RefreshNetworkAccessStatus()
        {
            _ = RefreshNetworkAccessStatusAsync();
        }

        private async Task RefreshNetworkAccessStatusAsync()
        {
            int refreshVersion = Interlocked.Increment(ref _firewallRefreshVersion);
            string? executablePath = GetCurrentExecutablePath();

            RunOnUiThread(() =>
            {
                PrivateFirewallStatusText = "检测中...";
                PrivateFirewallStatusDetail = "正在后台读取 Windows 防火墙规则。";
                CanAllowPrivateFirewall = false;
                PublicFirewallStatusText = "检测中...";
                PublicFirewallStatusDetail = "正在后台读取 Windows 防火墙规则。";
                CanAllowPublicFirewall = false;
            });

            try
            {
                FirewallProfileStatuses statuses = await Task.Run(() => SocketFirewallService.GetStatuses(executablePath)).ConfigureAwait(false);

                if (refreshVersion != Volatile.Read(ref _firewallRefreshVersion))
                    return;

                RunOnUiThread(() =>
                {
                    PrivateFirewallStatusText = statuses.PrivateStatus.Summary;
                    PrivateFirewallStatusDetail = statuses.PrivateStatus.Detail;
                    CanAllowPrivateFirewall = statuses.PrivateStatus.CanAllow;
                    PublicFirewallStatusText = statuses.PublicStatus.Summary;
                    PublicFirewallStatusDetail = statuses.PublicStatus.Detail;
                    CanAllowPublicFirewall = statuses.PublicStatus.CanAllow;
                });
            }
            catch (Exception ex)
            {
                if (refreshVersion != Volatile.Read(ref _firewallRefreshVersion))
                    return;

                RunOnUiThread(() =>
                {
                    PrivateFirewallStatusText = "无法读取";
                    PrivateFirewallStatusDetail = ex.Message;
                    CanAllowPrivateFirewall = false;
                    PublicFirewallStatusText = "无法读取";
                    PublicFirewallStatusDetail = ex.Message;
                    CanAllowPublicFirewall = false;
                });
            }
        }

        private static string? GetCurrentExecutablePath()
        {
            try
            {
                return Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch (Exception ex)
            {
                log.Warn("Unable to get current executable path.", ex);
                return null;
            }
        }

        private async Task AllowFirewallRuleAsync(string? profile)
        {
            string? executablePath = GetCurrentExecutablePath();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "无法获取当前程序路径，不能创建防火墙规则。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            FirewallCommandResult allowResult = await SocketFirewallService.AllowApplicationAsync(executablePath, profile ?? "private").ConfigureAwait(true);
            RefreshNetworkAccessStatus();

            MessageBoxImage image = allowResult.Success ? MessageBoxImage.Information : MessageBoxImage.Error;
            MessageBox.Show(Application.Current.GetActiveWindow(), allowResult.Message, "ColorVision", MessageBoxButton.OK, image);
        }

        /// <summary>
        /// 启动Socket服务器
        /// </summary>
        public void StartServer()
        {
            bool transitionAccepted = true;
            SocketConfig currentConfig;
            lock (_configBindingLock)
            {
                if (Volatile.Read(ref _shutdownStarted) != 0)
                    return;
                _serverInitialized = true;
                if (!_hasUsableConfig)
                    return;
                currentConfig = Config;
            }
            transitionAccepted = _serverLifecycle.Start(SocketServerSettings.Capture(currentConfig));
            if (!transitionAccepted)
                NotifyServerStatusChanged();
        }

        internal void InitializeServer()
        {
            bool transitionAccepted = true;
            bool shouldStart;
            SocketConfig currentConfig;
            lock (_configBindingLock)
            {
                _serverInitialized = true;
                currentConfig = Config;
                shouldStart = Volatile.Read(ref _shutdownStarted) == 0
                    && _hasUsableConfig
                    && currentConfig.IsServerEnabled;
            }
            if (shouldStart)
                transitionAccepted = _serverLifecycle.Start(SocketServerSettings.Capture(currentConfig));
            if (shouldStart && !transitionAccepted)
                NotifyServerStatusChanged();
        }

        /// <summary>
        /// 停止Socket服务器
        /// </summary>
        public void StopServer()
        {
            bool transitionAccepted = true;
            bool targetEnabled;
            lock (_configBindingLock)
            {
                if (Volatile.Read(ref _shutdownStarted) != 0)
                    return;
                targetEnabled = _hasUsableConfig && Config.IsServerEnabled;
            }
            transitionAccepted = _serverLifecycle.Stop(targetEnabled);
            if (!transitionAccepted)
                NotifyServerStatusChanged();
        }

        internal void BeginShutdown()
        {
            if (Interlocked.Exchange(ref _shutdownStarted, 1) == 0)
            {
                _serverLifecycle.BeginShutdown();
                DetachConfigWithoutBlockingShutdown();
            }
            else
            {
                _serverLifecycle.BeginShutdown();
            }
        }

        private void DetachConfigWithoutBlockingShutdown()
        {
            if (Monitor.TryEnter(_configBindingLock))
            {
                try
                {
                    DetachConfig(Config);
                    _hasUsableConfig = false;
                }
                finally
                {
                    Monitor.Exit(_configBindingLock);
                }
                return;
            }

            try
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    lock (_configBindingLock)
                    {
                        if (Volatile.Read(ref _shutdownStarted) != 0)
                        {
                            DetachConfig(Config);
                            _hasUsableConfig = false;
                        }
                    }
                });
            }
            catch
            {
                // The terminal flag and lifecycle shutdown still reject every later callback.
            }
        }

        internal bool Shutdown(TimeSpan timeout) => Shutdown(SocketShutdownDeadline.Start(timeout));

        internal bool Shutdown(SocketShutdownDeadline deadline)
        {
            BeginShutdown();

            bool workersCompleted = _workerTracker.Wait(deadline.Remaining);
            Exception? cleanupException = _serverLifecycle.ShutdownException;
            int remainingWorkers = _workerTracker.ActiveWorkers;
            QueueShutdownResultLog(
                workersCompleted,
                cleanupException,
                remainingWorkers,
                deadline.Elapsed.TotalMilliseconds);

            return workersCompleted && cleanupException == null;
        }

        private static void QueueShutdownResultLog(
            bool workersCompleted,
            Exception? cleanupException,
            int remainingWorkers,
            double elapsedMilliseconds)
        {
            try
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (cleanupException != null)
                            log.Error("Socket shutdown completed with a resource cleanup error.", cleanupException);
                        if (!workersCompleted)
                            log.Warn($"Socket shutdown timed out after {elapsedMilliseconds:F0} ms with {remainingWorkers} worker(s) still active.");
                        else
                            log.Info($"Socket shutdown completed in {elapsedMilliseconds:F0} ms.");
                    }
                    catch
                    {
                    }
                });
            }
            catch
            {
                // Logging must not extend or fail the application shutdown deadline.
            }
        }

        /// <summary>
        /// 已连接的TCP客户端集合
        /// </summary>
        public ObservableCollection<TcpClient> TcpClients { get; set; } = new ObservableCollection<TcpClient>();

        public void CheckUpdate()
        {
            RunOnUiThread(() =>
            {
                SocketConfig currentConfig;
                lock (_configBindingLock)
                {
                    if (Volatile.Read(ref _shutdownStarted) != 0 || !_hasUsableConfig)
                        return;
                    currentConfig = Config;
                }

                if (!_serverLifecycle.StartInline(SocketServerSettings.Capture(currentConfig)))
                    NotifyServerStatusChanged();
            });
        }

        private string BuildOpenFailureMessage(SocketServerSettings settings, Exception exception)
        {
            if (exception is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
            {
                return $"打开 {settings.ListenAddress} 失败：端口 {settings.ServerPort} 已被占用，请关闭占用该端口的程序或在服务设置中更换端口。";
            }

            if (exception is SocketException { SocketErrorCode: SocketError.AccessDenied })
            {
                return $"打开 {settings.ListenAddress} 失败：没有权限监听该地址，请检查监听地址或系统权限。";
            }

            return FormatResource(Properties.Resources.OpenListenAddressFailedFormat, settings.ListenAddress, exception.Message);
        }


        private void AcceptClient(SocketServerClient connection)
        {
            if (Volatile.Read(ref _shutdownStarted) != 0 || connection.IsClosed)
                return;

            PostOnUiThread(() =>
            {
                if (Volatile.Read(ref _shutdownStarted) == 0 && !connection.IsClosed)
                    TcpClients.Add(connection.Client);
            });
            if (Volatile.Read(ref _shutdownStarted) != 0
                || connection.IsClosed
                || !_workerTracker.TryRegister(out SocketWorkerLease? workerLease))
                return;

            Thread clientThread = CreateClientThread(() => HandleClient(connection, workerLease));
            try
            {
                clientThread.Start();
            }
            catch
            {
                workerLease.Dispose();
                _serverLifecycle.ReleaseClient(connection);
                throw;
            }
        }

        internal static Thread CreateClientThread(ThreadStart start) => new(start)
        {
            IsBackground = true,
            Name = "ColorVision.SocketClient"
        };

        private void HandleClient(SocketServerClient connection, SocketWorkerLease workerLease)
        {
            using (workerLease)
                HandleClientCore(connection);
        }

        private void HandleClientCore(SocketServerClient connection)
        {
            TcpClient client = connection.Client;
            SocketServerSettings settings = connection.Settings;
            string clientEndPoint = GetClientEndPoint(client);
            int bytesRead;
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = settings.SocketBufferSize > 1024 ? new byte[settings.SocketBufferSize] : new byte[1024];
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // 创建接收消息记录并持久化
                    var receivedMsg = new SocketMessage
                    {
                        ClientEndPoint = clientEndPoint,
                        Direction = SocketMessageDirection.Received,
                        Content = message,
                        MessageTime = DateTime.Now
                    };

                    log.Info("Received raw message: " + message);
                    switch (settings.SocketPhraseType)
                    {
                        case SocketPhraseType.Json:
                            SocketRequest? request = null;
                            try
                            {
                                request = JsonConvert.DeserializeObject<SocketRequest>(message);
                                receivedMsg.EventName = request?.EventName;
                                receivedMsg.MsgID = request?.MsgID;

                                // 持久化接收消息
                                MessageManager.AddMessage(receivedMsg);

                                var response = JsonDispatcher.Dispatch(stream, request);

                                if (response != null)
                                {
                                    string respString = JsonConvert.SerializeObject(response);

                                    // 创建发送消息记录并持久化
                                    var sentMsg = new SocketMessage
                                    {
                                        ClientEndPoint = clientEndPoint,
                                        Direction = SocketMessageDirection.Sent,
                                        Content = respString,
                                        MessageTime = DateTime.Now,
                                        EventName = response.EventName,
                                        MsgID = response.MsgID,
                                        ResponseCode = response.Code
                                    };
                                    MessageManager.AddMessage(sentMsg);

                                    stream.Write(Encoding.UTF8.GetBytes(respString));
                                }
                            }
                            catch (Exception ex)
                            {
                                var response = new SocketResponse
                                {
                                    Version = request?.Version ?? "1.0",
                                    MsgID = request?.MsgID ?? "",
                                    EventName = request?.EventName ?? "",
                                    SerialNumber = request?.SerialNumber ?? "",
                                    Code = -1,
                                    Msg = ex.Message,
                                    Data = null
                                };

                                // 持久化接收消息(即使出错)
                                MessageManager.AddMessage(receivedMsg);

                                string respString = JsonConvert.SerializeObject(response);

                                // 创建错误响应消息记录并持久化
                                var sentMsg = new SocketMessage
                                {
                                    ClientEndPoint = clientEndPoint,
                                    Direction = SocketMessageDirection.Sent,
                                    Content = respString,
                                    MessageTime = DateTime.Now,
                                    EventName = response.EventName,
                                    MsgID = response.MsgID,
                                    ResponseCode = response.Code
                                };
                                MessageManager.AddMessage(sentMsg);

                                byte[] respBytes = Encoding.UTF8.GetBytes(respString);
                                stream.Write(respBytes, 0, respBytes.Length);
                                continue;
                            }
                            break;
                        case SocketPhraseType.Text:
                            try
                            {
                                // 持久化接收消息
                                MessageManager.AddMessage(receivedMsg);

                                var string1 = TextDispatcher.Dispatch(stream, message);
                                if (string1 != null)
                                {
                                    // 创建发送消息记录并持久化
                                    var sentMsg = new SocketMessage
                                    {
                                        ClientEndPoint = clientEndPoint,
                                        Direction = SocketMessageDirection.Sent,
                                        Content = string1,
                                        MessageTime = DateTime.Now
                                    };
                                    MessageManager.AddMessage(sentMsg);

                                    byte[] respBytes = Encoding.UTF8.GetBytes(string1);
                                    stream.Write(respBytes, 0, respBytes.Length);
                                }
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);

                                // 创建错误响应消息记录并持久化
                                var sentMsg = new SocketMessage
                                {
                                    ClientEndPoint = clientEndPoint,
                                    Direction = SocketMessageDirection.Sent,
                                    Content = ex.Message,
                                    MessageTime = DateTime.Now
                                };
                                MessageManager.AddMessage(sentMsg);

                                byte[] respBytes = Encoding.UTF8.GetBytes(ex.Message);
                                stream.Write(respBytes, 0, respBytes.Length);
                            }
                            break;
                        default:
                            // 默认情况下也持久化消息
                            MessageManager.AddMessage(receivedMsg);
                            break;
                    }

                }
            }
            catch (IOException ex) when (IsClientDisconnect(ex))
            {
                log.Info("Socket client disconnected: " + clientEndPoint + ". " + ex.Message);
            }
            catch (SocketException ex) when (IsClientDisconnect(ex))
            {
                log.Info("Socket client disconnected: " + clientEndPoint + ". " + ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                log.Info("Socket client disposed: " + clientEndPoint + ". " + ex.Message);
            }
            catch (Exception ex)
            {
                log.Error("Client handling error: " + ex);
                client?.Close();
            }
            finally
            {
                _serverLifecycle.ReleaseClient(connection);
            }
        }

        private void CloseClient(SocketServerClient connection)
        {
            TcpClient client = connection.Client;
            try
            {
                if (client.Connected)
                    client.Client.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                log.Debug("Socket client shutdown skipped.", ex);
            }

            DisposeClient(client);
            if (Volatile.Read(ref _shutdownStarted) == 0)
                RemoveClient(client);
        }

        private static string GetClientEndPoint(TcpClient client)
        {
            try
            {
                return client.Client?.RemoteEndPoint?.ToString()
                    ?? client.Client?.LocalEndPoint?.ToString()
                    ?? $"Client:{client.GetHashCode():X8}";
            }
            catch (Exception ex)
            {
                log.Warn("Unable to get socket client endpoint.", ex);
                return $"Client:{client.GetHashCode():X8}";
            }
        }

        private void RemoveClient(TcpClient client)
        {
            try
            {
                PostOnUiThread(() => TcpClients.Remove(client));
            }
            catch (Exception ex)
            {
                log.Warn("Error removing socket client.", ex);
            }
        }

        private static void DisposeClient(TcpClient client)
        {
            try
            {
                client.Close();
                client.Dispose();
            }
            catch (Exception ex)
            {
                log.Warn("Error disposing socket client.", ex);
            }
        }

        private static bool IsClientDisconnect(Exception ex)
        {
            if (ex is SocketException socketException)
            {
                return IsClientDisconnect(socketException.SocketErrorCode);
            }

            return ex.InnerException is SocketException innerSocketException
                && IsClientDisconnect(innerSocketException.SocketErrorCode);
        }

        private static bool IsClientDisconnect(SocketError error)
        {
            return error == SocketError.ConnectionReset
                || error == SocketError.ConnectionAborted
                || error == SocketError.Shutdown
                || error == SocketError.OperationAborted;
        }
    }
}
