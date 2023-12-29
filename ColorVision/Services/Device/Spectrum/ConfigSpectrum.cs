using ColorVision.Services;
using ColorVision.Services.Device;
using Newtonsoft.Json;
using static cvColorVision.GCSDLL;

namespace ColorVision.Device.Spectrum
{

    public class SpectumData
    {
        public int ID { get; set; }
        public float V { get; set; }
        public float I { get; set; }
        public ColorParam Data { get; set; }

        public SpectumData(int id, ColorParam data)
        {
            ID = id;
            Data = data;
            V = float.NaN;
            I = float.NaN;
        }

        public SpectumData()
        {
            V = float.NaN;
            I = float.NaN;
        }
    }

    public class SpectumDeviceHeartbeatParam : DeviceHeartbeatParam
    {
        public bool IsAutoGetData { get; set; }
    }

    public class SpectumHeartbeatParam : HeartbeatParam
    {
        public bool IsAutoGetData { get; set; }
    }
    public class AutoIntTimeParam
    {
        public int iLimitTime { get; set; }
        public float fTimeB { get; set; }
    }

    public class InitDarkParamMQTT
    {
        [JsonProperty("fIntTime")]
        public float IntTime { get; set; }
        [JsonProperty("iAveNum")]
        public int AveNum { get; set; }
    }

    public class GetDataParamMQTT
    {
        [JsonProperty("IntegralTime")]
        public float IntTime { get; set; }
        [JsonProperty("NumberOfAverage")]
        public int AveNum { get; set; }
        [JsonProperty("AutoIntegration")]
        public bool BUseAutoIntTime { get; set; }
        [JsonProperty("SelfAdaptionInitDark")]
        public bool BUseAutoDark { get; set; }
        [JsonProperty("AutoInitDark")]
        public bool BUseAutoShutterDark { get; set; }
    }
    public class ShutterConfig
    {
        /// <summary>
        /// 
        /// </summary>
        public int BaudRate { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Addr { get; set; }
        public string OpenCmd { get; set; }
        public string CloseCmd { get; set; }
        public int DelayTime { get; set; }
    }

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

        //public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        //private string _szComName = "COM1";

        //public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        //private int _BaudRate = 115200;

        //public int ShutterDelay { get => _ShutterDelay; set { _ShutterDelay = value; NotifyPropertyChanged(); } }
        //private int _ShutterDelay = 1000;

        //public string OpenCommnad { get => _OpenCommnad; set { _OpenCommnad = value; NotifyPropertyChanged(); } }
        //private string _OpenCommnad;

        //public string CloseCommnad { get => _CloseCommnad; set { _CloseCommnad = value; NotifyPropertyChanged(); } }
        //private string _CloseCommnad;

        public ShutterConfig ShutterCfg { get => _ShutterCfg; set { _ShutterCfg = value; NotifyPropertyChanged(); } }
        private ShutterConfig _ShutterCfg;

    }
}
