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
using System.Windows;

namespace ColorVision.Services.Device.CfwPort
{
    public class DeviceServiceCfwPort : BaseDevService<ConfigCfwPort>
    {
        public DeviceServiceCfwPort(ConfigCfwPort config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatus.Closed;
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
                    case "SetPort":
                        break;
                    case "GetPort":
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(Application.Current.MainWindow, $"Port:{(char)(msg.Data.nPort)}")));
                        break;
                    case "Clode":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    default:
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(Application.Current.MainWindow, $"未定义{msg.EventName}")));
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
                    case "SetPort":
                        DeviceStatus = DeviceStatus.Closed;

                        break;
                    case "GetPort":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    default:
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => MessageBox.Show(Application.Current.MainWindow, $"未定义{msg.EventName}")));
                        break;
                }


            }


        }


        public MsgRecord Open()
        {

            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { {"CodeID", Config.Code }, { "szComName", Config.SzComName },{ "BaudRate", Config.BaudRate } }
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
