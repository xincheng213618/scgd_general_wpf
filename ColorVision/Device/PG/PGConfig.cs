using ColorVision.MQTT;
using System.Collections.Generic;

namespace ColorVision.Device.PG
{
    public class PGConfig : BaseDeviceConfig, IServiceConfig
    {
        public string Category { get; set; }
        public bool IsNet { get; set; }
        public string Addr { get; set; }
        public int Port { get; set; }
    }
}
