using ColorVision.Engine.Services.Configs;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IFileServerCfg
    {
        public bool IsCCTWave { get => _IsCCTWave; set { _IsCCTWave = value; NotifyPropertyChanged(); } }
        private bool _IsCCTWave;

        public int POI_DBMaxNum { get => _POI_DBMaxNum; set { _POI_DBMaxNum = value; NotifyPropertyChanged(); } }
        private int _POI_DBMaxNum = 10000000;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
