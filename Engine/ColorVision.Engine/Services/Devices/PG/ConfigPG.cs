namespace ColorVision.Engine.Services.Devices.PG
{
    public class ConfigPG : DeviceServiceConfig
    {
        public string Category { get => _Category; set { _Category = value; OnPropertyChanged(); } }
        private string _Category;
        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet;
        public string Addr { get => _Addr; set { _Addr = value; OnPropertyChanged(); } }
        private string _Addr;

        public int Port { get => _Port; set { _Port = value; OnPropertyChanged(); } }
        private int _Port;

        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen = true;
    }
}
