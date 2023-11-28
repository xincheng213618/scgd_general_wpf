using ColorVision.Services;
using ColorVision.Services.Device;

namespace ColorVision.Device.PG
{
    public class ConfigPG : BaseDeviceConfig, IServiceConfig
    {
        public string Category { get; set; }
        public bool IsNet { get; set; }
        public string Addr { get; set; }
        public int Port { get; set; }
    }
}
