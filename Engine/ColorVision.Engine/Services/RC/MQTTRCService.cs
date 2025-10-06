using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using CVCommCore;
using FlowEngineLib;
using log4net;
using MQTTMessageLib;
using MQTTMessageLib.RC;
using MQTTMessageLib.Util;
using MQTTnet.Client;
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
        
        // 使用volatile确保可见性
        private volatile bool TryTestRegist;

        public bool IsConnect { get => _IsConnect; set { if (_IsConnect == value) return;  _IsConnect = value; if (value) initialized = false; OnPropertyChanged(); } }
        private bool _IsConnect ;

        public List<MQTTServiceInfo> ServiceTokens { get; set; } = new List<MQTTServiceInfo>();

        private bool initialized;
        public event EventHandler ServiceTokensInitialized;
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
            MQTTControl.MQTTConnectChanged += (s,e) =>
            {
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    GetInstance().ReRegist();
                });
            };
            int heartbeatTime = 2 * 1000;

            Timer = new Timer(e=> KeepLive(),null,1000,2000);
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
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                LastAliveTime = DateTime.Now; 

                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MQTTNodeServiceHeader json = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(Msg);
                    if (json==null)
                        return Task.CompletedTask;

                    switch (json.EventName)
                    {
                        case MQTTNodeServiceEventEnum.Event_Regist:
                            //MQTTNodeServiceRegistResponse resp = JsonConvert.DeserializeObject<MQTTNodeServiceRegistResponse>(Msg);
                            break;
                        case MQTTNodeServiceEventEnum.Event_Startup:
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(Msg))
                                {
                                    var settings = new JsonSerializerSettings
                                    {
                                        NullValueHandling = NullValueHandling.Ignore,
                                        MissingMemberHandling = MissingMemberHandling.Ignore,
                                        Error = (sender, args) => args.ErrorContext.Handled = true
                                    };
                                    MQTTNodeServiceStartupRequest req = JsonConvert.DeserializeObject<MQTTNodeServiceStartupRequest>(Msg, settings);
                                    if (req?.Data?.Token != null)
                                    {
                                        // 线程安全地更新Token
                                        lock (_tokenLock)
                                        {
                                            Token = req.Data.Token;
                                        }

                                        // 在UI线程上更新IsConnect属性(如果需要触发UI更新)
                                        Application.Current?.Dispatcher.BeginInvoke(() =>
                                        {
                                            IsConnect = true;
                                        });

                                        // 读取TryTestRegist是线程安全的(volatile)
                                        if (!TryTestRegist)
                                        {
                                            QueryServices();
                                        }
                                    }
                                }
                            }
                            catch (JsonException ex)
                            {
                                log.Error($"JSON deserialization failed: {ex.Message}");
                                return Task.CompletedTask;
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                                return Task.CompletedTask;
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
            foreach (var serviceTerminal in ServiceManager.GetInstance().TerminalServices)
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
                if (MQTTNodeServiceStatus.OverTime > 0) serviceTerminal.Config.HeartbeatTime = MQTTNodeServiceStatus.OverTime;

                foreach (var baseChannel in serviceTerminal.VisualChildren.Cast<DeviceService>())
                {
                    if (baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
                    {
                        var devNew = MQTTNodeServiceStatus.DeviceList.FirstOrDefault(x => x.Code == baseDeviceConfig.Code);
                        if (devNew == null) continue;
                        MQTTServiceBase mQTTServiceBase = baseChannel.GetMQTTService();
                        if (mQTTServiceBase == null) continue;
                        mQTTServiceBase.DeviceStatus = (DeviceStatusType)Enum.Parse(typeof(DeviceStatusType), devNew.Status);
                        mQTTServiceBase.LastAliveTime = lastLive;
                    }
                }

            }

        }
        public void UpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            DoUpdateServiceTokens(services);
            DoUpdateServices(services);
        }
        private void DoUpdateServiceTokens(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            log.Debug("Refresh Token");
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

            if (!initialized)
            {
                ServiceTokensInitialized?.Invoke(tokens, new EventArgs());
            }
            initialized = true;
        }

        public static void DoUpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> data)
        {
            foreach (var itemService in data)
            {
                ServiceTypes cvSType = EnumTool.ParseEnum<ServiceTypes>(itemService.Key.ToString());
                var serviceKind  = ServiceManager.GetInstance().TypeServices.FirstOrDefault(serviceKind => cvSType == serviceKind.ServiceTypes);
                if (serviceKind == null) { continue; }
                foreach (var serviceTerminal in serviceKind.VisualChildren.Cast<TerminalService>())
                {
                    var nodeService = itemService.Value.FirstOrDefault(nodeService => nodeService.ServiceCode == serviceTerminal.Code);
                    if (nodeService == null) { continue; }
                    serviceTerminal.Config.SendTopic = nodeService.UpChannel;
                    serviceTerminal.Config.SubscribeTopic = nodeService.DownChannel;
                    if (nodeService.OverTime > 0) serviceTerminal.Config.HeartbeatTime = nodeService.OverTime;
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

        public bool Regist()
        {
            lock (_tokenLock)
            {
                Token = null;
            }
            
            // 在UI线程更新IsConnect
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                IsConnect = false;
            });
            
            ServiceTokens.Clear();

            MQTTNodeServiceRegist reg = new(NodeName, AppId, AppSecret, SubscribeTopic, NodeType);
            PublishAsyncClient(RCRegTopic, JsonConvert.SerializeObject(reg));
            return true;
        }
        public async Task <bool> Connect()
        {
            Regist();
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(10);
                if (IsConnect)
                    return true;
            }
            return false;
        }

        public void QueryServices()
        {
            NodeToken? token;
            lock (_tokenLock)
            {
                token = Token;
            }
            
            if (token != null)
            {
                MQTTRCServicesQueryRequest reg = new(NodeName, null, token.AccessToken);
                PublishAsyncClient(RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void QueryServiceStatus()
        {
            NodeToken? token;
            lock (_tokenLock)
            {
                token = Token;
            }
            
            if (token != null)
            {
                MQTTRCServiceStatusQueryRequest reg = new(NodeName, null, token.AccessToken);
                PublishAsyncClient(RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void KeepLive()
        {
            TimeSpan sp = DateTime.Now - LastAliveTime;
            if (sp.TotalMilliseconds > 10000)
            {
                Regist();
                return;
            }
            if (!IsConnect)
                return;

            NodeToken? token;
            lock (_tokenLock)
            {
                token = Token;
            }
            
            if (token == null)
                return;

            List<DeviceHeartbeat> deviceStatues = new();
            deviceStatues.Add(new DeviceHeartbeat(DevcieName, DeviceStatusType.Opened.ToString()));
            string serviceHeartbeat = JsonConvert.SerializeObject(new MQTTServiceHeartbeat(NodeName, "", "", NodeType, ServiceName, deviceStatues, token.AccessToken, (int)(2000 * 1.5f)));
            PublishAsyncClient(RCHeartbeatTopic, serviceHeartbeat);
            QueryServiceStatus();
        }

        public void RestartServices(string? nodeType = null, string? svrCode =null)
        {
            log.Info($"RestartServices {nodeType} {svrCode}");
            
            NodeToken? token;
            lock (_tokenLock)
            {
                token = Token;
            }
            
            if (token != null)
            {
                nodeType ??= string.Empty;
                MQTTRCServicesRestartRequest reg = svrCode == null ? new(AppId, NodeName, nodeType, token.AccessToken) : new(AppId, NodeName, nodeType, token.AccessToken, svrCode);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
        }
        
        public void RestartServices(string nodeType, string svrCode, string devCode)
        {
            log.Info($"RestartServices {nodeType} {svrCode} {devCode}");
            
            NodeToken? token;
            lock (_tokenLock)
            {
                token = Token;
            }
            
            if (token != null)
            {
                MQTTRCServicesRestartRequest reg = new(AppId, NodeName, nodeType, token.AccessToken, svrCode, devCode);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
            Task.Factory.StartNew(async () => {
                await Task.Delay(2000);
                QueryServices();
            });
        }

        public async Task<bool> TryRegist(RCServiceConfig cfg)
        {
            TryTestRegist = true;
            string RegTopic = MQTTRCServiceTypeConst.BuildRegTopic(cfg.RCName);
            string appId = cfg.AppId;
            string appSecret = cfg.AppSecret;
            
            // 在UI线程更新IsConnect
            await Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsConnect = false;
            });
            
            MQTTNodeServiceRegist reg = new(NodeName, appId, appSecret, SubscribeTopic, NodeType);
            await PublishAsyncClient(RegTopic, JsonConvert.SerializeObject(reg));
            
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(10);
                if (IsConnect)
                {
                    TryTestRegist = false;
                    return true;
                }
            }
            TryTestRegist = false;
            return false;
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
            Timer?.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
