using ColorVision.MQTT;
using ColorVision.MySql.DAO;

namespace ColorVision.Device.Sensor
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
