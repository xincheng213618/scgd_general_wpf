using ColorVision.Services.Device;

namespace ColorVision.Device.FileServer
{
    public class FileServerConfig : BaseDeviceConfig
    {
        public string Endpoint { get => _Endpoint; set { _Endpoint = value;NotifyPropertyChanged(); } }
        private string _Endpoint;

        public string FileBasePath { get => _FileBasePath; set { _FileBasePath = value; NotifyPropertyChanged(); } }
        private string _FileBasePath;

    }
}
