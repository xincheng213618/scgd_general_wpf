using ColorVision.Common.Extension;
using ColorVision.MQTT;
using ColorVision.Services.Core;
using MQTTMessageLib;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Services.Devices
{
    public class MessageRecvArgs
    {
        public int ResultCode { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }
        public dynamic Data { get; set; }

        public MessageRecvArgs(string eventName, string serialNumber, int resultCode, dynamic Data)
        {
            ResultCode = resultCode;
            EventName = eventName;
            SerialNumber = serialNumber;
            this.Data = Data;
        }
    }

    public delegate void MessageRecvHandler(object sender, MessageRecvArgs arg);


    public class MQTTDeviceService<T> : MQTTServiceBase where T : BaseConfig
    {
        public event DeviceStatusChangedHandler DeviceStatusChanged;

        public DeviceStatusType DeviceStatus
        {
            get => _DeviceStatus; set
            {
                _DeviceStatus = value;
                Application.Current.Dispatcher.Invoke(() => DeviceStatusChanged?.Invoke(value));
                NotifyPropertyChanged(); NotifyPropertyChanged(nameof(DeviceStatusString));
            }
        }
        private DeviceStatusType _DeviceStatus;

        public string DeviceStatusString { get => _DeviceStatus.ToDescription(); set { } }

        public T Config { get; set; }

        public event HeartbeatHandler HeartbeatEvent;

        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; } }

        public override string DeviceCode { get => Config.Code; set => Config.Code = value; }

        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; } }

        public override int HeartbeatTime { get => Config.HeartbeatTime; set { Config.HeartbeatTime = value; NotifyPropertyChanged(); } }

        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }

        public override DateTime LastAliveTime { get => Config.LastAliveTime; set => Config.LastAliveTime = value; }

        public override string ServiceToken { get => Config.ServiceToken; set => Config.ServiceToken = value; }

        public MQTTDeviceService(T config) : base()
        {
            Config = config;
            SendTopic = Config.SendTopic;
            SubscribeTopic = Config.SubscribeTopic;

            //SubscribeCache();
        }

        public void DoHeartbeat(List<DeviceHeartbeatParam> devsheartbeat)
        {
            foreach (DeviceHeartbeatParam dev_heartbeat in devsheartbeat)
            {
                if (dev_heartbeat.DeviceName.Equals(Config.Code, StringComparison.Ordinal))
                {
                    HeartbeatParam heartbeat = new HeartbeatParam();
                    heartbeat.DeviceStatus = dev_heartbeat.DeviceStatus;
                    DoHeartbeat(heartbeat);
                }
            }
        }

        public void DoHeartbeat(HeartbeatParam heartbeat)
        {
            Application.Current.Dispatcher.Invoke(() => HeartbeatEvent?.Invoke(heartbeat));
        }
    }
}
