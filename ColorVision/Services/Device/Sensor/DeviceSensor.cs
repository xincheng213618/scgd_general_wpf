using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Sensor
{
    public class DeviceSensor : DeviceService<ConfigSensor>
    {
        public DeviceServiceSensor DeviceService { get; set; }

        public DeviceSensor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new DeviceServiceSensor(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceSensorControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSensorControl(this, false);
        public override UserControl GetDisplayControl() => new DisplaySensorControl(this);


    }
}
