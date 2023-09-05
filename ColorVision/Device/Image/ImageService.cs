using ColorVision.Device.Sensor;
using ColorVision.MQTT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Device.Image
{
    public class ImageService : BaseService<ImageConfig>
    {
        public ImageService(ImageConfig config) : base(config)
        {
            Config = config;

            SendTopic = config.SendTopic;
            SubscribeTopic = config.SubscribeTopic;


            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
        }

        public void Open()
        {
            ImageOpenParam openParam = new ImageOpenParam("");
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = openParam
            };
            PublishAsyncClient(msg);
        }
    }

    public class ImageOpenParam
    {
        public string FileName { get; set; }

        public ImageOpenParam(string fileName)
        {
            FileName = fileName;
        }
    }
}
