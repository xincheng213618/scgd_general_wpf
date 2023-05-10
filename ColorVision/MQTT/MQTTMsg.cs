using Newtonsoft.Json;

namespace ColorVision.MQTT
{
    public class MQTTMsg
    {
        public string Version { get; set; } = "1.0";
        public string EventName { get; set; }
        [JsonProperty("params")]
        public dynamic Params { get; set; }
    }

    public class MQTTMsgReturn
    {
        public string Version { get; set; }
        public string EventName { get; set; }
        public bool Code { get; set; }
        public string Msg { get; set; }
        [JsonProperty("data")]
        public dynamic Data { get; set; }
    }
}
