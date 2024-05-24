using ColorVision.Services.Devices;
using ColorVision.Services.Core;
using ColorVision.Services.Types;

namespace ColorVision.Services.Terminal
{
    public class TerminalServiceConfig : BaseConfig, IServiceConfig, IHeartbeat
    {
        public ServiceTypes ServiceType { get; set; }
    }
}
