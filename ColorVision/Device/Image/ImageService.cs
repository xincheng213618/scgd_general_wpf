using ColorVision.Device.Sensor;
using ColorVision.MQTT;
using MQTTnet.Client;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Device.Image
{
    //public delegate void MQTTImageDataHandler(object sender, byte[] data);
    public class ImageService : BaseService<ImageConfig>
    {
        //public event MQTTImageDataHandler OnImageData;
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
    }
}
