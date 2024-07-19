using ColorVision.Engine.Services.Configs;
using ColorVision.Engine.Services.Core;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{

    public class ConfigSpectrum : DeviceServiceConfig, IServiceConfig
    {
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; NotifyPropertyChanged(); } }
        private bool _IsAutoOpen;


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
