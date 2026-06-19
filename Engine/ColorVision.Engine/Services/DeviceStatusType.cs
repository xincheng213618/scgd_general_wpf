#pragma warning disable CA1707
using ColorVision.Engine.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Services
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DeviceStatusType
    {
        [LocalizedDescription(nameof(Resources.DeviceStatusUnknown))]
        Unknown = -99,
        [LocalizedDescription(nameof(Resources.DeviceStatusUnauthorized))]
        Unauthorized = -3,
        [LocalizedDescription(nameof(Resources.DeviceStatusUnInit))]
        UnInit = -2,
        [LocalizedDescription(nameof(Resources.DeviceStatusOffLine))]
        OffLine = -1,
        [LocalizedDescription(nameof(Resources.DeviceStatusClosed))]
        Closed = 0,
        [LocalizedDescription(nameof(Resources.DeviceStatusClosing))]
        Closing = 1,
        [LocalizedDescription(nameof(Resources.DeviceStatusOpened))]
        Opened = 2,
        [LocalizedDescription(nameof(Resources.DeviceStatusOpening))]
        Opening = 3,
        [LocalizedDescription(nameof(Resources.DeviceStatusBusy))]
        Busy = 4,
        [LocalizedDescription(nameof(Resources.DeviceStatusFree))]
        Free = 5,
        [LocalizedDescription(nameof(Resources.DeviceStatusLiveOpened))]
        LiveOpened = 6,
        [LocalizedDescription(nameof(Resources.DeviceStatusSPContinuousMode))]
        SP_Continuous_Mode = 11
    }
}
