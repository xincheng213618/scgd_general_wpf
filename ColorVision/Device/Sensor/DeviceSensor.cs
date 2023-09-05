using ColorVision.Device.PG;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.Sensor
{
    public class DeviceSensor : BaseDevice<SensorConfig>
    {
        public SensorService SensorService { get; set; }

        public DeviceSensor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SensorService = new SensorService(Config);
        }

        public override UserControl GenDeviceControl() => new DeviceSensorControl(this);



    }
}
