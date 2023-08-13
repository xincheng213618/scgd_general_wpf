using ColorVision.MQTT.SMU;
using ColorVision.MQTT.Spectrum;
using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.Service
{

    public class DeviceSMU : MQTTDevice<SMUConfig>
    {
        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }


    }

    public class DevicePG : MQTTDevice<PGConfig>
    {
        public DevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }
    }

    public class MQTTDeviceSpectrum : MQTTDevice<SpectrumConfig>
    {

        public MQTTDeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }
    }

    public enum MQTTDeviceType
    {
        Camera = 1,
        PG = 2,
        Spectum = 3,
        SMU = 4,
    }

    public class MQTTServiceKind : BaseObject
    {
        public SysDictionaryModel SysDictionaryModel { get; set; }
        public MQTTServiceKind() : base()
        {
        }
    }
}
