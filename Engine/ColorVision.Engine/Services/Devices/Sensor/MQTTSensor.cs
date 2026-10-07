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
        internal DeviceSensor? Device { get; set; }
        public override DeviceStatusType DeviceStatus
        {
            get => Device?.SensorBackend.Status ?? base.DeviceStatus;
            set { if (Device == null) base.DeviceStatus = value; else Device.SensorBackend.ObserveService(value); }
        }
        internal void RefreshBackendStatus() => base.DeviceStatus = Device?.SensorBackend.Status ?? base.DeviceStatus;

        internal override MsgRecord PublishAsyncClient(MsgSend message, double timeout = 30000)
        {
            if (Device == null) return base.PublishAsyncClient(message, timeout);
            lock (Device.SensorBackend.Sync)
            {
                if (Device.SensorBackend.OpensLocally) return Device.RunLocalSensorCommand(message);
                Device.EnsureOtherSensorBackends(false, Local.LocalSensorSession.EndpointKey(Config));
                Device.SensorBackend.BeginServiceCommand(message.EventName);
            }
            try
            {
                MsgRecord record = base.PublishAsyncClient(message, timeout);
                int completed = 0;
                void Complete(object? sender, MsgRecordState state)
                {
                    if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                        || System.Threading.Interlocked.Exchange(ref completed, 1) != 0) return;
                    record.MsgRecordStateChanged -= Complete;
                    Device.SensorBackend.EndServiceCommand();
                    if (state == MsgRecordState.Success && message.EventName is "Open" or "Close" or "Reopen")
                        DeviceStatus = message.EventName == "Close" ? DeviceStatusType.Closed : DeviceStatusType.Opened;
                }
                record.MsgRecordStateChanged += Complete;
                Complete(record, record.MsgRecordState);
                return record;
            }
            catch { Device.SensorBackend.EndServiceCommand(); throw; }
        }

        public MQTTSensor(ConfigSensor sensorConfig) : base(sensorConfig)
        {

        }

        public MsgRecord Open()
        {
            MsgSend msg = new()
            {
                EventName = "Open",
                Params = new Dictionary<string, object> { { "eCOM_Type", Config.Category }, { "szIPAddress", Config.Addr }, { "nPort", Config.Port } }
            };
            return PublishAsyncClient(msg);
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
        public MsgRecord Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
            };
            return PublishAsyncClient(msg);
        }





    }
}
