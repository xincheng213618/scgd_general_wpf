using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Configs;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IServiceConfig
    {
        public bool IsCCTWave { get => _IsCCTWave; set { _IsCCTWave = value; NotifyPropertyChanged(); } }
        private bool _IsCCTWave;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
