#pragma warning disable CA1051,CA1707,CA1863
using ColorVision.Core;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Media;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Controls;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.HDR;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons.AutoExpTime;
using ColorVision.Engine.Utilities;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using FlowEngineLib.Algorithm;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;


namespace ColorVision.Engine.Services.Devices.Camera
{
    public class DisplayCameraConfig : IDisplayConfigBase
    {
        [Category("AcquisitionDisplay"), DisplayName("使用本地相机")]
        [Description("默认关闭；下次打开相机时生效。修改此项不切换当前会话，取图、自动曝光和关闭始终使用当前已打开的相机。")]
        public bool UseLocalCamera
        {
            get => _useLocalCamera;
            set { if (_useLocalCamera == value) return; _useLocalCamera = value; OnPropertyChanged(); }
        }
        private bool _useLocalCamera;

        [Category("AcquisitionDisplay"), LocalizedDisplayName("Camera_UseHikMvs")]
        [LocalizedDescription("Camera_UseHikMvsHint")]
        public bool UseHikMvs
        {
            get => _useHikMvs;
            set { if (_useHikMvs == value) return; _useHikMvs = value; OnPropertyChanged(); }
        }
        private bool _useHikMvs = true;

        [Category("AcquisitionDisplay"), LocalizedDisplayName("Camera_HikBayerQuality")]
        [LocalizedDescription("Camera_HikBayerQualityHint")]
        public HikBayerQuality HikBayerQuality
        {
            get => _hikBayerQuality;
            set { if (_hikBayerQuality == value) return; _hikBayerQuality = value; OnPropertyChanged(); }
        }
        private HikBayerQuality _hikBayerQuality = HikBayerQuality.OptimalPlus;

        [Category("AcquisitionDisplay"), LocalizedDisplayName("Camera_HikOutputBgr")]
        [LocalizedDescription("Camera_HikOutputBgrHint")]
        public bool HikOutputBgr
        {
            get => _hikOutputBgr;
            set { if (_hikOutputBgr == value) return; _hikOutputBgr = value; OnPropertyChanged(); }
        }
        private bool _hikOutputBgr = true;

        [Category("AcquisitionDisplay"), DisplayName("本地取图保存文件")]
        [Description("默认开启。主面板本地取图及 L/BV 节点本地转发保存 CVRAW，包含已执行的色度校正参数。关闭后仍显示图像并保存结果记录。")]
        public bool SaveLocalCaptureFiles { get; set; } = true;

        public double TakePictureDelay { get; set; }
        public int CalibrationTemplateIndex { get; set; }
        public int ExpTimeParamTemplateIndex { get; set; }
        public int ExpTimeParamTemplate1Index { get; set; }
        [Browsable(false)]
        public PhyExpTimeCfg LocalAutoExposureConfig { get; set; } = new();
        public int HDRTemplateIndex { get; set; }

        public int AutoFocusTemplateIndex { get; set; }

        public double OpenTime { get; set; } = 10;
        public double CloseTime { get; set; } = 10;
        public double LocalVideoOpenTime { get; set; } = 3000;

        [LocalizedDisplayName(nameof(Properties.Resources.Camera_RoiRegion))]
        public Rect LocalVideoRoi
        {
            get => _LocalVideoRoi;
            set
            {
                Rect normalized = NormalizeLocalVideoRoi(value);
                if (_LocalVideoRoi == normalized) return;
                _LocalVideoRoi = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LocalVideoRoiX));
                OnPropertyChanged(nameof(LocalVideoRoiY));
                OnPropertyChanged(nameof(LocalVideoRoiWidth));
                OnPropertyChanged(nameof(LocalVideoRoiHeight));
            }
        }
        private Rect _LocalVideoRoi = new(0, 0, 0, 0);

        [Browsable(false), JsonIgnore]
        public double LocalVideoRoiX { get => LocalVideoRoi.X; set => LocalVideoRoi = new Rect(value, LocalVideoRoi.Y, LocalVideoRoi.Width, LocalVideoRoi.Height); }

        [Browsable(false), JsonIgnore]
        public double LocalVideoRoiY { get => LocalVideoRoi.Y; set => LocalVideoRoi = new Rect(LocalVideoRoi.X, value, LocalVideoRoi.Width, LocalVideoRoi.Height); }

        [Browsable(false), JsonIgnore]
        public double LocalVideoRoiWidth { get => LocalVideoRoi.Width; set => LocalVideoRoi = new Rect(LocalVideoRoi.X, LocalVideoRoi.Y, value, LocalVideoRoi.Height); }

        [Browsable(false), JsonIgnore]
        public double LocalVideoRoiHeight { get => LocalVideoRoi.Height; set => LocalVideoRoi = new Rect(LocalVideoRoi.X, LocalVideoRoi.Y, LocalVideoRoi.Width, value); }

        [LocalizedDisplayName(nameof(Properties.Resources.Camera_EnableCrossGuide))]
        public bool IsCrossGuideEnabled { get => _IsCrossGuideEnabled; set { _IsCrossGuideEnabled = value; OnPropertyChanged(); } }
        private bool _IsCrossGuideEnabled;

        [Browsable(false)]
        [LocalizedDisplayName(nameof(Properties.Resources.Camera_CrossGuideRegion))]
        public Rect CrossGuideRoi
        {
            get => _CrossGuideRoi;
            set
            {
                Rect normalized = NormalizeLocalVideoRoi(value);
                if (_CrossGuideRoi == normalized) return;
                _CrossGuideRoi = normalized;
                OnPropertyChanged();
            }
        }
        private Rect _CrossGuideRoi = Rect.Empty;

        [LocalizedDisplayName(nameof(Properties.Resources.Camera_StandardCenterX))]
        public double CrossGuideStandardCenterX { get => _CrossGuideStandardCenterX; set { _CrossGuideStandardCenterX = value; OnPropertyChanged(); } }
        private double _CrossGuideStandardCenterX;

        [LocalizedDisplayName(nameof(Properties.Resources.Camera_StandardCenterY))]
        public double CrossGuideStandardCenterY { get => _CrossGuideStandardCenterY; set { _CrossGuideStandardCenterY = value; OnPropertyChanged(); } }
        private double _CrossGuideStandardCenterY;

        [LocalizedDisplayName(nameof(Properties.Resources.Camera_TolerancePx))]
        public double CrossGuideTolerancePx { get => _CrossGuideTolerancePx; set { _CrossGuideTolerancePx = Math.Max(0, value); OnPropertyChanged(); } }
        private double _CrossGuideTolerancePx = 3;

        [LocalizedDisplayName(nameof(Properties.Resources.Camera_RefreshIntervalMs))]
        public int CrossGuideIntervalMs { get => _CrossGuideIntervalMs; set { _CrossGuideIntervalMs = Math.Max(50, value); OnPropertyChanged(); } }
        private int _CrossGuideIntervalMs = 300;

        [LocalizedDisplayName(nameof(Properties.Resources.Camera_BrightnessThresholdRatio))]
        public double CrossGuideThresholdRatio { get => _CrossGuideThresholdRatio; set { _CrossGuideThresholdRatio = Math.Clamp(value, 0.05, 0.95); OnPropertyChanged(); } }
        private double _CrossGuideThresholdRatio = 0.45;

        [LocalizedDisplayName(nameof(Properties.Resources.Camera_MinCoverageRatio))]
        public double CrossGuideMinCoverageRatio { get => _CrossGuideMinCoverageRatio; set { _CrossGuideMinCoverageRatio = Math.Clamp(value, 0.01, 0.95); OnPropertyChanged(); } }
        private double _CrossGuideMinCoverageRatio = 0.02;

        [JsonIgnore]
        public string CrossGuideStatus { get => _CrossGuideStatus; set { if (_CrossGuideStatus == value) return; _CrossGuideStatus = value; OnPropertyChanged(); } }
        private string _CrossGuideStatus = string.Empty;

        public ReferenceLineParam ReferenceLineParam { get => _ReferenceLineParam; set { _ReferenceLineParam = value; OnPropertyChanged(); } }
        private ReferenceLineParam _ReferenceLineParam = new ReferenceLineParam();

        public int AvgCount { get => _AvgCount; set { _AvgCount = value; OnPropertyChanged(); } }
        private int _AvgCount = 1;

        public float Gain { get => _Gain; set { _Gain = value; OnPropertyChanged(); } }
        private float _Gain = 10;

        [Browsable(false), JsonIgnore]
        public bool IsGainControlledByCalibrationGroup { get => _IsGainControlledByCalibrationGroup; set { if (_IsGainControlledByCalibrationGroup == value) return; _IsGainControlledByCalibrationGroup = value; OnPropertyChanged(); } }
        private bool _IsGainControlledByCalibrationGroup;

        [Browsable(false), JsonIgnore]
        public string GainSourceHint { get => _GainSourceHint; set { if (_GainSourceHint == value) return; _GainSourceHint = value; OnPropertyChanged(); } }
        private string _GainSourceHint = string.Empty;

        public CVImageFlipMode FlipMode
        {
            get => _FlipMode;
            set { _hasExplicitFlipMode = true; if (_FlipMode == value) return; _FlipMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(LocalVideoTransform)); }
        }
        private CVImageFlipMode _FlipMode = CVImageFlipMode.None;
        private bool _hasExplicitFlipMode;

        // OpenCV X flips rows; the presenter calls that FlipY. Preserve the pixel orientation.
        [Browsable(false), JsonIgnore]
        public int LocalVideoTransform => FlipMode switch
        {
            CVImageFlipMode.X => RealtimeFramePresenter.TransformFlipY,
            CVImageFlipMode.Y => RealtimeFramePresenter.TransformFlipX,
            CVImageFlipMode.XY => RealtimeFramePresenter.TransformFlipXY,
            _ => RealtimeFramePresenter.TransformNone
        };

        [JsonProperty("LocalVideoTransform")]
        private int LegacyVideoTransform
        {
            set
            {
                if (!_hasExplicitFlipMode) _FlipMode = value switch
                {
                    RealtimeFramePresenter.TransformFlipX => CVImageFlipMode.Y,
                    RealtimeFramePresenter.TransformFlipY => CVImageFlipMode.X,
                    RealtimeFramePresenter.TransformFlipXY => CVImageFlipMode.XY,
                    _ => CVImageFlipMode.None
                };
            }
        }

        public double ExpTime { get => _ExpTime; set { _ExpTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeLog)); } }
        private double _ExpTime = 100;
        public double ExpTimeLog { get => Math.Log(ExpTime); set { ExpTime = Math.Pow(Math.E, value); } }

        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeRLog)); } }
        private double _ExpTimeR = 100;

        public double ExpTimeRLog { get => Math.Log(ExpTimeR); set { ExpTimeR = Math.Pow(Math.E, value); } }

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeGLog)); } }
        private double _ExpTimeG = 100;
        public double ExpTimeGLog { get => Math.Log(ExpTimeG); set { ExpTimeG = Math.Pow(Math.E, value); } }

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeBLog)); } }
        private double _ExpTimeB = 100;

        public double ExpTimeBLog { get => Math.Log(ExpTimeB); set { ExpTimeB = Math.Pow(Math.E, value); } }


        public double Saturation { get => _Saturation; set { _Saturation = value; OnPropertyChanged(); } }
        private double _Saturation = -1;

        public double SaturationR { get => _SaturationR; set { _SaturationR = value; OnPropertyChanged(); } }
        private double _SaturationR = -1;

        public double SaturationG { get => _SaturationG; set { _SaturationG = value; OnPropertyChanged(); } }
        private double _SaturationG = -1;

        public double SaturationB { get => _SaturationB; set { _SaturationB = value; OnPropertyChanged(); } }
        private double _SaturationB = -1;

        [Browsable(false)]
        public DisplayShaderFilterState DisplayShaderFilter { get => _DisplayShaderFilter; set { _DisplayShaderFilter = value ?? new DisplayShaderFilterState(); OnPropertyChanged(); } }
        private DisplayShaderFilterState _DisplayShaderFilter = new DisplayShaderFilterState();

        [JsonIgnore]
        public bool IsLocalVideoOpen { get => _IsLocalVideoOpen; set { _IsLocalVideoOpen = value; OnPropertyChanged(); } }
        private bool _IsLocalVideoOpen;

        private static Rect NormalizeLocalVideoRoi(Rect value)
        {
            if (value.IsEmpty) return new Rect(0, 0, 0, 0);

            return new Rect(
                Math.Max(0, value.X),
                Math.Max(0, value.Y),
                Math.Max(0, value.Width),
                Math.Max(0, value.Height));
        }
    }


    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class DisplayCamera : UserControl, IDisPlayControl, IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayCamera));
        public DeviceCamera Device { get; set; }
        public MQTTCamera DService { get => Device.DService; }
        public DisplayCameraConfig DisplayCameraConfig => Device.DisplayConfig;

        public ViewCamera View => Device.View;
        public string DisPlayName => Device.Config.Name;
        public string PersistenceKey => Device.Config.Code;

        private readonly ObservableCollection<AutoExpTimeTemplateOption> _autoExpTimeTemplateOptions = new();
        private readonly ObservableCollection<AutoExpTimeTemplateOption> _autoExpTimeTemplateOptionsWithEmpty = new();

        // Video display related fields
        private readonly CameraRealtimeFramePipeline _localRealtimePipeline;
        private readonly VideoCrossGuideProcessor _crossGuideProcessor;
        private readonly CrossGuideOverlayVisual _crossGuideOverlayVisual;
        private bool _isOpeningLocalVideo;
        private MsgRecord? _panelOperation;
        private Configs.ConfigCamera? _panelConfig;
        private DVRectangleText? _localVideoRoiVisual;
        private bool _isSyncingLocalVideoRoi;
        private bool _hasLocalVideoImageEditModeSnapshot;
        private bool _localVideoImageEditModeSnapshot;
        private bool _isLocalVideoRoiVisualRemoveSubscribed;
        private bool _crossGuideOverlayAdded;
        private readonly object _localVideoHandleSync = new();
        private readonly CameraPreviewParameterQueue _previewParameterUpdates;
        private int _disposeState;
        private bool _isInitialized;
        private bool _templateOptionsAreLocal;

        private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

        private enum AutoExpTimeTemplateKind
        {
            Empty,
            V1Detail,
            V2Json,
            LocalDefault
        }

        private sealed class AutoExpTimeTemplateOption
        {
            public string DisplayName { get; init; } = string.Empty;
            public ParamBase Value { get; init; } = new();
            public AutoExpTimeTemplateKind Kind { get; init; }
        }

        public DisplayCamera(DeviceCamera device)
        {
            Device = device;
            _localRealtimePipeline = new CameraRealtimeFramePipeline();
            _crossGuideProcessor = new VideoCrossGuideProcessor(HandleCrossGuideResult);
            _crossGuideOverlayVisual = new CrossGuideOverlayVisual();
            _previewParameterUpdates = new CameraPreviewParameterQueue(ApplyPreviewParameterCore, ex => logger.Error("更新本地预览参数失败", ex));
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (IsDisposed || _isInitialized) return;
            _isInitialized = true;

            DataContext = Device;
            this.AddViewConfig(Device.ViewRegistration, DisPlayName);
            EnsureTimedButtonOperations();

            Actions.OpenButton.Click += Open_Click;
            Actions.CloseButton.Click += Close_Click;
            Actions.ButtonOffline.Click += CameraOffline_Click;
            Actions.LocalVideoButton.Click += Video1_Click;
            Capture.TakePhotoButton.Click += GetData_Click;
            Capture.AutoExposureButton.Click += AutoExplose_Click;
            Capture.EditExposureButton.Click += EditAutoExpTime;
            Capture.EditCalibrationButton.Click += MenuItem_Template;
            Capture.ComboxCalibrationTemplate.SelectionChanged += ComboxCalibrationTemplate_SelectionChanged;
            Capture.EditCaptureExposureButton.Click += EditAutoExpTime1;
            Capture.ComboxAutoExpTimeParamTemplate1.SelectionChanged += ComboxAutoExpTimeParamTemplate1_SelectionChanged;
            Capture.EditHdrButton.Click += EditHDRTemplate;
            Capture.ReadFilterButton.Click += GetNDport_Click;
            Capture.ChangeFilterButton.Click += NDport_Click;
            Capture.AutoFocusButton.Click += AutoFocus_Click;
            Capture.EditFocusButton.Click += EditAutoFocus;
            Capture.MoveButton.Click += Move_Click;
            Capture.ApertureButton.Click += Move1_Click;
            Capture.HomeButton.Click += GoHome_Click;
            Capture.ReadPositionButton.Click += GetPosition_Click;
            Capture.ComboBoxHDRTemplate.SelectionChanged += (_, _) => RefreshPanelState();
            Preview.DefaultRegionButton.Click += LocalVideoRoiDefault_Click;
            Preview.FullFrameButton.Click += LocalVideoRoiFull_Click;

            UpdateCalibrationTemplates();
            Device.ConfigChanged += Device_ConfigChanged;
            Device.PropertyChanged += CameraPanel_PropertyChanged;
            _panelConfig = Device.Config;
            _panelConfig.PropertyChanged += CameraPanel_PropertyChanged;

            Capture.ComboxCalibrationTemplate.DataContext = Device.DisplayConfig;
            PhyCameraManager.GetInstance().Loaded += PhyCameraManager_Loaded;
            BindAutoExpTimeTemplateSources();

            Capture.ComboxAutoExpTimeParamTemplate.ItemsSource = _autoExpTimeTemplateOptions;
            Capture.ComboxAutoExpTimeParamTemplate.SelectedIndex = 0;
            Capture.ComboxAutoExpTimeParamTemplate.DataContext = Device.DisplayConfig;

            Capture.ComboxAutoExpTimeParamTemplate1.ItemsSource = _autoExpTimeTemplateOptionsWithEmpty;
            Capture.ComboxAutoExpTimeParamTemplate1.SelectedIndex = 0;
            Capture.ComboxAutoExpTimeParamTemplate1.DataContext = Device.DisplayConfig;

            Capture.ComboxAutoFocus.ItemsSource = TemplateAutoFocus.Params;
            Capture.ComboxAutoFocus.SelectedIndex = 0;
            Capture.ComboxAutoFocus.DataContext = Device.DisplayConfig;

            Capture.ComboBoxHDRTemplate.ItemsSource = TemplateHDR.Params.CreateEmpty();
            Capture.ComboBoxHDRTemplate.SelectedIndex = 0;
            Capture.ComboBoxHDRTemplate.DataContext = Device.DisplayConfig;

            DisplayCameraConfig.PropertyChanged += DisplayCameraConfig_PropertyChanged;
            Device.RealtimeCameraConfig.PropertyChanged += RealtimeCameraConfig_PropertyChanged;
            ApplyLocalVideoRoiToRealtimeConfig();

            Capture.CBFilp.ItemsSource = from e1 in Enum.GetValues<CVImageFlipMode>().Cast<CVImageFlipMode>()
                                 select new KeyValuePair<CVImageFlipMode, string>(e1, e1.ToString());

            DService_DeviceStatusChanged(sender, DService.DeviceStatus);
            DService.DeviceStatusChanged += DService_DeviceStatusChanged;
            this.ApplyChangedSelectedColor(DisPlayBorder);

        }

        private void Device_ConfigChanged(object? sender, EventArgs e)
        {
            if (_panelConfig != null) _panelConfig.PropertyChanged -= CameraPanel_PropertyChanged;
            _panelConfig = Device.Config;
            _panelConfig.PropertyChanged += CameraPanel_PropertyChanged;
            UpdateCalibrationTemplates();
            RefreshPanelState();
        }

        private void PhyCameraManager_Loaded(object? sender, EventArgs e) => UpdateCalibrationTemplates();

        private void UpdateCalibrationTemplates()
        {
            if (IsDisposed) return;

            Capture.ComboxCalibrationTemplate.ItemsSource = (Device.PhyCamera?.CalibrationParams).CreateEmpty();
            Capture.ComboxCalibrationTemplate.SelectedIndex = 0;
        }

        private void ComboxCalibrationTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IEnumerable<GroupResource> groups = Device.PhyCamera?.VisualChildren.OfType<GroupResource>() ?? Enumerable.Empty<GroupResource>();
            CalibrationGroupGainResolver.Synchronize(DisplayCameraConfig, Capture.ComboxCalibrationTemplate.SelectedValue as CalibrationParam, groups);
        }

        private void BindAutoExpTimeTemplateSources()
        {
            TemplateAutoExpTime.Params.CollectionChanged -= AutoExpTimeTemplateParams_CollectionChanged;
            TemplateAutoExpTime.Params.CollectionChanged += AutoExpTimeTemplateParams_CollectionChanged;
            TemplateAutoExpTimeV2.Params.CollectionChanged -= AutoExpTimeTemplateParams_CollectionChanged;
            TemplateAutoExpTimeV2.Params.CollectionChanged += AutoExpTimeTemplateParams_CollectionChanged;
            RefreshAutoExpTimeTemplateOptions();
        }

        private void AutoExpTimeTemplateParams_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshAutoExpTimeTemplateOptions();
        }

        private void RefreshAutoExpTimeTemplateOptions()
        {
            var selectedOption = Capture.ComboxAutoExpTimeParamTemplate?.SelectedItem as AutoExpTimeTemplateOption;
            var selectedOptionWithEmpty = Capture.ComboxAutoExpTimeParamTemplate1?.SelectedItem as AutoExpTimeTemplateOption;

            _autoExpTimeTemplateOptions.Clear();
            _templateOptionsAreLocal = Device.RoutesLocally;
            foreach (var option in EnumerateAutoExpTimeTemplateOptions())
                _autoExpTimeTemplateOptions.Add(option);
            if (_templateOptionsAreLocal && _autoExpTimeTemplateOptions.Count == 0)
                _autoExpTimeTemplateOptions.Add(new AutoExpTimeTemplateOption
                {
                    DisplayName = EngineLocalization.Get("CameraPanel_LocalExposure"),
                    Value = new ParamBase { Id = -2 }, Kind = AutoExpTimeTemplateKind.LocalDefault
                });
            _autoExpTimeTemplateOptionsWithEmpty.Clear();
            _autoExpTimeTemplateOptionsWithEmpty.Add(new AutoExpTimeTemplateOption
            {
                DisplayName = EngineLocalization.Get("CameraPanel_NotUsed"),
                Value = new ParamBase { Id = -1, Name = "Empty" },
                Kind = AutoExpTimeTemplateKind.Empty
            });
            foreach (var option in _autoExpTimeTemplateOptions)
                _autoExpTimeTemplateOptionsWithEmpty.Add(option);

            RestoreAutoExpTimeSelection(Capture.ComboxAutoExpTimeParamTemplate, _autoExpTimeTemplateOptions, selectedOption, 0);
            RestoreAutoExpTimeSelection(Capture.ComboxAutoExpTimeParamTemplate1, _autoExpTimeTemplateOptionsWithEmpty, selectedOptionWithEmpty, 0);
        }

        private static IEnumerable<AutoExpTimeTemplateOption> EnumerateAutoExpTimeTemplateOptions()
        {
            foreach (var template in TemplateAutoExpTime.Params)
            {
                yield return new AutoExpTimeTemplateOption
                {
                    DisplayName = $"[V1] {template.Key}",
                    Value = template.Value,
                    Kind = AutoExpTimeTemplateKind.V1Detail
                };
            }

            foreach (var template in TemplateAutoExpTimeV2.Params)
            {
                yield return new AutoExpTimeTemplateOption
                {
                    DisplayName = $"[V2] {template.Key}",
                    Value = template.Value,
                    Kind = AutoExpTimeTemplateKind.V2Json
                };
            }
        }

        private static void RestoreAutoExpTimeSelection(ComboBox? comboBox, ObservableCollection<AutoExpTimeTemplateOption> options, AutoExpTimeTemplateOption? previousSelection, int defaultIndex)
        {
            if (comboBox == null || comboBox.ItemsSource == null || options.Count == 0)
                return;

            if (previousSelection != null)
            {
                var matched = options.FirstOrDefault(option => option.Kind == previousSelection.Kind && option.Value.Id == previousSelection.Value.Id);
                if (matched != null)
                {
                    comboBox.SelectedItem = matched;
                    return;
                }
            }

            comboBox.SelectedIndex = defaultIndex >= 0 && defaultIndex < options.Count ? defaultIndex : -1;
        }

        private void DisplayCameraConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsDisposed || _isSyncingLocalVideoRoi) return;
            if (e.PropertyName == nameof(DisplayCameraConfig.IsLocalVideoOpen))
            {
                _previewParameterUpdates.Clear();
                EnsureTimedButtonOperations().RefreshIdleState(Actions.LocalVideoButton);
                RefreshPanelState();
            }
            if (e.PropertyName is nameof(DisplayCameraConfig.ExpTime) or nameof(DisplayCameraConfig.Gain))
                ApplyPreviewParameter(e.PropertyName == nameof(DisplayCameraConfig.ExpTime));
            if (e.PropertyName == nameof(DisplayCameraConfig.LocalVideoTransform))
            {
                _localRealtimePipeline.Transform = DisplayCameraConfig.LocalVideoTransform;
                _crossGuideOverlayVisual.Clear();
                _crossGuideProcessor.Reset();
            }
            if (e.PropertyName is nameof(DisplayCameraConfig.IsGainControlledByCalibrationGroup) or nameof(DisplayCameraConfig.GainSourceHint)) RefreshPanelState();

            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(DisplayCameraConfig.LocalVideoRoi))
            {
                ApplyLocalVideoRoiToRealtimeConfig();
                RefreshLocalVideoRoiVisual(selectNewVisual: true);
                RefreshCrossGuideOverlay();
                SaveDisplayConfig();
            }

            if (IsCrossGuideConfigProperty(e.PropertyName))
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(DisplayCameraConfig.IsCrossGuideEnabled))
                    RefreshLocalVideoRoiVisual(selectNewVisual: true);

                RefreshCrossGuideOverlay();
                if (e.PropertyName != nameof(DisplayCameraConfig.CrossGuideStatus))
                    SaveDisplayConfig();
            }
        }

        private static bool IsCrossGuideConfigProperty(string? propertyName) => string.IsNullOrEmpty(propertyName)
            || propertyName == nameof(DisplayCameraConfig.IsCrossGuideEnabled)
            || propertyName == nameof(DisplayCameraConfig.CrossGuideRoi)
            || propertyName == nameof(DisplayCameraConfig.CrossGuideStandardCenterX)
            || propertyName == nameof(DisplayCameraConfig.CrossGuideStandardCenterY)
            || propertyName == nameof(DisplayCameraConfig.CrossGuideTolerancePx)
            || propertyName == nameof(DisplayCameraConfig.CrossGuideIntervalMs)
            || propertyName == nameof(DisplayCameraConfig.CrossGuideThresholdRatio)
            || propertyName == nameof(DisplayCameraConfig.CrossGuideMinCoverageRatio);

        private void ApplyLocalVideoRoiToRealtimeConfig()
        {
            RectangleTextProperties rectangle = Device.RealtimeCameraConfig.RectangleTextProperties;
            rectangle.Rect = DisplayCameraConfig.LocalVideoRoi;
            rectangle.Brush = Brushes.Transparent;
            rectangle.Pen = new Pen(Brushes.LimeGreen, 4);
            rectangle.Foreground = Brushes.DarkOrange;
            rectangle.Position = RectangleTextPosition.Top;
            rectangle.IsShowText = true;
            if (rectangle.FontSize <= 0) rectangle.FontSize = 200;
        }

        private bool IsRealtimeArticulationEnabled => Device.RealtimeCameraConfig.IsCalArtculation;

        private bool IsLocalVideoRoiVisualNeeded => IsRealtimeArticulationEnabled || DisplayCameraConfig.IsCrossGuideEnabled;

        private static bool IsVisibleLocalVideoRoi(Rect rect) => rect.Width > 0 && rect.Height > 0;

        private static RectangleTextProperties CreateLocalVideoRoiVisualProperties(Rect rect)
        {
            return new RectangleTextProperties
            {
                Rect = rect,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.LimeGreen, 4),
                Foreground = Brushes.LimeGreen,
                Text = string.Empty,
                Position = RectangleTextPosition.Top,
                IsShowText = false
            };
        }

        private void RefreshLocalVideoRoiVisual(bool selectNewVisual = false)
        {
            if (!Device.DisplayConfig.IsLocalVideoOpen || !IsLocalVideoRoiVisualNeeded)
            {
                RemoveLocalVideoRoiVisual(restoreImageEditMode: true);
                return;
            }

            if (IsVisibleLocalVideoRoi(DisplayCameraConfig.LocalVideoRoi))
            {
                EnsureLocalVideoRoiVisual(selectNewVisual && _localVideoRoiVisual == null);
            }
            else
            {
                RemoveLocalVideoRoiVisual(restoreImageEditMode: false);
            }
        }

        private void EnsureLocalVideoRoiVisual(bool select = true)
        {
            if (IsDisposed || Device.ExistingView is not { IsContentInitialized: true }) return;

            if (!IsLocalVideoRoiVisualNeeded)
            {
                RemoveLocalVideoRoiVisual(restoreImageEditMode: true);
                return;
            }

            var imageView = Device.View.ImageView;
            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.Invoke(() => EnsureLocalVideoRoiVisual(select));
                return;
            }

            Rect roi = DisplayCameraConfig.LocalVideoRoi;
            if (!IsVisibleLocalVideoRoi(roi))
            {
                RemoveLocalVideoRoiVisual(restoreImageEditMode: false);
                return;
            }

            if (_localVideoRoiVisual == null)
            {
                RectangleTextProperties properties = CreateLocalVideoRoiVisualProperties(roi);
                _localVideoRoiVisual = new DVRectangleText(properties);
                _localVideoRoiVisual.TextAttribute.FontSize = Math.Max(_localVideoRoiVisual.Pen.Thickness * 10, 12);
                _localVideoRoiVisual.Render();
                properties.PropertyChanged += LocalVideoRoiVisual_PropertyChanged;
            }

            SyncLocalVideoRoiVisualFromConfig();

            if (!imageView.ImageShow.ContainsVisual(_localVideoRoiVisual))
            {
                imageView.ImageShow.AddVisual(_localVideoRoiVisual);
                SubscribeLocalVideoRoiRemoveEvent();
            }

            imageView.ImageShow.TopVisual(_localVideoRoiVisual);

            if (select)
            {
                CaptureLocalVideoImageEditMode(imageView);
                if (!imageView.ImageEditMode)
                {
                    imageView.ImageEditMode = true;
                }
                imageView.EditorContext.DrawEditorContext.SelectionVisual.SetRender(_localVideoRoiVisual);
            }
        }

        private void SyncLocalVideoRoiVisualFromConfig()
        {
            if (_localVideoRoiVisual == null) return;

            Rect roi = DisplayCameraConfig.LocalVideoRoi;
            if (_localVideoRoiVisual.Rect == roi) return;

            try
            {
                _isSyncingLocalVideoRoi = true;
                _localVideoRoiVisual.Rect = roi;
                _localVideoRoiVisual.Render();
            }
            finally
            {
                _isSyncingLocalVideoRoi = false;
            }
        }

        private void LocalVideoRoiVisual_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingLocalVideoRoi || _localVideoRoiVisual == null) return;
            if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(RectangleProperties.Rect)) return;

            try
            {
                _isSyncingLocalVideoRoi = true;
                DisplayCameraConfig.LocalVideoRoi = _localVideoRoiVisual.Rect;
            }
            finally
            {
                _isSyncingLocalVideoRoi = false;
            }

            ApplyLocalVideoRoiToRealtimeConfig();
            SyncLocalVideoRoiVisualFromConfig();
            SaveDisplayConfig();
        }

        private void RealtimeCameraConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (IsDisposed) return;
            if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(DefaultRealtimeCameraConfig.IsCalArtculation)) return;

            RefreshLocalVideoRoiVisual(selectNewVisual: true);
        }

        private void CaptureLocalVideoImageEditMode(ColorVision.ImageEditor.ImageView imageView)
        {
            if (_hasLocalVideoImageEditModeSnapshot) return;

            _localVideoImageEditModeSnapshot = imageView.ImageEditMode;
            _hasLocalVideoImageEditModeSnapshot = true;
        }

        private void RestoreLocalVideoImageEditMode(ColorVision.ImageEditor.ImageView imageView)
        {
            if (!_hasLocalVideoImageEditModeSnapshot) return;

            imageView.ImageEditMode = _localVideoImageEditModeSnapshot;
            _hasLocalVideoImageEditModeSnapshot = false;
        }

        private void SubscribeLocalVideoRoiRemoveEvent()
        {
            if (_isLocalVideoRoiVisualRemoveSubscribed) return;

            Device.View.ImageView.ImageShow.VisualsRemove += ImageShow_VisualsRemoveLocalVideoRoi;
            _isLocalVideoRoiVisualRemoveSubscribed = true;
        }

        private void UnsubscribeLocalVideoRoiRemoveEvent()
        {
            if (!_isLocalVideoRoiVisualRemoveSubscribed) return;

            if (Device.ExistingView is { IsContentInitialized: true } view)
                view.ImageView.ImageShow.VisualsRemove -= ImageShow_VisualsRemoveLocalVideoRoi;
            _isLocalVideoRoiVisualRemoveSubscribed = false;
        }

        private void ImageShow_VisualsRemoveLocalVideoRoi(object? sender, VisualChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Visual, _localVideoRoiVisual) || _localVideoRoiVisual == null) return;

            _localVideoRoiVisual.Attribute.PropertyChanged -= LocalVideoRoiVisual_PropertyChanged;
            _localVideoRoiVisual = null;
            UnsubscribeLocalVideoRoiRemoveEvent();

            if (_isSyncingLocalVideoRoi) return;

            try
            {
                _isSyncingLocalVideoRoi = true;
                DisplayCameraConfig.LocalVideoRoi = new Rect(0, 0, 0, 0);
            }
            finally
            {
                _isSyncingLocalVideoRoi = false;
            }

            ApplyLocalVideoRoiToRealtimeConfig();
            SaveDisplayConfig();
        }

        private void RemoveLocalVideoRoiVisual(bool restoreImageEditMode)
        {
            if (Device.ExistingView is not { IsContentInitialized: true }) return;
            var imageView = Device.View.ImageView;
            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.Invoke(() => RemoveLocalVideoRoiVisual(restoreImageEditMode));
                return;
            }

            DVRectangleText? visual = _localVideoRoiVisual;
            _localVideoRoiVisual = null;
            UnsubscribeLocalVideoRoiRemoveEvent();

            if (visual != null)
            {
                visual.Attribute.PropertyChanged -= LocalVideoRoiVisual_PropertyChanged;
                if (ReferenceEquals(imageView.EditorContext.DrawEditorContext.SelectionVisual.PrimarySelectedVisual, visual))
                {
                    imageView.EditorContext.DrawEditorContext.SelectionVisual.ClearRender();
                }
                if (imageView.ImageShow.ContainsVisual(visual))
                {
                    imageView.ImageShow.RemoveVisual(visual);
                }
            }

            if (restoreImageEditMode)
            {
                RestoreLocalVideoImageEditMode(imageView);
            }
        }

        private void LocalVideoRoiDefault_Click(object sender, RoutedEventArgs e)
        {
            DisplayCameraConfig.LocalVideoRoi = CreateCenteredLocalVideoRoi();
            RefreshLocalVideoRoiVisual(selectNewVisual: true);
        }

        private Rect CreateCenteredLocalVideoRoi()
        {
            if (!TryGetLocalVideoFrameSize(out int width, out int height))
                return new Rect(0, 0, 0, 0);

            double roiWidth = Math.Clamp(Math.Round(width * 0.35), 1, width);
            double roiHeight = Math.Clamp(Math.Round(height * 0.35), 1, height);
            double x = Math.Round((width - roiWidth) / 2);
            double y = Math.Round((height - roiHeight) / 2);
            return new Rect(x, y, roiWidth, roiHeight);
        }

        private bool TryGetLocalVideoFrameSize(out int width, out int height)
        {
            if (Device.ExistingView is not { IsContentInitialized: true })
            {
                width = height = 0;
                return false;
            }
            width = Device.View.ImageView.Config.GetProperties<int>(ImageViewPropertyKeys.Cols);
            height = Device.View.ImageView.Config.GetProperties<int>(ImageViewPropertyKeys.Rows);
            if (width > 0 && height > 0) return true;

            if (Device.View.ImageView.ImageShow.Source is System.Windows.Media.Imaging.BitmapSource bitmapSource)
            {
                width = bitmapSource.PixelWidth;
                height = bitmapSource.PixelHeight;
                return width > 0 && height > 0;
            }

            width = 0;
            height = 0;
            return false;
        }

        private void LocalVideoRoiFull_Click(object sender, RoutedEventArgs e)
        {
            DisplayCameraConfig.LocalVideoRoi = new Rect(0, 0, 0, 0);
            RemoveLocalVideoRoiVisual(restoreImageEditMode: false);
        }

        private void RefreshCrossGuideOverlay()
        {
            if (IsDisposed) return;

            _localRealtimePipeline.IsMetricsVisible = !Device.DisplayConfig.IsCrossGuideEnabled;

            if (!Device.DisplayConfig.IsLocalVideoOpen || !Device.DisplayConfig.IsCrossGuideEnabled)
            {
                RemoveCrossGuideOverlay();
                if (!Device.DisplayConfig.IsCrossGuideEnabled)
                {
                    Device.DisplayConfig.CrossGuideStatus = string.Empty;
                }
                return;
            }

            EnsureCrossGuideOverlay();
        }

        private void EnsureCrossGuideOverlay()
        {
            if (IsDisposed || Device.ExistingView is not { IsContentInitialized: true }) return;

            var imageView = Device.View.ImageView;
            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.BeginInvoke(new Action(EnsureCrossGuideOverlay));
                return;
            }

            _crossGuideOverlayVisual.Attach();
            if (_crossGuideOverlayAdded && imageView.ImageShow.ContainsVisual(_crossGuideOverlayVisual)) return;

            imageView.ImageShow.AddOverlayVisual(_crossGuideOverlayVisual);
            _crossGuideOverlayAdded = true;
        }

        private void RemoveCrossGuideOverlay()
        {
            if (Device.ExistingView is not { IsContentInitialized: true }) return;
            var imageView = Device.View.ImageView;
            if (!imageView.Dispatcher.CheckAccess())
            {
                imageView.Dispatcher.BeginInvoke(new Action(RemoveCrossGuideOverlay));
                return;
            }

            _crossGuideOverlayVisual.Detach();
            _crossGuideOverlayVisual.Clear();
            if (_crossGuideOverlayAdded || imageView.ImageShow.ContainsVisual(_crossGuideOverlayVisual))
            {
                imageView.ImageShow.RemoveOverlayVisual(_crossGuideOverlayVisual);
                _crossGuideOverlayAdded = false;
            }

            _crossGuideProcessor.Reset();
        }

        private bool TryCreateCrossGuideRequest(int width, int height, out VideoCrossGuideRequest request)
        {
            request = default;
            if (Device.ExistingView is not { IsContentInitialized: true }
                || !Device.DisplayConfig.IsLocalVideoOpen || !Device.DisplayConfig.IsCrossGuideEnabled) return false;
            if (width <= 0 || height <= 0) return false;

            int transform = Device.DisplayConfig.LocalVideoTransform;
            RoiRect sourceRoi = VideoCrossGuideDetector.TransformDisplayRoiToSource(Device.DisplayConfig.LocalVideoRoi, width, height, transform);
            Point standardCenter = new(Device.DisplayConfig.CrossGuideStandardCenterX, Device.DisplayConfig.CrossGuideStandardCenterY);
            request = new VideoCrossGuideRequest(
                sourceRoi,
                standardCenter,
                transform,
                Device.DisplayConfig.CrossGuideIntervalMs,
                Device.DisplayConfig.CrossGuideThresholdRatio,
                Device.DisplayConfig.CrossGuideMinCoverageRatio,
                Device.DisplayConfig.CrossGuideTolerancePx);
            return true;
        }

        private void HandleCrossGuideResult(VideoCrossGuideResult result)
        {
            if (IsDisposed) return;

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Device.ExistingView is not { IsContentInitialized: true }
                    || !Device.DisplayConfig.IsLocalVideoOpen || !Device.DisplayConfig.IsCrossGuideEnabled)
                    return;

                EnsureCrossGuideOverlay();
                _crossGuideOverlayVisual.Update(result, _localRealtimePipeline.CurrentMetrics);
                Device.DisplayConfig.CrossGuideStatus = BuildCrossGuideStatus(result);
            }));
        }

        private static string BuildCrossGuideStatus(VideoCrossGuideResult result)
        {
            if (!result.Found) return result.Message;

            string state = result.IsPass ? "PASS" : "NG";
            return $"dx(center):{result.OffsetX:F2}px  dy(center):{result.OffsetY:F2}px  d:{result.Distance:F2}px  Rotation:{result.RotationZDeg:+0.00;-0.00;0.00}deg  XRotation:{result.XRotationDeg:+0.00;-0.00;0.00}deg  YRotation:{result.YRotationDeg:+0.00;-0.00;0.00}deg  {state}";
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e) => RefreshPanelState();

        private void CameraPanel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DeviceCamera.RoutesLocally) or nameof(DeviceCamera.ServiceControlsEnabled)
                or nameof(Configs.ConfigCamera.IsExpThree) or nameof(Configs.ConfigCamera.IsAutoExpWithND))
                RefreshPanelState();
        }

        private void RefreshPanelState()
        {
            if (IsDisposed) return;
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(RefreshPanelState); return; }
            bool previewing = DisplayCameraConfig.IsLocalVideoOpen;
            CameraPanelState state = CameraPanelState.Create(Device.CameraBackend, previewing, _isOpeningLocalVideo || _panelOperation != null);
            static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
            if (_templateOptionsAreLocal != Device.RoutesLocally) RefreshAutoExpTimeTemplateOptions();
            CameraPanelState idle = CameraPanelState.Create(Device.CameraBackend, previewing, false);
            bool opening = _panelOperation?.MsgSend?.EventName == "Open";
            bool closing = _panelOperation?.MsgSend?.EventName == "Close";
            Actions.ToolTip = state.Busy ? EngineLocalization.Get(state.StatusKey) : null;
            Actions.OpenButton.Visibility = Visible(opening || (!state.Connected && !closing && (idle.CanConnect || state.Busy)));
            Actions.OpenButton.IsEnabled = state.CanConnect;
            Actions.CloseButton.Visibility = Visible(!opening && (state.Connected || closing));
            Actions.CloseButton.IsEnabled = !state.Busy;
            Actions.LocalVideoButton.IsEnabled = state.CanPreview;
            Capture.TakePhotoButton.IsEnabled = state.CanCapture;
            Actions.LocalVideoButton.Visibility = Visible(idle.CanPreview || (state.Busy && Device.CameraBackend.LocalOwned));
            Capture.TakePhotoButton.Visibility = Visible(!previewing && (idle.CanCapture || (state.Busy && !Device.CameraBackend.VideoOwned)));
            Actions.VideoColumn.Width = Actions.LocalVideoButton.Visibility == Visibility.Visible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            Actions.LocalVideoButton.ToolTip = state.ShowServiceSettings && state.Connected
                ? EngineLocalization.Get("CameraPanel_ServicePreviewHint") : null;
            bool showProblem = !state.Connected && !state.CanConnect && !state.Busy;
            DeviceStatusType status = DService.DeviceStatus;
            Actions.TextBlockUnknow.Visibility = Visible(showProblem && status == DeviceStatusType.Unknown);
            Actions.ButtonUnauthorized.Visibility = Visible(showProblem && status == DeviceStatusType.Unauthorized);
            Actions.ButtonInit.Visibility = Visible(showProblem && status == DeviceStatusType.UnInit);
            Actions.ButtonOffline.Visibility = Visible(showProblem && status == DeviceStatusType.OffLine);
            Capture.Visibility = Visible(state.ShowParameters);
            Capture.IsEnabled = !state.Busy;
            Capture.AutoExposureButton.IsEnabled = state.CanCapture;
            Capture.AutoExposureButton.Visibility = Visible(_autoExpTimeTemplateOptions.Count > 0);
            Capture.AutoExposureButton.ToolTip = Device.RoutesLocally ? null : EngineLocalization.Get("独立自曝沿用相机当前增益；拍前自曝使用本次取图增益");
            Capture.CaptureOnlyPanel.Visibility = Visible(!previewing && !Device.CameraBackend.VideoOwned);
            Capture.AveragePanel.Visibility = Visible(!previewing && !Device.CameraBackend.VideoOwned);
            bool calibrationGain = DisplayCameraConfig.IsGainControlledByCalibrationGroup;
            bool hdrGain = state.ShowServiceSettings && CameraTemplateSelection.TryResolveRequired(Capture.ComboBoxHDRTemplate.SelectedValue, out ParamBase _);
            Capture.GainPanel.Visibility = Visible(previewing || calibrationGain || !hdrGain);
            Capture.GainPanel.IsEnabled = previewing || !calibrationGain;
            Capture.GainPanel.ToolTip = calibrationGain
                ? previewing ? EngineLocalization.Get("预览增益；测量使用校正组增益") : DisplayCameraConfig.GainSourceHint
                : null;
            bool rgb = !previewing && UsesThreeCaptureExposures;
            Capture.SingleExposure.Visibility = Visible(!rgb);
            Capture.RgbExposure.Visibility = Visible(rgb);
            Capture.AutoExposureTemplatePanel.Visibility = Visibility.Visible;
            Capture.CaptureAutoExposureTemplatePanel.Visibility = Visibility.Visible;
            Capture.HdrPanel.Visibility = Visible(state.ShowServiceSettings);
            // A previously enabled ND option remains reachable so it can be cleared locally.
            Capture.AutoExposureND.Visibility = Visible(Device.Config.IsAutoExpWithND || (state.ShowServiceSettings && Device.Config.CFW.IsNDPort));
            Preview.Visibility = Visible(previewing);
            Preview.IsEnabled = !state.Busy;
        }

        private void TrackPanelOperation(MsgRecord record)
        {
            _panelOperation = record;
            record.MsgRecordStateChanged += PanelOperation_StateChanged;
            RefreshPanelState();
            PanelOperation_StateChanged(record, record.MsgRecordState);
        }

        private void PanelOperation_StateChanged(object? sender, MsgRecordState state)
        {
            if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)) return;
            if (sender is MsgRecord record) record.MsgRecordStateChanged -= PanelOperation_StateChanged;
            Dispatcher.BeginInvoke(() =>
            {
                if (!ReferenceEquals(_panelOperation, sender)) return;
                _panelOperation = null;
                RefreshPanelState();
            });
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void CameraOffline_Click(object sender, RoutedEventArgs e)
        {
            ServicesHelper.SendCommandEx(sender, DService.GetCameraID);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (_panelOperation != null || _isOpeningLocalVideo) return;
            if (sender is Button button)
            {
                EnsureTimedButtonOperations();
                MsgRecord msgRecord;
                try { msgRecord = DService.Open(DService.Config.CameraID, Device.Config.TakeImageMode, (int)DService.Config.ImageBpp); }
                catch (InvalidOperationException ex)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
                    return;
                }
                TrackPanelOperation(msgRecord);
                ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: (record, state) =>
                {
                    if (state == MsgRecordState.Success)
                    {
                        RefreshPanelState();
                    }
                    else
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), $"{record.MsgReturn.Message}", "ColorVision");
                    }
                });

                RotateTransform rotateTransform1 = new() { Angle = 0 };
                View.ImageView.ImageShow.RenderTransform = rotateTransform1;
                View.ImageView.ImageShow.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        public void GetData_Click(object sender, RoutedEventArgs e)
        {
            if (_panelOperation != null || _isOpeningLocalVideo) return;
            if (Device.RoutesLocally)
            {
                MsgRecord? local = TakePhoto();
                if (local == null) return;
                EnsureTimedButtonOperations();
                Device.SetMsgRecordChanged(local);
                TrackPanelOperation(local);
                ServicesHelper.SendTimedCommand(this, Capture.TakePhotoButton, local, onTerminalStateChanged: (record, state) =>
                {
                    if (state == MsgRecordState.Fail) MessageBox1.Show(Application.Current.GetActiveWindow(), record.MsgReturn.Message, "ColorVision");
                });
                return;
            }
            ParamBase autoExpTimeParam = CameraTemplateSelection.ResolveOptional<ParamBase>(Capture.ComboxAutoExpTimeParamTemplate1.SelectedValue);

            CalibrationParam param = CameraTemplateSelection.ResolveOptional<CalibrationParam>(Capture.ComboxCalibrationTemplate.SelectedValue);
            if (param.Id != -1)
            {
                if (Device.PhyCamera == null)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.PhysicalCameraNotConfigured, "ColorVision");
                    return;
                }

                if (Device.PhyCamera.CameraLicenseModel?.DevCaliId == null)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.CalibrationServiceRequiredForTemplate, "ColorVision");
                    return;
                }

                var groupResource = Device.PhyCamera.VisualChildren
                    .OfType<GroupResource>()
                    .FirstOrDefault(resource => resource.Name == param.CalibrationMode);
                groupResource?.SetCalibrationResource();
                bool isSelected = (param.Normal?.DarkNoise?.IsSelected ?? false) ||
                    (param.Normal?.DefectPoint?.IsSelected ?? false) ||
                    (param.Normal?.Distortion?.IsSelected ?? false) ||
                    (param.Normal?.DSNU?.IsSelected ?? false) ||
                    (param.Normal?.ColorShift?.IsSelected ?? false) ||
                    (param.Normal?.Uniformity?.IsSelected ?? false) ||
                    (param.Normal?.LineArity?.IsSelected ?? false) ||
                    (param.Normal?.ColorDiff?.IsSelected ?? false) ||
                    (param.Color?.Luminance?.IsSelected ?? false) ||
                    (param.Color?.LumOneColor?.IsSelected ?? false) ||
                    (param.Color?.LumFourColor?.IsSelected ?? false) ||
                    (param.Color?.LumMultiColor?.IsSelected ?? false);

                if (groupResource == null || !isSelected)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.CalibrationFileNotConfiguredWithTemplate, param.Name), "ColorVision");
                    return;
                }

                if (param.Normal?.DarkNoise?.IsSelected ?? false)
                {
                    if (!(groupResource.DarkNoise?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.DarkNoise?.FilePath ?? "DarkNoise"), "ColorVision");
                        return;
                    }
                }
                if (param.Normal?.DefectPoint?.IsSelected ?? false)
                {
                    if (!(groupResource.DefectPoint?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.DefectPoint?.FilePath ?? "DefectPoint"), "ColorVision");
                        return;
                    }
                }
                if (param.Normal?.Distortion?.IsSelected ?? false)
                {
                    if (!(groupResource.Distortion?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.Distortion?.FilePath ?? "Distortion"), "ColorVision");
                        return;
                    }
                }
                if (param.Normal?.DSNU?.IsSelected ?? false)
                {
                    if (!(groupResource.DSNU?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.DSNU?.FilePath ?? "DSNU"), "ColorVision");
                        return;
                    }
                }
                if (param.Normal?.ColorShift?.IsSelected ?? false)
                {
                    if (!(groupResource.ColorShift?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.ColorShift?.FilePath ?? "ColorShift"), "ColorVision");
                        return;
                    }
                }
                if (param.Normal?.Uniformity?.IsSelected ?? false)
                {
                    if (!(groupResource.Uniformity?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.Uniformity?.FilePath ?? "Uniformity"), "ColorVision");
                        return;
                    }
                }
                if (param.Normal?.LineArity?.IsSelected ?? false)
                {
                    if (!(groupResource.LineArity?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.LineArity?.FilePath ?? "LineArity"), "ColorVision");
                        return;
                    }
                }
                if (param.Normal?.ColorDiff?.IsSelected ?? false)
                {
                    if (!(groupResource.ColorDiff?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.ColorDiff?.FilePath ?? "ColorDiff"), "ColorVision");
                        return;
                    }
                }
                if (param.Color?.Luminance?.IsSelected ?? false)
                {
                    if (!(groupResource.Luminance?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.Luminance?.FilePath ?? "Luminance"), "ColorVision");
                        return;
                    }
                }
                if (param.Color?.LumOneColor?.IsSelected ?? false)
                {
                    if (!(groupResource.LumOneColor?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.LumOneColor?.FilePath ?? "LumOneColor"), "ColorVision");
                        return;
                    }
                }
                if (param.Color?.LumFourColor?.IsSelected ?? false)
                {
                    if (!(groupResource.LumFourColor?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.LumFourColor?.FilePath ?? "LumFourColor"), "ColorVision");
                        return;
                    }
                }
                if (param.Color?.LumMultiColor?.IsSelected ?? false)
                {
                    if (!(groupResource.LumMultiColor?.IsValid ?? false))
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateFileNotExist, param.Name, groupResource.LumMultiColor?.FilePath ?? "LumMultiColor"), "ColorVision");
                        return;
                    }
                }
            }

            double[] expTime = null;
            if (Device.Config.IsExpThree) { expTime = new double[] { Device.DisplayConfig.ExpTimeR, Device.DisplayConfig.ExpTimeG, Device.DisplayConfig.ExpTimeB }; }
            else expTime = new double[] { Device.DisplayConfig.ExpTime };






            ParamBase HDRparamBase = CameraTemplateSelection.ResolveOptional<ParamBase>(Capture.ComboBoxHDRTemplate.SelectedValue);

            int latestMeasureResultId = MeasureImgResultDao.Instance.GetLatestId(Device.Config.Code);
            EnsureTimedButtonOperations();
            MsgRecord msgRecord = DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);
            logger.Info($"正在取图：ExpTime{Device.DisplayConfig.ExpTime} othertime{DisplayCameraConfig.TakePictureDelay}");
            Device.SetMsgRecordChanged(msgRecord);

            TrackPanelOperation(msgRecord);
            ServicesHelper.SendTimedCommand(this, Capture.TakePhotoButton, msgRecord, onTerminalStateChanged: (record, state) =>
            {
                if (state == MsgRecordState.Timeout)
                {
                    if (TryHandleCaptureTimeoutFromDatabase(latestMeasureResultId))
                    {
                        return;
                    }

                    if (param.Id > 0 && Device?.PhyCamera?.DeviceCalibration == null)
                    {
                        MessageBox1.Show(Properties.Resources.CaptureTimeoutConfigureCalibration);
                    }
                    else
                    {
                        MessageBox1.Show(Properties.Resources.CaptureTimeoutResetTime);
                    }
                }
                if (state == MsgRecordState.Fail)
                {
                    HandleCaptureFail(record.MsgReturn?.Message);
                }
            });

        }

        private bool TryHandleCaptureTimeoutFromDatabase(int latestMeasureResultId)
        {
            MeasureResultImgModel? result = MeasureImgResultDao.Instance.GetLatestAfterId(Device.Config.Code, latestMeasureResultId);
            if (result == null) return false;

            View.SearchAll();
            if (!IsFailedMeasureResult(result))
            {
                logger.Info($"取图超时后检测到数据库已生成记录，Id:{result.Id}, ResultCode:{result.ResultCode}");
                return true;
            }

            string errorMessage = BuildMeasureResultErrorMessage(result);
            logger.Error($"取图超时后检测到数据库失败记录：{errorMessage}");
            HandleCaptureFail(errorMessage, refreshResults: false);
            return true;
        }

        private static bool IsFailedMeasureResult(MeasureResultImgModel result) => result.ResultCode != 0;

        private static string BuildMeasureResultErrorMessage(MeasureResultImgModel result)
        {
            string message = string.IsNullOrWhiteSpace(result.Result) ? Properties.Resources.Camera_UnknownError : result.Result;
            return string.Format(Properties.Resources.Camera_DatabaseFailureRecord, result.Id, result.ResultCode, message);
        }

        private async void HandleCaptureFail(string? message, bool refreshResults = true)
        {
            if (refreshResults)
            {
                View.SearchAll();
            }

            string errorMessage = string.IsNullOrWhiteSpace(message) ? Properties.Resources.Camera_CaptureFailed : message;
            string prompt = errorMessage + Environment.NewLine + Properties.Resources.TryRestartService + Environment.NewLine + Properties.Resources.Camera_ConfirmRestartService;
            if (MessageBox.Show(Application.Current.GetActiveWindow(), prompt, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    await DisplayFlow.RestartColorVisionServicesAsync();
                }
                catch (Exception ex)
                {
                    logger.Error("重启 ColorVision 服务失败", ex);
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
                }
            }
        }

        // Local capture switches Live to measurement after parameters have been built.
        private bool UsesThreeCaptureExposures => Device.Config.IsExpThree
            || (Device.RoutesLocally && Device.Config.CameraMode == CameraMode.CV_MODE);

        public MsgRecord? TakePhoto(double exp = 0)
        {
            ParamBase autoExpTimeParam = CameraTemplateSelection.ResolveOptional<ParamBase>(Capture.ComboxAutoExpTimeParamTemplate1.SelectedValue);

            CalibrationParam param = CameraTemplateSelection.ResolveOptional<CalibrationParam>(Capture.ComboxCalibrationTemplate.SelectedValue);
            if (param.Id != -1)
            {
                if (!Device.RoutesLocally && Device.PhyCamera != null && Device.PhyCamera.CameraLicenseModel?.DevCaliId == null)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.CalibrationServiceRequiredForTemplate, "ColorVision");
                    return null;
                }
            }

            double[] expTime = null;
            bool isExpThree = UsesThreeCaptureExposures;
            if (exp == 0)
            {
                if (isExpThree) { expTime = new double[] { Device.DisplayConfig.ExpTimeR, Device.DisplayConfig.ExpTimeG, Device.DisplayConfig.ExpTimeB }; }
                else expTime = new double[] { Device.DisplayConfig.ExpTime };
            }
            else
            {
                if (isExpThree) { expTime = new double[] { exp, exp, exp }; }
                else expTime = new double[] { exp };
            }

            ParamBase HDRparamBase = CameraTemplateSelection.ResolveOptional<ParamBase>(Capture.ComboBoxHDRTemplate.SelectedValue);

            return Device.RoutesLocally
                ? Device.CaptureLocally(expTime, param, autoExpTimeParam, TemplatesExtension.CreateEmptyParam<ParamBase>())
                : DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);

        }


        public MsgRecord? GetData()
        {
            ParamBase autoExpTimeParam = CameraTemplateSelection.ResolveOptional<ParamBase>(Capture.ComboxAutoExpTimeParamTemplate1.SelectedValue);
            CalibrationParam param = CameraTemplateSelection.ResolveOptional<CalibrationParam>(Capture.ComboxCalibrationTemplate.SelectedValue);

            double[] expTime = null;
            if (UsesThreeCaptureExposures) { expTime = new double[] { Device.DisplayConfig.ExpTimeR, Device.DisplayConfig.ExpTimeG, Device.DisplayConfig.ExpTimeB }; }
            else expTime = new double[] { Device.DisplayConfig.ExpTime };


            ParamBase HDRparamBase = CameraTemplateSelection.ResolveOptional<ParamBase>(Capture.ComboBoxHDRTemplate.SelectedValue);

            return Device.RoutesLocally
                ? Device.CaptureLocally(expTime, param, autoExpTimeParam, TemplatesExtension.CreateEmptyParam<ParamBase>())
                : DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);
        }

        private void AutoExplose_Click(object sender, RoutedEventArgs e)
        {
            if (_panelOperation != null || _isOpeningLocalVideo) return;
            if (Device.RoutesLocally)
            {
                ParamBase template = CameraTemplateSelection.ResolveOptional<ParamBase>(Capture.ComboxAutoExpTimeParamTemplate.SelectedValue);
                CalibrationParam calibration = CameraTemplateSelection.ResolveOptional<CalibrationParam>(Capture.ComboxCalibrationTemplate.SelectedValue);
                MsgRecord local = Device.AutoExposeLocally(template, calibration);
                local.MsgRecordStateChanged += (_, state) =>
                {
                    if (state == MsgRecordState.Fail) MessageBox1.Show(Application.Current.GetActiveWindow(), local.MsgReturn.Message, "ColorVision");
                };
                TrackPanelOperation(local);
                ServicesHelper.SendCommand(sender, local);
                return;
            }
            if (sender is Button button)
            {
                if (!CameraTemplateSelection.TryResolveRequired(Capture.ComboxAutoExpTimeParamTemplate.SelectedValue, out ParamBase param))
                {
                    ShowRequiredTemplateMessage(Properties.Resources.AutoExploreTemplate);
                    return;
                }

                var msgRecord = DService.GetAutoExpTime(param);
                msgRecord.MsgRecordStateChanged += (s, e) =>
                {
                    if (IsDisposed) return;

                    if (e == MsgRecordState.Timeout)
                    {
                        MessageBox1.Show(Properties.Resources.AutoExposureTimeoutCheckLog, "ColorVision");
                    }
                    if (e == MsgRecordState.Fail)
                    {
                        MessageBox1.Show(string.Format(Properties.Resources.AutoExposureFailedCheckLog, Environment.NewLine, msgRecord.MsgReturn.Message), "ColorVision");
                    }
                };
                TrackPanelOperation(msgRecord);
                ServicesHelper.SendCommand(button, msgRecord);
            }
        }

        private static void ShowRequiredTemplateMessage(string templateName)
        {
            MessageBox1.Show(Application.Current.GetActiveWindow(), $"{templateName}: {Properties.Resources.Flow_NoTemplateAvailable}", "ColorVision");
        }



        private async Task<(bool isSuccess, string errorMessage)> CloseLocalVideoInternalAsync()
        {
            return await Task.Run(() =>
            {
                lock (_localVideoHandleSync)
                {
                    try
                    {
                        if (_sharedLocalVideo)
                        {
                            Device.LocalCameraSession.StopPreview(StopSharedVideoPreview, closeCamera: false);
                            return (true, string.Empty);
                        }
                        int closeResult = cvErrorDefine.CV_ERR_SUCCESS;
                        if (m_hCamHandle != IntPtr.Zero)
                        {
                            DetachLocalVideoCallback(m_hCamHandle);
                            closeResult = CloseLocalVideoCamera(m_hCamHandle);
                        }

                        if (m_hCamHandle != IntPtr.Zero && cvCameraCSLib.CM_IsOpen(m_hCamHandle))
                            return (false, closeResult == cvErrorDefine.CV_ERR_SUCCESS ? "本地视频关闭失败，相机仍被占用。" : GetCameraErrorMessage(closeResult));
                        Device.CameraBackend.EndVideo();
                        if (closeResult != cvErrorDefine.CV_ERR_SUCCESS) return (false, GetCameraErrorMessage(closeResult));
                        return (true, string.Empty);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                        return (false, ex.Message);
                    }
                }
            });
        }

        private void AutoFocus_Click(object sender, RoutedEventArgs e)
        {
            if (!CameraTemplateSelection.TryResolveRequired(Capture.ComboxAutoFocus.SelectedValue, out AutoFocusParam param))
            {
                ShowRequiredTemplateMessage(Properties.Resources.AutoFocusTemplate);
                return;
            }
            MsgRecord msgRecord = DService.AutoFocus(param);
            msgRecord.MsgRecordStateChanged += (s, e) =>
            {
                if (IsDisposed) return;

                if (e == MsgRecordState.Fail)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                }
            };
            ServicesHelper.SendCommand(sender, msgRecord);

        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.ConfigurePhysicalCameraBeforeCalibration, "ColorVision");
                return;
            }

            var ITemplate = new TemplateCalibrationParam(Device.PhyCamera);
            var windowTemplate = new TemplateEditorWindow(ITemplate, Capture.ComboxCalibrationTemplate.SelectedIndex - 1) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();

            Capture.ComboxCalibrationTemplate.ItemsSource = (Device.PhyCamera?.CalibrationParams).CreateEmpty();
        }

        private void EditAutoExpTime(object sender, RoutedEventArgs e)
        {
            EditSelectedAutoExpTimeTemplate(Capture.ComboxAutoExpTimeParamTemplate);
        }

        private void EditAutoFocus(object sender, RoutedEventArgs e)
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoFocus(), Capture.ComboxAutoFocus.SelectedIndex) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }

        private void EditAutoExpTime1(object sender, RoutedEventArgs e)
        {
            EditSelectedAutoExpTimeTemplate(Capture.ComboxAutoExpTimeParamTemplate1);
        }

        private void EditSelectedAutoExpTimeTemplate(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is AutoExpTimeTemplateOption { Kind: AutoExpTimeTemplateKind.LocalDefault })
            {
                new PropertyEditorWindow(DisplayCameraConfig.LocalAutoExposureConfig) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
                SaveDisplayConfig();
                return;
            }
            ITemplate template;
            int defaultIndex;

            if (comboBox.SelectedItem is AutoExpTimeTemplateOption { Kind: AutoExpTimeTemplateKind.V1Detail } v1Option)
            {
                template = new TemplateAutoExpTime();
                defaultIndex = FindTemplateIndex(TemplateAutoExpTime.Params, v1Option.Value);
            }
            else if (comboBox.SelectedItem is AutoExpTimeTemplateOption { Kind: AutoExpTimeTemplateKind.V2Json } v2Option)
            {
                template = new TemplateAutoExpTimeV2();
                defaultIndex = FindTemplateIndex(TemplateAutoExpTimeV2.Params, v2Option.Value);
            }
            else
            {
                template = new TemplateAutoExpTimeV2();
                defaultIndex = 0;
            }

            var windowTemplate = new TemplateEditorWindow(template, defaultIndex) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
            RefreshAutoExpTimeTemplateOptions();
        }

        private static int FindTemplateIndex<T>(ObservableCollection<TemplateModel<T>> templates, ParamBase selectedValue) where T : ParamBase
        {
            int index = templates.ToList().FindIndex(item => item.Value.Id == selectedValue.Id);
            return index < 0 ? 0 : index;
        }


        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(Capture.TextPos.Text, out int pos))
                {
                    var msgRecord = DService.Move(pos, Capture.CheckBoxIsAbs.IsChecked ?? true);
                    ServicesHelper.SendCommand(button, msgRecord);
                }
            }
        }


        private void GoHome_Click(object sender, RoutedEventArgs e)
        {
            ServicesHelper.SendCommandEx(sender, DService.GoHome);
        }

        private void GetPosition_Click(object sender, RoutedEventArgs e)
        {
            ServicesHelper.SendCommandEx(sender, DService.GetPosition);
        }

        private void Move1_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(Capture.TextDiaphragm.Text, out double pos))
            {
                ServicesHelper.SendCommandEx(sender, () => DService.MoveDiaphragm(pos));
            }
        }

        private async void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_panelOperation != null || _isOpeningLocalVideo) return;
            if (Device.CameraBackend.VideoOwned)
            {
                _isOpeningLocalVideo = true;
                RefreshPanelState();
                try
                {
                    DisplayCameraConfig.IsLocalVideoOpen = false;
                    _localRealtimePipeline.Stop(resetRealtime: true);
                    SetLocalVideoPoiTemplateSupported(false);
                    RemoveLocalVideoRoiVisual(restoreImageEditMode: true);
                    RemoveCrossGuideOverlay();
                    var result = await CloseLocalVideoInternalAsync();
                    if (!IsDisposed && !result.isSuccess) MessageBox1.Show(Application.Current.GetActiveWindow(), result.errorMessage, "ColorVision");
                }
                finally { _isOpeningLocalVideo = false; RefreshPanelState(); }
                return;
            }
            if (sender is Button button)
            {
                EnsureTimedButtonOperations();
                MsgRecord msgRecord = DService.Close();
                TrackPanelOperation(msgRecord);
                ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: (_, state) =>
                {
                    if (state == MsgRecordState.Timeout)
                    {
                        MessageBox.Show(Properties.Resources.CloseCameraTimeoutCheckLog);
                        return;
                    }

                    if (state != MsgRecordState.Success)
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), msgRecord.MsgReturn?.Message ?? "相机关闭失败。", "ColorVision");
                        return;
                    }
                    DService_DeviceStatusChanged(this, DService.DeviceStatus);
                });
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void ComboxAutoExpTimeParamTemplate1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsDisposed) return;
            if (Device.CameraBackend.OpensLocally) return;
            Device.Config.IsAutoExpose = CameraTemplateSelection.TryResolveRequired(Capture.ComboxAutoExpTimeParamTemplate1.SelectedValue, out ParamBase _);
        }

        private void EditHDRTemplate(object sender, RoutedEventArgs e)
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateHDR(), Capture.ComboBoxHDRTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }

        private void NDport_Click(object sender, RoutedEventArgs e)
        {
            ServicesHelper.SendCommandEx(sender, () => DService.SetNDPort());
        }

        private void GetNDport_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.GetPort();
            msgRecord.MsgRecordStateChanged += (s, e) =>
            {
                if (IsDisposed) return;

                if (e == MsgRecordState.Success)
                {
                    int port = msgRecord.MsgReturn.Data.Port;
                    DService.Config.NDPort = port;
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionFailed, "ColorVision");
                }
            };
        }

        internal void AttachImageView(ViewCamera view)
        {
            if (IsDisposed || !Device.DisplayConfig.IsLocalVideoOpen || (_sharedLocalVideo && !_sharedVideoActive)) return;
            view.EnsureInitialized();
            _localRealtimePipeline.Start(view.ImageView, Device.DisplayConfig.LocalVideoTransform, showOverlayRoi: false,
                showOverlayMetrics: !Device.DisplayConfig.IsCrossGuideEnabled);
            SetLocalVideoPoiTemplateSupported(true);
            RefreshLocalVideoRoiVisual(selectNewVisual: false);
            RefreshCrossGuideOverlay();
        }

        internal void ReleaseImageView(ViewCamera view)
        {
            if (IsDisposed) return;
            _localRealtimePipeline.Stop(resetRealtime: true);
            _crossGuideProcessor.Reset();
            _crossGuideOverlayVisual.Detach();
            _crossGuideOverlayVisual.Clear();
            _crossGuideOverlayAdded = false;
            if (_localVideoRoiVisual != null)
                _localVideoRoiVisual.Attribute.PropertyChanged -= LocalVideoRoiVisual_PropertyChanged;
            _localVideoRoiVisual = null;
            if (_isLocalVideoRoiVisualRemoveSubscribed && view.IsContentInitialized)
                view.ImageView.ImageShow.VisualsRemove -= ImageShow_VisualsRemoveLocalVideoRoi;
            _isLocalVideoRoiVisualRemoveSubscribed = false;
            _hasLocalVideoImageEditModeSnapshot = false;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(DisposeCore);
            }
            else
            {
                DisposeCore();
            }

            GC.SuppressFinalize(this);
        }

        private void DisposeCore()
        {
            _previewParameterUpdates.Dispose();
            Device.ConfigChanged -= Device_ConfigChanged;
            Device.PropertyChanged -= CameraPanel_PropertyChanged;
            if (_panelConfig != null) _panelConfig.PropertyChanged -= CameraPanel_PropertyChanged;
            _panelConfig = null;
            if (_panelOperation != null) _panelOperation.MsgRecordStateChanged -= PanelOperation_StateChanged;
            _panelOperation = null;
            PhyCameraManager.GetInstance().Loaded -= PhyCameraManager_Loaded;
            DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            TemplateAutoExpTime.Params.CollectionChanged -= AutoExpTimeTemplateParams_CollectionChanged;
            TemplateAutoExpTimeV2.Params.CollectionChanged -= AutoExpTimeTemplateParams_CollectionChanged;
            DisplayCameraConfig.PropertyChanged -= DisplayCameraConfig_PropertyChanged;
            Device.RealtimeCameraConfig.PropertyChanged -= RealtimeCameraConfig_PropertyChanged;

            Device.DisplayConfig.IsLocalVideoOpen = false;
            SetLocalVideoPoiTemplateSupported(false);
            RemoveLocalVideoRoiVisual(restoreImageEditMode: true);
            RemoveCrossGuideOverlay();
            Device.DisplayConfig.CrossGuideStatus = string.Empty;
            _localRealtimePipeline.Stop(resetRealtime: true);
            ReleaseLocalVideoHandle();

            _localRealtimePipeline.Dispose();
            _crossGuideProcessor.Dispose();
            this.DisposeTimedButtonOperations();

            Capture.ComboxCalibrationTemplate.ItemsSource = null;
            Capture.ComboxCalibrationTemplate.DataContext = null;
            Capture.ComboxAutoExpTimeParamTemplate.ItemsSource = null;
            Capture.ComboxAutoExpTimeParamTemplate.DataContext = null;
            Capture.ComboxAutoExpTimeParamTemplate1.ItemsSource = null;
            Capture.ComboxAutoExpTimeParamTemplate1.DataContext = null;
            Capture.ComboxAutoFocus.ItemsSource = null;
            Capture.ComboxAutoFocus.DataContext = null;
            Capture.ComboBoxHDRTemplate.ItemsSource = null;
            Capture.ComboBoxHDRTemplate.DataContext = null;
            DataContext = null;
        }

        private void ReleaseLocalVideoHandle()
        {
            lock (_localVideoHandleSync)
            {
                if (_sharedLocalVideo)
                {
                    try { Device.LocalCameraSession.StopPreview(StopSharedVideoPreview, closeCamera: true); }
                    catch (Exception ex) { logger.Error("释放本地视频预览失败", ex); }
                    return;
                }
                IntPtr handle = m_hCamHandle;
                m_hCamHandle = IntPtr.Zero;
                if (handle == IntPtr.Zero) return;

                try
                {
                    if (cvCameraCSLib.CM_IsOpen(handle))
                    {
                        DetachLocalVideoCallback(handle);
                        _ = CloseLocalVideoCamera(handle);
                    }
                    _ = cvCameraCSLib.ReleaseCameraManager(handle);
                }
                catch (Exception ex)
                {
                    logger.Error("释放本地视频相机资源失败", ex);
                }
            }
        }

        private IntPtr _legacyVideoHandle;
        private bool _sharedLocalVideo;
        private volatile bool _sharedVideoActive;
        public IntPtr m_hCamHandle
        {
            get => _sharedLocalVideo ? Device.LocalCameraSession.Handle : _legacyVideoHandle;
            set => _legacyVideoHandle = value;
        }

        private void StopSharedVideoPreview()
        {
            _sharedVideoActive = false;
            _localRealtimePipeline.Stop(resetRealtime: true);
            _crossGuideProcessor.Reset();
            Dispatcher.BeginInvoke(() =>
            {
                if (IsDisposed || _sharedVideoActive) return;
                Device.DisplayConfig.IsLocalVideoOpen = false;
                SetLocalVideoPoiTemplateSupported(false);
                RemoveLocalVideoRoiVisual(restoreImageEditMode: true);
                RemoveCrossGuideOverlay();
                Device.DisplayConfig.CrossGuideStatus = string.Empty;
                EnsureTimedButtonOperations().RefreshIdleState(Actions.LocalVideoButton);
            });
        }
        private TimedButtonOperationRegistry EnsureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(BuildButtonOperationKey);
            operations.Register(Capture.TakePhotoButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, Device.DisplayConfig.ExpTime + DisplayCameraConfig.TakePictureDelay);
                options.OnSuccessfulCompletion = elapsed => DisplayCameraConfig.TakePictureDelay = Math.Max(0, elapsed - Device.DisplayConfig.ExpTime);
                options.PersistStatsImmediately = false;
            });

            operations.Register(Actions.OpenButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, DisplayCameraConfig.OpenTime);
                options.OnSuccessfulCompletion = elapsed =>
                {
                    DisplayCameraConfig.OpenTime = elapsed;
                    SaveDisplayConfig();
                };
            });

            operations.Register(Actions.CloseButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, DisplayCameraConfig.CloseTime);
                options.OnSuccessfulCompletion = elapsed =>
                {
                    DisplayCameraConfig.CloseTime = elapsed;
                    SaveDisplayConfig();
                };
            });

            operations.Register(Actions.LocalVideoButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, DisplayCameraConfig.LocalVideoOpenTime);
                options.OnSuccessfulCompletion = elapsed =>
                {
                    DisplayCameraConfig.LocalVideoOpenTime = elapsed;
                    SaveDisplayConfig();
                };
                options.ContentFactory = stats => Device.DisplayConfig.IsLocalVideoOpen
                    ? EngineLocalization.Get("CameraPanel_StopPreview")
                    : EngineLocalization.Get("CameraPanel_StartPreview");
                options.ToolTipFactory = stats => Device.DisplayConfig.IsLocalVideoOpen
                    ? EngineLocalization.Get("CameraPanel_StopPreviewHint")
                    : TimedButtonOperationTextFormatter.BuildTooltip(EngineLocalization.Get("CameraPanel_StartPreview"), stats);
            });

            return operations;
        }

        private string BuildButtonOperationKey(string actionKey)
        {
            return $"camera:{Device.Config.Code}:{actionKey}";
        }

        private static void SaveDisplayConfig()
        {
            ConfigHandler.GetInstance().Save<DisplayConfigManager>();
        }

        private static void SaveLocalPreferences()
        {
            ConfigHandler.GetInstance().SaveConfigs();
        }

        int QHYCCDProcCallBackFunction(int enumImgType, IntPtr pData, int width, int height, int lss, int bpp, int channels, IntPtr buffer)
        {
            if (IsDisposed || !Device.DisplayConfig.IsLocalVideoOpen || (_sharedLocalVideo && !_sharedVideoActive))
            {
                return 0;
            }

            var pixelFormat = RealtimeFramePresenter.GetPixelFormat(channels, bpp);
            int sourceStride = RealtimeFramePresenter.GetDefaultStride(width, pixelFormat);
            int frameBytes = sourceStride * height;

            if (TryCreateCrossGuideRequest(width, height, out VideoCrossGuideRequest crossGuideRequest))
            {
                _crossGuideProcessor.SubmitFrame(pData, frameBytes, width, height, channels, bpp, sourceStride, crossGuideRequest);
            }

            _localRealtimePipeline.SubmitFrame(pData, frameBytes, width, height, channels, bpp, sourceStride);
            return 0;
        }

        cvCameraCSLib.QHYCCDProcCallBack callback;

        private async void Video1_Click(object sender, RoutedEventArgs e)
        {
            if (IsDisposed || _panelOperation != null || _isOpeningLocalVideo || sender is not Button button) return;
            TimedButtonOperationRegistry operations = EnsureTimedButtonOperations();
            if (Device.DisplayConfig.IsLocalVideoOpen)
            {
                _isOpeningLocalVideo = true;
                RefreshPanelState();
                TimedButtonOperationScope? localVideoCloseScope = operations.Begin(Actions.LocalVideoButton, runningText: EngineLocalization.Get("CameraPanel_StopPreview"));
                bool closeSucceeded = false;
                string closeError = string.Empty;

                try
                {
                    Device.DisplayConfig.IsLocalVideoOpen = false;
                    SetLocalVideoPoiTemplateSupported(false);
                    RemoveLocalVideoRoiVisual(restoreImageEditMode: true);
                    RemoveCrossGuideOverlay();
                    Device.DisplayConfig.CrossGuideStatus = string.Empty;
                    _localRealtimePipeline.Stop(resetRealtime: true);

                    (closeSucceeded, closeError) = await CloseLocalVideoInternalAsync();
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        localVideoCloseScope?.Complete(false);
                        operations.RefreshIdleState(Actions.LocalVideoButton);
                        _isOpeningLocalVideo = false;
                        RefreshPanelState();
                    }
                }

                if (IsDisposed) return;
                if (!closeSucceeded && !string.IsNullOrWhiteSpace(closeError))
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), closeError, "ColorVision");
                }

                return;
            }

            _isOpeningLocalVideo = true;
            RefreshPanelState();
            TimedButtonOperationScope? localVideoScope = operations.Begin(Actions.LocalVideoButton);
            logger.Info("初始化视频模式");
            bool localVideoOpened = false;

            try
            {
                (bool isSuccess, string errorMessage) = await Task.Run(OpenLocalVideoInternal);
                if (IsDisposed) return;
                if (!isSuccess)
                {
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
                    }
                    return;
                }

                void StartPreview()
                {
                    button.Content = EngineLocalization.Get("CameraPanel_StopPreview");
                    ApplyLocalVideoRoiToRealtimeConfig();
                    _localRealtimePipeline.Start(Device.View.ImageView, Device.DisplayConfig.LocalVideoTransform, showOverlayRoi: false, showOverlayMetrics: !Device.DisplayConfig.IsCrossGuideEnabled);
                    SetLocalVideoPoiTemplateSupported(true);
                    Device.DisplayConfig.IsLocalVideoOpen = true;
                    RefreshLocalVideoRoiVisual(selectNewVisual: true);
                    RefreshCrossGuideOverlay();
                    localVideoOpened = true;
                }
                if (_sharedLocalVideo)
                    Device.LocalCameraSession.UseOpened(_ =>
                    {
                        if (!_sharedVideoActive || Device.LocalCameraSession.OpenedMode != TakeImageMode.Live) return false;
                        StartPreview();
                        return true;
                    });
                else StartPreview();
                logger.Info("视频模式初始化结束");
            }
            catch (Exception ex)
            {
                logger.Error("启动本地视频失败", ex);
                if (!IsDisposed) MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
            }
            finally
            {
                _isOpeningLocalVideo = false;
                if (!IsDisposed)
                {
                    localVideoScope?.Complete(localVideoOpened);
                    operations.RefreshIdleState(Actions.LocalVideoButton);
                    RefreshPanelState();
                }
            }
        }

        private (bool isSuccess, string errorMessage) OpenLocalVideoInternal()
        {
            lock (_localVideoHandleSync)
            {
                if (IsDisposed) return (false, string.Empty);
                if (Device.CameraBackend.OpensLocally) return OpenSharedLocalVideo();
                _sharedLocalVideo = false;
                try
                {
                    lock (CameraBackendState.OwnershipSync)
                    {
                        Device.EnsureLocalCameraAvailable();
                        Device.CameraBackend.BeginLocalOpen(video: true);
                    }
                    var result = OpenLocalVideoInternalCore();
                    if (!result.isSuccess) ReleaseFailedLocalVideo();
                    return result;
                }
                catch (Exception ex)
                {
                    ReleaseFailedLocalVideo();
                    return (false, ex.Message);
                }
            }
        }

        private (bool isSuccess, string errorMessage) OpenSharedLocalVideo()
        {
            bool commandStarted = false;
            try
            {
                Device.CameraBackend.BeginLocalCommand();
                commandStarted = true;
                _sharedLocalVideo = true;
                var session = Device.LocalCameraSession;
                lock (session.SyncRoot)
                {
                    int result = session.IsOpen ? session.SwitchMode(TakeImageMode.Live, 8) : session.Open(Device.Config.CameraID, TakeImageMode.Live, 8);
                    if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("进入本地视频模式失败", result);
                    session.UseOpened(handle =>
                    {
                        int code = cvCameraCSLib.CM_SetExpTime(handle, (float)Device.DisplayConfig.ExpTime);
                        if (code != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("本地视频设置曝光失败", code);
                        code = cvCameraCSLib.CM_SetGain(handle, Device.DisplayConfig.Gain);
                        if (code != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("本地视频设置增益失败", code);
                        return code;
                    });
                    callback ??= new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
                    session.RegisterPreview(handle => cvCameraCSLib.CM_SetCallBack(handle, callback, IntPtr.Zero), StopSharedVideoPreview);
                    _sharedVideoActive = true;
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                logger.Error("打开共享本地视频失败", ex);
                return (false, ex.Message);
            }
            finally { if (commandStarted) Device.CameraBackend.EndLocalCommand(); }
        }

        private void ReleaseFailedLocalVideo()
        {
            if (m_hCamHandle != IntPtr.Zero && cvCameraCSLib.CM_IsOpen(m_hCamHandle))
            {
                DetachLocalVideoCallback(m_hCamHandle);
                _ = CloseLocalVideoCamera(m_hCamHandle);
            }
            if (m_hCamHandle == IntPtr.Zero || !cvCameraCSLib.CM_IsOpen(m_hCamHandle)) Device.CameraBackend.EndVideo();
            else Dispatcher.BeginInvoke(() => Device.DisplayConfig.IsLocalVideoOpen = true);
        }

        private void DetachLocalVideoCallback(IntPtr handle)
        {
            int result = cvCameraCSLib.CM_UnregisterCallBack(handle);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) logger.Warn(LocalCameraCaptureService.CreateNativeException("注销本地视频回调失败", result));
        }

        private int CloseLocalVideoCamera(IntPtr handle)
        {
            int result = cvCameraCSLib.CM_Close(handle);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) logger.Warn(LocalCameraCaptureService.CreateNativeException("关闭本地视频相机失败", result));
            return result;
        }

        private (bool isSuccess, string errorMessage) OpenLocalVideoInternalCore()
        {
            string configuredCameraId = Device.Config.CameraID ?? string.Empty;
            if (m_hCamHandle == IntPtr.Zero)
            {
                cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
                m_hCamHandle = cvCameraCSLib.CM_CreatCameraManagerV1(Device.Config.CameraModel, Device.Config.CameraMode, null);
                if (m_hCamHandle == IntPtr.Zero) return (false, "创建本地视频相机管理器失败");
            }
            int modelResult = cvCameraCSLib.CM_SetCameraModel(m_hCamHandle, Device.Config.CameraModel, Device.Config.CameraMode);
            if (modelResult != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("设置本地视频相机型号失败", modelResult);

            string cameraId = ResolveLocalCameraId();
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return (false, "CameraID is empty, please check CameraCode configuration");
            }

            ApplyLocalVideoCameraSettings(cameraId);

            int nErr = cvErrorDefine.CV_ERR_UNKNOWN;
            logger.Info("CM_Open");
            if (!TryOpenLocalVideoCamera(out nErr, out string errorMessage))
            {
                string retryCameraId = ResolveLocalCameraId(ignoreConfiguredId: true);
                if (!string.IsNullOrWhiteSpace(retryCameraId) && !retryCameraId.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Warn($"CM_Open failed, retry with refreshed CameraID. errorCode={nErr}, error={errorMessage}, old={cameraId}, new={retryCameraId}");
                    ApplyLocalVideoCameraSettings(retryCameraId);
                    cameraId = retryCameraId;
                    if (!TryOpenLocalVideoCamera(out nErr, out errorMessage))
                    {
                        return (false, errorMessage);
                    }
                }
                else
                {
                    return (false, errorMessage);
                }
            }

            if (!string.Equals(configuredCameraId, cameraId, StringComparison.OrdinalIgnoreCase))
            {
                Device.Config.CameraID = cameraId;
                try
                {
                    Device.SaveConfig();
                    logger.Info($"Persisted refreshed CameraID. old={configuredCameraId}, new={cameraId}");
                }
                catch (Exception ex)
                {
                    logger.Error($"Failed to persist refreshed CameraID. old={configuredCameraId}, new={cameraId}", ex);
                }
            }
            SaveLocalPreferences();
            int exposureResult = cvCameraCSLib.CM_SetExpTime(m_hCamHandle, (float)Device.DisplayConfig.ExpTime);
            if (exposureResult != cvErrorDefine.CV_ERR_SUCCESS)
            {
                var error = LocalCameraCaptureService.CreateNativeException("本地视频设置曝光失败", exposureResult);
                logger.Error(error);
                return (false, error.Message);
            }
            int gainResult = cvCameraCSLib.CM_SetGain(m_hCamHandle, Device.DisplayConfig.Gain);
            if (gainResult != cvErrorDefine.CV_ERR_SUCCESS)
            {
                var error = LocalCameraCaptureService.CreateNativeException("本地视频设置增益失败", gainResult);
                logger.Error(error);
                return (false, error.Message);
            }
            callback ??= new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
            int callbackResult = cvCameraCSLib.CM_SetCallBack(m_hCamHandle, callback, IntPtr.Zero);
            if (callbackResult != cvErrorDefine.CV_ERR_SUCCESS)
            {
                var error = LocalCameraCaptureService.CreateNativeException("注册本地视频回调失败", callbackResult);
                logger.Error(error);
                return (false, error.Message);
            }

            return (true, string.Empty);
        }

        private void ApplyLocalVideoCameraSettings(string cameraId)
        {
            Device.Config.CameraID = cameraId;
            int result = cvCameraCSLib.CM_SetCameraID(m_hCamHandle, cameraId);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("设置本地视频相机 ID 失败", result);
            result = cvCameraCSLib.CM_SetTakeImageMode(m_hCamHandle, TakeImageMode.Live);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("设置本地视频采集模式失败", result);
            result = cvCameraCSLib.CM_SetImageBpp(m_hCamHandle, 8);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw LocalCameraCaptureService.CreateNativeException("设置本地视频位深失败", result);
        }

        private bool TryOpenLocalVideoCamera(out int errorCode, out string errorMessage)
        {
            var physicalConfig = Device.PhyCamera?.Config?.CameraCfg;
            JObject cameraConfig = physicalConfig == null ? new JObject() : JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(physicalConfig));
            cameraConfig["useHikMvs"] = Device.DisplayConfig.UseHikMvs;
            cameraConfig["hikBayerQuality"] = (int)Device.DisplayConfig.HikBayerQuality;
            cameraConfig["hikOutputBgr"] = Device.DisplayConfig.HikOutputBgr;
            int configResult = cvCameraCSLib.UpdateCfgJson(m_hCamHandle, ConfigType.Cfg_Camera, cameraConfig.ToString());
            if (configResult != cvErrorDefine.CV_ERR_SUCCESS)
            {
                errorCode = configResult;
                errorMessage = LocalCameraCaptureService.CreateNativeException("注入物理相机配置失败，无法打开本地视频", configResult).Message;
                return false;
            }

            errorCode = cvCameraCSLib.CM_Open(m_hCamHandle);
            if (errorCode != cvErrorDefine.CV_ERR_SUCCESS)
            {
                errorMessage = GetCameraErrorMessage(errorCode);
                return false;
            }

            Device.EnsureLocalCameraLicense();
            errorMessage = string.Empty;
            return true;
        }

        private static string GetCameraErrorMessage(int errorCode)
        {
            string message = string.Empty;
            cvCameraCSLib.CM_GetErrorMessage(errorCode, ref message);
            return string.IsNullOrWhiteSpace(message) ? $"CM_Open failed: {errorCode}" : message;
        }

        private void SetLocalVideoPoiTemplateSupported(bool isSupported)
        {
            if (Device.ExistingView is not { IsContentInitialized: true }) return;
            var imageView = Device.View.ImageView;

            void Apply()
            {
                if (IsDisposed && isSupported) return;

                imageView.Config.SetViewState(
                    PoiImageViewComponent.IsTemplateSupportedRuntimeKey,
                    isSupported,
                    nameof(DisplayCamera),
                    Properties.Resources.Camera_LocalVideoPoiTemplateSupport);
                imageView.ImageShow.RaiseImageInitialized();
            }

            if (imageView.Dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                imageView.Dispatcher.BeginInvoke(new Action(Apply));
            }
        }

        private string ResolveLocalCameraId(bool ignoreConfiguredId = false)
        {
            if (!ignoreConfiguredId && !string.IsNullOrWhiteSpace(Device.Config.CameraID))
            {
                return Device.Config.CameraID.Trim();
            }

            if (!TryGetLocalCameraIds(out IReadOnlyList<string> cameraIds))
            {
                return ignoreConfiguredId ? string.Empty : Device.Config.CameraID ?? string.Empty;
            }

            string cameraCode = Device.Config.CameraCode ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(cameraCode))
            {
                foreach (string cameraId in cameraIds)
                {
                    string md5 = ColorVision.Common.Utilities.Tool.GetMD5(cameraId);
                    if (md5.Contains(cameraCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return cameraId;
                    }
                }
            }

            return cameraIds.Count > 0 ? cameraIds[0] : string.Empty;
        }

        private bool TryGetLocalCameraIds(out IReadOnlyList<string> cameraIds)
        {
            cameraIds = Array.Empty<string>();

            string szText = string.Empty;
            int result = cvCameraCSLib.GetAllCameraIDV1(Device.Config.CameraModel, ref szText);
            if (result != cvErrorDefine.CV_ERR_SUCCESS)
            {
                logger.Warn($"GetAllCameraIDV1 failed for {Device.Config.CameraModel}: {result} {GetCameraErrorMessage(result)}");
                return false;
            }

            JObject jObject = JsonConvert.DeserializeObject<JObject>(szText);
            cameraIds = jObject?["ID"]?
                .ToArray()
                .Select(token => token.ToString().Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();

            return cameraIds.Count > 0;
        }

        private void ApplyPreviewParameter(bool exposure)
        {
            if (IsDisposed || !DisplayCameraConfig.IsLocalVideoOpen || _isOpeningLocalVideo) return;
            _previewParameterUpdates.Enqueue(exposure, exposure ? (float)DisplayCameraConfig.ExpTime : DisplayCameraConfig.Gain);
        }

        private void ApplyPreviewParameterCore(CameraPreviewParameterChange change)
        {
            int Apply(IntPtr handle)
            {
                if (IsDisposed || !_previewParameterUpdates.IsCurrent(change)) return cvErrorDefine.CV_ERR_SUCCESS;
                int code = change.Exposure ? cvCameraCSLib.CM_SetExpTime(handle, change.Value)
                    : cvCameraCSLib.CM_SetGain(handle, change.Value);
                if (code != cvErrorDefine.CV_ERR_SUCCESS)
                    logger.Error(LocalCameraCaptureService.CreateNativeException(change.Exposure ? "本地视频设置曝光失败" : "本地视频设置增益失败", code));
                return code;
            }
            try
            {
                if (_sharedLocalVideo)
                {
                    var session = Device.LocalCameraSession;
                    lock (session.SyncRoot)
                    {
                        if (!_sharedVideoActive || session.OpenedMode != TakeImageMode.Live || !session.IsOpen) return;
                        session.UseOpened(Apply);
                    }
                }
                else lock (_localVideoHandleSync)
                {
                    if (_previewParameterUpdates.IsCurrent(change) && _legacyVideoHandle != IntPtr.Zero && cvCameraCSLib.CM_IsOpen(_legacyVideoHandle)) Apply(_legacyVideoHandle);
                }
            }
            catch (Exception ex) { logger.Error("更新本地预览参数失败", ex); }
        }

    }
}

