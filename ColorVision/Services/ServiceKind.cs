using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Services
{
    public class ServiceKind : BaseServiceViewMode
    {
        public SysDictionaryModel SysDictionaryModel { get; set; }
        public ServiceKind() : base()
        {
        }

        public override UserControl GenDeviceControl() => new ServiceKindControl(this);
    }
}
