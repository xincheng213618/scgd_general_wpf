using ColorVision.Engine.Cache;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IFileServerCfg
    {
        [DisplayName("IsCCTWave")]
        public bool IsCCTWave { get => _IsCCTWave; set { _IsCCTWave = value; OnPropertyChanged(); } }
        private bool _IsCCTWave;

        [DisplayName("POI_DBMaxNum")]
        public int POI_DBMaxNum { get => _POI_DBMaxNum; set { _POI_DBMaxNum = value; OnPropertyChanged(); } }
        private int _POI_DBMaxNum = 10000000;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
