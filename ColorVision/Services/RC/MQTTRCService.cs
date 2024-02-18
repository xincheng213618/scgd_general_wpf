#pragma warning disable CS8603
using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Camera;
using ColorVision.Settings;
using MQTTMessageLib;
using MQTTMessageLib.RC;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.RC
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
        private NodeToken? Token;
        private bool TryTestRegist;
        public ServiceNodeStatus RegStatus { get=> _RegStatus; set { _RegStatus = value; NotifyPropertyChanged();NotifyPropertyChanged(nameof(IsConnect)); } }
        private ServiceNodeStatus _RegStatus;


        public bool IsConnect { get => RegStatus == ServiceNodeStatus.Registered; }

        public event RCServiceStatusChangedHandler StatusChangedEventHandler;
        public MQTTRCService():base()
        {
            this.NodeType = "client";
            this.NodeName = MQTTRCServiceTypeConst.BuildNodeName(NodeType, null);
            this.DevcieName = "dev." + NodeType + ".127.0.0.1";
            LoadCfg();
            this.RegStatus = ServiceNodeStatus.Unregistered;
            ServiceName = Guid.NewGuid().ToString();
            SubscribeTopic = MQTTRCServiceTypeConst.BuildNodeTopic(NodeName);

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;


            int heartbeatTime = 2 * 1000;
            System.Timers.Timer hbTimer = new System.Timers.Timer(heartbeatTime);
            hbTimer.Elapsed += (s, e) => KeepLive(heartbeatTime);
            hbTimer.Enabled = true;
            GC.KeepAlive(hbTimer);

        }

        public void LoadCfg()
        {
            RCServiceConfig RcServiceConfig = ConfigHandler.GetInstance().SoftwareConfig.RcServiceConfig;
            this.AppId = RcServiceConfig.AppId;
            this.AppSecret = RcServiceConfig.AppSecret;
            this.RCNodeName = RcServiceConfig.RCName;

            this.RCRegTopic = MQTTRCServiceTypeConst.BuildRegTopic(RCNodeName);
            this.RCHeartbeatTopic = MQTTRCServiceTypeConst.BuildHeartbeatTopic(RCNodeName);
            this.RCPublicTopic = MQTTRCServiceTypeConst.BuildPublicTopic(RCNodeName);
            this.RCAdminTopic = MQTTRCServiceTypeConst.BuildAdminTopic(RCNodeName);
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
                                this.RegStatus = ServiceNodeStatus.Registered;
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
                                    UpdateServiceStatus(respQurey.Data);
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
            }
            return Task.CompletedTask;
        }


        public static void UpdateServiceStatus(List<MQTTNodeServiceStatus> data)
        {
            foreach (var serviceKind in ServiceManager.GetInstance().TypeServices.ToList())
            {
                foreach (var baseObject in serviceKind.VisualChildren)
                {
                    if (baseObject is TerminalService serviceTerminal)
                    {
                        foreach (var ss in data)
                        {
                            if (ss.ServiceType.ToLower(CultureInfo.CurrentCulture) == serviceKind.ServiceTypes.ToString().ToLower(CultureInfo.CurrentCulture) && ss.ServiceName == serviceTerminal.Code)
                            {
                                serviceTerminal.Config.IsAlive = true;
                                serviceTerminal.Config.LastAliveTime = DateTime.Now;
                                foreach (var devNew in ss.DeviceList)
                                {
                                    foreach (var dev in serviceTerminal.VisualChildren)
                                    {
                                        if (dev is DeviceService baseChannel && baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
                                        {
                                            if (devNew.Code == baseDeviceConfig.Code)
                                            {
                                                baseDeviceConfig.IsAlive = true;
                                                baseDeviceConfig.LastAliveTime = DateTime.Parse(ss.LiveTime);
                                                baseDeviceConfig.DeviceStatus = (DeviceStatusType)Enum.Parse(typeof(DeviceStatusType), devNew.Status);
                                                if (dev is DeviceCamera deviceCamera)
                                                {
                                                    deviceCamera.DeviceService.DeviceStatus = baseDeviceConfig.DeviceStatus;
                                                }
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

        public static void UpdateServiceStatus(Dictionary<string, List<MQTTNodeService>> data)
        {
            List<TypeService> svrs = new List<TypeService>(ServiceManager.GetInstance().TypeServices);
            Dictionary<string, string> tokens = ServiceManager.GetInstance().ServiceTokens;
            foreach (var serviceKind in svrs)
            {
                //if (serviceKind.ServiceTypes.ToString() == ServiceTypes.Algorithm.ToString())
                //    continue;
                foreach (var item in data)
                {
                    if (item.Key.ToString() == serviceKind.ServiceTypes.ToString())
                    {
                        Dictionary<string, List<MQTTNodeService>> keyValuePairs = new Dictionary<string, List<MQTTNodeService>>();
                        foreach (var nodeService in item.Value)
                        {
                            if (tokens.ContainsKey(nodeService.ServiceName))
                            {
                                tokens[nodeService.ServiceName] = nodeService.ServiceToken;
                            }
                            if (keyValuePairs.ContainsKey(nodeService.UpChannel))
                                keyValuePairs[nodeService.UpChannel].Add(nodeService);
                            else
                                keyValuePairs.Add(nodeService.UpChannel, new List<MQTTNodeService>() { nodeService });
                        }

                        foreach (var baseObject in serviceKind.VisualChildren)
                        {
                            if (baseObject is TerminalService serviceTerminal)
                            {
                                foreach (var item1 in keyValuePairs)
                                {
                                    if (serviceTerminal.Config.SendTopic == item1.Key)
                                    {
                                        List<DateTime> dateTimes = new List<DateTime>();
                                        Dictionary<DateTime, MQTTNodeService> DateNodeServices = new Dictionary<DateTime, MQTTNodeService>();
                                        foreach (var mQTTNodeService in item1.Value)
                                        {
                                            DateTime dateTime = DateTime.Now;
                                            if(!string.IsNullOrEmpty(mQTTNodeService.LiveTime)) dateTime = DateTime.Parse(mQTTNodeService.LiveTime);
                                            dateTimes.Add(dateTime);
                                            DateNodeServices.Add(dateTime, mQTTNodeService);
                                        }
                                        List<DateTime> sortedDates = dateTimes.OrderBy(date => date).ToList();

                                        var ns = DateNodeServices[sortedDates.LastOrDefault()];
                                        serviceTerminal.Config.LastAliveTime = DateTime.Now;
                                        serviceTerminal.Config.IsAlive = true;
                                        serviceTerminal.Config.HeartbeatTime = DateNodeServices[sortedDates.LastOrDefault()].OverTime * 2;
                                        if (serviceTerminal.MQTTServiceTerminalBase.ServiceToken != DateNodeServices[sortedDates.LastOrDefault()].ServiceToken)
                                            serviceTerminal.MQTTServiceTerminalBase.ServiceToken = DateNodeServices[sortedDates.LastOrDefault()].ServiceToken;

                                        foreach (var baseObject1 in serviceTerminal.VisualChildren)
                                        {
                                            if (baseObject1 is DeviceService baseChannel && baseChannel.GetConfig() is DeviceServiceConfig baseDeviceConfig)
                                            {
                                                //baseDeviceConfig.IsAlive = true;
                                                if (!string.IsNullOrEmpty(ns.LiveTime)) baseDeviceConfig.LastAliveTime = DateTime.Parse(ns.LiveTime);
                                                baseDeviceConfig.HeartbeatTime = ns.OverTime * 2;
                                                baseDeviceConfig.ServiceToken = ns.ServiceToken;
                                                foreach(var devNew in ns.Devices)
                                                {
                                                    if (devNew.Value.Code == baseDeviceConfig.Code)
                                                    {
                                                        baseDeviceConfig.DeviceStatus = (DeviceStatusType)Enum.Parse(typeof(DeviceStatusType), devNew.Value.Status.ToString());
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
            this.Token = null;
            this.RegStatus = ServiceNodeStatus.Unregistered;
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
            this.RegStatus = ServiceNodeStatus.Unregistered;
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
                MQTTRCServicesRestartRequest reg = new MQTTRCServicesRestartRequest(NodeName, null, Token.AccessToken);
                PublishAsyncClient(RCAdminTopic, JsonConvert.SerializeObject(reg));
            }
        }


        public bool TryRegist(RCServiceConfig cfg)
        {
            ServiceNodeStatus curStatus = RegStatus;
            TryTestRegist = true;
            for (int i = 0; i < 3; i++)
            {
                if(DoRegist(cfg)) return true;
                Thread.Sleep(200);
            }
            TryTestRegist = false;
            RegStatus = curStatus;
            return false;
        }

        public bool IsRegisted()
        {
            return RegStatus == ServiceNodeStatus.Registered;
        }
    }
}
