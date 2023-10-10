using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ColorVision.Device
{
    public delegate void DeviceStatusChangedHandler(DeviceStatus deviceStatus);

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DeviceStatus
    {
        Unknown = -1,
        Closed = 0,
        Closing = 1,
        Opened = 2,
        Opening = 3,
        Busy = 4,
        Free = 5,
        UnInit,
        Init,
        UnConnected
    }

}
