#pragma warning disable CA1051,CA1707,CA1863
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.HDR;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.AutoExpTime;
using ColorVision.Engine.Utilities;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using FlowEngineLib.Algorithm;
using log4net;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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
        public double TakePictureDelay { get; set; }
        public int CalibrationTemplateIndex { get; set; }
        public int ExpTimeParamTemplateIndex { get; set; }
        public int ExpTimeParamTemplate1Index { get; set; }
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

        public CVImageFlipMode FlipMode { get => _FlipMode; set { _FlipMode = value; OnPropertyChanged(); } }
        private CVImageFlipMode _FlipMode = CVImageFlipMode.None;

        public int LocalVideoTransform { get => _LocalVideoTransform; set { _LocalVideoTransform = value; OnPropertyChanged(); } }
        private int _LocalVideoTransform = RealtimeFramePresenter.TransformNone;

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

        public ViewCamera View { get; set; }
        public string DisPlayName => Device.Config.Name;

        private readonly ObservableCollection<AutoExpTimeTemplateOption> _autoExpTimeTemplateOptions = new();
        private readonly ObservableCollection<AutoExpTimeTemplateOption> _autoExpTimeTemplateOptionsWithEmpty = new();

        // Video display related fields
        private readonly CameraRealtimeFramePipeline _localRealtimePipeline;
        private readonly VideoCrossGuideProcessor _crossGuideProcessor;
        private readonly CrossGuideOverlayVisual _crossGuideOverlayVisual;
        private bool _isOpeningLocalVideo;
        private DVRectangleText? _localVideoRoiVisual;
        private bool _isSyncingLocalVideoRoi;
        private bool _hasLocalVideoImageEditModeSnapshot;
        private bool _localVideoImageEditModeSnapshot;
        private bool _isLocalVideoRoiVisualRemoveSubscribed;
        private bool _crossGuideOverlayAdded;

        private enum AutoExpTimeTemplateKind
        {
            Empty,
            V1Detail,
            V2Json
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
            View = Device.View;
            _localRealtimePipeline = new CameraRealtimeFramePipeline();
            _crossGuideProcessor = new VideoCrossGuideProcessor(HandleCrossGuideResult);
            _crossGuideOverlayVisual = new CrossGuideOverlayVisual();
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(EngineCommands.TakePhotoCommand, GetData_Click, (s, e) => e.CanExecute = Device.DService.DeviceStatus == DeviceStatusType.Opened));
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            this.AddViewConfig(View, DisPlayName);
            EnsureTimedButtonOperations();

            void UpdateTemplate()
            {
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
                ComboxCalibrationTemplate.SelectedIndex = 0;
            }
            UpdateTemplate();
            Device.ConfigChanged += (s, e) => UpdateTemplate();

            ComboxCalibrationTemplate.DataContext = Device.DisplayConfig;
            PhyCameraManager.GetInstance().Loaded += (s, e) => UpdateTemplate();
            BindAutoExpTimeTemplateSources();

            ComboxAutoExpTimeParamTemplate.ItemsSource = _autoExpTimeTemplateOptions;
            ComboxAutoExpTimeParamTemplate.SelectedIndex = 0;
            ComboxAutoExpTimeParamTemplate.DataContext = Device.DisplayConfig;

            ComboxAutoExpTimeParamTemplate1.ItemsSource = _autoExpTimeTemplateOptionsWithEmpty;
            ComboxAutoExpTimeParamTemplate1.SelectedIndex = 0;
            ComboxAutoExpTimeParamTemplate1.DataContext = Device.DisplayConfig;

            ComboxAutoFocus.ItemsSource = TemplateAutoFocus.Params;
            ComboxAutoFocus.SelectedIndex = 0;
            ComboxAutoFocus.DataContext = Device.DisplayConfig;

            ComboBoxHDRTemplate.ItemsSource = TemplateHDR.Params.CreateEmpty();
            ComboBoxHDRTemplate.SelectedIndex = 0;
            ComboBoxHDRTemplate.DataContext = Device.DisplayConfig;

            DisplayCameraConfig.PropertyChanged += DisplayCameraConfig_PropertyChanged;
            Device.RealtimeCameraConfig.PropertyChanged += RealtimeCameraConfig_PropertyChanged;
            ApplyLocalVideoRoiToRealtimeConfig();

            CBFilp.ItemsSource = from e1 in Enum.GetValues<CVImageFlipMode>().Cast<CVImageFlipMode>()
                                 select new KeyValuePair<CVImageFlipMode, string>(e1, e1.ToString());

            CBFilp2.ItemsSource = new[]
            {
                new KeyValuePair<int, string>(RealtimeFramePresenter.TransformNone, "None"),
                new KeyValuePair<int, string>(RealtimeFramePresenter.TransformFlipX, "FlipX"),
                new KeyValuePair<int, string>(RealtimeFramePresenter.TransformFlipY, "FlipY"),
                new KeyValuePair<int, string>(RealtimeFramePresenter.TransformFlipXY, "FlipXY")
            };


            DService_DeviceStatusChanged(sender, DService.DeviceStatus);
            DService.DeviceStatusChanged += DService_DeviceStatusChanged;
            this.ApplyChangedSelectedColor(DisPlayBorder);
            var vb = new Binding("DService.DeviceStatus")
            {
                Source = Device,
                Mode = BindingMode.OneWay
            };
            vb.Converter = TryFindResource("enum2VisibilityConverter") as IValueConverter;
            vb.ConverterParameter = DeviceStatusType.Closed;
            LocalVideo.SetBinding(StackPanel.VisibilityProperty, vb);

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
            var selectedOption = ComboxAutoExpTimeParamTemplate?.SelectedItem as AutoExpTimeTemplateOption;
            var selectedOptionWithEmpty = ComboxAutoExpTimeParamTemplate1?.SelectedItem as AutoExpTimeTemplateOption;

            _autoExpTimeTemplateOptions.Clear();
            foreach (var option in EnumerateAutoExpTimeTemplateOptions())
                _autoExpTimeTemplateOptions.Add(option);

            _autoExpTimeTemplateOptionsWithEmpty.Clear();
            _autoExpTimeTemplateOptionsWithEmpty.Add(new AutoExpTimeTemplateOption
            {
                DisplayName = "Empty",
                Value = new ParamBase { Id = -1, Name = "Empty" },
                Kind = AutoExpTimeTemplateKind.Empty
            });
            foreach (var option in EnumerateAutoExpTimeTemplateOptions())
                _autoExpTimeTemplateOptionsWithEmpty.Add(option);

            RestoreAutoExpTimeSelection(ComboxAutoExpTimeParamTemplate, _autoExpTimeTemplateOptions, selectedOption, 0);
            RestoreAutoExpTimeSelection(ComboxAutoExpTimeParamTemplate1, _autoExpTimeTemplateOptionsWithEmpty, selectedOptionWithEmpty, 0);
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
            if (_isSyncingLocalVideoRoi) return;

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

            Device.View.ImageView.ImageShow.VisualsRemove -= ImageShow_VisualsRemoveLocalVideoRoi;
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
            if (!Device.DisplayConfig.IsLocalVideoOpen || !Device.DisplayConfig.IsCrossGuideEnabled) return false;
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
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!Device.DisplayConfig.IsLocalVideoOpen || !Device.DisplayConfig.IsCrossGuideEnabled)
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

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }
            void HideAllButtons()
            {
                SetVisibility(ButtonOpen, Visibility.Collapsed);
                SetVisibility(ButtonInit, Visibility.Collapsed);
                SetVisibility(ButtonOffline, Visibility.Collapsed);
                SetVisibility(ButtonClose, Visibility.Collapsed);
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(StackPanelOpen, Visibility.Collapsed);
            }
            // Default state

            switch (e)
            {
                case DeviceStatusType.Unauthorized:
                    HideAllButtons();
                    SetVisibility(ButtonUnauthorized, Visibility.Visible);
                    break;
                case DeviceStatusType.Unknown:
                    HideAllButtons();
                    SetVisibility(TextBlockUnknow, Visibility.Visible);
                    break;
                case DeviceStatusType.OffLine:
                    HideAllButtons();
                    SetVisibility(ButtonOffline, Visibility.Visible);
                    break;
                case DeviceStatusType.UnInit:
                    HideAllButtons();
                    SetVisibility(ButtonInit, Visibility.Visible);
                    break;
                case DeviceStatusType.Closed:
                    HideAllButtons();
                    SetVisibility(ButtonOpen, Visibility.Visible);
                    break;
                case DeviceStatusType.LiveOpened:
                    HideAllButtons();
                    SetVisibility(StackPanelOpen, Visibility.Visible);
                    SetVisibility(ButtonClose, Visibility.Visible);
                    break;
                case DeviceStatusType.Opened:
                    HideAllButtons();
                    SetVisibility(StackPanelOpen, Visibility.Visible);
                    SetVisibility(ButtonClose, Visibility.Visible);
                    break;
                default:
                    break;
            }
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
            if (sender is Button button)
            {
                EnsureTimedButtonOperations();
                var msgRecord = DService.Open(DService.Config.CameraID, Device.Config.TakeImageMode, (int)DService.Config.ImageBpp);
                ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: (record, state) =>
                {
                    if (state == MsgRecordState.Success)
                    {
                        ButtonOpen.Visibility = Visibility.Collapsed;
                        ButtonClose.Visibility = Visibility.Visible;
                        StackPanelOpen.Visibility = Visibility.Visible;
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
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not ParamBase autoExpTimeParam) return;

            if (ComboxCalibrationTemplate.SelectedValue is not CalibrationParam param)
            {
                param = new CalibrationParam() { Id = -1, Name = "Empty" };
            }
            else if (param.Id != -1)
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






            if (ComboBoxHDRTemplate.SelectedValue is not ParamBase HDRparamBase) return;

            int latestMeasureResultId = MeasureImgResultDao.Instance.GetLatestId(Device.Config.Code);
            EnsureTimedButtonOperations();
            MsgRecord msgRecord = DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);
            logger.Info($"正在取图：ExpTime{Device.DisplayConfig.ExpTime} othertime{DisplayCameraConfig.TakePictureDelay}");
            Device.SetMsgRecordChanged(msgRecord);

            ServicesHelper.SendTimedCommand(this, TakePhotoButton, msgRecord, onTerminalStateChanged: (record, state) =>
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

        public MsgRecord? TakePhoto(double exp = 0)
        {
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not ParamBase autoExpTimeParam) return null;

            if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
            {
                if (param.Id != -1)
                {
                    if (Device.PhyCamera != null && Device.PhyCamera.CameraLicenseModel?.DevCaliId == null)
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.CalibrationServiceRequiredForTemplate, "ColorVision");
                        return null;
                    }
                }
            }
            else
            {
                param = new CalibrationParam() { Id = -1, Name = "Empty" };
            }

            double[] expTime = null;
            if (exp == 0)
            {
                if (Device.Config.IsExpThree) { expTime = new double[] { Device.DisplayConfig.ExpTimeR, Device.DisplayConfig.ExpTimeG, Device.DisplayConfig.ExpTimeB }; }
                else expTime = new double[] { Device.DisplayConfig.ExpTime };
            }
            else
            {
                if (Device.Config.IsExpThree) { expTime = new double[] { exp, exp, exp }; }
                else expTime = new double[] { exp };
            }

            if (ComboBoxHDRTemplate.SelectedValue is not ParamBase HDRparamBase) return null;

            return DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);

        }


        public MsgRecord? GetData()
        {
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not ParamBase autoExpTimeParam) return null;
            if (ComboxCalibrationTemplate.SelectedValue is not CalibrationParam param) return null;

            double[] expTime = null;
            if (Device.Config.IsExpThree) { expTime = new double[] { Device.DisplayConfig.ExpTimeR, Device.DisplayConfig.ExpTimeG, Device.DisplayConfig.ExpTimeB }; }
            else expTime = new double[] { Device.DisplayConfig.ExpTime };


            if (ComboBoxHDRTemplate.SelectedValue is not ParamBase HDRparamBase) return null;

            return DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);
        }

        private void AutoExplose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxAutoExpTimeParamTemplate.SelectedValue is ParamBase param && param.Id != -1)
                {
                    var msgRecord = DService.GetAutoExpTime(param);
                    msgRecord.MsgRecordStateChanged += (s, e) =>
                    {
                        if (e == MsgRecordState.Timeout)
                        {
                            MessageBox1.Show(Properties.Resources.AutoExposureTimeoutCheckLog, "ColorVision");
                        }
                        ;
                        if (e == MsgRecordState.Fail)
                        {
                            MessageBox1.Show(string.Format(Properties.Resources.AutoExposureFailedCheckLog, Environment.NewLine, msgRecord.MsgReturn.Message), "ColorVision");
                        }
                        ;
                    };
                    ServicesHelper.SendCommand(button, msgRecord);

                }
            }
        }



        private async Task<(bool isSuccess, string errorMessage)> CloseLocalVideoInternalAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (m_hCamHandle != IntPtr.Zero)
                    {
                        cvCameraCSLib.CM_UnregisterCallBack(m_hCamHandle);
                        cvCameraCSLib.CM_Close(m_hCamHandle);
                    }

                    return (true, string.Empty);
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    return (false, ex.Message);
                }
            });
        }

        private void AutoFocus_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxAutoFocus.SelectedValue is not AutoFocusParam param) return;
            MsgRecord msgRecord = DService.AutoFocus(param);
            msgRecord.MsgRecordStateChanged += (s, e) =>
            {
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
            var windowTemplate = new TemplateEditorWindow(ITemplate, ComboxCalibrationTemplate.SelectedIndex - 1) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();

            ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
        }

        private void EditAutoExpTime(object sender, RoutedEventArgs e)
        {
            EditSelectedAutoExpTimeTemplate(ComboxAutoExpTimeParamTemplate);
        }

        private void EditAutoFocus(object sender, RoutedEventArgs e)
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoFocus(), ComboxAutoFocus.SelectedIndex) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }

        private void EditAutoExpTime1(object sender, RoutedEventArgs e)
        {
            EditSelectedAutoExpTimeTemplate(ComboxAutoExpTimeParamTemplate1);
        }

        private void EditSelectedAutoExpTimeTemplate(ComboBox comboBox)
        {
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
                if (int.TryParse(TextPos.Text, out int pos))
                {
                    var msgRecord = DService.Move(pos, CheckBoxIsAbs.IsChecked ?? true);
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
            if (double.TryParse(TextDiaphragm.Text, out double pos))
            {
                ServicesHelper.SendCommandEx(sender, () => DService.MoveDiaphragm(pos));
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                EnsureTimedButtonOperations();
                MsgRecord msgRecord = DService.Close();
                ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: (_, state) =>
                {
                    if (state == MsgRecordState.Timeout)
                    {
                        MessageBox.Show(Properties.Resources.CloseCameraTimeoutCheckLog);
                        return;
                    }

                    ButtonOpen.Visibility = Visibility.Visible;
                    ButtonClose.Visibility = Visibility.Collapsed;
                    StackPanelOpen.Visibility = Visibility.Collapsed;
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
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not ParamBase autoExpTimeParam) return;

            Device.Config.IsAutoExpose = autoExpTimeParam.Id != -1;
        }

        private void EditHDRTemplate(object sender, RoutedEventArgs e)
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateHDR(), ComboBoxHDRTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow() };
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

        public void Dispose()
        {
            DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            TemplateAutoExpTime.Params.CollectionChanged -= AutoExpTimeTemplateParams_CollectionChanged;
            TemplateAutoExpTimeV2.Params.CollectionChanged -= AutoExpTimeTemplateParams_CollectionChanged;
            DisplayCameraConfig.PropertyChanged -= DisplayCameraConfig_PropertyChanged;
            Device.RealtimeCameraConfig.PropertyChanged -= RealtimeCameraConfig_PropertyChanged;
            RemoveLocalVideoRoiVisual(restoreImageEditMode: true);
            RemoveCrossGuideOverlay();

            // Clean up video display resources
            _localRealtimePipeline.Dispose();
            _crossGuideProcessor.Dispose();
            this.DisposeTimedButtonOperations();
            GC.SuppressFinalize(this);
        }

        public IntPtr m_hCamHandle;
        public string strPathSysCfg = "cfg\\sys.cfg";
        private static System.Windows.Media.PixelFormat GetPixelFormat(int channels, int bpp)
        {
            if (channels == 3)
            {
                return bpp == 16
                    ? System.Windows.Media.PixelFormats.Rgb48
                    : System.Windows.Media.PixelFormats.Bgr24;
            }
            else
            {
                return bpp == 16
                    ? System.Windows.Media.PixelFormats.Gray16
                    : System.Windows.Media.PixelFormats.Gray8;
            }
        }

        private TimedButtonOperationRegistry EnsureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(BuildButtonOperationKey);
            operations.Register(TakePhotoButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, Device.DisplayConfig.ExpTime + DisplayCameraConfig.TakePictureDelay);
                options.OnSuccessfulCompletion = elapsed => DisplayCameraConfig.TakePictureDelay = Math.Max(0, elapsed - Device.DisplayConfig.ExpTime);
                options.PersistStatsImmediately = false;
            });

            operations.Register(OpenButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, DisplayCameraConfig.OpenTime);
                options.OnSuccessfulCompletion = elapsed =>
                {
                    DisplayCameraConfig.OpenTime = elapsed;
                    SaveDisplayConfig();
                };
            });

            operations.Register(CloseButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, DisplayCameraConfig.CloseTime);
                options.OnSuccessfulCompletion = elapsed =>
                {
                    DisplayCameraConfig.CloseTime = elapsed;
                    SaveDisplayConfig();
                };
            });

            operations.Register(LocalVideoButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, DisplayCameraConfig.LocalVideoOpenTime);
                options.OnSuccessfulCompletion = elapsed =>
                {
                    DisplayCameraConfig.LocalVideoOpenTime = elapsed;
                    SaveDisplayConfig();
                };
                options.ContentFactory = stats => Device.DisplayConfig.IsLocalVideoOpen
                    ? Properties.Resources.Close
                    : TimedButtonOperationTextFormatter.BuildCompactContent(Properties.Resources.Video, stats);
                options.ToolTipFactory = stats => Device.DisplayConfig.IsLocalVideoOpen
                    ? Properties.Resources.CloseLocalVideo
                    : TimedButtonOperationTextFormatter.BuildTooltip(Properties.Resources.Video, stats);
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

        ulong QHYCCDProcCallBackFunction(int enumImgType, IntPtr pData, int width, int height, int lss, int bpp, int channels, IntPtr buffer)
        {
            if (!Device.DisplayConfig.IsLocalVideoOpen)
            {
                return 0;
            }

            var pixelFormat = GetPixelFormat(channels, bpp);
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
            if (sender is not Button button) return;
            TimedButtonOperationRegistry operations = EnsureTimedButtonOperations();
            if (Device.DisplayConfig.IsLocalVideoOpen)
            {
                OpenButton.Visibility = Visibility.Collapsed;
                TimedButtonOperationScope? localVideoCloseScope = operations.Begin(LocalVideoButton, runningText: Properties.Resources.Close);
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
                    localVideoCloseScope?.Complete(false);
                    operations.RefreshIdleState(LocalVideoButton);
                    OpenButton.Visibility = closeSucceeded ? Visibility.Visible : Visibility.Collapsed;
                }

                if (!closeSucceeded && !string.IsNullOrWhiteSpace(closeError))
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), closeError, "ColorVision");
                }

                return;
            }

            if (_isOpeningLocalVideo)
            {
                return;
            }

            _isOpeningLocalVideo = true;
            OpenButton.Visibility = Visibility.Collapsed;
            TimedButtonOperationScope? localVideoScope = operations.Begin(LocalVideoButton);
            logger.Info("初始化视频模式");
            bool localVideoOpened = false;

            try
            {
                (bool isSuccess, string errorMessage) = await Task.Run(OpenLocalVideoInternal);
                if (!isSuccess)
                {
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
                    }
                    return;
                }

                button.Content = Properties.Resources.Close;
                ApplyLocalVideoRoiToRealtimeConfig();
                _localRealtimePipeline.Start(Device.View.ImageView, Device.DisplayConfig.LocalVideoTransform, showOverlayRoi: false, showOverlayMetrics: !Device.DisplayConfig.IsCrossGuideEnabled);
                SetLocalVideoPoiTemplateSupported(true);
                Device.DisplayConfig.IsLocalVideoOpen = true;
                RefreshLocalVideoRoiVisual(selectNewVisual: true);
                RefreshCrossGuideOverlay();
                localVideoOpened = true;
                logger.Info("视频模式初始化结束");
            }
            finally
            {
                localVideoScope?.Complete(localVideoOpened);
                operations.RefreshIdleState(LocalVideoButton);
                _isOpeningLocalVideo = false;
                if (!localVideoOpened)
                {
                    OpenButton.Visibility = Visibility.Visible;
                }
            }
        }

        private (bool isSuccess, string errorMessage) OpenLocalVideoInternal()
        {
            if (m_hCamHandle == IntPtr.Zero)
            {
                cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
                m_hCamHandle = cvCameraCSLib.CM_CreatCameraManagerV1(Device.Config.CameraModel, Device.Config.CameraMode, strPathSysCfg);
                int initResult = cvCameraCSLib.CM_InitXYZ(m_hCamHandle);
                if (initResult != cvErrorDefine.CV_ERR_SUCCESS)
                {
                    string initMessage = string.Empty;
                    cvCameraCSLib.CM_GetErrorMessage(initResult, ref initMessage);
                    return (false, string.IsNullOrWhiteSpace(initMessage) ? "CM_InitXYZ failed" : initMessage);
                }
                cvCameraCSLib.CM_SetCameraModel(m_hCamHandle, Device.Config.CameraModel, Device.Config.CameraMode);
            }
            else
            {
                cvCameraCSLib.CM_SetCameraModel(m_hCamHandle, Device.Config.CameraModel, Device.Config.CameraMode);
            }

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

            if (!Device.Config.CameraID.Equals(cameraId, StringComparison.OrdinalIgnoreCase))
            {
                Device.Config.CameraID = cameraId;
            }
            SaveLocalPreferences();
            cvCameraCSLib.CM_SetExpTime(m_hCamHandle, (float)Device.DisplayConfig.ExpTime);
            cvCameraCSLib.CM_SetGain(m_hCamHandle, Device.DisplayConfig.Gain);
            callback ??= new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
            cvCameraCSLib.CM_SetCallBack(m_hCamHandle, callback, IntPtr.Zero);

            return (true, string.Empty);
        }

        private void ApplyLocalVideoCameraSettings(string cameraId)
        {
            Device.Config.CameraID = cameraId;
            cvCameraCSLib.CM_SetCameraID(m_hCamHandle, cameraId);
            cvCameraCSLib.CM_SetTakeImageMode(m_hCamHandle, TakeImageMode.Live);
            cvCameraCSLib.CM_SetImageBpp(m_hCamHandle, 8);
        }

        private bool TryOpenLocalVideoCamera(out int errorCode, out string errorMessage)
        {
            errorCode = cvCameraCSLib.CM_Open(m_hCamHandle);
            if (errorCode != cvErrorDefine.CV_ERR_SUCCESS)
            {
                errorMessage = GetCameraErrorMessage(errorCode);
                return false;
            }

            if (Device.PhyCamera?.Config?.CameraCfg?.IsRoiConfigured == true)
            {
                cvCameraCSLib.CM_Close(m_hCamHandle);
                errorCode = cvCameraCSLib.CM_Open(m_hCamHandle);
                if (errorCode != cvErrorDefine.CV_ERR_SUCCESS)
                {
                    errorMessage = GetCameraErrorMessage(errorCode);
                    return false;
                }
            }

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
            var imageView = Device.View.ImageView;

            void Apply()
            {
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
            if (!TryGetLocalCameraIds(out IReadOnlyList<string> cameraIds))
            {
                return ignoreConfiguredId ? string.Empty : Device.Config.CameraID ?? string.Empty;
            }

            if (!ignoreConfiguredId
                && !string.IsNullOrWhiteSpace(Device.Config.CameraID)
                && cameraIds.Contains(Device.Config.CameraID, StringComparer.OrdinalIgnoreCase))
            {
                return Device.Config.CameraID;
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
            if (!cvCameraCSLib.GetAllCameraIDV1(Device.Config.CameraModel, ref szText))
            {
                logger.Warn($"GetAllCameraIDV1 failed for {Device.Config.CameraModel}");
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

        private void PreviewSliderLocalExp_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            cvCameraCSLib.CM_SetExpTime(m_hCamHandle, (float)Device.DisplayConfig.ExpTime);
        }

        private void PreviewSliderLocalGain_ValueChanged1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            cvCameraCSLib.CM_SetGain(m_hCamHandle, Device.DisplayConfig.Gain);
        }

        private void CBFilp2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedValue: int transform })
            {
                _localRealtimePipeline.Transform = transform;
                _crossGuideOverlayVisual.Clear();
                _crossGuideProcessor.Reset();
                return;
            }

            _localRealtimePipeline.Transform = Device.DisplayConfig.LocalVideoTransform;
            _crossGuideOverlayVisual.Clear();
            _crossGuideProcessor.Reset();
        }

    }
}

