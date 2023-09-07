using ColorVision.Device.Sensor;
using ColorVision.Device.SMU;
using ColorVision.MQTT;
using MQTTnet.Client;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Device.Image
{
    public class ImageDataEventArgs
    {
        public string EventName { get; set; }
        public dynamic Data { get; set; }

        public ImageDataEventArgs(string EventName, dynamic Data)
        {
            this.EventName = EventName;
            this.Data = Data;
        }
    }
    public delegate void MQTTImageDataHandler(object sender, ImageDataEventArgs arg);
    public class ImageService : BaseService<ImageConfig>
    {
        public event MQTTImageDataHandler OnImageData;
        public ImageService(ImageConfig config) : base(config)
        {
            Config = config;

            SendTopic = config.SendTopic;
            SubscribeTopic = config.SubscribeTopic;


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
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    if (json.Code == 0 && json.ServiceName.Equals(Config.Code))
                    {
                        if (json.EventName == "GetAllFiles")
                        {
                            OnImageData?.Invoke(this, new ImageDataEventArgs(json.EventName, json.Data));
                        }
                        else if (json.EventName == "SetParam")
                        {
                            //MessageBox.Show("SetParam");
                        }
                        else if (json.EventName == "Open")
                        {
                            //MessageBox.Show("Open");
                        }
                        else if (json.EventName == "GetData")
                        {
                            //MessageBox.Show("GetData");
                        }
                        else if (json.EventName == "Close")
                        {
                            //MessageBox.Show("Close");
                        }
                        else if (json.EventName == "UnInit")
                        {
                            //MessageBox.Show("UnInit");
                        }
                        else if (json.EventName == "Heartbeat")
                        {

                        }
                    }
                }
                catch(Exception ex)
                {
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }

        public void Open(string fileName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                ServiceName = Config.Code,
                Params = new Dictionary<string,object> { { "FileName", fileName } }
            };
            PublishAsyncClient(msg);
        }

        internal void GetAllFiles()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetAllFiles",
                ServiceName = Config.Code
            };
            PublishAsyncClient(msg);
        }
    }
}
