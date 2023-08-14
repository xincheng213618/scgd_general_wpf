using ColorVision.MQTT.SMU;
using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.Sensor
{
    public class DeviceSensor : MQTTDevice<SensorConfig>
    {
        public SensorService SensorService { get; set; }

        public DeviceSensor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SensorService = new SensorService(Config);
        }
    }
}
