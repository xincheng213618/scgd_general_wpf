using ColorVision.MVVM;

namespace ColorVision.Services.Devices.Camera.Configs
{
    public class FileServerCfg : ViewModelBase
    {
        /// <summary>
        /// 数据基础路径
        /// </summary>
        public string DataBasePath { get => _DataBasePath; set { _DataBasePath = value; NotifyPropertyChanged(); } }
        private string _DataBasePath;
        /*
        /// <summary>
        /// 色/亮度图像
        /// </summary>
        public string CIEFileBasePath { get => _CIEFileBasePath; set { _CIEFileBasePath = value; NotifyPropertyChanged(); } }
        private string _CIEFileBasePath;

        /// <summary>
        /// 相机原始图像
        /// </summary>
        public string RawFileBasePath { get => _RawFileBasePath; set { _RawFileBasePath = value; NotifyPropertyChanged(); } }
        private string _RawFileBasePath;

        /// <summary>
        /// 校正后tif图像
        /// </summary>
        public string SrcFileBasePath { get => _SrcFileBasePath; set { _SrcFileBasePath = value; NotifyPropertyChanged(); } }
        private string _SrcFileBasePath;

        /// <summary>
        /// 校正文件路径
        /// </summary>
        public string CalibrationFileBasePath { get => _CalibrationFileBasePath; set { _CalibrationFileBasePath = value; NotifyPropertyChanged(); } }
        private string _CalibrationFileBasePath;
        ////////*/
        /// <summary>
        /// 端口地址
        /// </summary>
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; NotifyPropertyChanged(); } }
        private string _Endpoint;
    }
}