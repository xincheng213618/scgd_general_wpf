using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Services.Device.FilterWheel
{
    public class DeviceFilterWheel : BaseDevice<ConfigFilterWheel>
    {
        public DeviceServiceFilterWheel DeviceService { get; set; }

        public DeviceFilterWheel(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new DeviceServiceFilterWheel(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceFilterWheelControl(this);

        public override UserControl GetDisplayControl() => new DisplayFilterWheelControl(this);


    }
}
