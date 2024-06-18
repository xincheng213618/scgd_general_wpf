using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras;
using Newtonsoft.Json;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class ConfigCalibration: DeviceServiceConfig
    {
        public string CameraID { get => _CameraID; set { _CameraID = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(CameraCode)); } }
        private string _CameraID;

        [JsonIgnore]
        public string? CameraCode => PhyCameraManager.GetInstance().PhyCameras.First(a => a.Name == CameraID).SysResourceModel.Code;

        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); } }
        private double _ExpTimeR = 10;

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); } }
        private double _ExpTimeG = 10;

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); } }
        private double _ExpTimeB = 10;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
