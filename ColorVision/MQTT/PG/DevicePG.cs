using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.PG
{
    public class DevicePG : MQTTDevice<PGConfig>
    {
        public DevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }
    }
}
