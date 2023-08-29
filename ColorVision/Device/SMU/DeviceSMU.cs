using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.SMU
{
    public class DeviceSMU : BaseDevice<SMUConfig>
    {
        public SMUService SMUService { get; set; }

        public SMUView View { get; set; }

        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SMUService = new SMUService(Config);
            View = new SMUView();
        }
        public override UserControl GenDeviceControl() => new DeviceSMUControl(this);

    }
}
