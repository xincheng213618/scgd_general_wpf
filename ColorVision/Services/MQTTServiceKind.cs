using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Services
{

    public enum DeviceType
    {
        Camera = 1,
        PG = 2,
        Spectum = 3,
        SMU = 4,
        Sensor = 5,
        Image = 6,
    }




    public class MQTTServiceKind : BaseMQTTService
    {
        public SysDictionaryModel SysDictionaryModel { get; set; }
        public MQTTServiceKind() : base()
        {
        }

        public override UserControl GenDeviceControl() => new ServiceKindControl(this);
    }
}
