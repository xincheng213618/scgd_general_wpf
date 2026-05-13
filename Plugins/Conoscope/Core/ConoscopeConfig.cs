using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.Core;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Conoscope.Core
{
    public class ConoscopeConfig : ViewModelBase, IConfig
    {
        [JsonIgnore]
        public ConoscopeRenderingSettings Rendering { get; }

        [JsonIgnore]
        public ConoscopePreprocessSettings Preprocess { get; }

        [JsonIgnore]
        public ConoscopeColorDifferenceSettings ColorDifference { get; }

        [JsonIgnore]
        public ConoscopeCaptureSettings Capture { get; }

        [JsonIgnore]
        public ConoscopeExportSettings Export { get; }

        // CurrentModel 作为 Key，同时触发 ModelTypeChanged
        public ConoscopeModelType CurrentModel
        {
            get => _CurrentModel;
            set
            {
                if (_CurrentModel == value) return;
                _CurrentModel = value;
                OnPropertyChanged();
                // Ensure the profile exists
                EnsureProfile(value);
                // Notify subscribers
                ModelTypeChanged?.Invoke(this, _CurrentModel);
            }
        }
        private ConoscopeModelType _CurrentModel = ConoscopeModelType.VA60;

        public event EventHandler<ConoscopeModelType> ModelTypeChanged;

        // Model-specific configuration profiles
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<ConoscopeModelProfile> ModelProfiles
        {
            get => _ModelProfiles;
            set { _ModelProfiles = value; OnPropertyChanged(); }
        }
        private ObservableCollection<ConoscopeModelProfile> _ModelProfiles = new();

        /// <summary>
        /// Current model profile accessor
        /// </summary>
        public ConoscopeModelProfile CurrentModelProfile
        {
            get
            {
                EnsureProfile(CurrentModel);
                return ModelProfiles.FirstOrDefault(p => p.ModelType == CurrentModel)!;
            }
        }

        /// <summary>
        /// Ensure profile exists for given model type
        /// </summary>
        private void EnsureProfile(ConoscopeModelType modelType)
        {
            if (!ModelProfiles.Any(p => p.ModelType == modelType))
            {
                ModelProfiles.Add(ConoscopeModelProfile.CreateDefault(modelType));
            }
        }

        public ExportChannel DisplayChannel { get => _DisplayChannel; set { if (_DisplayChannel == value) return; _DisplayChannel = value; OnPropertyChanged(); } }
        private ExportChannel _DisplayChannel = ExportChannel.Y;

        public ColormapTypes PseudoColorMap { get => _PseudoColorMap; set { if (_PseudoColorMap == value) return; _PseudoColorMap = value; OnPropertyChanged(); } }
        private ColormapTypes _PseudoColorMap = ColormapTypes.COLORMAP_JET;

        public bool ApplyFilterOnOpen { get => _ApplyFilterOnOpen; set { if (_ApplyFilterOnOpen == value) return; _ApplyFilterOnOpen = value; OnPropertyChanged(); } }
        private bool _ApplyFilterOnOpen = true;

        [Category("预处理"), DisplayName("加载时修正非正 XYZ"), Description("启用后，在加载 CVCIE 数据时把 X/Y/Z 中小于等于 0 的像素修正为 1e-6，用于错误校正文件的兜底。会改变原始 XYZ 数据。")]
        public bool ClampNonPositiveXyzOnLoad { get => _ClampNonPositiveXyzOnLoad; set { if (_ClampNonPositiveXyzOnLoad == value) return; _ClampNonPositiveXyzOnLoad = value; OnPropertyChanged(); } }
        private bool _ClampNonPositiveXyzOnLoad = true;

        [Category("滤波"), DisplayName("滤波类型"), Description("Conoscope 图像打开或手动应用时使用的预处理滤波类型。")]
        public ImageFilterType FilterType { get => _FilterType; set { if (_FilterType == value) return; _FilterType = value; OnPropertyChanged(); } }
        private ImageFilterType _FilterType = ImageFilterType.Gaussian;

        [Category("滤波"), DisplayName("核大小"), Description("均值、高斯、中值滤波使用的核大小，自动修正为奇数。")]
        public int FilterKernelSize { get => _FilterKernelSize; set { _FilterKernelSize = NormalizeOdd(value, 1, 101); OnPropertyChanged(); } }
        private int _FilterKernelSize = 55;

        [Category("滤波"), DisplayName("高斯 Sigma"), Description("高斯滤波使用的标准差。")]
        public double FilterSigma { get => _FilterSigma; set { _FilterSigma = Math.Max(0.1, value); OnPropertyChanged(); } }
        private double _FilterSigma = 1.0;

        [Category("滤波"), DisplayName("双边 d"), Description("双边滤波邻域直径。")]
        public int FilterD { get => _FilterD; set { _FilterD = Math.Max(1, value); OnPropertyChanged(); } }
        private int _FilterD = 5;

        [Category("滤波"), DisplayName("双边 SigmaColor"), Description("双边滤波颜色域 Sigma。")]
        public double FilterSigmaColor { get => _FilterSigmaColor; set { _FilterSigmaColor = Math.Max(1, value); OnPropertyChanged(); } }
        private double _FilterSigmaColor = 75;

        [Category("滤波"), DisplayName("双边 SigmaSpace"), Description("双边滤波空间域 Sigma。")]
        public double FilterSigmaSpace { get => _FilterSigmaSpace; set { _FilterSigmaSpace = Math.Max(1, value); OnPropertyChanged(); } }
        private double _FilterSigmaSpace = 75;

        [Category("灰尘滤除"), DisplayName("启用灰尘滤除"), Description("灰尘滤除作为独立预处理步骤，可与常规滤波叠加使用。")]
        public bool DustRemovalEnabled { get => _DustRemovalEnabled; set { if (_DustRemovalEnabled == value) return; _DustRemovalEnabled = value; OnPropertyChanged(); } }
        private bool _DustRemovalEnabled;

        [Category("灰尘滤除"), DisplayName("灰尘类型"), Description("暗斑用于滤除黑点灰尘，亮斑用于滤除亮点异常。")]
        public DustRemovalMode DustRemovalMode { get => _DustRemovalMode; set { _DustRemovalMode = value; OnPropertyChanged(); } }
        private DustRemovalMode _DustRemovalMode = DustRemovalMode.DarkSpot;

        [Category("灰尘滤除"), DisplayName("检测阈值(%)"), Description("基于归一化亮度和局部背景的差异阈值，数值越小越敏感。")]
        public double DustThresholdPercent { get => _DustThresholdPercent; set { _DustThresholdPercent = Math.Max(0.1, Math.Min(value, 100)); OnPropertyChanged(); } }
        private double _DustThresholdPercent = 12;

        [Category("灰尘滤除"), DisplayName("最小面积(px)"), Description("低于该面积的候选点会被忽略。")]
        public int DustMinArea { get => _DustMinArea; set { _DustMinArea = Math.Max(1, value); OnPropertyChanged(); } }
        private int _DustMinArea = 1;

        [Category("灰尘滤除"), DisplayName("最大面积(px)"), Description("高于该面积的候选区域会被忽略，避免误修大面积结构。")]
        public int DustMaxArea { get => _DustMaxArea; set { _DustMaxArea = Math.Max(1, value); OnPropertyChanged(); } }
        private int _DustMaxArea = 500;

        [Category("灰尘滤除"), DisplayName("修复半径(px)"), Description("用于估计局部背景和扩展修复区域的像素半径。")]
        public int DustRepairRadius { get => _DustRepairRadius; set { _DustRepairRadius = Math.Max(1, Math.Min(value, 31)); OnPropertyChanged(); } }
        private int _DustRepairRadius = 3;

        public ColorDifferenceReferenceMode ColorDifferenceReferenceMode { get => _ColorDifferenceReferenceMode; set { _ColorDifferenceReferenceMode = value; OnPropertyChanged(); } }
        private ColorDifferenceReferenceMode _ColorDifferenceReferenceMode = ColorDifferenceReferenceMode.D65;

        public double ColorDifferenceCustomU { get => _ColorDifferenceCustomU; set { _ColorDifferenceCustomU = value; OnPropertyChanged(); } }
        private double _ColorDifferenceCustomU = 0.1978;

        public double ColorDifferenceCustomV { get => _ColorDifferenceCustomV; set { _ColorDifferenceCustomV = value; OnPropertyChanged(); } }
        private double _ColorDifferenceCustomV = 0.4684;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<ConoscopeNdCalibrationBinding> NdCalibrationBindings
        {
            get => _NdCalibrationBindings;
            set { _NdCalibrationBindings = value; OnPropertyChanged(); }
        }
        private ObservableCollection<ConoscopeNdCalibrationBinding> _NdCalibrationBindings = new();

        [Category("导出"), DisplayName("当前曲线采样间隔(度)"), Description("当前曲线 CSV 导出的默认采样间隔，范围 0.01 到 360 度。")]
        public double CurrentCurveExportStepDegrees
        {
            get => _CurrentCurveExportStepDegrees;
            set
            {
                double normalized = Math.Max(0.01, Math.Min(value, 360));
                if (Math.Abs(_CurrentCurveExportStepDegrees - normalized) < 0.000001) return;
                _CurrentCurveExportStepDegrees = normalized;
                OnPropertyChanged();
            }
        }
        private double _CurrentCurveExportStepDegrees = 1.0;

        [Category("导出"), DisplayName("当前曲线导出元数据"), Description("启用后，在当前曲线 CSV 顶部写入标题和元数据信息。")]
        public bool CurrentCurveExportIncludeMetadata
        {
            get => _CurrentCurveExportIncludeMetadata;
            set
            {
                if (_CurrentCurveExportIncludeMetadata == value) return;
                _CurrentCurveExportIncludeMetadata = value;
                OnPropertyChanged();
            }
        }
        private bool _CurrentCurveExportIncludeMetadata = true;


        public ConoscopeConfig()
        {
            Rendering = new ConoscopeRenderingSettings(this);
            Preprocess = new ConoscopePreprocessSettings(this);
            ColorDifference = new ConoscopeColorDifferenceSettings(this);
            Capture = new ConoscopeCaptureSettings(this);
            Export = new ConoscopeExportSettings(this);
            EnsureProfile(ConoscopeModelType.VA60);
            EnsureProfile(ConoscopeModelType.VA80);
        }

        private static int NormalizeOdd(int value, int min, int max)
        {
            value = Math.Max(min, Math.Min(value, max));
            return value % 2 == 0 ? Math.Min(value + 1, max) : value;
        }
    }

    public sealed class ConoscopeRenderingSettings
    {
        private readonly ConoscopeConfig config;

        internal ConoscopeRenderingSettings(ConoscopeConfig config)
        {
            this.config = config;
        }

        public ExportChannel DisplayChannel
        {
            get => config.DisplayChannel;
            set => config.DisplayChannel = value;
        }

        public ColormapTypes PseudoColorMap
        {
            get => config.PseudoColorMap;
            set => config.PseudoColorMap = value;
        }
    }

    public sealed class ConoscopePreprocessSettings
    {
        private readonly ConoscopeConfig config;

        internal ConoscopePreprocessSettings(ConoscopeConfig config)
        {
            this.config = config;
        }

        public bool ApplyFilterOnOpen
        {
            get => config.ApplyFilterOnOpen;
            set => config.ApplyFilterOnOpen = value;
        }

        public bool ClampNonPositiveXyzOnLoad
        {
            get => config.ClampNonPositiveXyzOnLoad;
            set => config.ClampNonPositiveXyzOnLoad = value;
        }

        public ImageFilterType FilterType
        {
            get => config.FilterType;
            set => config.FilterType = value;
        }

        public int FilterKernelSize
        {
            get => config.FilterKernelSize;
            set => config.FilterKernelSize = value;
        }

        public double FilterSigma
        {
            get => config.FilterSigma;
            set => config.FilterSigma = value;
        }

        public int FilterD
        {
            get => config.FilterD;
            set => config.FilterD = value;
        }

        public double FilterSigmaColor
        {
            get => config.FilterSigmaColor;
            set => config.FilterSigmaColor = value;
        }

        public double FilterSigmaSpace
        {
            get => config.FilterSigmaSpace;
            set => config.FilterSigmaSpace = value;
        }

        public bool DustRemovalEnabled
        {
            get => config.DustRemovalEnabled;
            set => config.DustRemovalEnabled = value;
        }

        public DustRemovalMode DustRemovalMode
        {
            get => config.DustRemovalMode;
            set => config.DustRemovalMode = value;
        }

        public double DustThresholdPercent
        {
            get => config.DustThresholdPercent;
            set => config.DustThresholdPercent = value;
        }

        public int DustMinArea
        {
            get => config.DustMinArea;
            set => config.DustMinArea = value;
        }

        public int DustMaxArea
        {
            get => config.DustMaxArea;
            set => config.DustMaxArea = value;
        }

        public int DustRepairRadius
        {
            get => config.DustRepairRadius;
            set => config.DustRepairRadius = value;
        }
    }

    public sealed class ConoscopeColorDifferenceSettings
    {
        private readonly ConoscopeConfig config;

        internal ConoscopeColorDifferenceSettings(ConoscopeConfig config)
        {
            this.config = config;
        }

        public ColorDifferenceReferenceMode ReferenceMode
        {
            get => config.ColorDifferenceReferenceMode;
            set => config.ColorDifferenceReferenceMode = value;
        }

        public double CustomU
        {
            get => config.ColorDifferenceCustomU;
            set => config.ColorDifferenceCustomU = value;
        }

        public double CustomV
        {
            get => config.ColorDifferenceCustomV;
            set => config.ColorDifferenceCustomV = value;
        }
    }

    public sealed class ConoscopeCaptureSettings
    {
        private readonly ConoscopeConfig config;

        internal ConoscopeCaptureSettings(ConoscopeConfig config)
        {
            this.config = config;
        }

        public ObservableCollection<ConoscopeNdCalibrationBinding> NdCalibrationBindings
        {
            get => config.NdCalibrationBindings;
            set => config.NdCalibrationBindings = value;
        }
    }

    public sealed class ConoscopeExportSettings
    {
        private readonly ConoscopeConfig config;

        internal ConoscopeExportSettings(ConoscopeConfig config)
        {
            this.config = config;
        }

        public double CurrentCurveStepDegrees
        {
            get => config.CurrentCurveExportStepDegrees;
            set => config.CurrentCurveExportStepDegrees = value;
        }

        public bool IncludeMetadata
        {
            get => config.CurrentCurveExportIncludeMetadata;
            set => config.CurrentCurveExportIncludeMetadata = value;
        }
    }

    public class ConoscopeNdCalibrationBinding : ViewModelBase
    {
        public string CameraCode { get => _CameraCode; set { _CameraCode = value; OnPropertyChanged(); } }
        private string _CameraCode = string.Empty;

        public string CameraName { get => _CameraName; set { _CameraName = value; OnPropertyChanged(); } }
        private string _CameraName = string.Empty;

        public int NdPort { get => _NdPort; set { _NdPort = value; OnPropertyChanged(); } }
        private int _NdPort;

        public int CalibrationTemplateId { get => _CalibrationTemplateId; set { _CalibrationTemplateId = value; OnPropertyChanged(); } }
        private int _CalibrationTemplateId = -1;

        public string CalibrationTemplateName { get => _CalibrationTemplateName; set { _CalibrationTemplateName = value; OnPropertyChanged(); } }
        private string _CalibrationTemplateName = string.Empty;
    }
}
