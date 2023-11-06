using ColorVision.Device;

namespace ColorVision.Services.Algorithm
{
    public class AlgorithmConfig : BaseDeviceConfig, IServiceConfig
    {
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; NotifyPropertyChanged(); } }
        private string _Endpoint;
        public string CIEFileBasePath { get => _CIEFileBasePath; set { _CIEFileBasePath = value; NotifyPropertyChanged(); } }
        private string _CIEFileBasePath;
        public string RawFileBasePath { get => _RawFileBasePath; set { _RawFileBasePath = value; NotifyPropertyChanged(); } }
        private string _RawFileBasePath;
        public string SrcFileBasePath { get => _SrcFileBasePath; set { _SrcFileBasePath = value; NotifyPropertyChanged(); } }
        private string _SrcFileBasePath;
    }
}
