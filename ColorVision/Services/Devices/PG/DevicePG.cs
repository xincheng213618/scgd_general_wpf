using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices;
using System.Windows.Controls;

namespace ColorVision.Device.PG
{
    public class DevicePG : DeviceService<ConfigPG>
    {
        public MQTTPG DeviceService { get; set; }

        public DevicePG(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTPG(Config);
        }

        public override UserControl GetDeviceControl() => new DevicePGControl(this);
        public override UserControl GetDeviceInfo() => new DevicePGControl(this, false);

        public override UserControl GetDisplayControl() => new DisplayPGControl(this);

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
