using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.Distortion
{
    public class DistortionParam : ParamModBase
    {

        public DistortionParam() { }
        public DistortionParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        [Category("Size"), Description("Width"), DisplayName("Width")]
        public int Width { get => GetValue(_Width); set { SetProperty(ref _Width, value); } }
        private int _Width = 16;
        [Category("Size"), Description("Resources.height"), DisplayName("height")]
        public int Height { get => GetValue(_Height); set { SetProperty(ref _Height, value); } }
        private int _Height = 16;

        [Category("Blob_Threshold_Params"), DisplayName("是否使用颜色过滤"), Description("是否使用颜色过滤")]
        public bool filterByColor { get => GetValue(_filterByColor); set { SetProperty(ref _filterByColor, value); } }
        private bool _filterByColor = true;

        [Category("Blob_Threshold_Params"), Description("亮斑255暗斑0")]
        public int blobColor { get => GetValue(_blobColor); set { SetProperty(ref _blobColor, value); } }
        private int _blobColor;

        [Category("阈值"), Description("阈值每次间隔值")]
        public float thresholdStep { get => GetValue(_thresholdStep); set { SetProperty(ref _thresholdStep, value); } }
        private float _thresholdStep = 10;

        [Category("阈值"), Description("斑点最小灰度")]
        public float minThreshold { get => GetValue(_minThreshold); set { SetProperty(ref _minThreshold, value); } }
        private float _minThreshold = 10;

        [Category("阈值"), Description("斑点最大灰度")]
        public float maxThreshold { get => GetValue(_maxThreshold); set { SetProperty(ref _maxThreshold, value); } }
        private float _maxThreshold = 220;

        [Category("阈值"), Description("斑点间隔距离")]
        public float minDistBetweenBlobs { get => GetValue(_minDistBetweenBlobs); set { SetProperty(ref _minDistBetweenBlobs, value); } }
        private float _minDistBetweenBlobs = 50;


        [Category("Blob_Threshold_Params")]
        public bool ifDEBUG { get => GetValue(_ifDEBUG); set { SetProperty(ref _ifDEBUG, value); } }
        private bool _ifDEBUG;

        [Category("Blob_Threshold_Params"), Description("暗斑比例")]
        public float darkRatio { get => GetValue(_darkRatio); set { SetProperty(ref _darkRatio, value); } }
        private float _darkRatio = 0.01f;

        [Category("Blob_Threshold_Params"), Description("对比度比例")]
        public float contrastRatio { get => GetValue(_contrastRatio); set { SetProperty(ref _contrastRatio, value); } }
        private float _contrastRatio = 0.1f;

        [Category("Blob_Threshold_Params"), Description("背景半径")]
        public int bgRadius { get => GetValue(_bgRadius); set { SetProperty(ref _bgRadius, value); } }
        private int _bgRadius = 31;



        [Category("面积"), Description("是否使用面积过滤")]
        public bool filterByArea { get => GetValue(_filterByArea); set { SetProperty(ref _filterByArea, value); } }
        private bool _filterByArea = true;

        [Category("面积"), Description("斑点最小面积值")]
        public float minArea { get => GetValue(_minArea); set { SetProperty(ref _minArea, value); } }
        private float _minArea = 200;


        [Category("面积"), Description("斑点最大面积值")]
        public float maxArea { get => GetValue(_maxArea); set { SetProperty(ref _maxArea, value); } }
        private float _maxArea = 10000;

        [Category("Blob_Threshold_Params"), Description("重复次数认定")]
        public int minRepeatability { get => GetValue(_minRepeatability); set { SetProperty(ref _minRepeatability, value); } }
        private int _minRepeatability = 2;

        [Category("形状圆控制"), Description("形状控制（圆，方(")]
        public bool filterByCircularity { get => GetValue(_filterByCircularity); set { SetProperty(ref _filterByCircularity, value); } }
        private bool _filterByCircularity;

        [Category("形状圆控制")]
        public float minCircularity { get => GetValue(_minCircularity); set { SetProperty(ref _minCircularity, value); } }
        private float _minCircularity = 0.9f;

        [Category("形状圆控制")]
        public float maxCircularity { get => GetValue(_maxCircularity); set { SetProperty(ref _maxCircularity, value); } }

        private float _maxCircularity = 1e37f;

        [Category("形状豁口控制"), Description("形状控制（豁口）")]
        public bool filterByConvexity { get => GetValue(_filterByConvexity); set { SetProperty(ref _filterByConvexity, value); } }
        private bool _filterByConvexity;

        [Category("形状豁口控制"), Description("形状控制（豁口）")]
        public float minConvexity { get => GetValue(_minConvexity); set { SetProperty(ref _minConvexity, value); } }
        private float _minConvexity = 0.9f;

        [Category("形状豁口控制"), Description("形状控制（豁口）")]
        public float maxConvexity { get => GetValue(_maxConvexity); set { SetProperty(ref _maxConvexity, value); } }
        private float _maxConvexity = 1e37f;

        [Category("形状椭圆控制"), Description("形状控制（椭圆度）")]
        public bool filterByInertia { get => GetValue(_filterByInertia); set { SetProperty(ref _filterByInertia, value); } }
        private bool _filterByInertia;

        [Category("形状椭圆控制")]
        public float minInertiaRatio { get => GetValue(_minInertiaRatio); set { SetProperty(ref _minInertiaRatio, value); } }
        private float _minInertiaRatio = 0.9f;

        [Category("形状椭圆控制")]
        public float maxInertiaRatio { get => GetValue(_maxInertiaRatio); set { SetProperty(ref _maxInertiaRatio, value); } }
        private float _maxInertiaRatio = 1e37f;

        [Category("全局"), DisplayName("角点提取方法"), Description("角点提取方法")]
        public CornerType type { get => GetValue(_type); set { SetProperty(ref _type, value); } }
        private CornerType _type = CornerType.Circlepoint;

        [Category("全局"), DisplayName("斜率计算方法"), Description("斜率计算方法")]
        public SlopeType sType { get => GetValue(_sType); set { SetProperty(ref _sType, value); } }
        private SlopeType _sType = SlopeType.lb_Variance;

        [Category("全局"), DisplayName("理想点布点方法"), Description("理想点布点方法")]
        public LayoutType lType { get => GetValue(_lType); set { SetProperty(ref _lType, value); } }
        private LayoutType _lType = LayoutType.SlopeOUT;

        [Category("全局"), DisplayName("光学畸变的检测方法"), Description("TV畸变H,V方向与光学畸变的检测方法")]
        public DistortionType dType { get => GetValue(_dType); set { SetProperty(ref _dType, value); } }
        private DistortionType _dType = DistortionType.TVDistV;

    }
}
