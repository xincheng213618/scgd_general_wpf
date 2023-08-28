using ColorVision.MQTT;

namespace ColorVision.Device.PG
{
    public class PGConfig : BaseDeviceConfig, IServiceConfig
    {
        public string Category { get; set; }
        public bool IsTCPIP { get; set; }
        public string Addr { get; set; }
        public int Port { get; set; }
    }
}
