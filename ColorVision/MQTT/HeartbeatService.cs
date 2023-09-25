using ColorVision.MQTT.Service;
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
            MsgReturnReceived +=(msg)=>
            {
                switch (msg.EventName)
                {
                    case "CM_GetAllSnID":
                        try
                        {
                            JArray SnIDs = msg.Data.SnID;
                            JArray MD5IDs = msg.Data.MD5ID;
                            if (SnIDs == null || MD5IDs == null)
                            {
                                return;
                            }
                            for (int i = 0; i < SnIDs.Count; i++)
                            {
                                if (ServicesDevices.TryGetValue(SubscribeTopic, out ObservableCollection<string> list))
                                {
                                    if (!list.Contains(SnIDs[i].ToString()))
                                         list.Add(SnIDs[i].ToString());

                                }
                                else
                                {
                                    ServicesDevices.Add(SubscribeTopic, new ObservableCollection<string>() { SnIDs[i].ToString() });
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            if (log.IsErrorEnabled)
                                log.Error(ex);
                        }
                        return;
                }

            };
            this.Connected += (s, e) =>
            {
                if (SendTopic.Contains("camera"))
                {
                    GetAllSnID();
                }
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
    }
}
