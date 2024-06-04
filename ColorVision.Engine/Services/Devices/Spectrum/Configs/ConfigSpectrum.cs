using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Core;
using CVCommCore;
using Newtonsoft.Json;

namespace ColorVision.Services.Devices.Spectrum.Configs
{

    public class ConfigSpectrum : DeviceServiceConfig, IServiceConfig
    {
        private bool _IsAutoStart;
        public bool IsAutoOpen { get => _IsAutoStart; set { _IsAutoStart = value; NotifyPropertyChanged(); } }

        private int _TimeLimit;
        public int MaxIntegralTime { get => _TimeLimit; set { _TimeLimit = value; NotifyPropertyChanged(); } }

        private int _AutoTestTime;
        public int AutoTestTime { get => _AutoTestTime; set { _AutoTestTime = value; NotifyPropertyChanged(); } }

        private float _TimeFrom;
        public float BeginIntegralTime { get => _TimeFrom; set { _TimeFrom = value; NotifyPropertyChanged(); } }

        public bool IsShutterEnable { get => _IsShutter; set { _IsShutter = value; NotifyPropertyChanged(); } } 
        private bool _IsShutter;

        public ShutterConfig ShutterCfg { get => _ShutterCfg; set { _ShutterCfg = value; NotifyPropertyChanged(); } }
        private ShutterConfig _ShutterCfg;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

    }
}
