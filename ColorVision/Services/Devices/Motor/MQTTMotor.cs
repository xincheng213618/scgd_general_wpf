using System.Collections.Generic;
using ColorVision.Services.Msg;
using MQTTMessageLib;

namespace ColorVision.Services.Devices.Motor
{
    public class MQTTMotor : MQTTDeviceService<ConfigMotor>
    {
        public MQTTMotor(ConfigMotor config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatusType.Closed;
            DisConnected += (s, e) =>
            {
                DeviceStatus = DeviceStatusType.Closed;
            };
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
                    case "Move":
                        break;
                    case "MoveDiaphragm":
                        break;
                    case "GetPosition":
                         Config.Position = msg.Data.nPosition;
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatusType.Closed;
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
                    case "Move":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    case "GetPosition":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    default:
                        DeviceStatus = DeviceStatusType.Closed;
                        break;

                }
            }
        }

        public MsgRecord Open()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                //Params = new Dictionary<string, object>() { { "CodeID", Config.Code }, { "eFOCUS_COMMUN",(int)Config.eFOCUSCOMMUN },{ "szComName", Config.SzComName }, { "BaudRate", Config.BaudRate } }
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
