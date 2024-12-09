using ColorVision.Engine.Services.Configs;
using ColorVision.Engine.Services.Core;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{

    public class ConfigSpectrum : DeviceServiceConfig, IServiceConfig, IFileServerCfg
    {
        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; NotifyPropertyChanged(); } }
        private bool _IsAutoOpen;

        /// <summary>
        /// 最大积分时间
        /// </summary>
        public int MaxIntegralTime { get => _TimeLimit; set { _TimeLimit = value; NotifyPropertyChanged(); } }
        private int _TimeLimit = 60000;

        /// <summary>
        /// 自动测试间隔
        /// </summary>
        public int AutoTestTime { get => _AutoTestTime; set { _AutoTestTime = value; NotifyPropertyChanged(); } }
        private int _AutoTestTime = 100;
        /// <summary>
        /// 起始积分时间
        /// </summary>
        public float BeginIntegralTime { get => _TimeFrom; set { _TimeFrom = value; NotifyPropertyChanged(); } }
        private float _TimeFrom = 10;

        public bool IsShutterEnable { get => _IsShutter; set { _IsShutter = value; NotifyPropertyChanged(); } } 
        private bool _IsShutter;

        public ShutterConfig ShutterCfg { get => _ShutterCfg; set { _ShutterCfg = value; NotifyPropertyChanged(); } }
        private ShutterConfig _ShutterCfg;

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

    }
}
