#pragma warning disable CA1707
using ColorVision.MySql.DAO;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Template.Algorithm
{
    public class SFRParam : ParamBase
    {
        public SFRParam() { }
        public SFRParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modDetails)
        {
        }

        [Category("SFR"), Description("SFR gamma")]
        public double SFR_gamma { get => GetValue(_SFR_gamma); set { SetProperty(ref _SFR_gamma, value); } }
        private double _SFR_gamma = 0.01;
    }
}
