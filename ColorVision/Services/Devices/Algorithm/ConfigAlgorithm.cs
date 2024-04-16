using ColorVision.Services.Core;
using ColorVision.Services.Devices.Camera.Configs;

namespace ColorVision.Services.Devices.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IServiceConfig
    {
        public bool IsCCTWave { get => _IsCCTWave; set { _IsCCTWave = value; NotifyPropertyChanged(); } }
        private bool _IsCCTWave;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
