using ColorVision.Services.Core;
using ColorVision.Services.Devices;
using ColorVision.Services.Type;

namespace ColorVision.Services.Terminal
{
    public class TerminalServiceConfig : BaseConfig, IServiceConfig, IHeartbeat
    {
        public ServiceTypes ServiceType { get; set; }
    }

    public class DBTerminalServiceConfig
    {
        public int HeartbeatTime { get; set; }
    }
}
