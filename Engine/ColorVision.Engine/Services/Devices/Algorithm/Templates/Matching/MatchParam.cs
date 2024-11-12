#pragma warning disable CA1707,IDE1006

using ColorVision.Engine.Templates;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Matching
{
    public class MatchParam : ParamModBase
    {
        public MatchParam() { }
        public MatchParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

    }
}
