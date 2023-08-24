using ColorVision.MQTT;
using ColorVision.MySql.DAO;

namespace ColorVision.Device.PG
{
    public class DevicePG : MQTTDevice<PGConfig>
    {
        public PGService PGService { get; set; }

        public DevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            PGService = new PGService(Config);
        }
    }
}
