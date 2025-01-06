using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.JND
{

    public class JNDParam : ParamModBase
    {
        public JNDParam() { }
        public JNDParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("JND"), Description("轮廓裁剪系数")]
        public double CutOff { get => GetValue(_CutOff); set { SetProperty(ref _CutOff, value); } }
        private double _CutOff = 0.3;


    }
}
