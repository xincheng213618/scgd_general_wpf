using ColorVision.Services.Device;

namespace ColorVision.Services
{
    public class BaseServiceConfig : BaseConfig, IServiceConfig, IHeartbeat
    {
        public ServiceTypes ServiceType { get; set; }


    }


}
