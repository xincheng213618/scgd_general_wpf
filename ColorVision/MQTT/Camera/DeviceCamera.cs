using ColorVision.MQTT.Service;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;
using System;

namespace ColorVision.MQTT.Camera
{
    public class DeviceCamera : MQTTDevice<CameraConfig>
    {
        public MQTTCamera CameraService { get; set; }

        public DeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            CameraService = new MQTTCamera(Config);
        }
    }
}
