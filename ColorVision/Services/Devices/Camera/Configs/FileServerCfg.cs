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
        /// <summary>
        /// 端口地址
        /// </summary>
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; NotifyPropertyChanged(); } }
        private string _Endpoint;
        /// <summary>
        /// 端口范围
        /// </summary>
        public string PortRange { get => _PortRange; set { _PortRange = value; NotifyPropertyChanged(); } }
        private string _PortRange;
    }
}