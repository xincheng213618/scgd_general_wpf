using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Types;

namespace ColorVision.Engine.Services.Terminal
{
    public class TerminalServiceConfig : BaseConfig, IServiceConfig
    {
        public ServiceTypes ServiceType { get; set; }
    }
}
