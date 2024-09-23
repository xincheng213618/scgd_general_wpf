using ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost;
using ColorVision.Engine.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ROI
{

    public class ROIParam : ParamBase
    {
        public ROIParam() { }
        public ROIParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
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
