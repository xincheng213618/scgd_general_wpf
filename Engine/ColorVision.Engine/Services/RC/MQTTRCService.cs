#pragma warning disable CS8602
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using FlowEngineLib;
using log4net;
using MQTTMessageLib;
using MQTTMessageLib.RC;
using MQTTnet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.RC
{
    public class MQTTNodeServiceHeader
    {
        public string Version { get; set; }

        public string MsgId { get; set; }

        public string NodeName { get; set; }

        public string ServiceType { get; set; }

        public string EventName { get; set; }

    }
    public class MQTTNodeServiceStartupRequest : MQTTNodeServiceHeader
    {
        public MQTTServiceNode Data { get; set; }
    }

    public class MQTTNodeServiceRegistResponse : MQTTNodeServiceHeader
    {
        public int Code { get; set; }

        public string Message { get; set; }

        public NodeToken Token { get; set; }

    }
    public class MQTTNodeServiceResponseHeader
    {
        public string Version { get; set; }

        public string MsgId { get; set; }

        public string NodeName { get; set; }

        public string EventName { get; set; }

        public int Code { get; set; }

        public string Message { get; set; }

    }

    public enum CVServiceType
    {
        Client = 0,
        Camera,
        PG,
        Spectrum,
        SMU,
        Sensor,
        FileServer,
        Algorithm,
        FilterWheel,
        Calibration,
        Motor,
        FocusRing,
        Flow,
        Archived,
        ThirdPartyAlgorithms,
        ThirdPartyAlgorithms32,
        PowerControl,
        LightingControl,
    }

    public class MQTTRCServicesQueryResponse : MQTTNodeServiceResponseHeader
    {
        public Dictionary<CVServiceType, List<MQTTNodeService>> Data { get; set; }
    }
    public class MQTTRCServiceStatusQueryResponse : MQTTNodeServiceResponseHeader
    {
        public List<MQTTNodeServiceStatus> Data { get; set; }
    }

    /// <summary>
    /// 注册服务
    /// </summary>
    public class MqttRCService : MQTTServiceBase,IDisposable
    {
        private static MqttRCService _instance;
        private static readonly object _locker = new();
        public static MqttRCService GetInstance() { lock (_locker) { return _instance ??= new MqttRCService(); } }

        public const string RCNodeName = "RC_local";
        public const string AppId = "app1";
        public const string AppSecret = "123456";

        private const string NodeType = "client";
        private const string ClientDeviceCode = "dev.client.127.0.0.1";
        private const string RCTopicPrefix = "MQTTRCService";
        private const string RCRegTopic = $"{RCTopicPrefix}/Regist/{RCNodeName}";
        private const string RCHeartbeatTopic = $"{RCTopicPrefix}/Heartbeat/{RCNodeName}";
        private const string RCPublicTopic = $"{RCTopicPrefix}/Public/{RCNodeName}";
        private const string RCAdminTopic = $"{RCTopicPrefix}/Admin/{RCNodeName}";
        private readonly string NodeName;
        
        // 使用锁保护Token访问
        private readonly object _tokenLock = new object();
        private NodeToken? Token;
        private DateTime TokenReceivedTime = DateTime.MinValue;
        private readonly object _reconnectLock = new object();
        private readonly PendingServiceUpdateBuffer _pendingServiceUpdates = new();
        private readonly Func<CancellationToken, Task> _waitForReconnectRegistration;
        private DateTime LastRegistTime = DateTime.MinValue;
        private static readonly TimeSpan AutoRegistInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan RegistrationResponseTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ReconnectRegistrationDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxServerSilence = TimeSpan.FromSeconds(10);
        private const int KeepAliveDueTimeMilliseconds = 1000;
        private const int KeepAlivePeriodMilliseconds = 2000;
        private CancellationTokenSource? _reconnectRegistrationCancellation;
        private long _transportGeneration;
        private int _disposed;

        public event EventHandler RCServiceConnectChanged;

        public bool IsConnect => Volatile.Read(ref _isConnect) != 0;
        private int _isConnect;

        public List<MQTTServiceInfo> ServiceTokens
        {
            get => Volatile.Read(ref _serviceTokens);
            set => Volatile.Write(ref _serviceTokens, value);
        }
        private List<MQTTServiceInfo> _serviceTokens = new();

        private readonly Timer? _keepAliveTimer;

        public MqttRCService() : this(MQTTControl.GetInstance(),
            static cancellation => Task.Delay(ReconnectRegistrationDelay, cancellation), startKeepAlive: true)
        {
        }

        internal MqttRCService(MQTTControl mqttControl, Func<CancellationToken, Task> waitForReconnectRegistration, bool startKeepAlive)
            : base(mqttControl)
        {
            _waitForReconnectRegistration = waitForReconnectRegistration;
            NodeName = $"{NodeType}.{Guid.NewGuid()}";
            DeviceCode = ClientDeviceCode;
            SubscribeTopic = $"{RCTopicPrefix}/Node/{NodeName}/{RCNodeName}";
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.SubscribeCache($"SysRes/config/Resp/{RCNodeName}");
            ServiceName = Guid.NewGuid().ToString();
            MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.MQTTConnectChanged += MQTTControl_MQTTConnectChanged;
            if (startKeepAlive)
                _keepAliveTimer = new Timer(_ => KeepLive(), null, KeepAliveDueTimeMilliseconds, KeepAlivePeriodMilliseconds);
        }

        private void MQTTControl_MQTTConnectChanged(object? sender, EventArgs e)
        {
            CancellationTokenSource? cancellation = null;
            bool connectionChanged = false;
            long generation;
            lock (_reconnectLock)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    return;

                generation = ++_transportGeneration;
                CancelScheduledReRegistrationLocked();
                if (MQTTControl.IsConnect)
                    _reconnectRegistrationCancellation = cancellation = new();
                else
                {
                    _pendingServiceUpdates.Clear();
                    connectionChanged = ResetRegistrationState();
                }
            }

            if (connectionChanged)
                NotifyConnectionStateChanged();
            if (cancellation != null)
                _ = ReRegisterAfterConnectionAsync(generation, cancellation);
        }

        private async Task ReRegisterAfterConnectionAsync(long generation, CancellationTokenSource cancellation)
        {
            try
            {
                await _waitForReconnectRegistration(cancellation.Token);
                RegistCore(force: true, transportGeneration: generation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                lock (_reconnectLock)
                {
                    if (ReferenceEquals(_reconnectRegistrationCancellation, cancellation))
                        _reconnectRegistrationCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        private void CancelScheduledReRegistrationLocked()
        {
            // The worker releases ownership under the same lock before disposing its source.
            CancellationTokenSource? cancellation = _reconnectRegistrationCancellation;
            _reconnectRegistrationCancellation = null;
            cancellation?.Cancel();
        }

        private bool IsCurrentTransportLocked(long generation) =>
            generation == _transportGeneration && MQTTControl.IsConnect && Volatile.Read(ref _disposed) == 0;

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic != SubscribeTopic)
                return Task.CompletedTask;

            long transportGeneration;
            lock (_reconnectLock)
            {
                transportGeneration = _transportGeneration;
                if (!IsCurrentTransportLocked(transportGeneration))
                    return Task.CompletedTask;
                LastAliveTime = DateTime.Now;
            }

            string payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
            try
            {
                MQTTNodeServiceHeader header = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(payload);
                switch (header?.EventName)
                {
                    case MQTTNodeServiceEventEnum.Event_Regist:
                        QueryServices();
                        break;
                    case MQTTNodeServiceEventEnum.Event_Startup:
                        var settings = new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore,
                            MissingMemberHandling = MissingMemberHandling.Ignore,
                            Error = (sender, args) => args.ErrorContext.Handled = true
                        };
                        MQTTNodeServiceStartupRequest startup = JsonConvert.DeserializeObject<MQTTNodeServiceStartupRequest>(payload, settings);
                        if (startup?.Data?.Token is { } token && AcceptStartup(transportGeneration, token))
                            QueryServices();
                        break;
                    case MQTTNodeServiceEventEnum.Event_QueryServices:
                        MQTTRCServicesQueryResponse directory = JsonConvert.DeserializeObject<MQTTRCServicesQueryResponse>(payload);
                        if (directory?.Data is { } services)
                            Application.Current?.Dispatcher.BeginInvoke(() => ApplyReceivedServices(transportGeneration, services));
                        break;
                    case MQTTNodeServiceEventEnum.Event_QueryServiceStatus:
                        MQTTRCServiceStatusQueryResponse status = JsonConvert.DeserializeObject<MQTTRCServiceStatusQueryResponse>(payload);
                        if (status?.Data is { } statuses)
                            Application.Current?.Dispatcher.BeginInvoke(() => ApplyReceivedServiceStatus(transportGeneration, statuses));
                        break;
                    case MQTTNodeServiceEventEnum.Event_NotRegist:
                        RegistCore(force: true, transportGeneration: transportGeneration);
                        break;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            return Task.CompletedTask;
        }

        private bool AcceptStartup(long generation, NodeToken token)
        {
            bool connectionChanged;
            lock (_reconnectLock)
            {
                if (!IsCurrentTransportLocked(generation))
                    return false;

                SetToken(token);
                connectionChanged = Interlocked.Exchange(ref _isConnect, 1) == 0;
            }

            if (connectionChanged)
                NotifyConnectionStateChanged();
            return true;
        }

        private void ApplyReceivedServices(long generation, Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            List<MQTTServiceInfo> tokens = CreateServiceTokenSnapshot(services);
            ServiceManager? manager = ServiceManager.Current;
            lock (_reconnectLock)
            {
                if (!IsCurrentTransportLocked(generation))
                    return;

                ServiceTokens = tokens;
                if (manager == null)
                    _pendingServiceUpdates.StoreServices(services);
            }

            if (manager != null)
                ApplyServices(manager, services);
            ServiceTokensUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyReceivedServiceStatus(long generation, List<MQTTNodeServiceStatus> statuses)
        {
            ServiceManager? manager = ServiceManager.Current;
            lock (_reconnectLock)
            {
                if (!IsCurrentTransportLocked(generation))
                    return;

                if (manager == null)
                    _pendingServiceUpdates.StoreStatuses(statuses);
            }

            if (manager != null)
                ApplyServiceStatus(manager, statuses);
        }
        public static void UpdateServiceStatus(List<MQTTNodeServiceStatus> services)
        {
            MqttRCService instance = GetInstance();
            ServiceManager? serviceManager = ServiceManager.Current;
            if (serviceManager == null)
            {
                instance._pendingServiceUpdates.StoreStatuses(services);
                return;
            }

            ApplyServiceStatus(serviceManager, services);
        }

        internal static void ApplyServiceStatus(ServiceManager serviceManager, List<MQTTNodeServiceStatus> services)
        {
            foreach (var serviceTerminal in serviceManager.TerminalServices)
            {
                var remoteDevices = services.FirstOrDefault(service => service?.ServiceCode == serviceTerminal.Code)?.DeviceList;
                if (remoteDevices == null) continue;
                foreach (var device in serviceTerminal.VisualChildren.OfType<DeviceService>())
                {
                    if (device.GetConfig() is not DeviceServiceConfig config) continue;
                    string? status = remoteDevices.FirstOrDefault(remote => remote?.Code == config.Code)?.Status;
                    if (Enum.TryParse(status, out DeviceStatusType parsed) && Enum.IsDefined(parsed)
                        && device.GetMQTTService() is { } mqttService)
                    {
                        mqttService.DeviceStatus = parsed;
                        device.RefreshLicenseFailure(mqttService, parsed);
                    }
                }
            }
        }
        public event EventHandler ServiceTokensUpdated;
        public void UpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            ServiceTokens = CreateServiceTokenSnapshot(services);
            DoUpdateServices(services);
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                ServiceTokensUpdated?.Invoke(this, EventArgs.Empty);
            }));
        }

        public static void DoUpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            MqttRCService instance = GetInstance();
            ServiceManager? serviceManager = ServiceManager.Current;
            if (serviceManager == null)
            {
                instance._pendingServiceUpdates.StoreServices(services);
            }
            else
            {
                ApplyServices(serviceManager, services);
            }
        }
        internal static List<MQTTServiceInfo> CreateServiceTokenSnapshot(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            List<MQTTServiceInfo> tokens = new();
            foreach (var itemService in services.Values)
            {
                if (itemService == null) continue;
                foreach (var nodeService in itemService)
                {
                    if (string.IsNullOrEmpty(nodeService?.ServiceCode)) continue;
                    MQTTServiceInfo serviceInfo = new()
                    {
                        ServiceType = nodeService.ServiceType,
                        ServiceCode = nodeService.ServiceCode,
                        PublishTopic = nodeService.UpChannel,
                        SubscribeTopic = nodeService.DownChannel,
                        Token = nodeService.ServiceToken,
                    };
                    if (nodeService.Devices != null)
                    {
                        foreach (var dev in nodeService.Devices)
                        {
                            if (!string.IsNullOrEmpty(dev.Value?.Code))
                                serviceInfo.AddDevice(dev.Key, dev.Value.Code);
                        }
                    }
                    tokens.Add(serviceInfo);
                }
            }
            return tokens;
        }

        internal void ApplyPendingServiceUpdates(ServiceManager serviceManager)
        {
            ArgumentNullException.ThrowIfNull(serviceManager);
            PendingServiceUpdates updates = _pendingServiceUpdates.Take();

            if (updates.Services != null)
            {
                ApplyServices(serviceManager, updates.Services);
            }
            if (updates.Statuses != null)
            {
                ApplyServiceStatus(serviceManager, updates.Statuses);
            }
        }

        internal static void ApplyServices(ServiceManager serviceManager, Dictionary<CVServiceType, List<MQTTNodeService>> data)
        {
            foreach (var itemService in data)
            {
                if (itemService.Value == null || !Enum.TryParse(Enum.GetName(itemService.Key), out ServiceTypes cvSType)) continue;
                var serviceKind = serviceManager.TypeServices.FirstOrDefault(serviceKind => cvSType == serviceKind.ServiceTypes);
                if (serviceKind == null) { continue; }
                foreach (var serviceTerminal in serviceKind.VisualChildren.Cast<TerminalService>())
                {
                    var nodeService = itemService.Value.FirstOrDefault(nodeService => nodeService?.ServiceCode == serviceTerminal.Code);
                    if (nodeService == null) { continue; }
                    serviceTerminal.Config.SendTopic = nodeService.UpChannel;
                    serviceTerminal.Config.SubscribeTopic = nodeService.DownChannel;
                    serviceTerminal.MQTTServiceTerminalBase.ServiceToken = nodeService.ServiceToken;

                    foreach (var baseChannel in serviceTerminal.VisualChildren.Cast<DeviceService>())
                    {
                        if ( baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
                        {
                            baseDeviceConfig.ServiceToken = nodeService.ServiceToken;
                            baseDeviceConfig.SendTopic = nodeService.UpChannel;
                            baseDeviceConfig.SubscribeTopic = nodeService.DownChannel;
                            baseChannel.GetMQTTService()?.SubscribeCache();
                        }
                    }
                }
            }
        }
        private void SetToken(NodeToken? token)
        {
            lock (_tokenLock)
            {
                Token = token;
                TokenReceivedTime = token == null ? DateTime.MinValue : DateTime.Now;
            }
        }

        private NodeToken? GetToken(out DateTime tokenReceivedTime)
        {
            lock (_tokenLock)
            {
                tokenReceivedTime = TokenReceivedTime;
                return Token;
            }
        }

        private bool TryGetUsableToken(out NodeToken? token)
        {
            token = GetToken(out DateTime tokenReceivedTime);
            if (token == null)
                return false;

            if (IsTokenExpiredOrExpiring(token, tokenReceivedTime))
            {
                SetDisconnectedState();
                RequestRegist();
                token = null;
                return false;
            }

            return true;
        }

        private static bool IsTokenExpiredOrExpiring(NodeToken token, DateTime tokenReceivedTime)
        {
            if (token.Expires <= 0 || tokenReceivedTime == DateTime.MinValue)
                return false;

            int refreshAheadSeconds = Math.Min(60, Math.Max(1, token.Expires / 10));
            DateTime refreshAt = tokenReceivedTime.AddSeconds(token.Expires).Subtract(TimeSpan.FromSeconds(refreshAheadSeconds));
            return DateTime.Now >= refreshAt;
        }

        private bool RequestRegist()
        {
            return RegistCore(false);
        }

        public bool Regist()
        {
            return RegistCore(true);
        }

        private bool RegistCore(bool force, long? transportGeneration = null)
        {
            bool connectionChanged;
            lock (_reconnectLock)
            {
                if (Volatile.Read(ref _disposed) != 0 ||
                    (transportGeneration is { } generation && !IsCurrentTransportLocked(generation)))
                    return false;

                DateTime now = DateTime.Now;
                if (!force && now - LastRegistTime < AutoRegistInterval)
                    return false;

                LastRegistTime = now;
                connectionChanged = ResetRegistrationState();
            }

            if (connectionChanged)
                NotifyConnectionStateChanged();

            MQTTNodeServiceRegist reg = new(NodeName, AppId, AppSecret, SubscribeTopic, NodeType);
            PublishAsyncClient(RCRegTopic, JsonConvert.SerializeObject(reg));
            return true;
        }

        private void SetDisconnectedState()
        {
            bool connectionChanged;
            lock (_reconnectLock)
                connectionChanged = ResetRegistrationState();
            if (connectionChanged)
                NotifyConnectionStateChanged();
        }

        private bool ResetRegistrationState()
        {
            SetToken(null);
            ServiceTokens = new();
            return Interlocked.Exchange(ref _isConnect, 0) != 0;
        }

        private void NotifyConnectionStateChanged()
        {
            void NotifyChanged()
            {
                OnPropertyChanged(nameof(IsConnect));
                RCServiceConnectChanged?.Invoke(this, EventArgs.Empty);
            }

            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(NotifyChanged);
            else
                NotifyChanged();
        }

        public async Task<bool> Connect()
        {
            Regist();
            if (IsConnect)
                return true;

            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnConnectionChanged(object? sender, EventArgs e)
            {
                if (IsConnect)
                    completion.TrySetResult(true);
            }

            RCServiceConnectChanged += OnConnectionChanged;
            try
            {
                return IsConnect || await completion.Task.WaitAsync(RegistrationResponseTimeout);
            }
            catch (TimeoutException)
            {
                return false;
            }
            finally
            {
                RCServiceConnectChanged -= OnConnectionChanged;
            }
        }

        public void QueryServices()
        {
            if (TryGetUsableToken(out NodeToken? token) && token != null)
            {
                MQTTRCServicesQueryRequest reg = new(NodeName, null, token.AccessToken);
                PublishAsyncClient(RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void QueryServiceStatus()
        {
            if (TryGetUsableToken(out NodeToken? token) && token != null)
            {
                MQTTRCServiceStatusQueryRequest reg = new(NodeName, null, token.AccessToken);
                PublishAsyncClient(RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }
        private DateTime LastAliveTime = DateTime.MinValue;
        public void KeepLive()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            TimeSpan sp = DateTime.Now - LastAliveTime;
            if (sp > MaxServerSilence)
            {
                RequestRegist();
                return;
            }
            if (!IsConnect)
                return;

            if (!TryGetUsableToken(out NodeToken? token) || token == null)
                return;

            List<DeviceHeartbeat> deviceStatues = new();
            deviceStatues.Add(new DeviceHeartbeat(ClientDeviceCode, DeviceStatusType.Opened.ToString()));
            string serviceHeartbeat = JsonConvert.SerializeObject(new MQTTServiceHeartbeat(NodeName, "", "", NodeType, ServiceName, deviceStatues, token.AccessToken, KeepAlivePeriodMilliseconds * 3 / 2));
            PublishAsyncClient(RCHeartbeatTopic, serviceHeartbeat);
            QueryServiceStatus();
        }

        public void RestartServices(string? nodeType = null, string? svrCode =null)
        {
            log.Info($"RestartServices {nodeType} {svrCode}");

            if (TryGetUsableToken(out NodeToken? token) && token != null)
            {
                nodeType ??= string.Empty;
                MQTTRCServicesRestartRequest reg = svrCode == null ? new(AppId, NodeName, nodeType, token.AccessToken) : new(AppId, NodeName, nodeType, token.AccessToken, svrCode);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
        }
        
        public void RestartServices(string nodeType, string svrCode, string devCode)
        {
            _ = TryRestartServices(nodeType, svrCode, devCode);
        }

        internal bool TryRestartServices(string nodeType, string svrCode, string devCode, Func<bool>? ownsRequest = null) =>
            TryReloadServiceDevices(nodeType, svrCode, [devCode], ownsRequest);

        internal bool TryReloadServiceDevices(string nodeType, string serviceCode, IReadOnlyList<string> deviceCodes,
            Func<bool>? ownsRequest = null)
        {
            // The RC protocol falls back to a broader restart if the target is incomplete.
            string[] targets = deviceCodes.Distinct(StringComparer.Ordinal).ToArray();
            if (string.IsNullOrWhiteSpace(nodeType) || string.IsNullOrWhiteSpace(serviceCode)
                || targets.Length == 0 || targets.Any(string.IsNullOrWhiteSpace))
            {
                log.Warn("RC device reload rejected because its target is incomplete.");
                return false;
            }

            log.Info($"ReloadServiceDevices {nodeType} {serviceCode} Count:{targets.Length}");

            MQTTControl transport;
            MqttClientOptions? options;
            long generation;
            lock (_reconnectLock)
            {
                transport = MQTTControl;
                options = transport.MQTTClient.Options;
                generation = _transportGeneration;
            }

            if (options == null || !IsConnect || !TryGetUsableToken(out NodeToken? token) || token == null || !CanDispatch())
            {
                log.Warn($"RC device reload rejected before dispatch. Service:{serviceCode}");
                return false;
            }

            _ = PublishReloadsAsync(options);
            return true;

            bool CanDispatch()
            {
                if (ownsRequest?.Invoke() == false) return false;
                lock (_reconnectLock)
                {
                    if (!ReferenceEquals(transport, MQTTControl) || !ReferenceEquals(options, transport.MQTTClient.Options)
                        || !IsCurrentTransportLocked(generation) || !IsConnect) return false;

                    NodeToken? current = GetToken(out DateTime receivedAt);
                    return current != null && string.Equals(current.AccessToken, token.AccessToken, StringComparison.Ordinal)
                        && !IsTokenExpiredOrExpiring(current, receivedAt);
                }
            }

            async Task PublishReloadsAsync(MqttClientOptions connectionOptions)
            {
                try
                {
                    bool sent = false;
                    foreach (string deviceCode in targets)
                    {
                        MQTTRCServicesRestartRequest request = new(AppId, NodeName, nodeType, token.AccessToken, serviceCode, deviceCode);
                        if (!await transport.TryPublishOnConnectionAsync(connectionOptions, RCAdminTopic,
                            JsonConvert.SerializeObject(request), CanDispatch).ConfigureAwait(false))
                        {
                            log.Warn($"RC device reload source changed before dispatch. Service:{serviceCode}, Device:{deviceCode}");
                            break;
                        }
                        sent = true;
                    }

                    if (!sent) return;
                    await Task.Delay(2000).ConfigureAwait(false);
                    MQTTRCServicesQueryRequest query = new(NodeName, null, token.AccessToken);
                    await transport.TryPublishOnConnectionAsync(connectionOptions, RCPublicTopic,
                        JsonConvert.SerializeObject(query), CanDispatch).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log.Warn($"RC device reload or directory refresh failed. Service:{serviceCode}", ex);
                }
            }
        }

        public Task PublishAsyncClient(string topic, string json) => MQTTControl.PublishAsyncClient(topic, json, false);

        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _keepAliveTimer?.Dispose();
            lock (_reconnectLock)
            {
                CancelScheduledReRegistrationLocked();
                _pendingServiceUpdates.Clear();
            }
            MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.MQTTConnectChanged -= MQTTControl_MQTTConnectChanged;
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
