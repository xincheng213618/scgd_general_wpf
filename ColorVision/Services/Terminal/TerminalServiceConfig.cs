using ColorVision.Services.Devices;
using ColorVision.Services.Interfaces;
using ColorVision.Services.Types;

namespace ColorVision.Services.Terminal
{
    public class TerminalServiceConfig : BaseConfig, IServiceConfig, IHeartbeat
    {
        public ServiceTypes ServiceType { get; set; }
    }
}
