using ColorVision.Engine.Templates;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.FocusPoints
{
    public class FocusPointsParam : ParamBase
    {
        public FocusPointsParam() { }

        public FocusPointsParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        public bool value1 { get; set; }
    }
}
