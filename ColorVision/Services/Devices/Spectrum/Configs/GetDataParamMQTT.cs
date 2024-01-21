using Newtonsoft.Json;

namespace ColorVision.Services.Devices.Spectrum.Configs
{
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
}
