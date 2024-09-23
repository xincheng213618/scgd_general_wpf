using ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost;
using ColorVision.Engine.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
