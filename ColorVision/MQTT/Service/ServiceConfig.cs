using ColorVision.Device;
using ColorVision.MQTT;
using ColorVision.MVVM;
using System;

namespace ColorVision.MQTT.Service
{
    public class ServiceConfig : BaseDeviceConfig, IServiceConfig, IHeartbeat
    {
        /// <summary>
        /// 服务类型
        /// </summary>
        public string Type { get; set; }

    }
}
