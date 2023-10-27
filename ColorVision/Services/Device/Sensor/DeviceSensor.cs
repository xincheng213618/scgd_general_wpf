using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.Sensor
{
    public class DeviceSensor : BaseDevice<ConfigSensor>
    {
        public SensorService Service { get; set; }

        public DeviceSensor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new SensorService(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceSensorControl(this);
    }
}
