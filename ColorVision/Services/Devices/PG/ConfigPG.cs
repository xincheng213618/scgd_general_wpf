using ColorVision.Services.Core;
using ColorVision.Services.Devices;

namespace ColorVision.Device.PG
{
    public class ConfigPG : DeviceServiceConfig, IServiceConfig
    {
        public string Category { get; set; }
        public bool IsNet { get; set; }
        public string Addr { get; set; }
        public int Port { get; set; }
    }
}
