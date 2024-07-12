using ColorVision.Engine.Services.Configs;
using ColorVision.Engine.Services.Core;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    public class ConfigThirdPartyAlgorithms : DeviceServiceConfig, IServiceConfig
    {
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
