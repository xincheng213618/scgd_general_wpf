using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.FindLightArea
{

    public class RoiParam : ParamModBase
    {
        public RoiParam() { }
        public RoiParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("ROI"), Description("阈值")]
        public int Threshold { get => GetValue(_Threshold); set { SetProperty(ref _Threshold, value); } }
        private int _Threshold = 1;
        [Category("ROI"), Description("Times")]
        public int Times { get => GetValue(_Times); set { SetProperty(ref _Times, value); } }
        private int _Times = 1;
        [Category("ROI"), Description("SmoothSize")]
        public int SmoothSize { get => GetValue(_SmoothSize); set { SetProperty(ref _SmoothSize, value); } }
        private int _SmoothSize = 1;
    }
}
