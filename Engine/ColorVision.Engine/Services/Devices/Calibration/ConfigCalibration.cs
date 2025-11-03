using ColorVision.Engine.Cache;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class ConfigCalibration: DeviceServiceConfig, IFileServerCfg
    {
        public string? CameraCode { get => _CameraCode; set { _CameraCode = value; OnPropertyChanged(); } }
        private string? _CameraCode;
        public string CameraID { get => _CameraID; set { _CameraID = value; OnPropertyChanged(); } }
        private string _CameraID;

        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; OnPropertyChanged(); } }
        private double _ExpTimeR = 10;

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; OnPropertyChanged(); } }
        private double _ExpTimeG = 10;

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; OnPropertyChanged(); } }
        private double _ExpTimeB = 10;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
