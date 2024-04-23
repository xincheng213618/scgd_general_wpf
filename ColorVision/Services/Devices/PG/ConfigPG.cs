using ColorVision.Services.Interfaces;

namespace ColorVision.Services.Devices.PG
{
    public class ConfigPG : DeviceServiceConfig, IServiceConfig
    {
        public string Category { get; set; }
        public bool IsNet { get; set; }
        public string Addr { get; set; }
        public int Port { get; set; }
    }
}
