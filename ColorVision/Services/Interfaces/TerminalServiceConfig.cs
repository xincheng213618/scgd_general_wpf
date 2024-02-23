using ColorVision.Services.Devices;

namespace ColorVision.Services
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
