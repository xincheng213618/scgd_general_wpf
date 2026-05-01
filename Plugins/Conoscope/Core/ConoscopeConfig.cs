using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Conoscope.Core
{
    public class ConoscopeConfig : ViewModelBase, IConfig
    {
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

        public ExportChannel DisplayChannel { get => _DisplayChannel; set { _DisplayChannel = value; OnPropertyChanged(); } }
        private ExportChannel _DisplayChannel = ExportChannel.Y;

        public bool ApplyFilterOnOpen { get => _ApplyFilterOnOpen; set { _ApplyFilterOnOpen = value; OnPropertyChanged(); } }
        private bool _ApplyFilterOnOpen = true;

        [Category("滤波"), DisplayName("滤波类型"), Description("Conoscope 图像打开或手动应用时使用的预处理滤波类型。")]
        public ImageFilterType FilterType { get => _FilterType; set { _FilterType = value; OnPropertyChanged(); } }
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
        public bool DustRemovalEnabled { get => _DustRemovalEnabled; set { _DustRemovalEnabled = value; OnPropertyChanged(); } }
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


        public ConoscopeConfig()
        {
            EnsureProfile(ConoscopeModelType.VA60);
            EnsureProfile(ConoscopeModelType.VA80);
        }

        private static int NormalizeOdd(int value, int min, int max)
        {
            value = Math.Max(min, Math.Min(value, max));
            return value % 2 == 0 ? Math.Min(value + 1, max) : value;
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
