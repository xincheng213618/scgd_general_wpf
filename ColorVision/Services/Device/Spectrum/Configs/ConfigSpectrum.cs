using ColorVision.Services;
using ColorVision.Services.Device;

namespace ColorVision.Device.Spectrum.Configs
{

    public class ConfigSpectrum : BaseDeviceConfig, IServiceConfig
    {
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

    }
}
