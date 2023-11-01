using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Services.Device.CfwPort
{
    public class DeviceCfwPort : BaseDevice<ConfigCfwPort>
    {
        public DeviceServiceCfwPort DeviceService { get; set; }

        public DeviceCfwPort(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new DeviceServiceCfwPort(Config);
        }

        public override UserControl GetDeviceControl() => new DeviceCfwPortControl(this);

        public override UserControl GetDisplayControl() => new DisplayCfwPortControl(this);


    }
}
