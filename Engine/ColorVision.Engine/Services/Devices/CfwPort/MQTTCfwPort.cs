using ColorVision.Engine.Messages;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class MQTTCfwPort : MQTTDeviceService<ConfigCfwPort>
    {
        public MQTTCfwPort(ConfigCfwPort config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatusType.Closed;
        }

        private void ProcessingReceived(MsgReturn msg)
        {
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "Open":
                        DeviceStatus = DeviceStatusType.Opened;
                        break;
                    case "SetPort":
                        break;
                    case "GetPort":
                        break;
                    case "Clode":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    default:
                        break;
                }
            }
            else if (msg.Code == 1)
            {

                switch (msg.EventName)
                {
                    case "Open":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    case "SetPort":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    case "GetPort":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    default:
                        break;
                }
            }
        }

        public MsgRecord Clsoe()
        {

            MsgSend msg = new MsgSend()
            {
                EventName = "Close",
            };

            return PublishAsyncClient(msg);
        }


        public MsgRecord Open()
        {

            MsgSend msg = new MsgSend()
            {
                EventName = "Open",
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord SetPort(int port)
        {

            MsgSend msg = new()
            {
                EventName = "SetPort",
                Params = new Dictionary<string, object>() { { "PortNum", port } }
            };

            MsgRecord record = PublishAsyncClient(msg);
            TrackPortCommand(record, port);
            return record;
        }

        private void TrackPortCommand(MsgRecord record, int? requestedPort)
        {
            int completed = 0;
            void Complete(object? sender, MsgRecordState state)
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                    || Interlocked.Exchange(ref completed, 1) != 0) return;
                record.MsgRecordStateChanged -= Complete;
                if (state != MsgRecordState.Success) return;

                int? port = requestedPort;
                if (record.MsgReturn?.Data is JObject data && data["Port"] is JValue { Type: JTokenType.Integer } value
                    && value.Value<long>() is >= 0 and <= int.MaxValue)
                    port = value.Value<int>();
                if (port.HasValue)
                    DisplayConfigManager.Instance.GetDisplayConfig<DisplayCfwPortConfig>(DeviceCode).PortNum = port;
            }
            record.MsgRecordStateChanged += Complete;
            Complete(record, record.MsgRecordState);
        }

        public MsgRecord GetPort()
        {

            MsgSend msg = new()
            {
                EventName = "GetPort",
                Params = new Dictionary<string, object>() { }
            };

            MsgRecord record = PublishAsyncClient(msg);
            TrackPortCommand(record, null);
            return record;
        }

    }
}
