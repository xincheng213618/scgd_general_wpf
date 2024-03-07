namespace ColorVision.Services.Devices.FileServer
{
    public class FileServerConfig : DeviceServiceConfig
    {
        public string Endpoint { get => _Endpoint; set { _Endpoint = value;NotifyPropertyChanged(); } }
        private string _Endpoint;

        public string PortRange { get => _PortRange; set { _PortRange = value; NotifyPropertyChanged(); } }
        private string _PortRange;

        public string FileBasePath { get => _FileBasePath; set { _FileBasePath = value; NotifyPropertyChanged(); } }
        private string _FileBasePath;

    }
}
