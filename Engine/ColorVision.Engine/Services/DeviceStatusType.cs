#pragma warning disable CS1998
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace ColorVision.Engine.Services
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DeviceStatusType
    {
        [Description("未知")]
        Unknown = -99,
        [Description("未授权")]
        Unauthorized = -3,
        [Description("未初始化")]
        UnInit = -2,
        [Description("已断开")]
        OffLine = -1,
        [Description("已关闭")]
        Closed = 0,
        [Description("关闭中")]
        Closing = 1,
        [Description("已打开")]
        Opened = 2,
        [Description("打开中")]
        Opening = 3,
        [Description("设备忙")]
        Busy = 4,
        [Description("设备空闲")]
        Free = 5,
        [Description("视频模式")]
        LiveOpened = 6,
        [Description("光谱仪连续模式")]
        SP_Continuous_Mode = 11
    }
}
