using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.Msg;
using System.Collections.Generic;

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
                Params = new Dictionary<string, object> { { "eCOM_Type", Config.CommunicateType } ,{ "szIPAddress",Config.SzIPAddress },{ "nPort",Config.Port} }
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
                Params = new Dictionary<string, object> { { "eCOM_Type", Config.CommunicateType }, { "szIPAddress", Config.SzIPAddress }, { "nPort", Config.Port } }
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
