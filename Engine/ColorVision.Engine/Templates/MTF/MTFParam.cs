#pragma warning disable CA1707,IDE1006

using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.MTF
{
    public class MTFParam : ParamModBase
    {
        public MTFParam() { }
        public MTFParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("MTF"), Description("MTF dRatio"),DisplayName]
        public double MTF_dRatio { get => GetValue(_MTF_dRatio); set { SetProperty(ref _MTF_dRatio, value); } }
        private double _MTF_dRatio = 0.01;


        [Category("MTF"), Description("EvaFunc")]
        public EvaFunc eEvaFunc { get => GetValue(_eEvaFunc); set { SetProperty(ref _eEvaFunc, value); } }
        private EvaFunc _eEvaFunc = EvaFunc.CalResol;


        [Category("MTF"), Description("dx")]
        public int dx { get => GetValue(_dx); set { SetProperty(ref _dx, value); } }
        private int _dx;

        [Category("MTF"), Description("dy")]
        public int dy { get => GetValue(_dy); set { SetProperty(ref _dy, value); } }
        private int _dy = 1;

        [Category("MTF"), Description("dx")]
        public int ksize { get => GetValue(_ksize); set { SetProperty(ref _ksize, value); } }
        private int _ksize = 5;

    }
}
