using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.SMU
{
    public class DeviceSMU : BaseDevice<SMUConfig>
    {
        public SMUService Service { get; set; }

        public SMUView View { get; set; }

        public SMUDisplayControl Control { get; set; }

        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new SMUService(Config);
            View = new SMUView();
           
        }
        public override UserControl GetDeviceControl() => new DeviceSMUControl(this);
        public override UserControl GetDisplayControl() => Control ?? new SMUDisplayControl(this);


    }
}
