using ColorVision.Services.Device;

namespace ColorVision.Services.Algorithm
{
    public class ConfigAlgorithm : BaseDeviceConfig, IServiceConfig
    {
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; NotifyPropertyChanged(); } }
        private string _Endpoint;

        /// <summary>
        /// 数据基础路径
        /// </summary>
        public string DataBasePath { get => _DataBasePath; set { _DataBasePath = value; NotifyPropertyChanged(); } }
        private string _DataBasePath;
        //public string CIEFileBasePath { get => _CIEFileBasePath; set { _CIEFileBasePath = value; NotifyPropertyChanged(); } }
        //private string _CIEFileBasePath;
        //public string RawFileBasePath { get => _RawFileBasePath; set { _RawFileBasePath = value; NotifyPropertyChanged(); } }
        //private string _RawFileBasePath;
        //public string SrcFileBasePath { get => _SrcFileBasePath; set { _SrcFileBasePath = value; NotifyPropertyChanged(); } }
        //private string _SrcFileBasePath;
    }
}
