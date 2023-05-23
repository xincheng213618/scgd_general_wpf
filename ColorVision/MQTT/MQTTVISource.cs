using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT
{
    public class BaseService
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
        public MQTTControl MQTTControl { get; set; }
        public ulong ServiceID { get; set; }

        internal List<Guid> RunTimeUUID = new List<Guid> { Guid.NewGuid() };

        internal void PublishAsyncClient(MQTTMsg msg)
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
            MQTTControl.Connected += (s, e) => MQTTControlInit();
            Task.Run(() => MQTTControl.Connect());
        }


        private void MQTTControlInit()
        {
            SendTopic = "VISource";
            SubscribeTopic = "VISourceService";
            MQTTControl.SubscribeAsyncClient(SubscribeTopic);
            //如果之前绑定了，先移除在添加
            MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.Connected -= (s, e) => MQTTControlInit();
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MQTTMsgReturn json = JsonConvert.DeserializeObject<MQTTMsgReturn>(Msg);
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

        public bool Init()
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Init"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool UnInit()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "UnInit",
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }


        public bool SetParam()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "SetParam"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }
        
        public bool Open()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Open"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool GetData()
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "GetData",
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool Close()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Close"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }



    }
}
