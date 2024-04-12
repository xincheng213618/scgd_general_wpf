using ColorVision.Services.Msg;
using ColorVision.Services.OnlineLicensing;
using ColorVision.Services.Terminal;
using MQTTMessageLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Devices.Camera
{
    public class MQTTTerminalCamera : MQTTServiceTerminalBase<TerminalServiceConfig>
    {
        public event DeviceStatusChangedHandler DeviceStatusChanged;
        public DeviceStatusType DeviceStatus  { get => _DeviceStatus;
            set 
            {
                if (_DeviceStatus == value) return;
                _DeviceStatus = value;
                Application.Current.Dispatcher.Invoke(() => DeviceStatusChanged?.Invoke(value)); NotifyPropertyChanged();
            } 
        }


        private DeviceStatusType _DeviceStatus;

        public List<MQTTCamera> Devices { get; set; }

        public MQTTTerminalCamera(TerminalServiceConfig Config) :base(Config)
        {
            Devices = new List<MQTTCamera>();
            Connected += (s, e) =>
            {
                GetAllSnID();
            };
            if (MQTTControl.IsConnect)
                GetAllSnID();
            MsgReturnReceived += (msg) => Application.Current.Dispatcher.Invoke(()=> MQTTCamera_MsgReturnChanged(msg));
        }

        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            switch (msg.EventName)
            {
                case "CM_GetAllSnID":
                    try
                    {
                        if (msg.Data == null)
                            return;
                        JArray SnIDs = msg.Data.SnID;
                        if (SnIDs != null)
                        {
                            DevicesSN = new ObservableCollection<string>();
                            for (int i = 0; i < SnIDs.Count; i++)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    DevicesSN.Add(SnIDs[i].ToString());
                                });
                            }
                        }
                        JArray MD5IDs = msg.Data.MD5ID;
                        if (SnIDs == null || MD5IDs == null)
                        {
                            return;
                        }
                        for (int i = 0; i < MD5IDs.Count; i++)
                        {
                            if (DevicesSNMD5.ContainsKey(SnIDs[i].ToString()))
                            {
                                DevicesSNMD5[SnIDs[i].ToString()] = MD5IDs[i].ToString();
                            }
                            else
                            {
                                DevicesSNMD5.Add(SnIDs[i].ToString(), MD5IDs[i].ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled)
                            log.Error(ex);
                    }
                    return;
            }

        }
        public MsgRecord GetAllSnID()
        {
            return PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });
        }
    }
}
