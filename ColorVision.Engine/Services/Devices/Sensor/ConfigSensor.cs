using ColorVision.Services.Devices.PG;

namespace ColorVision.Services.Devices.Sensor
{
    public class ConfigSensor : DeviceServiceConfig
    {
        public string Category { get => _Category; set { _Category = value; NotifyPropertyChanged(); } }
        private string _Category = "Sensor.HeYuan";
        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }
        private bool _IsNet;
        public string Addr { get => _Addr; set { _Addr = value; NotifyPropertyChanged(); } }
        private string _Addr;

        public int Port { get => _Port; set { _Port = value; NotifyPropertyChanged(); } }
        private int _Port;

        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; NotifyPropertyChanged(); } }
        private bool _IsAutoOpen = true;
    }
}
