#pragma warning disable CA1707

using ColorVision.MySql.DAO;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Template.Algorithm
{
    public class MTFParam : ParamBase
    {
        public MTFParam() { }
        public MTFParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modDetails)
        {
        }

        [Category("MTF"), Description("MTF dRatio")]
        public double MTF_dRatio { get => GetValue(_MTF_dRatio); set { SetProperty(ref _MTF_dRatio, value); } }
        private double _MTF_dRatio = 0.01;
    }
}
