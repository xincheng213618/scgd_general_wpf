#pragma warning disable CA1707,IDE1006

using ColorVision.MySql.DAO;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Templates.Algorithm
{
    public class LedCheckParam : ParamBase
    {
        public LedCheckParam() { }
        public LedCheckParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name??string.Empty, modDetails)
        {
        }

        [Category("LedCheck"), Description("MTF dRatio")]
        public double MTF_dRatio { get => GetValue(_MTF_dRatio); set { SetProperty(ref _MTF_dRatio, value); } }
        private double _MTF_dRatio = 0.01;


        [Category("LedCheck"), Description("EvaFunc")]
        public EvaFunc eEvaFunc { get => GetValue(_eEvaFunc); set { SetProperty(ref _eEvaFunc, value); } }
        private EvaFunc _eEvaFunc = EvaFunc.CalResol;


        [Category("LedCheck"), Description("dx")]
        public int dx { get => GetValue(_dx); set { SetProperty(ref _dx, value); } }
        private int _dx ;

        [Category("LedCheck"), Description("dy")]
        public int dy { get => GetValue(_dy); set { SetProperty(ref _dy, value); } }
        private int _dy = 1;

        [Category("LedCheck"), Description("dx")]
        public int ksize { get => GetValue(_ksize); set { SetProperty(ref _ksize, value); } }
        private int _ksize = 5;

    }
}
