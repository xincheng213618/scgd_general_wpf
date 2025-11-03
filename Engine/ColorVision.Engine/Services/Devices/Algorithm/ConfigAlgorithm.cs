using ColorVision.Engine.Cache;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IFileServerCfg
    {

        [DisplayName("色温波长")]
        public bool IsCCTWave { get => _IsCCTWave; set { _IsCCTWave = value; OnPropertyChanged(); } }
        private bool _IsCCTWave;

        [DisplayName("最大数量")]
        public int POI_DBMaxNum { get => _POI_DBMaxNum; set { _POI_DBMaxNum = value; OnPropertyChanged(); } }
        private int _POI_DBMaxNum = 10000000;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
