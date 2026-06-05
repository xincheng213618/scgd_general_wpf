
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Messages;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;

namespace ColorVision.Engine.Services.Devices.FileServer
{

    public class MQTTFileServer : MQTTDeviceService<ConfigFileServer>
    {
        public MQTTFileServer(ConfigFileServer config) : base(config)
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
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
 
            }
            return Task.CompletedTask;
        }

    }
}
