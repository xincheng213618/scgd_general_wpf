#pragma warning disable CA1707
using ColorVision.Engine.Cache;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IFileServerCfg
    {
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
