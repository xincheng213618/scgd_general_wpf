using ColorVision.Device.Camera;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;
using System.Windows.Controls;

namespace ColorVision.Device.PG
{
    public class DevicePG : BaseDevice<PGConfig>
    {
        public PGService PGService { get; set; }

        public DevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            PGService = new PGService(Config);
        }

        public override UserControl GenDeviceControl() => new DevicePGControl(this);
    }
}
