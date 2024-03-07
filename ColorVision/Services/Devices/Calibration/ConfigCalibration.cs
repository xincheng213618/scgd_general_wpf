using ColorVision.Services.Core;
using ColorVision.Services.Devices.Camera.Configs;

namespace ColorVision.Services.Devices.Calibration
{
    public class ConfigCalibration: DeviceServiceConfig
    {
        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); } }
        private double _ExpTimeR = 10;

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); } }
        private double _ExpTimeG = 10;

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); } }
        private double _ExpTimeB = 10;

        public string BindDeviceCode { get => _BindDeviceCode; set { _BindDeviceCode = value; NotifyPropertyChanged(); } }
        private string _BindDeviceCode;
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
