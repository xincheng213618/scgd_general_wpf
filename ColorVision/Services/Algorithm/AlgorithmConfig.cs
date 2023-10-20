using ColorVision.Services;

namespace ColorVision.Device.Algorithm
{
    public class AlgorithmConfig : BaseDeviceConfig, IServiceConfig
    {
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; NotifyPropertyChanged(); } }
        private string _Endpoint;

        public string FileBasePath { get => _FileBasePath; set { _FileBasePath = value; NotifyPropertyChanged(); } }
        private string _FileBasePath;
    }
}
