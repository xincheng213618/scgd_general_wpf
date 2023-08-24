using ColorVision.Device;
using ColorVision.MQTT;
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

    public class HeartbeatParam
    {
        //[JsonProperty("isOpen")]
        public DeviceStatus DeviceStatus { get; set; }
        //[JsonProperty("isAutoGetData")]
        public bool IsAutoGetData { get; set; }
        [JsonProperty("time")]
        public string Time { get; set; }
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
        [JsonProperty("fIntTime")]
        public float IntTime { get; set; }
        [JsonProperty("iAveNum")]
        public int AveNum { get; set; }
        [JsonProperty("bUseAutoIntTime")]
        public bool BUseAutoIntTime { get; set; }
        [JsonProperty("bUseAutoDark")]
        public bool BUseAutoDark { get; set; }
    }

    public class SpectrumConfig : BaseDeviceConfig, IServiceConfig
    {
        private int _TimeLimit;
        public int TimeLimit { get => _TimeLimit; set { _TimeLimit = value; NotifyPropertyChanged(); } }

        private float _TimeFrom;
        public float TimeFrom { get => _TimeFrom; set { _TimeFrom = value; NotifyPropertyChanged(); } }
    }
}
