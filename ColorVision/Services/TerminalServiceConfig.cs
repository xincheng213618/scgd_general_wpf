using ColorVision.Services.Device;

namespace ColorVision.Services
{
    public class TerminalServiceConfig : BaseConfig, IServiceConfig, IHeartbeat
    {
        public ServiceTypes ServiceType { get; set; }


    }


}
