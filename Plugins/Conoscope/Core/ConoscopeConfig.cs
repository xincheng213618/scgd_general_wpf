using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
        public ConoscopeContrastSettings Contrast { get; }

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

        public bool UsePseudoColor
        {
            get => _UsePseudoColor;
            set
            {
                if (_UsePseudoColor == value) return;
                _UsePseudoColor = value;
                OnPropertyChanged();
            }
        }
        private bool _UsePseudoColor = true;

        public bool UsePseudoColorRangeLimit
        {
            get => _UsePseudoColorRangeLimit;
            set
            {
                if (_UsePseudoColorRangeLimit == value) return;
                _UsePseudoColorRangeLimit = value;
                OnPropertyChanged();
            }
        }
        private bool _UsePseudoColorRangeLimit = true;

        public bool ApplyFilterOnOpen { get => _ApplyFilterOnOpen; set { if (_ApplyFilterOnOpen == value) return; _ApplyFilterOnOpen = value; OnPropertyChanged(); } }
        private bool _ApplyFilterOnOpen = true;

        [Display(Name = "Con_Cfg_FixNonPositiveXYZ", GroupName = "Con_Category_Preprocess", Description = "启用后，在加载 CVCIE 数据时把 X/Y/Z 中小于等于 0 的像素修正为 1e-6，用于错误校正文件的兜底。会改变原始 XYZ 数据。", ResourceType = typeof(Properties.Resources))]
        public bool ClampNonPositiveXyzOnLoad { get => _ClampNonPositiveXyzOnLoad; set { if (_ClampNonPositiveXyzOnLoad == value) return; _ClampNonPositiveXyzOnLoad = value; OnPropertyChanged(); } }
        private bool _ClampNonPositiveXyzOnLoad = true;

        [Display(Name = "Con_Cfg_FilterType", GroupName = "Con_Category_Filter", Description = "Conoscope 图像打开或手动应用时使用的预处理滤波类型。", ResourceType = typeof(Properties.Resources))]
        public ImageFilterType FilterType { get => _FilterType; set { if (_FilterType == value) return; _FilterType = value; OnPropertyChanged(); } }
        private ImageFilterType _FilterType = ImageFilterType.Gaussian;

        [Display(Name = "Con_Cfg_KernelSize", GroupName = "Con_Category_Filter", Description = "均值、高斯、中值滤波使用的核大小，自动修正为奇数。", ResourceType = typeof(Properties.Resources))]
        public int FilterKernelSize { get => _FilterKernelSize; set { _FilterKernelSize = NormalizeOdd(value, 1, 101); OnPropertyChanged(); } }
        private int _FilterKernelSize = 55;

        [Display(Name = "Con_Cfg_GaussianSigma", GroupName = "Con_Category_Filter", Description = "高斯滤波使用的标准差。", ResourceType = typeof(Properties.Resources))]
        public double FilterSigma { get => _FilterSigma; set { _FilterSigma = Math.Max(0.1, value); OnPropertyChanged(); } }
        private double _FilterSigma = 1.0;

        [Display(Name = "Con_Cfg_BilateralD", GroupName = "Con_Category_Filter", Description = "双边滤波邻域直径。", ResourceType = typeof(Properties.Resources))]
        public int FilterD { get => _FilterD; set { _FilterD = Math.Max(1, value); OnPropertyChanged(); } }
        private int _FilterD = 5;

        [Display(Name = "Con_Cfg_BilateralSigmaColor", GroupName = "Con_Category_Filter", Description = "双边滤波颜色域 Sigma。", ResourceType = typeof(Properties.Resources))]
        public double FilterSigmaColor { get => _FilterSigmaColor; set { _FilterSigmaColor = Math.Max(1, value); OnPropertyChanged(); } }
        private double _FilterSigmaColor = 75;

        [Display(Name = "Con_Cfg_BilateralSigmaSpace", GroupName = "Con_Category_Filter", Description = "双边滤波空间域 Sigma。", ResourceType = typeof(Properties.Resources))]
        public double FilterSigmaSpace { get => _FilterSigmaSpace; set { _FilterSigmaSpace = Math.Max(1, value); OnPropertyChanged(); } }
        private double _FilterSigmaSpace = 75;

        [Display(Name = "Con_Cfg_EnableDust", GroupName = "Con_Category_Dust", Description = "灰尘滤除作为独立预处理步骤，可与常规滤波叠加使用。", ResourceType = typeof(Properties.Resources))]
        public bool DustRemovalEnabled { get => _DustRemovalEnabled; set { if (_DustRemovalEnabled == value) return; _DustRemovalEnabled = value; OnPropertyChanged(); } }
        private bool _DustRemovalEnabled;

        [Display(Name = "Con_Cfg_DustType", GroupName = "Con_Category_Dust", Description = "暗斑用于滤除黑点灰尘，亮斑用于滤除亮点异常。", ResourceType = typeof(Properties.Resources))]
        public DustRemovalMode DustRemovalMode { get => _DustRemovalMode; set { _DustRemovalMode = value; OnPropertyChanged(); } }
        private DustRemovalMode _DustRemovalMode = DustRemovalMode.DarkSpot;

        [Display(Name = "Con_Cfg_DetectThreshold", GroupName = "Con_Category_Dust", Description = "基于归一化亮度和局部背景的差异阈值，数值越小越敏感。", ResourceType = typeof(Properties.Resources))]
        public double DustThresholdPercent { get => _DustThresholdPercent; set { _DustThresholdPercent = Math.Max(0.1, Math.Min(value, 100)); OnPropertyChanged(); } }
        private double _DustThresholdPercent = 12;

        [Display(Name = "Con_Cfg_MinArea", GroupName = "Con_Category_Dust", Description = "低于该面积的候选点会被忽略。", ResourceType = typeof(Properties.Resources))]
        public int DustMinArea { get => _DustMinArea; set { _DustMinArea = Math.Max(1, value); OnPropertyChanged(); } }
        private int _DustMinArea = 1;

        [Display(Name = "Con_Cfg_MaxArea", GroupName = "Con_Category_Dust", Description = "高于该面积的候选区域会被忽略，避免误修大面积结构。", ResourceType = typeof(Properties.Resources))]
        public int DustMaxArea { get => _DustMaxArea; set { _DustMaxArea = Math.Max(1, value); OnPropertyChanged(); } }
        private int _DustMaxArea = 500;

        [Display(Name = "Con_Cfg_RepairRadius", GroupName = "Con_Category_Dust", Description = "用于估计局部背景和扩展修复区域的像素半径。", ResourceType = typeof(Properties.Resources))]
        public int DustRepairRadius { get => _DustRepairRadius; set { _DustRepairRadius = Math.Max(1, Math.Min(value, 31)); OnPropertyChanged(); } }
        private int _DustRepairRadius = 3;

        public ColorDifferenceReferenceMode ColorDifferenceReferenceMode { get => _ColorDifferenceReferenceMode; set { _ColorDifferenceReferenceMode = value; OnPropertyChanged(); } }
        private ColorDifferenceReferenceMode _ColorDifferenceReferenceMode = ColorDifferenceReferenceMode.D65;

        public double ColorDifferenceCustomU { get => _ColorDifferenceCustomU; set { _ColorDifferenceCustomU = value; OnPropertyChanged(); } }
        private double _ColorDifferenceCustomU = 0.1978;

        public double ColorDifferenceCustomV { get => _ColorDifferenceCustomV; set { _ColorDifferenceCustomV = value; OnPropertyChanged(); } }
        private double _ColorDifferenceCustomV = 0.4684;

        public string ColorDifferenceReferenceUMatPath { get => _ColorDifferenceReferenceUMatPath; set { _ColorDifferenceReferenceUMatPath = value ?? string.Empty; OnPropertyChanged(); } }
        private string _ColorDifferenceReferenceUMatPath = string.Empty;

        public string ColorDifferenceReferenceVMatPath { get => _ColorDifferenceReferenceVMatPath; set { _ColorDifferenceReferenceVMatPath = value ?? string.Empty; OnPropertyChanged(); } }
        private string _ColorDifferenceReferenceVMatPath = string.Empty;

        public string ColorDifferenceReferenceDisplayName { get => _ColorDifferenceReferenceDisplayName; set { _ColorDifferenceReferenceDisplayName = value ?? string.Empty; OnPropertyChanged(); } }
        private string _ColorDifferenceReferenceDisplayName = string.Empty;

        public ContrastReferenceKind ContrastReferenceKind { get => _ContrastReferenceKind; set { _ContrastReferenceKind = value; OnPropertyChanged(); } }
        private ContrastReferenceKind _ContrastReferenceKind = ContrastReferenceKind.Black;

        public string ContrastBlackReferenceYMatPath { get => _ContrastBlackReferenceYMatPath; set { _ContrastBlackReferenceYMatPath = value ?? string.Empty; OnPropertyChanged(); } }
        private string _ContrastBlackReferenceYMatPath = string.Empty;

        public string ContrastBlackReferenceDisplayName { get => _ContrastBlackReferenceDisplayName; set { _ContrastBlackReferenceDisplayName = value ?? string.Empty; OnPropertyChanged(); } }
        private string _ContrastBlackReferenceDisplayName = string.Empty;

        public string ContrastWhiteReferenceYMatPath { get => _ContrastWhiteReferenceYMatPath; set { _ContrastWhiteReferenceYMatPath = value ?? string.Empty; OnPropertyChanged(); } }
        private string _ContrastWhiteReferenceYMatPath = string.Empty;

        public string ContrastWhiteReferenceDisplayName { get => _ContrastWhiteReferenceDisplayName; set { _ContrastWhiteReferenceDisplayName = value ?? string.Empty; OnPropertyChanged(); } }
        private string _ContrastWhiteReferenceDisplayName = string.Empty;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<ConoscopeNdCalibrationBinding> NdCalibrationBindings
        {
            get => _NdCalibrationBindings;
            set { _NdCalibrationBindings = value; OnPropertyChanged(); }
        }
        private ObservableCollection<ConoscopeNdCalibrationBinding> _NdCalibrationBindings = new();

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ConoscopeAdvancedExportState AdvancedExport
        {
            get => _AdvancedExport;
            set { _AdvancedExport = value ?? new ConoscopeAdvancedExportState(); OnPropertyChanged(); }
        }
        private ConoscopeAdvancedExportState _AdvancedExport = new();

        [Display(Name = "Con_Cfg_SampleInterval", GroupName = "Con_Category_Export", Description = "当前曲线 CSV 导出的默认采样间隔，范围 0.01 到 360 度。", ResourceType = typeof(Properties.Resources))]
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

        [Display(Name = "Con_Cfg_ExportMetadata", GroupName = "Con_Category_Export", Description = "启用后，在当前曲线 CSV 顶部写入标题和元数据信息。", ResourceType = typeof(Properties.Resources))]
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

        [Display(Name = "Con_Cfg_Decimals", GroupName = "Con_Category_Export", Description = "CSV 导出时数据值默认保留的小数位数，范围 0 到 8，默认 4。", ResourceType = typeof(Properties.Resources))]
        public int ExportDecimalPlaces
        {
            get => _ExportDecimalPlaces;
            set
            {
                int normalized = Math.Max(0, Math.Min(value, 8));
                if (_ExportDecimalPlaces == normalized) return;
                _ExportDecimalPlaces = normalized;
                OnPropertyChanged();
            }
        }
        private int _ExportDecimalPlaces = 4;


        public ConoscopeConfig()
        {
            Rendering = new ConoscopeRenderingSettings(this);
            Preprocess = new ConoscopePreprocessSettings(this);
            ColorDifference = new ConoscopeColorDifferenceSettings(this);
            Contrast = new ConoscopeContrastSettings(this);
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

        public bool UsePseudoColor
        {
            get => config.UsePseudoColor;
            set => config.UsePseudoColor = value;
        }

        public bool UsePseudoColorRangeLimit
        {
            get => config.UsePseudoColorRangeLimit;
            set => config.UsePseudoColorRangeLimit = value;
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

    public sealed class ConoscopeContrastSettings
    {
        private readonly ConoscopeConfig config;

        internal ConoscopeContrastSettings(ConoscopeConfig config)
        {
            this.config = config;
        }

        public ContrastReferenceKind ReferenceKind
        {
            get => config.ContrastReferenceKind;
            set => config.ContrastReferenceKind = value;
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

        public int DecimalPlaces
        {
            get => config.ExportDecimalPlaces;
            set => config.ExportDecimalPlaces = value;
        }
    }

    public sealed class ConoscopeAdvancedExportState
    {
        public string FilePrefix { get; set; } = "Conoscope_Export";

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<ExportChannel> Channels { get; set; } = new() { ExportChannel.Y };

        public bool ExportAzimuth { get; set; } = true;
        public bool ExportPolar { get; set; }
        public double AzimuthStep { get; set; } = 1;
        public double RadialStep { get; set; } = 1;
        public double PolarStep { get; set; } = 1;
        public double CircumferentialStep { get; set; } = 1;
        public bool EnableCrossSection { get; set; }
        public bool UseAzimuthCrossSection { get; set; } = true;
        public double CrossSectionAzimuthAngle { get; set; }
        public double CrossSectionPolarAngle { get; set; } = 45;
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
