using ColorVision.Engine.Messages;
using MQTTMessageLib.LightingController;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.LightingController
{
    public class MQTTLightingController : MQTTDeviceService<ConfigLightingController>
    {
        public MQTTLightingController(ConfigLightingController config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatusType.Closed;
        }

        private void ProcessingReceived(MsgReturn msg)
        {
            if (msg.Code != 0)
            {
                if (msg.EventName == MQTTPMEventEnum.Event_Open)
                    DeviceStatus = DeviceStatusType.Closed;
                return;
            }

            switch (msg.EventName)
            {
                case MQTTPMEventEnum.Event_Open:
                    DeviceStatus = DeviceStatusType.Opened;
                    break;
                case MQTTPMEventEnum.Event_Close:
                    DeviceStatus = DeviceStatusType.Closed;
                    break;
                case MQTTPMEventEnum.Event_SetValue:
                case MQTTPMEventEnum.Event_GetValue:
                    UpdateChannelValue(msg.Data as JObject);
                    break;
            }
        }

        private void UpdateChannelValue(JObject? data)
        {
            if (data == null
                || !data.TryGetValue("Y", StringComparison.OrdinalIgnoreCase, out JToken? channelToken)
                || !data.TryGetValue("Value", StringComparison.OrdinalIgnoreCase, out JToken? valueToken)
                || !int.TryParse(valueToken.ToString(), out int value))
                return;

            string channelCode = channelToken.ToString();
            PMChannelConfig? channel = Config.Channels.FirstOrDefault(item => string.Equals(item.Code, channelCode, StringComparison.OrdinalIgnoreCase));
            if (channel != null)
                channel.Value = value;
        }

        public MsgRecord Open() => Publish(MQTTPMEventEnum.Event_Open);

        public MsgRecord Close() => Publish(MQTTPMEventEnum.Event_Close);

        public MsgRecord SetValue(string channelCode, int value)
        {
            MsgSend msg = new()
            {
                EventName = MQTTPMEventEnum.Event_SetValue,
                Params = new Dictionary<string, object>
                {
                    ["Y"] = channelCode,
                    ["Value"] = value,
                },
            };
            return PublishAsyncClient(msg, GetTimeout());
        }

        public MsgRecord GetValue(string channelCode)
        {
            MsgSend msg = new()
            {
                EventName = MQTTPMEventEnum.Event_GetValue,
                Params = new Dictionary<string, object> { ["Y"] = channelCode },
            };
            return PublishAsyncClient(msg, GetTimeout());
        }

        public MsgRecord TurnOn(PMChannelConfig channel) => SetValue(channel.Code, channel.OnValue);

        public MsgRecord TurnOff(PMChannelConfig channel) => SetValue(channel.Code, channel.OffValue);

        private MsgRecord Publish(string eventName)
        {
            MsgSend msg = new() { EventName = eventName };
            return PublishAsyncClient(msg, GetTimeout());
        }

        private int GetTimeout() => Math.Max(1000, Config.Timeout);
    }
}
