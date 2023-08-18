using ColorVision.MQTT.Service;
using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.SMU
{
    public class DeviceSMU : MQTTDevice<SMUConfig>
    {
        public SMUService SMUService { get; set; }

        public ChartView ChartView { get; set; }

        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            //Config.SubscribeTopic = sysResourceModel.Pcode + "/STATUS/" + sysResourceModel.Code;
            SMUService = new SMUService(Config);
            ChartView = new ChartView(DeviceType.SMU);
        }


    }
}
