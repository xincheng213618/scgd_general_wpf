using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.Sensor
{
    public class DeviceSensor : MQTTDevice<SensorConfig>
    {
        public DeviceSensor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }
    }
}
