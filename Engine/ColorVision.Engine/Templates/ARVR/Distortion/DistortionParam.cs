using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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

        [Category("Blob_Threshold_Params"), Display(Name = "Engine_PG_FilterByColor", Description = "Engine_PG_FilterByColor", ResourceType = typeof(Properties.Resources))]
        public bool filterByColor { get => GetValue(_filterByColor); set { SetProperty(ref _filterByColor, value); } }
        private bool _filterByColor = true;

        [Category("Blob_Threshold_Params"), Display(Description = "Engine_PG_BlobColorDesc", ResourceType = typeof(Properties.Resources))]
        public int blobColor { get => GetValue(_blobColor); set { SetProperty(ref _blobColor, value); } }
        private int _blobColor;

        [Display(GroupName = "Engine_PG_ThresholdCategory", Description = "Engine_PG_ThresholdStepDesc", ResourceType = typeof(Properties.Resources))]
        public float thresholdStep { get => GetValue(_thresholdStep); set { SetProperty(ref _thresholdStep, value); } }
        private float _thresholdStep = 10;

        [Display(GroupName = "Engine_PG_ThresholdCategory", Description = "Engine_PG_MinThresholdDesc", ResourceType = typeof(Properties.Resources))]
        public float minThreshold { get => GetValue(_minThreshold); set { SetProperty(ref _minThreshold, value); } }
        private float _minThreshold = 10;

        [Display(GroupName = "Engine_PG_ThresholdCategory", Description = "Engine_PG_MaxThresholdDesc", ResourceType = typeof(Properties.Resources))]
        public float maxThreshold { get => GetValue(_maxThreshold); set { SetProperty(ref _maxThreshold, value); } }
        private float _maxThreshold = 220;

        [Display(GroupName = "Engine_PG_ThresholdCategory", Description = "Engine_PG_MinDistBetweenBlobsDesc", ResourceType = typeof(Properties.Resources))]
        public float minDistBetweenBlobs { get => GetValue(_minDistBetweenBlobs); set { SetProperty(ref _minDistBetweenBlobs, value); } }
        private float _minDistBetweenBlobs = 50;


        [Category("Blob_Threshold_Params")]
        public bool ifDEBUG { get => GetValue(_ifDEBUG); set { SetProperty(ref _ifDEBUG, value); } }
        private bool _ifDEBUG;

        [Category("Blob_Threshold_Params"), Display(Description = "Engine_PG_DarkRatioDesc", ResourceType = typeof(Properties.Resources))]
        public float darkRatio { get => GetValue(_darkRatio); set { SetProperty(ref _darkRatio, value); } }
        private float _darkRatio = 0.01f;

        [Category("Blob_Threshold_Params"), Display(Description = "Engine_PG_ContrastRatioDesc", ResourceType = typeof(Properties.Resources))]
        public float contrastRatio { get => GetValue(_contrastRatio); set { SetProperty(ref _contrastRatio, value); } }
        private float _contrastRatio = 0.1f;

        [Category("Blob_Threshold_Params"), Display(Description = "Engine_PG_BgRadiusDesc", ResourceType = typeof(Properties.Resources))]
        public int bgRadius { get => GetValue(_bgRadius); set { SetProperty(ref _bgRadius, value); } }
        private int _bgRadius = 31;



        [Display(GroupName = "Engine_PG_Area", Description = "Engine_PG_FilterByAreaDesc", ResourceType = typeof(Properties.Resources))]
        public bool filterByArea { get => GetValue(_filterByArea); set { SetProperty(ref _filterByArea, value); } }
        private bool _filterByArea = true;

        [Display(GroupName = "Engine_PG_Area", Description = "Engine_PG_MinAreaDesc", ResourceType = typeof(Properties.Resources))]
        public float minArea { get => GetValue(_minArea); set { SetProperty(ref _minArea, value); } }
        private float _minArea = 200;


        [Display(GroupName = "Engine_PG_Area", Description = "Engine_PG_MaxAreaDesc", ResourceType = typeof(Properties.Resources))]
        public float maxArea { get => GetValue(_maxArea); set { SetProperty(ref _maxArea, value); } }
        private float _maxArea = 10000;

        [Category("Blob_Threshold_Params"), Display(Description = "Engine_PG_MinRepeatabilityDesc", ResourceType = typeof(Properties.Resources))]
        public int minRepeatability { get => GetValue(_minRepeatability); set { SetProperty(ref _minRepeatability, value); } }
        private int _minRepeatability = 2;

        [Display(GroupName = "Engine_PG_CircularityControl", Description = "Engine_PG_FilterByCircularityDesc", ResourceType = typeof(Properties.Resources))]
        public bool filterByCircularity { get => GetValue(_filterByCircularity); set { SetProperty(ref _filterByCircularity, value); } }
        private bool _filterByCircularity;

        [Category("形状圆控制")]
        public float minCircularity { get => GetValue(_minCircularity); set { SetProperty(ref _minCircularity, value); } }
        private float _minCircularity = 0.9f;

        [Category("形状圆控制")]
        public float maxCircularity { get => GetValue(_maxCircularity); set { SetProperty(ref _maxCircularity, value); } }

        private float _maxCircularity = 1e37f;

        [Display(GroupName = "Engine_PG_ConvexityControl", Description = "Engine_PG_ConvexityDesc", ResourceType = typeof(Properties.Resources))]
        public bool filterByConvexity { get => GetValue(_filterByConvexity); set { SetProperty(ref _filterByConvexity, value); } }
        private bool _filterByConvexity;

        [Display(GroupName = "Engine_PG_ConvexityControl", Description = "Engine_PG_ConvexityDesc", ResourceType = typeof(Properties.Resources))]
        public float minConvexity { get => GetValue(_minConvexity); set { SetProperty(ref _minConvexity, value); } }
        private float _minConvexity = 0.9f;

        [Display(GroupName = "Engine_PG_ConvexityControl", Description = "Engine_PG_ConvexityDesc", ResourceType = typeof(Properties.Resources))]
        public float maxConvexity { get => GetValue(_maxConvexity); set { SetProperty(ref _maxConvexity, value); } }
        private float _maxConvexity = 1e37f;

        [Display(GroupName = "Engine_PG_InertiaControl", Description = "Engine_PG_FilterByInertiaDesc", ResourceType = typeof(Properties.Resources))]
        public bool filterByInertia { get => GetValue(_filterByInertia); set { SetProperty(ref _filterByInertia, value); } }
        private bool _filterByInertia;

        [Category("形状椭圆控制")]
        public float minInertiaRatio { get => GetValue(_minInertiaRatio); set { SetProperty(ref _minInertiaRatio, value); } }
        private float _minInertiaRatio = 0.9f;

        [Category("形状椭圆控制")]
        public float maxInertiaRatio { get => GetValue(_maxInertiaRatio); set { SetProperty(ref _maxInertiaRatio, value); } }
        private float _maxInertiaRatio = 1e37f;

        [Display(Name = "Engine_PG_CornerExtractionMethod", GroupName = "Engine_PG_Global", Description = "Engine_PG_CornerExtractionMethodDesc", ResourceType = typeof(Properties.Resources))]
        public CornerType type { get => GetValue(_type); set { SetProperty(ref _type, value); } }
        private CornerType _type = CornerType.Circlepoint;

        [Display(Name = "Engine_PG_SlopeCalculationMethod", GroupName = "Engine_PG_Global", Description = "Engine_PG_SlopeCalculationMethodDesc", ResourceType = typeof(Properties.Resources))]
        public SlopeType sType { get => GetValue(_sType); set { SetProperty(ref _sType, value); } }
        private SlopeType _sType = SlopeType.lb_Variance;

        [Display(Name = "Engine_PG_LayoutMethod", GroupName = "Engine_PG_Global", Description = "Engine_PG_LayoutMethodDesc", ResourceType = typeof(Properties.Resources))]
        public LayoutType lType { get => GetValue(_lType); set { SetProperty(ref _lType, value); } }
        private LayoutType _lType = LayoutType.SlopeOUT;

        [Display(Name = "Engine_PG_DistortionDetectionMethod", GroupName = "Engine_PG_Global", Description = "Engine_PG_DistortionDetectionMethodDesc", ResourceType = typeof(Properties.Resources))]
        public DistortionType dType { get => GetValue(_dType); set { SetProperty(ref _dType, value); } }
        private DistortionType _dType = DistortionType.TVDistV;

    }
}
