using ColorVision.MySql.DAO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Template.Algorithm
{
    public class GhostParam :ParamBase
    {
        public GhostParam() { }
        public GhostParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modDetails)
        {
        }
    }


}
