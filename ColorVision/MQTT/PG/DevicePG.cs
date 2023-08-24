using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.PG
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
