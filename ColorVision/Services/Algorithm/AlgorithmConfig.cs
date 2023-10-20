using ColorVision.Services;

namespace ColorVision.Device.Algorithm
{
    public class AlgorithmConfig : BaseDeviceConfig, IServiceConfig
    {
        public string Endpoint { get; set; }
    }
}
