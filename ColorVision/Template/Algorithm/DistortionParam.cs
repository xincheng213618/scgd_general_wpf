using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Template.Algorithm
{
    public class DistortionParam:ParamBase
    {
        public DistortionParam() { }
        public DistortionParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modDetails)
        {
        }
    }
}
