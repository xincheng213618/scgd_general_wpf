#pragma warning disable CA1707
using ColorVision.Engine.Cache;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class ConfigAlgorithm : DeviceServiceConfig, IFileServerCfg
    {
        [Browsable(false)]
        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
