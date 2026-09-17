#pragma warning disable CS8602
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using FlowEngineLib;
using log4net;
using MQTTMessageLib;
using MQTTMessageLib.RC;
using MQTTMessageLib.Util;
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
        private static readonly ILog logger = LogManager.GetLogger(typeof(MqttRCService));
        private static MqttRCService _instance;
        private static readonly object _locker = new();
        public static MqttRCService GetInstance() { lock (_locker) { return _instance ??= new MqttRCService(); } }

        private string NodeName;
        private string NodeType;
        private static string AppId => RCSetting.Instance.Config.AppId;
        private static string AppSecret => RCSetting.Instance.Config.AppSecret;
        private static string RCNodeName => RCSetting.Instance.Config.RCName;
        private string DevcieName;
        private string RCRegTopic;
        private string RCHeartbeatTopic;
        private string RCPublicTopic;
        private string RCAdminTopic;
        private string ArchivedTopic;
        private string SysConfigTopic;
        private string SysConfigRespTopic;
        
        // 使用锁保护Token访问
        private readonly object _tokenLock = new object();
        private NodeToken? Token;
        private DateTime TokenReceivedTime = DateTime.MinValue;
        private readonly object _registLock = new object();
        private readonly object _reconnectLock = new object();
        private readonly PendingServiceUpdateBuffer _pendingServiceUpdates = new();
        private DateTime LastRegistTime = DateTime.MinValue;
        private static readonly TimeSpan AutoRegistInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan RegistrationResponseTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ReconnectRegistrationDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxServerSilence = TimeSpan.FromSeconds(10);
        private const int KeepAliveDueTimeMilliseconds = 1000;
        private const int KeepAlivePeriodMilliseconds = 2000;
        private CancellationTokenSource? _reconnectRegistrationCancellation;
        private int _disposed;

        public event EventHandler RCServiceConnectChanged;

        public bool IsConnect => Volatile.Read(ref _isConnect) != 0;
        private int _isConnect;

        public List<MQTTServiceInfo> ServiceTokens { get; set; } = new List<MQTTServiceInfo>();

        System.Threading.Timer Timer { get; set; }

        public MqttRCService()
        {
            NodeType = "client";
            NodeName = MQTTRCServiceTypeConst.BuildNodeName(NodeType, null);
            DeviceCode = DevcieName = "dev." + NodeType + ".127.0.0.1";
            LoadCfg();
            ServiceName = Guid.NewGuid().ToString();
            MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.MQTTConnectChanged += MQTTControl_MQTTConnectChanged;
            Timer = new Timer(e => KeepLive(), null, KeepAliveDueTimeMilliseconds, KeepAlivePeriodMilliseconds);
        }

        private void MQTTControl_MQTTConnectChanged(object? sender, EventArgs e)
        {
            if (!MQTTControl.IsConnect)
            {
                CancelScheduledReRegistration();
                SetDisconnectedState();
                return;
            }

            ScheduleReRegistration();
        }

        private void ScheduleReRegistration()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            CancellationTokenSource cancellation = new();
            CancellationTokenSource? previous;
            lock (_reconnectLock)
            {
                previous = _reconnectRegistrationCancellation;
                _reconnectRegistrationCancellation = cancellation;
            }

            previous?.Cancel();
            _ = ReRegisterAfterConnectionAsync(cancellation);
        }

        private async Task ReRegisterAfterConnectionAsync(CancellationTokenSource cancellation)
        {
            try
            {
                await Task.Delay(ReconnectRegistrationDelay, cancellation.Token);
                if (MQTTControl.IsConnect && Volatile.Read(ref _disposed) == 0)
                    ReRegist();
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

        private void CancelScheduledReRegistration()
        {
            CancellationTokenSource? cancellation;
            lock (_reconnectLock)
            {
                cancellation = _reconnectRegistrationCancellation;
                _reconnectRegistrationCancellation = null;
            }
            cancellation?.Cancel();
        }

        public void LoadCfg()
        {
            SubscribeTopic = MQTTRCServiceTypeConst.BuildNodeTopic(NodeName, RCNodeName);
            SysConfigRespTopic = MQTTRCServiceTypeConst.BuildSysConfigRespTopic(RCNodeName);
            RCRegTopic = MQTTRCServiceTypeConst.BuildRegTopic(RCNodeName);
            RCHeartbeatTopic = MQTTRCServiceTypeConst.BuildHeartbeatTopic(RCNodeName);
            RCPublicTopic = MQTTRCServiceTypeConst.BuildPublicTopic(RCNodeName);
            RCAdminTopic = MQTTRCServiceTypeConst.BuildAdminTopic(RCNodeName);
            ArchivedTopic = MQTTRCServiceTypeConst.BuildArchivedTopic(RCNodeName);
            SysConfigTopic = MQTTRCServiceTypeConst.BuildSysConfigTopic(RCNodeName);

            RCFileUpload.GetInstance().SendTopic = MQTTRCServiceTypeConst.BuildSysConfigTopic(RCNodeName); ;
            RCFileUpload.GetInstance().SubscribeTopic = MQTTRCServiceTypeConst.BuildSysConfigRespTopic(RCNodeName);

            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.SubscribeCache(SysConfigRespTopic);
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return Task.CompletedTask;

            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                LastAliveTime = DateTime.Now;
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
                try
                {
                    MQTTNodeServiceHeader json = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(Msg);
                    if (json==null)
                        return Task.CompletedTask;

                    switch (json.EventName)
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
                            MQTTNodeServiceStartupRequest req = JsonConvert.DeserializeObject<MQTTNodeServiceStartupRequest>(Msg, settings);
                            if (req?.Data?.Token != null)
                            {
                                SetToken(req.Data.Token);

                                SetConnectionState(true);

                                // 读取TryTestRegist是线程安全的(volatile)
                                QueryServices();
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_QueryServices:
                            MQTTRCServicesQueryResponse respQurey = JsonConvert.DeserializeObject<MQTTRCServicesQueryResponse>(Msg);
                            if (respQurey != null)
                            {
                                Application.Current?.Dispatcher.BeginInvoke(() => {
                                    UpdateServices(respQurey.Data);
                                });
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_QueryServiceStatus:
                            MQTTRCServiceStatusQueryResponse respStatus = JsonConvert.DeserializeObject<MQTTRCServiceStatusQueryResponse>(Msg);
                            if (respStatus != null)
                            {
                                // UpdateServiceStatus可能访问UI对象,建议在UI线程执行
                                Application.Current?.Dispatcher.BeginInvoke(() =>
                                {
                                    UpdateServiceStatus(respStatus.Data);
                                });
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_NotRegist:
                            Regist();
                            break;
                    }

                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
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

        private static void ApplyServiceStatus(ServiceManager serviceManager, List<MQTTNodeServiceStatus> services)
        {
            foreach (var serviceTerminal in serviceManager.TerminalServices)
            {
                var MQTTNodeServiceStatus = services.FirstOrDefault(x => x.ServiceCode == serviceTerminal.Code);
                if (MQTTNodeServiceStatus == null) continue;
                DateTime lastLive = DateTime.Now;
                if (!string.IsNullOrEmpty(MQTTNodeServiceStatus.LiveTime))
                {
                    if (DateTime.TryParse(MQTTNodeServiceStatus.LiveTime, out  lastLive))
                    {
                    }
                }
                foreach (var baseChannel in serviceTerminal.VisualChildren.Cast<DeviceService>())
                {
                    if (baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
                    {
                        var devNew = MQTTNodeServiceStatus.DeviceList.FirstOrDefault(x => x.Code == baseDeviceConfig.Code);
                        if (devNew == null) continue;
                        MQTTServiceBase mQTTServiceBase = baseChannel.GetMQTTService();
                        if (mQTTServiceBase == null) continue;
                        mQTTServiceBase.DeviceStatus = Enum.Parse<DeviceStatusType>(devNew.Status);
                    }
                }

            }

        }
        public event EventHandler ServiceTokensUpdated;
        public void UpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            DoUpdateServiceTokens(services);
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
        private void DoUpdateServiceTokens(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            logger.Debug("Refresh Token");
            var tokens = ServiceTokens;
            tokens.Clear();
            foreach (var itemService in services.Values)
            {
                foreach (var nodeService in itemService)
                {
                    MQTTServiceInfo serviceInfo = new()
                    {
                        ServiceType = nodeService.ServiceType,
                        ServiceCode = nodeService.ServiceCode,
                        PublishTopic = nodeService.UpChannel,
                        SubscribeTopic = nodeService.DownChannel,
                        Token = nodeService.ServiceToken,
                    };
                    foreach (var dev in nodeService.Devices)
                    {
                        serviceInfo.AddDevice(dev.Key, dev.Value.Code);
                    }
                    tokens.Add(serviceInfo);
                }
            }
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

        private static void ApplyServices(ServiceManager serviceManager, Dictionary<CVServiceType, List<MQTTNodeService>> data)
        {
            foreach (var itemService in data)
            {
                ServiceTypes cvSType = EnumTool.ParseEnum<ServiceTypes>(itemService.Key.ToString());
                var serviceKind = serviceManager.TypeServices.FirstOrDefault(serviceKind => cvSType == serviceKind.ServiceTypes);
                if (serviceKind == null) { continue; }
                foreach (var serviceTerminal in serviceKind.VisualChildren.Cast<TerminalService>())
                {
                    var nodeService = itemService.Value.FirstOrDefault(nodeService => nodeService.ServiceCode == serviceTerminal.Code);
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
        public bool ReRegist()
        {
            LoadCfg();
            return Regist();
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

        private bool RegistCore(bool force)
        {
            lock (_registLock)
            {
                DateTime now = DateTime.Now;
                if (!force && now - LastRegistTime < AutoRegistInterval)
                    return false;

                LastRegistTime = now;
            }

            SetDisconnectedState();

            MQTTNodeServiceRegist reg = new(NodeName, AppId, AppSecret, SubscribeTopic, NodeType);
            PublishAsyncClient(RCRegTopic, JsonConvert.SerializeObject(reg));
            return true;
        }

        private void SetDisconnectedState()
        {
            SetToken(null);
            SetConnectionState(false);
            ServiceTokens.Clear();
        }

        private void SetConnectionState(bool isConnected)
        {
            int value = isConnected ? 1 : 0;
            if (Interlocked.Exchange(ref _isConnect, value) == value)
                return;

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

        private Task<bool> WaitForConnectionAsync() => RCConnectionWaiter.WaitAsync(
            () => IsConnect,
            handler => RCServiceConnectChanged += handler,
            handler => RCServiceConnectChanged -= handler,
            RegistrationResponseTimeout);

        public async Task <bool> Connect()
        {
            Regist();
            return await WaitForConnectionAsync();
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
            deviceStatues.Add(new DeviceHeartbeat(DevcieName, DeviceStatusType.Opened.ToString()));
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

        internal bool TryRestartServices(string nodeType, string svrCode, string devCode)
        {
            log.Info($"RestartServices {nodeType} {svrCode} {devCode}");

            if (!IsConnect || !TryGetUsableToken(out NodeToken? token) || token == null)
                return false;

            MQTTRCServicesRestartRequest reg = new(AppId, NodeName, nodeType, token.AccessToken, svrCode, devCode);
            _ = PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            Task.Factory.StartNew(async () => {
                await Task.Delay(2000);
                QueryServices();
            });
            return true;
        }

        public async Task<bool> TryRegist(RCServiceConfig cfg)
        {
            string RegTopic = MQTTRCServiceTypeConst.BuildRegTopic(cfg.RCName);
            string appId = cfg.AppId;
            string appSecret = cfg.AppSecret;
            
            SetConnectionState(false);
            
            MQTTNodeServiceRegist reg = new(NodeName, appId, appSecret, SubscribeTopic, NodeType);
            await PublishAsyncClient(RegTopic, JsonConvert.SerializeObject(reg));
            return await WaitForConnectionAsync();
        }

        public void Archived(string sn)
        {
            MQTTArchivedRequest request = new MQTTArchivedRequest(sn);
            PublishAsyncClient(ArchivedTopic, JsonConvert.SerializeObject(request));
        }
        public void ArchivedAll()
        {
            MQTTArchivedRequest request = new MQTTArchivedRequest();
            request.EventName = "ArchivedAll";
            PublishAsyncClient(ArchivedTopic, JsonConvert.SerializeObject(request));
        }

        public Task PublishAsyncClient(string topic, string json) => MQTTControl.PublishAsyncClient(topic, json, false);

        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            Timer?.Dispose();
            CancelScheduledReRegistration();
            MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.MQTTConnectChanged -= MQTTControl_MQTTConnectChanged;
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
