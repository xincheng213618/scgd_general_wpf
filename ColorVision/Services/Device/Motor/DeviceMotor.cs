using ColorVision.Device.FileServer;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Motor
{
    public class DeviceMotor : BaseDevice<ConfigMotor>
    {
        public DeviceServiceMotor DeviceService { get; set; }

        public DeviceMotor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new DeviceServiceMotor(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceMotorControl(this);
        public override UserControl GetDeviceInfo() => new DeviceMotorControl(this, false);

        public override UserControl GetDisplayControl() => new DisplayMotorControl(this);


    }
}
