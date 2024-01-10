using MQTTMessageLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Services.Device
{
    public delegate void DeviceStatusChangedHandler(DeviceStatusType deviceStatus);
}
