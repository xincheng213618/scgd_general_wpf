using CVCommCore;
using Newtonsoft.Json;

namespace ColorVision.Services.Devices
{
    public delegate void HeartbeatHandler(HeartbeatParam heartbeat);
    public class HeartbeatParam
    {
        public DeviceStatusType DeviceStatus { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; }
    }
}
