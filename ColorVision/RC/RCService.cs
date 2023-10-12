using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.SettingUp;
using Microsoft.Win32;
using MQTTMessageLib;
using MQTTMessageLib.RC;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.RC
{
    public class RCServiceStatusChangedEventArgs
    {
        public RCServiceStatusChangedEventArgs(ServiceNodeStatus status)
        {
            NodeStatus = status;
        }

        public ServiceNodeStatus NodeStatus { get;set; }
    }

    public delegate void RCServiceStatusChangedEventHandler(object sender, RCServiceStatusChangedEventArgs args);
    public class RCService : BaseService<RCConfig>
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

        public event RCServiceStatusChangedEventHandler StatusChangedEventHandler;
        public RCService(RCConfig config) : base(config)
        {
            Config = config;
            this.NodeType = "client";
            this.NodeName = MQTTRCServiceTypeConst.BuildNodeName(NodeType, null);
            this.DevcieName = "dev." + NodeType + ".127.0.0.1";
            //
            LoadCfg();
            //
            this.regStatus = ServiceNodeStatus.Unregistered;
            //
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
                            this.regStatus = ServiceNodeStatus.Registered;
                            if (!TryTestRegist)
                            {
                                Token = req.Data;
                                StatusChangedEventHandler?.Invoke(this, new RCServiceStatusChangedEventArgs(ServiceNodeStatus.Registered));
                                QueryServices();
                            }
                            break;
                        case MQTTNodeServiceEventEnum.Event_ServicesQuery:
                            MQTTRCServicesQueryResponse respQurey = JsonConvert.DeserializeObject<MQTTRCServicesQueryResponse>(Msg);
                            ServiceControl.GetInstance().UpdateStatus(respQurey.Data);
                            break;
                        case MQTTNodeServiceEventEnum.Event_NotRegist:
                            StatusChangedEventHandler?.Invoke(this, new RCServiceStatusChangedEventArgs(ServiceNodeStatus.Unregistered));
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

        public bool ReRegist()
        {
            LoadCfg();

            return Regist();
        }

        public bool Regist()
        {
            StatusChangedEventHandler?.Invoke(this, new RCServiceStatusChangedEventArgs(ServiceNodeStatus.Unregistered));
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
