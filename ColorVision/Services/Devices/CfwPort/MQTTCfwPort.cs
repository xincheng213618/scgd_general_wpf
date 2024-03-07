using ColorVision.Services.Msg;
using MQTTMessageLib;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Services.Devices.CfwPort
{
    public class MQTTCfwPort : MQTTDeviceService<ConfigCfwPort>
    {
        public MQTTCfwPort(ConfigCfwPort config) : base(config)
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
                    case "SetPort":
                        break;
                    case "GetPort":
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, $"Port:{(char)(msg.Data.nPort)}"));
                        break;
                    case "Clode":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    default:
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, $"未定义{msg.EventName}"));
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
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, $"未定义{msg.EventName}"));
                        break;
                }


            }


        }


        public MsgRecord Open()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                //Params = new Dictionary<string, object>() { {"CodeID", Config.Code }, { "szComName", Config.SzComName },{ "BaudRate", Config.BaudRate } }
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord SetPort(int port)
        {

            MsgSend msg = new MsgSend
            {
                EventName = "SetPort",
                Params = new Dictionary<string, object>() { { "szComName", port } }
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
