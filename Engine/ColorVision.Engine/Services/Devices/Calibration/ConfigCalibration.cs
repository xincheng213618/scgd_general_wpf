using ColorVision.Engine.Abstractions;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class ConfigCalibration: DeviceServiceConfig, IFileServerCfg
    {
        public string? CameraCode { get => _CameraCode; set { _CameraCode = value; NotifyPropertyChanged(); } }
        private string? _CameraCode;
        public string CameraID { get => _CameraID; set { _CameraID = value; NotifyPropertyChanged(); } }
        private string _CameraID;

        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); } }
        private double _ExpTimeR = 10;

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); } }
        private double _ExpTimeG = 10;

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); } }
        private double _ExpTimeB = 10;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
