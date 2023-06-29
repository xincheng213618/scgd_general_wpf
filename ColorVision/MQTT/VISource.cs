using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.MQTT
{
    public class BaseService
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
        public MQTTControl MQTTControl { get; set; }
        public ulong ServiceID { get; set; }

        internal List<Guid> RunTimeUUID = new List<Guid> { Guid.NewGuid() };

        internal void PublishAsyncClient(MsgSend msg)
        {
            Guid guid = Guid.NewGuid();
            RunTimeUUID.Add(guid);

            msg.ServiceName = SendTopic;
            msg.MsgID = guid;
            msg.ServiceID = ServiceID;

            string json = JsonConvert.SerializeObject(msg, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, json, false));
        }
    }


    public class MQTTVISource: BaseService
    {
        public MQTTVISource()
        {
            MQTTControl = MQTTControl.GetInstance();
            SendTopic = "VISource";
            SubscribeTopic = "VISourceService";
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
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    if (json.Code == 0)
                    {
                        if (json.EventName == "Init")
                        {
                            ServiceID = json.ServiceID;
                        }
                        else if (json.EventName == "SetParam")
                        {
                            MessageBox.Show("SetParam");
                        }
                        else if (json.EventName == "Open")
                        {
                            MessageBox.Show("Open");
                        }
                        else if (json.EventName == "GetData")
                        {
                        }
                        else if (json.EventName == "Close")
                        {
                            MessageBox.Show("Close");
                        }
                        else if (json.EventName == "Uninit")
                        {
                            MessageBox.Show("Uninit");
                        }
                    }
                }
                catch
                {
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }


        public bool SetParam()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam"
            };
            PublishAsyncClient(msg);
            return true;
        }
        
        public bool Open()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open"
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool GetData()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Close()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MsgSend msg = new MsgSend
            {
                EventName = "Close"
            };
            PublishAsyncClient(msg);
            return true;
        }



    }
}
