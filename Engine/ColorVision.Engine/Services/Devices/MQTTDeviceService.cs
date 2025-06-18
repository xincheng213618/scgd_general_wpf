using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Core;
using CVCommCore;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.Devices
{
    public class MQTTDeviceService<T> : MQTTServiceBase where T : BaseConfig
    {
        public event DeviceStatusChangedHandler DeviceStatusChanged;

        public override DeviceStatusType DeviceStatus
        {
            get => _DeviceStatus; set
            {
                if (value == DeviceStatusType.Unknown)
                {
                    TryN++;
                    if (TryN > 4)
                    {
                        TryN = 0;
                        return;
                    }
                }

                if (value == DeviceStatus) return;
                _DeviceStatus = value;

                try
                {
                    Application.Current?.Dispatcher.BeginInvoke(() => DeviceStatusChanged?.Invoke(value));
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                NotifyPropertyChanged(); NotifyPropertyChanged(nameof(DeviceStatusString));
            }
        }

        public int TryN { get; set; }



        private DeviceStatusType _DeviceStatus;

        public string DeviceStatusString { get => _DeviceStatus.ToDescription(); set { } }

        public T Config { get; set; }

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
        }
    }
}
