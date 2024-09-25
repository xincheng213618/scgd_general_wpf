using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JND
{

    public class JNDParam : ParamBase
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
