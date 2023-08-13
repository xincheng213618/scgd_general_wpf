using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.SMU
{
    public class DeviceSMU : MQTTDevice<SMUConfig>
    {
        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }


    }
}
