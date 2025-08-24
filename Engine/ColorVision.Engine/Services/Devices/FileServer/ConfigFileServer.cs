namespace ColorVision.Engine.Services.Devices.FileServer
{
    public class ConfigFileServer : DeviceServiceConfig
    {
        public string Endpoint { get => _Endpoint; set { _Endpoint = value;OnPropertyChanged(); } }
        private string _Endpoint;

        public string PortRange { get => _PortRange; set { _PortRange = value; OnPropertyChanged(); } }
        private string _PortRange;

        public string FileBasePath { get => _FileBasePath; set { _FileBasePath = value; OnPropertyChanged(); } }
        private string _FileBasePath;

    }
}
