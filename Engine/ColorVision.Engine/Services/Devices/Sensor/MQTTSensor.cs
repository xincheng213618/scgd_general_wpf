using ColorVision.Engine.Messages;
using MQTTMessageLib;
using MQTTMessageLib.Sensor;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Sensor
{


    /// <summary>
    /// 传感器的部分
    /// </summary>

    public class MQTTSensor : MQTTDeviceService<ConfigSensor>
    {

        public MQTTSensor(ConfigSensor sensorConfig) : base(sensorConfig)
        {

        }

        public void Open()
        {
            MsgSend msg = new()
            {
                EventName = "Open",
                Params = new Dictionary<string, object> { { "eCOM_Type", Config.Category }, { "szIPAddress", Config.Addr }, { "nPort", Config.Port } }
            };
            PublishAsyncClient(msg);
        }

        /// <summary>
        /// 发送单个指令
        /// </summary>
        /// <param name="command"></param>
        public MsgRecord ExecCmd(SensorCmd command)
        {
            SensorExecCmdParam req = new();
            req.Cmd = command;
            MsgSend msg = new()
            {
                EventName = MQTTSensorEventEnum.Event_ExecCmd,
                Params = req,
            };
            return PublishAsyncClient(msg);
        }
        /// <summary>
        /// 发送模板
        /// </summary>
        /// <param name="temp"></param>
        public void ExecCmd(CVTemplateParam temp)
        {
            SensorExecCmdParam req = new();
            req.TemplateParam = temp;
            req.Cmd = new SensorCmd() { CmdType = SensorCmdType.None };
            MsgSend msg = new()
            {
                EventName = MQTTSensorEventEnum.Event_ExecCmd,
                Params = req,
            };
            PublishAsyncClient(msg);
        }
        public void Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
            };
            PublishAsyncClient(msg);
        }





    }
}
