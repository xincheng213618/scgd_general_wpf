using ColorVision.Device.Camera;
using ColorVision.Device;
using ColorVision.Services.Device.Camera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ColorVision.Services.Msg;
using System.Diagnostics;

namespace ColorVision.Services.Device.CfwPort
{
    public class DeviceServiceCfwPort : BaseDevService<ConfigCfwPort>
    {
        public DeviceServiceCfwPort(ConfigCfwPort config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatus.UnInit;
        }

        private void ProcessingReceived(MsgReturn msg)
        {
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "Open":
                        DeviceStatus = DeviceStatus.Opened;
                        SnID = msg.SnID ?? string.Empty;
                        break;

                    case "SetPort":


                        break;
                    case "GetPort":


                        break;
                }
            }


        }


        public MsgRecord Open()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "szComName", Config.szComName },{ "BaudRate", Config.BaudRate } }
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord SetPort()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "SetPort",
                Params = new Dictionary<string, object>() { }
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord GetPort()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "GetPort",
                Params = new Dictionary<string, object>() { }
            };

            return PublishAsyncClient(msg);
        }

    }
}
