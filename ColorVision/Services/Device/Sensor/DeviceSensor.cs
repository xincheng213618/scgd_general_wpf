using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Services.Device.Motor;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Sensor
{
    public class DeviceSensor : BaseDevice<ConfigSensor>
    {
        public DeviceServiceSensor DeviceService { get; set; }

        public DeviceSensor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new DeviceServiceSensor(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceSensorControl(this);
        public override UserControl GetDisplayControl() => new DisplaySensorControl(this);


    }
}
