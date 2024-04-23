using ColorVision.Services.Devices;
using ColorVision.Services.Interfaces;
using ColorVision.Services.Type;

namespace ColorVision.Services.Terminal
{
    public class TerminalServiceConfig : BaseConfig, IServiceConfig, IHeartbeat
    {
        public ServiceTypes ServiceType { get; set; }
    }
}
