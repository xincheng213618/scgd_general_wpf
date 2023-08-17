using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.MQTT
{
    public class HeartbeatService: BaseService<ServiceConfig>
    {
        public ServiceConfig ServiceConfig { get; set; }
        public HeartbeatService(ServiceConfig serviceConfig) : base(serviceConfig)
        {
            ServiceConfig = serviceConfig;

            SendTopic = ServiceConfig.SendTopic;
            SubscribeTopic = ServiceConfig.SubscribeTopic;

            MQTTControl.SubscribeCache(SubscribeTopic);

            MsgReturnReceived +=(msg)=>
            {
                switch (msg.EventName)
                {
                    case "CM_GetAllSnID":
                        JArray SnIDs = msg.Data.SnID;
                        JArray MD5IDs = msg.Data.MD5ID;
                        if (SnIDs == null || MD5IDs == null)
                        {
                            return;
                        }
                        for (int i = 0; i < SnIDs.Count; i++)
                        {
                            if (ServicesDevices.TryGetValue(SubscribeTopic, out ObservableCollection<string> list) && !list.Contains(SnIDs[i].ToString()))
                            {
                                list.Add(SnIDs[i].ToString());
                            }
                            else
                            {
                                ServicesDevices.Add(SubscribeTopic, new ObservableCollection<string>() { SnIDs[i].ToString() });
                            }
                        }
                        return;
                }

            };
            this.Connected += (s, e) =>
            {
                GetAllSnID();
            };
        }
        public static Dictionary<string, ObservableCollection<string>> ServicesDevices { get; set; } = new Dictionary<string, ObservableCollection<string>>();


        public bool GetAllSnID()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "CM_GetAllSnID",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public override string SubscribeTopic { get => ServiceConfig.SubscribeTopic; set { ServiceConfig.SubscribeTopic = value; } }
        public override string SendTopic { get => ServiceConfig.SendTopic; set { ServiceConfig.SendTopic = value; } }

        public override bool IsAlive { get => ServiceConfig.IsAlive; set => ServiceConfig.IsAlive = value; }
         
        public override DateTime LastAliveTime { get => ServiceConfig.LastAliveTime; set => ServiceConfig.LastAliveTime = value; }


    }



}
