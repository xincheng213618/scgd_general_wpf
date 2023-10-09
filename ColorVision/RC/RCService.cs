using ColorVision.MQTT;
using ColorVision.Services;
using MQTTMessageLib;
using MQTTMessageLib.RC;
using MQTTnet.Client;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.RC
{
    public class RCService : BaseService<RCConfig>
    {
        private string NodeName;
        private string NodeType;
        private string NodeAppId;
        private string NodeKey;
        private NodeToken Token;
        public RCService(RCConfig config) : base(config)
        {
            Config = config;
            NodeName = "client.node.1";
            NodeAppId = "app1";
            NodeKey = "123456";
            NodeType = "client";
            SubscribeTopic = MQTTRCServiceTypeConst.RCServiceType + "/Node/" + NodeName;

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
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
                            Token = req.Data;
                            QueryServices();
                            break;
                        case MQTTNodeServiceEventEnum.Event_ServicesQuery:
                            MQTTRCServicesQueryResponse respQurey = JsonConvert.DeserializeObject<MQTTRCServicesQueryResponse>(Msg);
                            ServiceControl.GetInstance().UpdateStatus(respQurey.Data);
                            break;
                        case MQTTNodeServiceEventEnum.Event_NotRegist:
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

        public bool Regist()
        {
            MQTTNodeServiceRegist reg = new MQTTNodeServiceRegist(NodeName, NodeAppId, NodeKey, SubscribeTopic, NodeType);
            PublishAsyncClient(MQTTRCServiceTypeConst.RCRegTopic, JsonConvert.SerializeObject(reg));
            return true;
        }

        public void QueryServices()
        {
            if (Token != null)
            {
                MQTTRCServicesQueryRequest reg = new MQTTRCServicesQueryRequest(NodeName, Token.AccessToken);
                PublishAsyncClient(MQTTRCServiceTypeConst.RCPublicTopic, JsonConvert.SerializeObject(reg));
            }
        }

        public void PublishAsyncClient(string topic, string json)
        {
            Task.Run(() => MQTTControl.PublishAsyncClient(topic, json, false));
        }
    }
}
