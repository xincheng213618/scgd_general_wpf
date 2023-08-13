using ColorVision.MQTT.Service;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;

namespace ColorVision.MQTT.Camera
{
    public class DeviceCamera : MQTTDevice<CameraConfig>
    {

        public DeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }

    }
}
