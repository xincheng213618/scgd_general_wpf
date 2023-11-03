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
using System.Security.Cryptography;

namespace ColorVision.Services.Device.Motor
{
    public class DeviceServiceMotor : BaseDevService<ConfigMotor>
    {
        public DeviceServiceMotor(ConfigMotor config) : base(config)
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
                        break;

                    case "Move":


                        break;
                }
            }


        }

        public MsgRecord Open()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "CodeID", Config.Code }, { "eFOCUS_COMMUN",(int)Config.eFOCUSCOMMUN },{ "szComName", Config.szComName }, { "BaudRate", Config.BaudRate } }
            };

            return PublishAsyncClient(msg);
        }



        public MsgRecord Move()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Move",
                Params = new Dictionary<string, object>() { }
            };

            return PublishAsyncClient(msg);
        }




    }
}
