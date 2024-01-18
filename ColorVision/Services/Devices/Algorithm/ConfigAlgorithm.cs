using ColorVision.Services.Devices.Camera.Configs;

namespace ColorVision.Services.Devices.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IServiceConfig
    {
        public string BindDeviceCode { get => _BindDeviceCode; set { _BindDeviceCode = value; NotifyPropertyChanged(); } }
        private string _BindDeviceCode;
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
