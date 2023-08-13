using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.Service
{

    public enum MQTTDeviceType
    {
        Camera = 1,
        PG = 2,
        Spectum = 3,
        SMU = 4,
        Sensor = 5,
    }

    public class MQTTServiceKind : BaseObject
    {
        public SysDictionaryModel SysDictionaryModel { get; set; }
        public MQTTServiceKind() : base()
        {
        }
    }
}
