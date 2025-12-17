using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.RC;
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
                    if (TryN > 3)
                    {
                        TryN = 0;
                        return;
                    }
                }

                if (value == DeviceStatus) return;
                _DeviceStatus = value;

                Application.Current?.Dispatcher.BeginInvoke(() => DeviceStatusChanged?.Invoke(value));
                OnPropertyChanged(); 
            }
        }

        public int TryN { get; set; }


        private DeviceStatusType _DeviceStatus;

        public T Config { get; set; }

        public MQTTDeviceService(T config) : base()
        {
            Config = config;
            SendTopic = Config.SendTopic;
            SubscribeTopic = Config.SubscribeTopic;
            DeviceCode = Config.Code;
            HeartbeatTime =Config.HeartbeatTime;    
        }
    }
}
