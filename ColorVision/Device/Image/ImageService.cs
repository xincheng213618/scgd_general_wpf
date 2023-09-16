#pragma warning disable CA1710 // 标识符应具有正确的后缀

using ColorVision.MQTT;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Device.Image
{
    public class ImageEventName
    {
        public const string GetAllFiles = "GetAllFiles";
        public const string UploadFile = "UploadFile";
        public const string Open = "Open";
        public const string Heartbeat = "Heartbeat";
    }
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
                    if (json.Code == 0 && json.ServiceName.Equals(Config.Code, StringComparison.Ordinal))
                    {
                        if (!json.EventName.Equals(ImageEventName.Heartbeat, StringComparison.Ordinal))
                        {
                            OnImageData?.Invoke(this, new ImageDataEventArgs(json.EventName, json.Data));

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

        public void Open(string fileName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = ImageEventName.Open,
                ServiceName = Config.Code,
                Params = new Dictionary<string,object> { { "FileName", fileName } }
            };
            PublishAsyncClient(msg);
        }

        public void GetAllFiles()
        {
            MsgSend msg = new MsgSend
            {
                EventName = ImageEventName.GetAllFiles,
                ServiceName = Config.Code
            };
            PublishAsyncClient(msg);
        }

        public void UploadFile(string fileName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = ImageEventName.UploadFile,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName } }
            };
            PublishAsyncClient(msg);
        }
    }
}
