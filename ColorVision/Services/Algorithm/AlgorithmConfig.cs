using ColorVision.Device;
using ColorVision.Services;

namespace ColorVision.Services.Algorithm
{
    public class AlgorithmConfig : BaseDeviceConfig, IServiceConfig
    {
        public string Endpoint { get; set; }
    }
}
