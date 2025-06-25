﻿using ColorVision.Engine.Messages;
using CVCommCore;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.CfwPort
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

        public MsgRecord Clsoe()
        {

            MsgSend msg = new MsgSend()
            {
                EventName = "Clsoe",
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

            return PublishAsyncClient(msg);
        }

        public MsgRecord GetPort()
        {

            MsgSend msg = new()
            {
                EventName = "GetPort",
                Params = new Dictionary<string, object>() { }
            };

            return PublishAsyncClient(msg);
        }

    }
}
