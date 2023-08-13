using ColorVision.MVVM;
using System;

namespace ColorVision.MQTT
{
    public class ServiceConfig : BaseDeviceConfig, IMQTTServiceConfig, IHeartbeat
    {
        /// <summary>
        /// 服务类型
        /// </summary>
        public string Type { get; set; }

    }
}
