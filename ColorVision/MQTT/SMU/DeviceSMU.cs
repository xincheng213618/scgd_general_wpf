using ColorVision.MQTT.Service;
using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.SMU
{
    public class DeviceSMU : MQTTDevice<SMUConfig>
    {
        public SMUService SMUService { get; set; }

        public SMUView View { get; set; }

        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SMUService = new SMUService(Config);
            View = new SMUView();
        }


    }
}
