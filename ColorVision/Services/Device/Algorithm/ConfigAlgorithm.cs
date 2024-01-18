using ColorVision.Services.Device;
using ColorVision.Services.Device.Camera.Configs;

namespace ColorVision.Services.Device.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IServiceConfig
    {
        public string BindDeviceCode { get => _BindDeviceCode; set { _BindDeviceCode = value; NotifyPropertyChanged(); } }
        private string _BindDeviceCode;
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
