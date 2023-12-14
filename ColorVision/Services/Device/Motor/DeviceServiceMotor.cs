using System.Collections.Generic;
using ColorVision.Services.Msg;

namespace ColorVision.Services.Device.Motor
{
    public class DeviceServiceMotor : BaseDevService<ConfigMotor>
    {
        public DeviceServiceMotor(ConfigMotor config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatus.Closed;
            DisConnected += (s, e) =>
            {
                DeviceStatus = DeviceStatus.Closed;
            };
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
                    case "MoveDiaphragm":
                        break;
                    case "GetPosition":
                         Config.Position = msg.Data.nPosition;
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                }
          
            }
            else if (msg.Code == 1)
            {
                switch (msg.EventName)
                {
                    case "Open":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    case "Move":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    case "GetPosition":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    default:
                        DeviceStatus = DeviceStatus.Closed;
                        break;

                }
            }
        }

        public MsgRecord Open()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "CodeID", Config.Code }, { "eFOCUS_COMMUN",(int)Config.eFOCUSCOMMUN },{ "szComName", Config.SzComName }, { "BaudRate", Config.BaudRate } }
            };

            return PublishAsyncClient(msg,1000);
        }

        public MsgRecord Close()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Close",
                Params = new Dictionary<string, object>() {  }
            };

            return PublishAsyncClient(msg,1000);
        }

        public MsgRecord Move(int nPosition ,bool IsbAbs =true, int dwTimeOut = 5000)
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Move",
                Params = new Dictionary<string, object>() {
                    {"nPosition",nPosition },{"dwTimeOut", Config.dwTimeOut },{ "bAbs", IsbAbs}
                }
            };
            return PublishAsyncClient(msg);
        }
        public MsgRecord MoveDiaphragm(double dPosition, int dwTimeOut = 5000)
        {

            MsgSend msg = new MsgSend
            {
                EventName = "MoveDiaphragm",
                Params = new Dictionary<string, object>() { { "dPosition", dPosition },{"dwTimeOut", Config.dwTimeOut }   }
            };
            return PublishAsyncClient(msg);
        }


        

        public MsgRecord GoHome()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GoHome",
                Params = new Dictionary<string, object>() { { "dwTimeOut", Config.dwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetPosition()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetPosition",
                Params = new Dictionary<string, object>() { { "dwTimeOut", Config.dwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }




    }
}
