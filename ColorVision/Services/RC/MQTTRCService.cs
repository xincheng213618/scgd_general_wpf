#pragma warning disable CS8603
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.Core;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Msg;
using ColorVision.Services.Terminal;
using ColorVision.Services.Type;
using ColorVision.Settings;
using log4net;
using MQTTMessageLib;
using MQTTMessageLib.FileServer;
using MQTTMessageLib.RC;
using MQTTMessageLib.Util;
using MQTTnet.Client;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace ColorVision.Services.RC
{
    public class RCServiceStatusChangedEvent
    {
        public RCServiceStatusChangedEvent(ServiceNodeStatus status)
        {
            NodeStatus = status;
        }

        public ServiceNodeStatus NodeStatus { get;set; }
    }

    public delegate void RCServiceStatusChangedHandler(object sender, RCServiceStatusChangedEvent args);

    /// <summary>
    /// 注册服务
    /// </summary>
    public class MQTTRCService : MQTTServiceBase
    {
        private static readonly log4net.ILog logger = LogManager.GetLogger(typeof(MQTTRCService));
        private static MQTTRCService _instance;
        private static readonly object _locker = new();
        public static MQTTRCService GetInstance() { lock (_locker) { return _instance ??= new MQTTRCService(); } }


        private string NodeName;
        private string NodeType;
        private string AppId;
        private string AppSecret;
        private string DevcieName;
        private string RCNodeName;
        private string RCRegTopic;
        private string RCHeartbeatTopic;
        private string RCPublicTopic;
        private string RCAdminTopic;
        private string ArchivedTopic;
        private string SysConfigTopic;
        private string SysConfigRespTopic;
        private NodeToken? Token;
        private bool TryTestRegist;
        public ServiceNodeStatus RegStatus { get=> _RegStatus; set { _RegStatus = value; NotifyPropertyChanged();NotifyPropertyChanged(nameof(IsConnect)); } }
        private ServiceNodeStatus _RegStatus;


        public bool IsConnect { get => RegStatus == ServiceNodeStatus.Registered; }

        public event RCServiceStatusChangedHandler StatusChangedEventHandler;
        public MQTTRCService():base()
        {
            NodeType = "client";
            NodeName = MQTTRCServiceTypeConst.BuildNodeName(NodeType, null);
            DeviceCode = DevcieName = "dev." + NodeType + ".127.0.0.1";
            LoadCfg();
            RegStatus = ServiceNodeStatus.Unregistered;
            ServiceName = Guid.NewGuid().ToString();

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.MQTTConnectChanged += (object? sender, EventArgs e) =>
            {
                logger.InfoFormat("MQTTConnectChanged=>{0}", JsonConvert.SerializeObject(e));
                Task.Run(() =>
                {
                    Thread.Sleep(2000);
                    GetInstance().ReRegist();
                });
            };

            int heartbeatTime = 2 * 1000;
            System.Timers.Timer hbTimer = new System.Timers.Timer(heartbeatTime);
            hbTimer.Elapsed += (s, e) => KeepLive(heartbeatTime);
            hbTimer.Enabled = true;
            GC.KeepAlive(hbTimer);

        }

        public void LoadCfg()
        {
            RCServiceConfig RcServiceConfig = ConfigHandler.GetInstance().SoftwareConfig.RcServiceConfig;
            AppId = RcServiceConfig.AppId;
            AppSecret = RcServiceConfig.AppSecret;
            RCNodeName = RcServiceConfig.RCName;

            SubscribeTopic = MQTTRCServiceTypeConst.BuildNodeTopic(NodeName, RCNodeName);

            RCRegTopic = MQTTRCServiceTypeConst.BuildRegTopic(RCNodeName);
            RCHeartbeatTopic = MQTTRCServiceTypeConst.BuildHeartbeatTopic(RCNodeName);
            RCPublicTopic = MQTTRCServiceTypeConst.BuildPublicTopic(RCNodeName);
            RCAdminTopic = MQTTRCServiceTypeConst.BuildAdminTopic(RCNodeName);
            ArchivedTopic = MQTTRCServiceTypeConst.BuildArchivedTopic(RCNodeName);
            SysConfigTopic = MQTTRCServiceTypeConst.BuildSysConfigTopic(RCNodeName);
            SysConfigRespTopic = MQTTRCServiceTypeConst.BuildSysConfigRespTopic(RCNodeName);

            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.SubscribeCache(SysConfigRespTopic);
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MQTTNodeServiceHeader json = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(Msg);
                    if (json==null)
                        return Task.CompletedTask;

                    switch (json.EventName)
                    {
                        case MQTTNodeServiceEventEnum.Event_Regist:
                            MQTTNodeServiceRegistResponse resp = JsonConvert.DeserializeObject<MQTTNodeServiceRegistResponse>(Msg);
                            break;
                        case MQTTNodeServiceEventEnum.Event_Startup:
                            MQTTNodeServiceStartupRequest req = JsonConvert.DeserializeObject<MQTTNodeServiceStartupRequest>(Msg);
                            if (req != null)
                            {
                                RegStatus = ServiceNodeStatus.Registered;
                                if (!TryTestRegist)
                                {
                                    Token = req.Data.Token;
                                    StatusChangedEventHandler?.Invoke(this, new RCServiceStatusChangedEvent(ServiceNodeStatus.Registered));
                                    QueryServices();
                                }
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_QueryServices:
                            MQTTRCServicesQueryResponse respQurey = JsonConvert.DeserializeObject<MQTTRCServicesQueryResponse>(Msg);
                            if (respQurey != null)
                            {
                                Application.Current.Dispatcher.Invoke((Action)delegate {
                                    UpdateServices(respQurey.Data);
                                });
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_QueryServiceStatus:
                            MQTTRCServiceStatusQueryResponse respStatus = JsonConvert.DeserializeObject<MQTTRCServiceStatusQueryResponse>(Msg);
                            if (respStatus != null)
                            {
                                UpdateServiceStatus(respStatus.Data);
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_NotRegist:
                            StatusChangedEventHandler?.Invoke(this, new RCServiceStatusChangedEvent(ServiceNodeStatus.Unregistered));
                            Regist();
                            break;
                    }

                }
                catch
                {
                    return Task.CompletedTask;
                }
            }else if (arg.ApplicationMessage.Topic == SysConfigRespTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);

            }
            return Task.CompletedTask;
        }
        public static void UpdateServiceStatus(List<MQTTNodeServiceStatus> services)
        {
            foreach (var serviceKind in ServiceManager.GetInstance().TypeServices.ToList())
            {
                foreach (var baseObject in serviceKind.VisualChildren)
                {
                    if (baseObject is TerminalService serviceTerminal)
                    {
                        foreach (var svr in services)
                        {
                            bool updateTime = false;
                            DateTime lastLive = DateTime.Now;
                            if (svr.ServiceType.ToLower(CultureInfo.CurrentCulture) == serviceKind.ServiceTypes.ToString().ToLower(CultureInfo.CurrentCulture) && svr.ServiceCode == serviceTerminal.Code)
                            {
                                if (!string.IsNullOrEmpty(svr.LiveTime))
                                {
                                    if (DateTime.TryParse(svr.LiveTime, out lastLive))
                                    {
                                        serviceTerminal.Config.LastAliveTime = lastLive;
                                        updateTime = true;
                                    }
                                }
                                if (svr.OverTime > 0) serviceTerminal.Config.HeartbeatTime = svr.OverTime;
                                foreach (var devNew in svr.DeviceList)
                                {
                                    foreach (var dev in serviceTerminal.VisualChildren)
                                    {
                                        if (dev is DeviceService baseChannel && baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
                                        {
                                            if (devNew.Code == baseDeviceConfig.Code)
                                            {
                                                baseDeviceConfig.DeviceStatus = (DeviceStatusType)Enum.Parse(typeof(DeviceStatusType), devNew.Status);
                                                if (dev is DeviceCamera deviceCamera)
                                                {
                                                    deviceCamera.DeviceService.DeviceStatus = baseDeviceConfig.DeviceStatus;
                                                }
                                                if(updateTime) baseDeviceConfig.LastAliveTime = lastLive;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                    }

                }

            }
        }
        public static void UpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            DoUpdateServiceTokens(services);
            DoUpdateServices(services);
        }

        private static void DoUpdateServiceTokens(Dictionary<CVServiceType, List<MQTTNodeService>> services)
        {
            var tokens = ServiceManager.GetInstance().ServiceTokens;
            tokens.Clear();
            foreach (var itemService in services.Values)
            {
                foreach (var nodeService in itemService)
                {
                    FlowEngineLib.MQTTServiceInfo serviceInfo = new FlowEngineLib.MQTTServiceInfo()
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

        private static TypeService GetTypeService(List<TypeService> svrs, CVServiceType serviceTypes)
        {
            ServiceTypes cvSType = EnumTool.ParseEnum<ServiceTypes>(serviceTypes.ToString());
            foreach (var serviceKind in svrs)
            {
                if(cvSType == serviceKind.ServiceTypes) return serviceKind;
            }

            return null;
        }

        private static MQTTNodeService GetNodeService(List<MQTTNodeService> svrs,string codeName)
        {
            foreach (var nodeService in svrs)
            {
                if (codeName == nodeService.ServiceCode)
                {
                    return nodeService;
                }
            }
            return null;
        }

        public static void DoUpdateServices(Dictionary<CVServiceType, List<MQTTNodeService>> data)
        {
            List<TypeService> svrs = new List<TypeService>(ServiceManager.GetInstance().TypeServices);
            foreach (var itemService in data)
            {
                var serviceKind = GetTypeService(svrs, itemService.Key);
                if (serviceKind == null) { continue; }
                foreach (var baseObject in serviceKind.VisualChildren)
                {
                    if (baseObject is TerminalService serviceTerminal)
                    {
                        var nodeService = GetNodeService(itemService.Value, serviceTerminal.Code);
                        if (nodeService == null) { continue; }
                        serviceTerminal.Config.SendTopic = nodeService.UpChannel;
                        serviceTerminal.Config.SubscribeTopic = nodeService.DownChannel;
                        if (nodeService.OverTime > 0) serviceTerminal.Config.HeartbeatTime = nodeService.OverTime;
                        serviceTerminal.MQTTServiceTerminalBase.ServiceToken = nodeService.ServiceToken;

                        foreach (var deviceObj in serviceTerminal.VisualChildren)
                        {
                            if (deviceObj is DeviceService baseChannel && baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
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
        }
        public bool ReRegist()
        {
            LoadCfg();

            return Regist();
        }

        public bool Regist()
        {
            StatusChangedEventHandler?.Invoke(this, new RCServiceStatusChangedEvent(ServiceNodeStatus.Unregistered));
            Token = null;
            RegStatus = ServiceNodeStatus.Unregistered;
            MQTTNodeServiceRegist reg = new MQTTNodeServiceRegist(NodeName, AppId, AppSecret, SubscribeTopic, NodeType);
            PublishAsyncClient(RCRegTopic, JsonConvert.SerializeObject(reg));
            return true;
        }
        public async Task <bool> Connect()
        {
            Regist();
            for (int i = 0; i < 200; i++)
            {
                await Task.Delay(1);
                if (IsConnect)
                    return true;
            }
            return false;
        }

        public void QueryServices()
        {
            if (Token != null)
            {
                MQTTRCServicesQueryRequest reg = new MQTTRCServicesQueryRequest(NodeName, null, Token.AccessToken);
                PublishAsyncClient(RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void QueryServiceStatus()
        {
            if (Token != null)
            {
                MQTTRCServiceStatusQueryRequest reg = new MQTTRCServiceStatusQueryRequest(NodeName, null, Token.AccessToken);
                PublishAsyncClient(RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void PublishAsyncClient(string topic, string json)
        {
            Task.Run(() => MQTTControl.PublishAsyncClient(topic, json, false));
        }

        public void KeepLive(int heartbeatTime)
        {
            if (Token == null)
            {
                ReRegist();
                return;
            }

            List<DeviceHeartbeat> deviceStatues = new List<DeviceHeartbeat>();
            deviceStatues.Add(new DeviceHeartbeat(DevcieName, DeviceStatusType.Opened.ToString()));
            string serviceHeartbeat = JsonConvert.SerializeObject(new MQTTServiceHeartbeat(NodeName, "", "", NodeType, ServiceName, deviceStatues, Token.AccessToken, (int)(heartbeatTime * 1.5f)));

            PublishAsyncClient(RCHeartbeatTopic, serviceHeartbeat);

            QueryServiceStatus();
        }

        private bool DoRegist(RCServiceConfig cfg)
        {
            string RegTopic = MQTTRCServiceTypeConst.BuildRegTopic(cfg.RCName);
            string appId = cfg.AppId;
            string appSecret = cfg.AppSecret;
            RegStatus = ServiceNodeStatus.Unregistered;
            MQTTNodeServiceRegist reg = new MQTTNodeServiceRegist(NodeName, appId, appSecret, SubscribeTopic, NodeType);
            PublishAsyncClient(RegTopic, JsonConvert.SerializeObject(reg));
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(200);
                if (IsConnect) return true;
            }

            return false;
        }

        public void RestartServices()
        {
            if (Token != null)
            {
                MQTTRCServicesRestartRequest reg = new MQTTRCServicesRestartRequest(AppId, NodeName, string.Empty, Token.AccessToken);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void RestartServices(string nodeType)
        {
            if (Token != null)
            {
                MQTTRCServicesRestartRequest reg = new MQTTRCServicesRestartRequest(AppId, NodeName, nodeType, Token.AccessToken);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void RestartServices(string nodeType, string svrCode)
        {
            if (Token != null)
            {
                MQTTRCServicesRestartRequest reg = new MQTTRCServicesRestartRequest(AppId, NodeName, nodeType, Token.AccessToken, svrCode);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
        }
        public void RestartServices(string nodeType, string svrCode, string devCode)
        {
            if (Token != null)
            {
                MQTTRCServicesRestartRequest reg = new MQTTRCServicesRestartRequest(AppId, NodeName, nodeType, Token.AccessToken, svrCode, devCode);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }

            Task.Factory.StartNew(() => {
                Thread.Sleep(2000);
                QueryServices();
            });
        }
        public bool TryRegist(RCServiceConfig cfg)
        {
            bool isok = false;
            ServiceNodeStatus curStatus = RegStatus;
            TryTestRegist = true;
            for (int i = 0; i < 3; i++)
            {
                if (DoRegist(cfg))
                {
                    isok = true;
                    break;
                }
                Thread.Sleep(200);
            }
            TryTestRegist = false;
            RegStatus = curStatus;
            return isok;
        }

        public bool IsRegisted()
        {
            return RegStatus == ServiceNodeStatus.Registered;
        }

        public void Archived(string sn)
        {
            MQTTArchivedRequest request = new MQTTArchivedRequest(sn);
            PublishAsyncClient(ArchivedTopic, JsonConvert.SerializeObject(request));
        }

        public async Task<MsgRecord> UploadCalibrationFileAsync(string name, string fileName, int fileType, int timeout = 50000)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start(); 
            TaskCompletionSource<MsgRecord> tcs = new TaskCompletionSource<MsgRecord>();
            string md5 = Tool.CalculateMD5(fileName);
            MsgSend msg = new MsgSend
            {
                DeviceCode = "e58adc4ea51efbbf9",
                Token = Token?.AccessToken ?? string.Empty,
                MsgID = Guid.NewGuid().ToString("N"),
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                Params = new Dictionary<string, object> { { "Name", name }, { "FileName", fileName }, { "FileExtType", FileExtType.Calibration }, { "MD5", md5 } }
            };
            SendTopic = SysConfigTopic;
            SubscribeTopic = SysConfigRespTopic;
            MsgRecord msgRecord = PublishAsyncClient(msg);

            MsgRecordStateChangedHandler handler = (sender) =>
            {
                log.Info($"UploadCalibrationFileAsync:{fileName}  状态{sender}  Operation time: {stopwatch.ElapsedMilliseconds} ms");
                tcs.TrySetResult(msgRecord);
            };
            msgRecord.MsgRecordStateChanged += handler;
            var timeoutTask = Task.Delay(timeout);
            try
            {

                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    log.Info($"UploadCalibrationFileAsync:{fileName}  超时  Operation time: {stopwatch.ElapsedMilliseconds} ms");
                    tcs.TrySetException(new TimeoutException("The operation has timed out."));
                }
                return await tcs.Task; // 如果超时，这里将会抛出异常
            }
            catch (Exception ex)
            {
                log.Info($"UploadCalibrationFileAsync:{fileName}  异常 {ex.Message} Operation time: {stopwatch.ElapsedMilliseconds} ms");
                tcs.TrySetException(ex);
                return await tcs.Task; // 
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= handler;
            }
        }

    }
}
