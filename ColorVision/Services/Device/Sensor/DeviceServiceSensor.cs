using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.Msg;

namespace ColorVision.Services.Device.Sensor
{


    /// <summary>
    /// 传感器的部分
    /// </summary>

    public class DeviceServiceSensor : BaseDevService<ConfigSensor>
    {

        public DeviceServiceSensor(ConfigSensor sensorConfig) : base(sensorConfig)
        {

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
