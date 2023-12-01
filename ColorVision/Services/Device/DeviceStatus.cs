using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace ColorVision.Services.Device
{
    public delegate void DeviceStatusChangedHandler(DeviceStatus deviceStatus);

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DeviceStatus
    {
        [Description("Unknown")]
        Unknown = -1,
        [Description("Closed")]
        Closed = 0,
        [Description("Closing")]
        Closing = 1,
        [Description("Opened")]
        Opened = 2,
        [Description("Opening")]
        Opening = 3,
        [Description("Busy")]
        Busy = 4,
        [Description("Free")]
        Free = 5,
        [Description("UnInit")]
        UnInit,
        [Description("Init")]
        Init,
        [Description("UnConnected")]
        UnConnected
    }



}
