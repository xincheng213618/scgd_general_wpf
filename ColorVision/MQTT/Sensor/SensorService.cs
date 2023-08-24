using ColorVision.MQTT.SMU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MQTT.Sensor
{


    /// <summary>
    /// 传感器的部分
    /// </summary>

    public class SensorService : BaseService<SensorConfig>
    {

        public SensorService(SensorConfig sensorConfig):base(sensorConfig)
        {
            this.Config = sensorConfig;

            this.SendTopic = sensorConfig.SendTopic;
            this.SubscribeTopic = sensorConfig.SubscribeTopic;


            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
        }


        public void Init()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
            };
            PublishAsyncClient(msg);
        }
        public void UnInit()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "UnInit",
            };
            PublishAsyncClient(msg);
        }

        public void Open()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
            };
            PublishAsyncClient(msg);
        }
        public void Close()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Close",
            };
            PublishAsyncClient(msg);
        }





    }
}
