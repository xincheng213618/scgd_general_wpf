using ColorVision.Services.Core;

namespace ColorVision.Services.Devices.PG
{
    public class ConfigPG : DeviceServiceConfig, IServiceConfig
    {
        public string Category { get => _Category; set { _Category = value; NotifyPropertyChanged(); } }
        private string _Category;
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
