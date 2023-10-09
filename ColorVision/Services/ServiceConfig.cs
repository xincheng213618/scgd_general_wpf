using ColorVision.Device;
using ColorVision.MQTT;

namespace ColorVision.Services
{
    public class ServiceConfig : BaseDeviceConfig, IServiceConfig, IHeartbeat
    {
        /// <summary>
        /// 服务类型
        /// </summary>
        public string Type { get; set; }

    }
}
