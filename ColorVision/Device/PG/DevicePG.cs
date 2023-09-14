using ColorVision.Device.Camera;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;
using System.Windows.Controls;

namespace ColorVision.Device.PG
{
    public class DevicePG : BaseDevice<PGConfig>
    {

        public PGDisplayControl Control { get; set; }
        public PGService PGService { get; set; }

        public DevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            PGService = new PGService(Config);
            Control = new PGDisplayControl(PGService);
        }

        public override UserControl GetDeviceControl() => new DevicePGControl(this);
        public override UserControl GetDisplayControl() => Control;

        public string IsNet
        {
            get
            {
                if (Config.IsNet) { return "网络"; }
                else { return "串口"; }
            }
        }
    }
}
