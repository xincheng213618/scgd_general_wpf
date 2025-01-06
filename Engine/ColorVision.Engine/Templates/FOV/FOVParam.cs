using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.FOV
{
    public class FOVParam : ParamModBase
    {

        public FOVParam() { }

        public FOVParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        [Category("FOV"), Description("计算FOV时中心区亮度的百分比多少认为是暗区")]
        public double Radio { get => GetValue(_Radio); set { SetProperty(ref _Radio, value); } }
        private double _Radio = 0.2;
        [Category("FOV"), Description("相机镜头有效像素对应的角度")]
        public double CameraDegrees { get => GetValue(_CameraDegrees); set { SetProperty(ref _CameraDegrees, value); } }
        private double _CameraDegrees = 0.2;
        [Category("FOV"), Description("FOV中计算圆心或者矩心时使用的二值化阈值")]
        public int ThresholdValus { get => GetValue(_ThresholdValus); set { SetProperty(ref _ThresholdValus, value); } }
        private int _ThresholdValus = 20;

        [Category("FOV"), Description("相机镜头使用的有效像素")]
        public double DFovDist { get => GetValue(_DFovDist); set { SetProperty(ref _DFovDist, value); } }
        private double _DFovDist = 8443;
        [Category("FOV"), Description("计算pattern(FovCircle-圆形；FovRectangle-矩形)")]
        public FovPattern FovPattern { get => GetValue(_FovPattern); set { SetProperty(ref _FovPattern, value); } }
        private FovPattern _FovPattern = FovPattern.FovCircle;
        [Category("FOV"), Description("计算路线(Horizontal-水平；Vertical-垂直；Leaning-斜向)")]
        public FovType FovType { get => GetValue(_FovType); set { SetProperty(ref _FovType, value); } }
        private FovType _FovType = FovType.Horizontal;


        [Category("FOV"), Description("FovCircle,Leaning)")]
        public double Xc { get => GetValue(_Xc); set { SetProperty(ref _Xc, value); } }
        private double _Xc;
        [Category("FOV"), Description("FovCircle,Leaning)")]
        public double Yc { get => GetValue(_y_c); set { SetProperty(ref _y_c, value); } }
        private double _y_c;
        [Category("FOV"), Description("FovCircle,Leaning)")]
        public double Xp { get => GetValue(_Xp); set { SetProperty(ref _Xp, value); } }
        private double _Xp;
        [Category("FOV"), Description("FovCircle,Leaning)")]
        public double Yp { get => GetValue(_Yp); set { SetProperty(ref _Yp, value); } }
        private double _Yp;

    }
}
