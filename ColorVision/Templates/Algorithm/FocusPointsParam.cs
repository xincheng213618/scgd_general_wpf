#pragma warning disable CA1707,IDE1006

using ColorVision.MySql.DAO;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Templates.Algorithm
{
    public class FocusPointsParam : ParamBase
    {
        public FocusPointsParam() { }
        public FocusPointsParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name??string.Empty, modDetails)
        {
        }


    }
}
