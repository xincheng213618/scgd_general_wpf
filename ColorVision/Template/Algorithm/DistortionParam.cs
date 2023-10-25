#pragma warning disable CA1707,CA1822
using ColorVision.MySql.DAO;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Template.Algorithm
{
    public class DistortionParam:ParamBase
    {
        public DistortionParam() { }
        public DistortionParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }

        [Category("Distortion"), Description("ISIZE cx"), DisplayName("cx")]
        public int Width { get => GetValue(_Width); set { SetProperty(ref _Width, value); } }
        private int _Width = 16;
        [Category("Distortion"), Description("ISIZE cy"),DisplayName("cy")]
        public int Height { get => GetValue(_Height); set { SetProperty(ref _Height, value); } }
        private int _Height = 16;





        [Category("Distortion"), Description("是否使用颜色过滤")]
        public bool filterByColor { get => GetValue(_filterByColor); set { SetProperty(ref _filterByColor, value); } }
        private bool _filterByColor = true;

        [Category("Distortion"), Description("亮斑255暗斑0")]
        public int blobColor { get => GetValue(_blobColor); set { SetProperty(ref _blobColor, value); } }
        private int _blobColor;

        [Category("Distortion"), Description("阈值每次间隔值")]
        public float minThreshold { get => GetValue(_minThreshold); set { SetProperty(ref _minThreshold, value); } }
        private float _minThreshold =10;

        [Category("Distortion"), Description("斑点最小灰度")]
        public float thresholdStep { get => GetValue(_thresholdStep); set { SetProperty(ref _thresholdStep, value); } }
        private float _thresholdStep =10;

        [Category("Distortion"), Description("斑点最大灰度")]
        public float maxThreshold { get => GetValue(_maxThreshold); set { SetProperty(ref _maxThreshold, value); } }
        private float _maxThreshold =220;

        public bool ifDEBUG { get => GetValue(_ifDEBUG); set { SetProperty(ref _ifDEBUG, value); } }
        private bool _ifDEBUG;

        [Category("Distortion"), Description("暗斑比例")]
        public float darkRatio { get => GetValue(_darkRatio); set { SetProperty(ref _darkRatio, value); } }
        private float _darkRatio =0.01f;

        [Category("Distortion"), Description("对比度比例")]
        public float contrastRatio { get => GetValue(_contrastRatio); set { SetProperty(ref _contrastRatio, value); } }
        private float _contrastRatio =0.1f;

        [Category("Distortion"), Description("背景半径")]
        public int bgRadius { get => GetValue(_bgRadius); set { SetProperty(ref _bgRadius, value); } }
        private int _bgRadius =31;

        [Category("Distortion"), Description("斑点间隔距离")]
        public float minDistBetweenBlobs { get => GetValue(_minDistBetweenBlobs); set { SetProperty(ref _minDistBetweenBlobs, value); } }
        private float _minDistBetweenBlobs =50;

        [Category("Distortion"), Description("是否使用面积过滤")]
        public bool filterByArea { get => GetValue(_filterByArea); set { SetProperty(ref _filterByArea, value); } }
        private bool _filterByArea =true;

        [Category("Distortion"), Description("斑点最小面积值")]
        public float minArea { get => GetValue(_minArea); set { SetProperty(ref _minArea, value); } }
        private float _minArea =200;


        [Category("Distortion"), Description("斑点最大面积值")]
        public float maxArea { get => GetValue(_maxArea); set { SetProperty(ref _maxArea, value); } }
        private float _maxArea = 10000;

        [Category("Distortion"), Description("重复次数认定")]
        public int minRepeatability { get => GetValue(_minRepeatability); set { SetProperty(ref _minRepeatability, value); } }
        private int _minRepeatability =2;

        [Category("Distortion"), Description("形状控制（圆，方(")]
        public bool filterByCircularity { get => GetValue(_filterByCircularity); set { SetProperty(ref _filterByCircularity, value); } }
        private bool _filterByCircularity;

        [Category("Distortion")]
        public float minCircularity { get => GetValue(_minCircularity); set { SetProperty(ref _minCircularity, value); } }
        private float _minCircularity =0.9f;

        [Category("Distortion")]
        public float maxCircularity { get => GetValue(_maxCircularity); set { SetProperty(ref _maxCircularity, value); } }

        private float _maxCircularity = 1e37f;

        [Category("Distortion"), Description("形状控制（豁口）")]
        public bool filterByConvexity { get => GetValue(_filterByConvexity); set { SetProperty(ref _filterByConvexity, value); } }
        private bool _filterByConvexity;

        [Category("Distortion"), Description("形状控制（豁口）")]
        public float minConvexity { get => GetValue(_minConvexity); set { SetProperty(ref _minConvexity, value); } }
        private float _minConvexity = 0.9f;

        [Category("Distortion"), Description("形状控制（豁口）")]
        public float maxConvexity { get => GetValue(_maxConvexity); set { SetProperty(ref _maxConvexity, value); } }
        private float _maxConvexity =1e37f;

        [Category("Distortion"), Description("形状控制（椭圆度）")]
        public bool filterByInertia { get => GetValue(_filterByInertia); set { SetProperty(ref _filterByInertia, value); } }
        private bool _filterByInertia ;

        [Category("Distortion")]
        public float minInertiaRatio { get => GetValue(_minInertiaRatio); set { SetProperty(ref _minInertiaRatio, value); } }
        private float _minInertiaRatio = 0.9f;

        [Category("Distortion")]
        public float maxInertiaRatio { get => GetValue(_maxInertiaRatio); set { SetProperty(ref _maxInertiaRatio, value); } }
        private float _maxInertiaRatio =1e37f;
           
    }
}
