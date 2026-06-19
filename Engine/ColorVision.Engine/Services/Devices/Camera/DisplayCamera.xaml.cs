#pragma warning disable CA1051,CA1707,CA1863
using ColorVision.Common.Utilities;
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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;


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

        [DisplayName("清晰度区域")]
        public Rect LocalVideoRoi
        {
            get => _LocalVideoRoi;
            set
            {
                Rect normalized = NormalizeLocalVideoRoi(value);
                if (_LocalVideoRoi == normalized) return;
                _LocalVideoRoi = normalized;
                OnPropertyChanged();
            }
        }
        private Rect _LocalVideoRoi = new(50, 50, 100, 100);

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

        // Video display related fields
        private readonly CameraRealtimeFramePipeline _localRealtimePipeline;
        private bool _isOpeningLocalVideo;
        private DVRectangleText? _localVideoRoiVisual;
        private bool _isSyncingLocalVideoRoi;
        private bool _hasLocalVideoImageEditModeSnapshot;
        private bool _localVideoImageEditModeSnapshot;
        private bool _isLocalVideoRoiVisualRemoveSubscribed;

        public DisplayCamera(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            _localRealtimePipeline = new CameraRealtimeFramePipeline();
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
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
            ComboxAutoExpTimeParamTemplate.ItemsSource = TemplateAutoExpTime.Params;
            ComboxAutoExpTimeParamTemplate.SelectedIndex = 0;
            ComboxAutoExpTimeParamTemplate.DataContext = Device.DisplayConfig;

            ComboxAutoExpTimeParamTemplate1.ItemsSource = TemplateAutoExpTime.Params.CreateEmpty();
            ComboxAutoExpTimeParamTemplate1.SelectedIndex = 0;
            ComboxAutoExpTimeParamTemplate1.DataContext = Device.DisplayConfig;

            ComboxAutoFocus.ItemsSource = TemplateAutoFocus.Params;
            ComboxAutoFocus.SelectedIndex = 0;
            ComboxAutoFocus.DataContext = Device.DisplayConfig;

            ComboBoxHDRTemplate.ItemsSource = TemplateHDR.Params.CreateEmpty();
            ComboBoxHDRTemplate.SelectedIndex = 0;
            ComboBoxHDRTemplate.DataContext = Device.DisplayConfig;

            InitializeLocalVideoRoiEditor();
            DisplayCameraConfig.PropertyChanged += DisplayCameraConfig_PropertyChanged;
            ApplyLocalVideoRoiToRealtimeConfig();

            CBFilp.ItemsSource = from e1 in Enum.GetValues<CVImageFlipMode>().Cast<CVImageFlipMode>()
                                 select new KeyValuePair<CVImageFlipMode, string>(e1, e1.ToString());

            CBFilp1.ItemsSource = from e1 in Enum.GetValues<CVImageFlipMode>().Cast<CVImageFlipMode>()
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

        private void InitializeLocalVideoRoiEditor()
        {
            LocalVideoRoiEditorHost.Children.Clear();
            LocalVideoRoiEditorHost.Children.Add(PropertyEditorHelper.GenProperties(DisplayCameraConfig, nameof(DisplayCameraConfig.LocalVideoRoi)));
        }

        private void DisplayCameraConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingLocalVideoRoi) return;

            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(DisplayCameraConfig.LocalVideoRoi))
            {
                ApplyLocalVideoRoiToRealtimeConfig();
                RefreshLocalVideoRoiVisual(selectNewVisual: true);
                SaveDisplayConfig();
            }
        }

        private void ApplyLocalVideoRoiToRealtimeConfig()
        {
            DefaultRealtimeCameraConfig.Current.RectangleTextProperties.Rect = DisplayCameraConfig.LocalVideoRoi;
        }

        private static bool IsVisibleLocalVideoRoi(Rect rect) => rect.Width > 0 && rect.Height > 0;

        private static RectangleTextProperties CreateLocalVideoRoiVisualProperties(Rect rect)
        {
            return new RectangleTextProperties
            {
                Rect = rect,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.LimeGreen, 1),
                Foreground = Brushes.LimeGreen,
                Text = "清晰度区域",
                Position = RectangleTextPosition.Top,
                IsShowText = true
            };
        }

        private void RefreshLocalVideoRoiVisual(bool selectNewVisual = false)
        {
            if (!Device.DisplayConfig.IsLocalVideoOpen)
            {
                SyncLocalVideoRoiVisualFromConfig();
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
            DisplayCameraConfig.LocalVideoRoi = new Rect(50, 50, 100, 100);
            if (Device.DisplayConfig.IsLocalVideoOpen)
            {
                EnsureLocalVideoRoiVisual();
            }
        }

        private void LocalVideoRoiFull_Click(object sender, RoutedEventArgs e)
        {
            DisplayCameraConfig.LocalVideoRoi = new Rect(0, 0, 0, 0);
            RemoveLocalVideoRoiVisual(restoreImageEditMode: false);
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
                    Device.CameraVideoControl ??= new VideoReader();
                    if (!DService.IsVideoOpen)
                    {
                        DService.CurrentTakeImageMode = TakeImageMode.Live;
                        string host = Device.Config.VideoConfig.Host;
                        int port = Tool.GetFreePort(Device.Config.VideoConfig.Port);
                        if (port > 0)
                        {
                            View.ImageView.ImageShow.Source = null;
                        }
                        else
                        {
                            Device.CameraVideoControl.Close();
                        }
                    }
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
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return;

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

            EnsureTimedButtonOperations();
            MsgRecord msgRecord = DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);
            logger.Info($"正在取图：ExpTime{Device.DisplayConfig.ExpTime} othertime{DisplayCameraConfig.TakePictureDelay}");
            Device.SetMsgRecordChanged(msgRecord);

            ServicesHelper.SendTimedCommand(this, TakePhotoButton, msgRecord, onTerminalStateChanged: (record, state) =>
            {
                if (state == MsgRecordState.Timeout)
                {
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
                    View.SearchAll();
                    MessageBox.Show(Application.Current.GetActiveWindow(), record.MsgReturn.Message + Environment.NewLine + Properties.Resources.TryRestartService, "ColorVisoin");
                }
            });

        }

        public MsgRecord? TakePhoto(double exp = 0)
        {
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return null;

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
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return null;
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
                if (ComboxAutoExpTimeParamTemplate.SelectedValue is AutoExpTimeParam param)
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



        private void Video_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (!DService.IsVideoOpen)
                {
                    DService.CurrentTakeImageMode = TakeImageMode.Live;

                    MsgRecord msgRecord = DService.OpenVideo();
                    EnsureTimedButtonOperations();
                    ServicesHelper.SendTimedCommand(this, button, msgRecord, onTerminalStateChanged: (record, state) =>
                    {
                        if (state == MsgRecordState.Success)
                        {
                            DeviceOpenLiveResult pm_live = JsonConvert.DeserializeObject<DeviceOpenLiveResult>(JsonConvert.SerializeObject(record.MsgReturn.Data));
                            string mapName = Device.Code;
                            if (pm_live.IsLocal) mapName = pm_live.MapName;
                            if (string.IsNullOrEmpty(mapName))
                            {
                                MessageBox.Show(Application.Current.GetActiveWindow(), "CameraID is empty, cannot start video", "ColorVision");
                                DService.IsVideoOpen = false;
                                return;
                            }
                            Device.CameraVideoControl.Startup(mapName, View.ImageView);

                            DService.IsVideoOpen = true;
                            ButtonOpen.Visibility = Visibility.Collapsed;
                            ButtonClose.Visibility = Visibility.Visible;
                            StackPanelOpen.Visibility = Visibility.Visible;
                            return;
                        }

                        if (state == MsgRecordState.Fail)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"{record.MsgReturn.Message}", "ColorVision");
                        }
                        else if (state == MsgRecordState.Timeout)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.VideoCaptureTimeoutCheckLog, "ColorVision");
                        }

                        Device.CameraVideoControl.Close();
                        DService.Close();
                        DService.IsVideoOpen = false;
                    });

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
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoExpTime(), ComboxAutoExpTimeParamTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }

        private void EditAutoFocus(object sender, RoutedEventArgs e)
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoFocus(), ComboxAutoFocus.SelectedIndex) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }

        private void EditAutoExpTime1(object sender, RoutedEventArgs e)
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoExpTime(), ComboxAutoExpTimeParamTemplate1.SelectedIndex - 1) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
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
            if (DService.IsVideoOpen)
                Device.CameraVideoControl.Close();

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

                    DService.IsVideoOpen = false;
                    ButtonOpen.Visibility = Visibility.Visible;
                    ButtonClose.Visibility = Visibility.Collapsed;
                    StackPanelOpen.Visibility = Visibility.Collapsed;
                });
            }
        }

        private DispatcherTimer _timer;

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timer.Stop();
            DService.SetExp();
        }


        private void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DService.CurrentTakeImageMode == TakeImageMode.Live)
            {
                _timer.Stop();
                _timer.Start();
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
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return;

            Device.Config.IsAutoExpose = autoExpTimeParam.Id != -1;
        }

        private void PreviewSlider_ValueChanged1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DService.IsVideoOpen)
            {
                Common.Utilities.DebounceTimer.AddOrResetTimer("SetGain", 500, () => DService.SetExp());
            }
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
            DisplayCameraConfig.PropertyChanged -= DisplayCameraConfig_PropertyChanged;
            RemoveLocalVideoRoiVisual(restoreImageEditMode: true);

            // Clean up video display resources
            _localRealtimePipeline.Dispose();
            this.DisposeTimedButtonOperations();
            GC.SuppressFinalize(this);
        }

        private void CBFilp1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Device.DService.IsVideoOpen)
            {
                MsgRecord msgRecord = DService.SetFlip();
                msgRecord.MsgRecordStateChanged += (s, e) =>
                {
                    if (e == MsgRecordState.Success)
                    {
                    }
                    else
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExecutionFailed, "ColorVision");
                    }
                };
            }
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

            operations.Register(VideoButton);

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
                    ? "Close Video"
                    : TimedButtonOperationTextFormatter.BuildCompactContent("LocalVideo", stats);
                options.ToolTipFactory = stats => Device.DisplayConfig.IsLocalVideoOpen
                    ? Properties.Resources.CloseLocalVideo
                    : TimedButtonOperationTextFormatter.BuildTooltip(Properties.Resources.LocalVideo, stats);
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

        ulong QHYCCDProcCallBackFunction(int enumImgType, IntPtr pData, int width, int height, int lss, int bpp, int channels, IntPtr buffer)
        {
            if (!Device.DisplayConfig.IsLocalVideoOpen)
            {
                return 0;
            }

            var pixelFormat = GetPixelFormat(channels, bpp);
            int sourceStride = RealtimeFramePresenter.GetDefaultStride(width, pixelFormat);
            int frameBytes = sourceStride * height;

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
                TimedButtonOperationScope? localVideoCloseScope = operations.Begin(LocalVideoButton, runningText: "Close Video");
                bool closeSucceeded = false;
                string closeError = string.Empty;

                try
                {
                    Device.DisplayConfig.IsLocalVideoOpen = false;
                    SetLocalVideoPoiTemplateSupported(false);
                    RemoveLocalVideoRoiVisual(restoreImageEditMode: true);
                    _localRealtimePipeline.Stop(resetRealtime: true);

                    (closeSucceeded, closeError) = await CloseLocalVideoInternalAsync();
                }
                finally
                {
                    localVideoCloseScope?.Complete(false);
                    operations.RefreshIdleState(LocalVideoButton);
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

                button.Content = "Close Video";
                ApplyLocalVideoRoiToRealtimeConfig();
                _localRealtimePipeline.Start(Device.View.ImageView, Device.DisplayConfig.LocalVideoTransform);
                EnsureLocalVideoRoiVisual();
                SetLocalVideoPoiTemplateSupported(true);
                Device.DisplayConfig.IsLocalVideoOpen = true;
                localVideoOpened = true;
                logger.Info("视频模式初始化结束");
            }
            finally
            {
                localVideoScope?.Complete(localVideoOpened);
                operations.RefreshIdleState(LocalVideoButton);
                _isOpeningLocalVideo = false;
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

            string cameraId = ResolveLocalCameraId();
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return (false, "CameraID is empty, please check CameraCode configuration");
            }

            Device.Config.CameraID = cameraId;
            cvCameraCSLib.CM_SetCameraID(m_hCamHandle, cameraId);
            cvCameraCSLib.CM_SetTakeImageMode(m_hCamHandle, TakeImageMode.Live);
            cvCameraCSLib.CM_SetImageBpp(m_hCamHandle, 8);

            int nErr = cvErrorDefine.CV_ERR_UNKNOWN;
            logger.Info("CM_Open");
            if ((nErr = cvCameraCSLib.CM_Open(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
            {
                string szMsg = string.Empty;
                cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);
                return (false, szMsg);
            }

            if (Device.PhyCamera?.Config?.CameraCfg?.IsRoiConfigured == true)
            {
                cvCameraCSLib.CM_Close(m_hCamHandle);
                if ((nErr = cvCameraCSLib.CM_Open(m_hCamHandle)) != cvErrorDefine.CV_ERR_SUCCESS)
                {
                    string szMsg = string.Empty;
                    cvCameraCSLib.CM_GetErrorMessage(nErr, ref szMsg);
                    return (false, szMsg);
                }
            }

            cvCameraCSLib.CM_SetExpTime(m_hCamHandle, (float)Device.DisplayConfig.ExpTime);
            cvCameraCSLib.CM_SetGain(m_hCamHandle, Device.DisplayConfig.Gain);
            callback ??= new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
            cvCameraCSLib.CM_SetCallBack(m_hCamHandle, callback, IntPtr.Zero);

            return (true, string.Empty);
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
                    "本地视频模式是否允许选择 POI 模板");
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

        private string ResolveLocalCameraId()
        {
            if (!string.IsNullOrWhiteSpace(Device.Config.CameraID))
            {
                return Device.Config.CameraID;
            }

            string szText = string.Empty;
            if (!cvCameraCSLib.GetAllCameraIDV1(Device.Config.CameraModel, ref szText))
            {
                return string.Empty;
            }

            JObject jObject = JsonConvert.DeserializeObject<JObject>(szText);
            JToken[] data = jObject?["ID"]?.ToArray();
            if (data == null)
            {
                return string.Empty;
            }

            string cameraCode = Device.Config.CameraCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cameraCode))
            {
                return string.Empty;
            }

            for (int i = 0; i < data.Length; i++)
            {
                string cameraId = data[i].ToString();
                string md5 = ColorVision.Common.Utilities.Tool.GetMD5(cameraId);
                if (md5.Contains(cameraCode, StringComparison.OrdinalIgnoreCase))
                {
                    return cameraId;
                }
            }

            return string.Empty;
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
                return;
            }

            _localRealtimePipeline.Transform = Device.DisplayConfig.LocalVideoTransform;
        }

    }
}

