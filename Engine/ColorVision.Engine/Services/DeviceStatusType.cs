
using ColorVision.Engine.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ColorVision.Engine.Services
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DeviceStatusType
    {
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusUnknown")]
        Unknown = -99,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusUnauthorized")]
        Unauthorized = -3,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusUnInit")]
        UnInit = -2,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusOffLine")]
        OffLine = -1,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusClosed")]
        Closed = 0,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusClosing")]
        Closing = 1,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusOpened")]
        Opened = 2,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusOpening")]
        Opening = 3,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusBusy")]
        Busy = 4,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusFree")]
        Free = 5,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusLiveOpened")]
        LiveOpened = 6,
        [LocalizedDescription(typeof(Properties.Resources), "DeviceStatusSPContinuousMode")]
        SP_Continuous_Mode = 11
    }
}
