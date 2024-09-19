using ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost;
using ColorVision.Engine.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ROI
{

    public class ROIParam : ParamBase
    {
        public ROIParam() { }
        public ROIParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }
    }
}
