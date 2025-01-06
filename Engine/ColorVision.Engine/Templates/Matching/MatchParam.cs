#pragma warning disable CA1707,IDE1006

using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.Matching
{
    public class MatchParam : ParamModBase
    {
        public MatchParam() { }
        public MatchParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("MatchParam"), Description("取样细致度 64 ~ 2048")]
        public int MinReducedArea { get => GetValue(_MinReducedArea); set { SetProperty(ref _MinReducedArea, value); } }
        private int _MinReducedArea = 256;

        [Category("MatchParam"), Description("误差角度[0-180]")]
        public double ToleranceAngle { get => GetValue(_ToleranceAngle); set { SetProperty(ref _ToleranceAngle, value); } }
        private double _ToleranceAngle;

        [Category("MatchParam"), Description("相似度 [0-1]")]
        public double Similarity { get => GetValue(_Similarity); set { SetProperty(ref _Similarity, value); } }
        private double _Similarity = 0.7;

        [Category("MatchParam"), Description("最大交叠比例 [0-0.8]")]
        public double MaxOverlapRatio { get => GetValue(_MaxOverlapRatio); set { SetProperty(ref _MaxOverlapRatio, value); } }
        private double _MaxOverlapRatio;

        [Category("MatchParam"), Description("目标数量")]
        public int TargetNumber { get => GetValue(_TargetNumber); set { SetProperty(ref _TargetNumber, value); } }
        private int _TargetNumber = 70;


    }
}
