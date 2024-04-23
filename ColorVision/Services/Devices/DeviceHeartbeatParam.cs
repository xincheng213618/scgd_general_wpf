using MQTTMessageLib;

namespace ColorVision.Services.Devices
{
    public class DeviceHeartbeatParam
    {
        public string DeviceName { get; set; }
        public DeviceStatusType DeviceStatus { get; set; }
    }
}
