using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using ColorVision.Services.Device.Motor;
using System.Windows.Controls;

namespace ColorVision.Device.PG
{
    public class DevicePG : BaseDevice<ConfigPG>
    {
        public PGDevService DeviceService { get; set; }

        public DevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new PGDevService(Config);
        }

        public override UserControl GetDeviceControl() => new DevicePGControl(this);
        public override UserControl GetDeviceInfo() => new DevicePGControl(this, false);

        public override UserControl GetDisplayControl() => new PGDisplayControl(this);

        public string IsNet
        {
            get
            {
                if (Config.IsNet) { return "网络"; }
                else { return "串口"; }
            }
            set { }
        }
    }
}
