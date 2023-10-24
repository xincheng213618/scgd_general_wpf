using ColorVision.MQTT;
using ColorVision.Services;
using MQTTMessageLib;
using MQTTMessageLib.RC;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    public class RCService : BaseDevService<RCConfig>
    {
        private string NodeName;
        private string NodeType;
        private string AppId;
        private string AppSecret;
        private string DevcieName;
        private string RCNodeName;
        private string RCRegTopic;
        private string RCHeartbeatTopic;
        private string RCPublicTopic;
        private NodeToken? Token;
        private bool TryTestRegist;
        private ServiceNodeStatus regStatus;

        public event RCServiceStatusChangedHandler StatusChangedEventHandler;
        public RCService(RCConfig config) : base(config)
        {
            Config = config;
            this.NodeType = "client";
            this.NodeName = MQTTRCServiceTypeConst.BuildNodeName(NodeType, null);
            this.DevcieName = "dev." + NodeType + ".127.0.0.1";
            LoadCfg();
            this.regStatus = ServiceNodeStatus.Unregistered;
            ServiceName = Guid.NewGuid().ToString();
            SubscribeTopic = MQTTRCServiceTypeConst.BuildNodeTopic(NodeName);

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }

        public void LoadCfg()
        {
            RCServiceConfig RcServiceConfig = GlobalSetting.GetInstance().SoftwareConfig.RcServiceConfig;
            this.AppId = RcServiceConfig.AppId;
            this.AppSecret = RcServiceConfig.AppSecret;
            this.RCNodeName = RcServiceConfig.RCName;

            this.RCRegTopic = MQTTRCServiceTypeConst.BuildRegTopic(RCNodeName);
            this.RCHeartbeatTopic = MQTTRCServiceTypeConst.BuildHeartbeatTopic(RCNodeName);
            this.RCPublicTopic = MQTTRCServiceTypeConst.BuildPublicTopic(RCNodeName);
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MQTTNodeServiceHeader json = JsonConvert.DeserializeObject<MQTTNodeServiceHeader>(Msg);
                    //if (json == null || !json.ServiceName.Equals(Config.Code, StringComparison.Ordinal))
                    //    return Task.CompletedTask;

                    //if (json.Code == 0)
                    //{
                    //}

                    switch (json.EventName)
                    {
                        case MQTTNodeServiceEventEnum.Event_Regist:
                            MQTTNodeServiceRegistResponse resp = JsonConvert.DeserializeObject<MQTTNodeServiceRegistResponse>(Msg);
                            break;
                        case MQTTNodeServiceEventEnum.Event_Startup:
                            MQTTNodeServiceStartupRequest req = JsonConvert.DeserializeObject<MQTTNodeServiceStartupRequest>(Msg);
                            if (req != null)
                            {
                                this.regStatus = ServiceNodeStatus.Registered;
                                if (!TryTestRegist)
                                {
                                    Token = req.Data;
                                    StatusChangedEventHandler?.Invoke(this, new RCServiceStatusChangedEvent(ServiceNodeStatus.Registered));
                                    QueryServices();
                                }
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_ServicesQuery:
                            MQTTRCServicesQueryResponse respQurey = JsonConvert.DeserializeObject<MQTTRCServicesQueryResponse>(Msg);
                            if (respQurey != null)
                            {
                                UpdateServiceStatus(respQurey.Data);
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


        public void UpdateServiceStatus(Dictionary<string, List<MQTTNodeService>> data)
        {
            foreach (var serviceKind in ServiceManager.GetInstance().MQTTServices)
            {
                foreach (var item in data)
                {
                    if (item.Key.ToString() == serviceKind.ServiceType.ToString())
                    {
                        Dictionary<string, List<MQTTNodeService>> keyValuePairs = new Dictionary<string, List<MQTTNodeService>>();
                        foreach (var nodeService in item.Value)
                        {
                            if (keyValuePairs.ContainsKey(nodeService.UpChannel))
                                keyValuePairs[nodeService.UpChannel].Add(nodeService);
                            else
                                keyValuePairs.Add(nodeService.UpChannel, new List<MQTTNodeService>() { nodeService });
                        }

                        foreach (var baseObject in serviceKind.VisualChildren)
                        {
                            if (baseObject is ServiceTerminal serviceTerminal)
                                foreach (var item1 in keyValuePairs)
                                {
                                    if (serviceTerminal.Config.SendTopic == item1.Key)
                                    {
                                        List<DateTime> dateTimes = new List<DateTime>();
                                        foreach (var mQTTNodeService in item1.Value)
                                        {
                                            dateTimes.Add(DateTime.Parse(mQTTNodeService.LiveTime));
                                        }
                                        List<DateTime> sortedDates = dateTimes.OrderBy(date => date).ToList();


                                        serviceTerminal.Config.LastAliveTime = sortedDates.LastOrDefault();
                                        serviceTerminal.Config.IsAlive = true;
                                        serviceTerminal.Config.HeartbeatTime = 99999;

                                        foreach (var baseObject1 in serviceTerminal.VisualChildren)
                                        {
                                            if (baseObject1 is BaseChannel baseChannel)
                                            {
                                                baseChannel.IsAlive = true;
                                                baseChannel.LastAliveTime = sortedDates.LastOrDefault(); ;
                                                baseChannel.HeartbeatTime = 99999;
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
            this.regStatus = ServiceNodeStatus.Unregistered;
            MQTTNodeServiceRegist reg = new MQTTNodeServiceRegist(NodeName, AppId, AppSecret, SubscribeTopic, NodeType);
            PublishAsyncClient(RCRegTopic, JsonConvert.SerializeObject(reg));
            return true;
        }

        public void QueryServices()
        {
            if (Token != null)
            {
                MQTTRCServicesQueryRequest reg = new MQTTRCServicesQueryRequest(NodeName, Token.AccessToken);
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
                return;

            List<DeviceHeartbeat> deviceStatues = new List<DeviceHeartbeat>();
            deviceStatues.Add(new DeviceHeartbeat(DevcieName, DeviceStatusType.Opened));
            string serviceHeartbeat = JsonConvert.SerializeObject(new MQTTServiceHeartbeat(NodeName, "", "", NodeType, ServiceName, deviceStatues, Token.AccessToken, (int)(heartbeatTime * 1.5f)));

            PublishAsyncClient(RCHeartbeatTopic, serviceHeartbeat);

            QueryServices();
        }

        private bool DoRegist(RCServiceConfig cfg)
        {
            string RegTopic = MQTTRCServiceTypeConst.BuildRegTopic(cfg.RCName);
            string appId = cfg.AppId;
            string appSecret = cfg.AppSecret;
            this.regStatus = ServiceNodeStatus.Unregistered;
            MQTTNodeServiceRegist reg = new MQTTNodeServiceRegist(NodeName, appId, appSecret, SubscribeTopic, NodeType);
            PublishAsyncClient(RegTopic, JsonConvert.SerializeObject(reg));
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(200);
                if (IsRegisted()) return true;
            }

            return false;
        }

        public bool TryRegist(RCServiceConfig cfg)
        {
            ServiceNodeStatus curStatus = regStatus;
            TryTestRegist = true;
            for (int i = 0; i < 3; i++)
            {
                if(DoRegist(cfg)) return true;
                Thread.Sleep(200);
            }
            TryTestRegist = false;
            regStatus = curStatus;
            return false;
        }

        public bool IsRegisted()
        {
            return regStatus == ServiceNodeStatus.Registered;
        }
    }
}
