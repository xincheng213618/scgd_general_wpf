using ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost;
using ColorVision.Engine.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JND
{

    public class JNDParam : ParamBase
    {
        public JNDParam() { }
        public JNDParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }
    }
}
