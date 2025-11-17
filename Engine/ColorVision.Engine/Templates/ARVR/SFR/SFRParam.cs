using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Templates.SFR
{

    public class SFRParam : ParamModBase
    {
        public SFRParam()
        {

        }
        public SFRParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("SFR"), Description("Gamma")]
        public double Gamma { get => GetValue(_Gamma); set { SetProperty(ref _Gamma, value); } }
        private double _Gamma = 0.01;
    }
}
