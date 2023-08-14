using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.SMU
{
    public class DeviceSMU : MQTTDevice<SMUConfig>
    {
        public SMUService SMUService { get; set; }
        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SMUService = new SMUService(Config);
        }


    }
}
