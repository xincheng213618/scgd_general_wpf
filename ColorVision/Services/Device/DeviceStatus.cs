using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace ColorVision.Services.Device
{
    public delegate void DeviceStatusChangedHandler(DeviceStatus deviceStatus);

    [JsonConverter(typeof(StringEnumConverter))]
    public enum DeviceStatus
    {
        [Description("未知")]
        Unknown = -1,
        [Description("已关闭")]
        Closed = 0,
        [Description("正在关闭")]
        Closing = 1,
        [Description("已打开")]
        Opened = 2,
        [Description("正在打开")]
        Opening = 3,
        [Description("忙碌")]
        Busy = 4,
        [Description("空闲")]
        Free = 5,
        [Description("未连接")]
        UnInit,
        [Description("已连接")]
        Init,
        [Description("未连接")]
        UnConnected
    }



}
