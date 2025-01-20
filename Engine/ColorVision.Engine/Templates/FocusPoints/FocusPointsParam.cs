using System.Collections.Generic;

namespace ColorVision.Engine.Templates.FocusPoints
{
    public class FocusPointsParam : ParamModBase
    {
        public FocusPointsParam() { }

        public FocusPointsParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        public bool value1 { get; set; }
    }
}
