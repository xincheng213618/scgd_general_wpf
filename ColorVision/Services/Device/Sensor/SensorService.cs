using ColorVision.MQTT;
using ColorVision.Services.Msg;

namespace ColorVision.Device.Sensor
{


    /// <summary>
    /// 传感器的部分
    /// </summary>

    public class SensorService : BaseDevService<SensorConfig>
    {

        public SensorService(SensorConfig sensorConfig) : base(sensorConfig)
        {
            Config = sensorConfig;

            SendTopic = sensorConfig.SendTopic;
            SubscribeTopic = sensorConfig.SubscribeTopic;


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
