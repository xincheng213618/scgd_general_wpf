using ColorVision.MySql.DAO;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.Services
{
    public class ServiceKind : BaseServiceTerminal
    {
        public SysDictionaryModel SysDictionaryModel { get; set; }

        public ServiceType ServiceType { get => 
                (ServiceType)SysDictionaryModel.Value; }
        public ServiceKind() : base()
        {
        }

        public List<string> ServicesCodes { get
            {
                List<string> codes = new List<string>();
                foreach (var item in VisualChildren)
                {
                    if (item is ServiceTerminal serviceTerminal)
                    {
                        if (!string.IsNullOrWhiteSpace(serviceTerminal.SysResourceModel.Code))
                            codes.Add(serviceTerminal.SysResourceModel.Code);
                    }
                }
                return codes;
            }
        }

        public override UserControl GenDeviceControl() => new ServiceKindControl(this);
    }
}
