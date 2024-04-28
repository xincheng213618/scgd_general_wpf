using ColorVision.Services.Devices.PG;

namespace ColorVision.Services.Devices.Sensor
{
    public class ConfigSensor : DeviceServiceConfig
    {
        public CommunicateType CommunicateType { get => _CommunicateType; set { _CommunicateType = value; NotifyPropertyChanged(); } }
        private CommunicateType _CommunicateType;

        public string SzIPAddress { get => _szIPAddress; set { _szIPAddress = value; NotifyPropertyChanged(); } }
        private string _szIPAddress;

        public string Category { get => _Category; set { _Category = value; NotifyPropertyChanged(); } }
        private string _Category;

        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }
        private bool _IsNet;

        public string PorName { get => _PorName; set { _PorName = value; NotifyPropertyChanged(); } }
        private string _PorName;

        public uint Port { get => _Port; set { _Port = value; NotifyPropertyChanged(); } }
        private uint _Port;


        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; NotifyPropertyChanged(); } }
        private bool _IsAutoOpen = true;
    }
}
