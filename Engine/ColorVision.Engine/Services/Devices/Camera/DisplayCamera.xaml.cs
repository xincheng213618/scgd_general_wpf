using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons.HDR;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        public ReferenceLineParam ReferenceLineParam { get => _ReferenceLineParam; set { _ReferenceLineParam = value; OnPropertyChanged(); } }
        private ReferenceLineParam _ReferenceLineParam = new ReferenceLineParam();

        public int AvgCount { get => _AvgCount; set { _AvgCount = value; OnPropertyChanged(); } }
        private int _AvgCount = 1;

        public float Gain { get => _Gain; set { _Gain = value; OnPropertyChanged(); } }
        private float _Gain = 10;

        public CVImageFlipMode FlipMode { get => _FlipMode; set { _FlipMode = value; OnPropertyChanged(); } }
        private CVImageFlipMode _FlipMode = CVImageFlipMode.None;

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

        [JsonIgnore]
        public bool IsLocalVideoOpen { get => _IsLocalVideoOpen; set { _IsLocalVideoOpen = value; OnPropertyChanged(); } }
        private bool _IsLocalVideoOpen;
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
        private VideoReaderConfig VideoConfig { get; set; }
        private DVRectangleText DVRectangleText { get; set; }
        private DVText DVText { get; set; }
        private VideoFrameProcessor? _localFrameProcessor;
        private bool _visualsAdded = false;
        private bool _isOpeningLocalVideo;

        public DisplayCamera(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
            CommandBindings.Add(new CommandBinding(EngineCommands.TakePhotoCommand, GetData_Click, (s, e) => e.CanExecute = Device.DService.DeviceStatus == DeviceStatusType.Opened));

            // Initialize video display components
            VideoConfig = ConfigService.Instance.GetRequiredService<VideoReaderConfig>();
            DVRectangleText = new DVRectangleText(VideoConfig.RectangleTextProperties);
            DVText = new DVText(VideoConfig.TextProperties);
        }

        ButtonProgressBar ButtonProgressBarGetData { get; set; }
        ButtonProgressBar ButtonProgressBarOpen { get; set; }
        ButtonProgressBar ButtonProgressBarClose { get; set; }
        ButtonProgressBar ButtonProgressBarLocalVideo { get; set; }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            this.AddViewConfig(View, DisPlayName);
            ButtonProgressBarGetData = new ButtonProgressBar(ProgressBar, TakePhotoButton);
            ButtonProgressBarOpen = new ButtonProgressBar(ProgressBarOpen, OpenButton);
            ButtonProgressBarClose = new ButtonProgressBar(ProgressBarClose, CloseButton);
            ButtonProgressBarLocalVideo = new ButtonProgressBar(ProgressBarLocalVideo, LocalVideoButton);
            UpdateLocalVideoButtonState();

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

            CBFilp.ItemsSource = from e1 in Enum.GetValues(typeof(CVImageFlipMode)).Cast<CVImageFlipMode>()
                                 select new KeyValuePair<CVImageFlipMode, string>(e1, e1.ToString());

            CBFilp1.ItemsSource = from e1 in Enum.GetValues(typeof(CVImageFlipMode)).Cast<CVImageFlipMode>()
                                  select new KeyValuePair<CVImageFlipMode, string>(e1, e1.ToString());

            CBFilp2.ItemsSource = from e1 in Enum.GetValues(typeof(CVImageFlipMode)).Cast<CVImageFlipMode>()
                                  select new KeyValuePair<CVImageFlipMode, string>(e1, e1.ToString());


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
                    TakePhotoButton.Visibility = Visibility.Visible;
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
                var msgRecord = DService.Open(DService.Config.CameraID, Device.Config.TakeImageMode, (int)DService.Config.ImageBpp);
                ButtonProgressBarOpen.Start();
                ButtonProgressBarOpen.TargetTime = DisplayCameraConfig.OpenTime;
                ServicesHelper.SendCommand(button, msgRecord);

                msgRecord.MsgRecordStateChanged += (s, e) =>
                {
                    ButtonProgressBarOpen.Stop();
                    DisplayCameraConfig.OpenTime = ButtonProgressBarOpen.Elapsed;
                    if (e == MsgRecordState.Success)
                    {
                        ButtonOpen.Visibility = Visibility.Collapsed;
                        ButtonClose.Visibility = Visibility.Visible;
                        StackPanelOpen.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), $"{msgRecord.MsgReturn.Message}", "ColorVision");
                    }
                };

                RotateTransform rotateTransform1 = new() { Angle = 0 };
                View.ImageView.ImageShow.RenderTransform = rotateTransform1;
                View.ImageView.ImageShow.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        public void GetData_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return;


            if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
            {
                if (param.Id != -1)
                {
                    if (Device.PhyCamera == null)
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), "物理相机未配置", "ColorVision");
                        return;
                    }

                    if (Device.PhyCamera.CameraLicenseModel?.DevCaliId == null)
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), "使用校正模板需要先配置校正服务", "ColorVision");
                        return;
                    }

                    var groupResource = Device.PhyCamera.VisualChildren.OfType<GroupResource>().FirstOrDefault(a => a.Name == param.CalibrationMode);

                    if (groupResource == null)
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), "校正组不存在", "ColorVision");
                        return;
                    }

                    bool isSelected =
                        (param.Normal?.DarkNoise?.IsSelected ?? false) ||
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

                    if (!isSelected)
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板,需要确认校正文件已经配置", "ColorVision");
                        return;
                    }

                    // 文件有效性验证
                    if (param.Normal?.DarkNoise?.IsSelected ?? false)
                    {
                        if (!(groupResource.DarkNoise?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.DarkNoise?.FilePath ?? "DarkNoise文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Normal?.DefectPoint?.IsSelected ?? false)
                    {
                        if (!(groupResource.DefectPoint?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.DefectPoint?.FilePath ?? "DefectPoint文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Normal?.Distortion?.IsSelected ?? false)
                    {
                        if (!(groupResource.Distortion?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.Distortion?.FilePath ?? "Distortion文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Normal?.DSNU?.IsSelected ?? false)
                    {
                        if (!(groupResource.DSNU?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.DSNU?.FilePath ?? "DSNU文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Normal?.ColorShift?.IsSelected ?? false)
                    {
                        if (!(groupResource.ColorShift?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.ColorShift?.FilePath ?? "ColorShift文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Normal?.Uniformity?.IsSelected ?? false)
                    {
                        if (!(groupResource.Uniformity?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.Uniformity?.FilePath ?? "Uniformity文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Normal?.LineArity?.IsSelected ?? false)
                    {
                        if (!(groupResource.LineArity?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.LineArity?.FilePath ?? "LineArity文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Normal?.ColorDiff?.IsSelected ?? false)
                    {
                        if (!(groupResource.ColorDiff?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.ColorDiff?.FilePath ?? "ColorDiff文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Color?.Luminance?.IsSelected ?? false)
                    {
                        if (!(groupResource.Luminance?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.Luminance?.FilePath ?? "Luminance文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Color?.LumOneColor?.IsSelected ?? false)
                    {
                        if (!(groupResource.LumOneColor?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.LumOneColor?.FilePath ?? "LumOneColor文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Color?.LumFourColor?.IsSelected ?? false)
                    {
                        if (!(groupResource.LumFourColor?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.LumFourColor?.FilePath ?? "LumFourColor文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }
                    if (param.Color?.LumMultiColor?.IsSelected ?? false)
                    {
                        if (!(groupResource.LumMultiColor?.IsValid ?? false))
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), $"使用{param.Name}模板, {groupResource.LumMultiColor?.FilePath ?? "LumMultiColor文件路径"} 不存在", "ColorVision");
                            return;
                        }
                    }

                }
            }
            else
            {
                param = new CalibrationParam() { Id = -1, Name = "Empty" };
            }

            double[] expTime = null;
            if (Device.Config.IsExpThree) { expTime = new double[] { Device.DisplayConfig.ExpTimeR, Device.DisplayConfig.ExpTimeG, Device.DisplayConfig.ExpTimeB }; }
            else expTime = new double[] { Device.DisplayConfig.ExpTime };






            if (ComboBoxHDRTemplate.SelectedValue is not ParamBase HDRparamBase) return;
            TakePhotoButton.Visibility = Visibility.Hidden;

            MsgRecord msgRecord = DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);

            ButtonProgressBarGetData.Start();
            ButtonProgressBarGetData.TargetTime = Device.DisplayConfig.ExpTime + DisplayCameraConfig.TakePictureDelay;
            logger.Info($"正在取图：ExpTime{Device.DisplayConfig.ExpTime} othertime{DisplayCameraConfig.TakePictureDelay}");
            Device.SetMsgRecordChanged(msgRecord);
            msgRecord.MsgRecordStateChanged += (s, e) =>
            {
                ButtonProgressBarGetData.Stop();
                DisplayCameraConfig.TakePictureDelay = ButtonProgressBarGetData.Elapsed - Device.DisplayConfig.ExpTime;
                if (e == MsgRecordState.Timeout)
                {
                    if (param.Id > 0 && Device?.PhyCamera?.DeviceCalibration == null)
                    {
                        MessageBox1.Show("取图超时,是否为物理相机配置校正");
                    }
                    else
                    {
                        MessageBox1.Show("取图超时,请重设超时时间");
                    }
                }
                if (e == MsgRecordState.Fail)
                {
                    View.SearchAll();
                    MessageBox.Show(Application.Current.GetActiveWindow(), msgRecord.MsgReturn.Message + Environment.NewLine + "重启服务试试", "ColorVisoin");
                }
            };
            ServicesHelper.SendCommand(TakePhotoButton, msgRecord);

        }

        public MsgRecord? TakePhoto(double exp = 0)
        {
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return null;

            TakePhotoButton.Visibility = Visibility.Hidden;

            if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
            {
                if (param.Id != -1)
                {
                    if (Device.PhyCamera != null && Device.PhyCamera.CameraLicenseModel?.DevCaliId == null)
                    {
                        MessageBox1.Show(Application.Current.GetActiveWindow(), "使用校正模板需要先配置校正服务", "ColorVision");
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
                            MessageBox1.Show("自动曝光超时，请检查服务日志", "ColorVision");
                        }
                        ;
                        if (e == MsgRecordState.Fail)
                        {
                            MessageBox1.Show($"自动曝光失败，请检查服务日志{Environment.NewLine}{msgRecord.MsgReturn.Message}", "ColorVision");
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
                    string host = Device.Config.VideoConfig.Host;
                    int port = Tool.GetFreePort(Device.Config.VideoConfig.Port);
                    if (port > 0)
                    {
                        MsgRecord msgRecord = DService.OpenVideo(host, port);
                        msgRecord.MsgRecordStateChanged += (s, e) =>
                        {
                            if (e == MsgRecordState.Fail)
                            {
                                MessageBox.Show(Application.Current.GetActiveWindow(), $"{msgRecord.MsgReturn.Message}", "ColorVision");
                                Device.CameraVideoControl.Close();
                                DService.Close();
                                DService.IsVideoOpen = false;
                            }
                            else
                            {
                                DeviceOpenLiveResult pm_live = JsonConvert.DeserializeObject<DeviceOpenLiveResult>(JsonConvert.SerializeObject(msgRecord.MsgReturn.Data));
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
                            }
                        };
                        ServicesHelper.SendCommand(button, msgRecord);
                    }
                    else
                    {
                        MessageBox1.Show("视频模式下，本地端口打开失败");
                        logger.Debug($"Local socket open failed.{host}:{port}");
                    }
                }
            }
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


        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "在使用校正前，请先配置对映的物理相机", "ColorVision");
                return;
            }
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.DatabaseConnectionFailed, "ColorVision");
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

            MsgRecord msgRecord = ServicesHelper.SendCommandEx(sender, () => DService.Close());
            ButtonProgressBarClose.Start();
            ButtonProgressBarClose.TargetTime = DisplayCameraConfig.CloseTime;
            if (msgRecord != null)
            {
                msgRecord.MsgRecordStateChanged += (s, e) =>
                {
                    if (e == MsgRecordState.Timeout)
                    {
                        MessageBox.Show("关闭相机超时,请查看日志并排查问题");
                        return;
                    }

                    DService.IsVideoOpen = false;
                    ButtonOpen.Visibility = Visibility.Visible;
                    ButtonClose.Visibility = Visibility.Collapsed;
                    StackPanelOpen.Visibility = Visibility.Collapsed;
                    ButtonProgressBarClose.Stop();
                    DisplayCameraConfig.CloseTime = ButtonProgressBarClose.Elapsed;
                };
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

            // Clean up video display resources
            Device.View.ImageView.Config.PseudoChanged -= VideoConfig_PseudoChanged;
            _localFrameProcessor?.Dispose();
            _localFrameProcessor = null;
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

        private void UpdateLocalVideoButtonState()
        {
            if (Device.DisplayConfig.IsLocalVideoOpen)
            {
                LocalVideoButton.Content = "Close Video";
                LocalVideoButton.ToolTip = "关闭本地视频";
                return;
            }

            LocalVideoButton.Content = BuildLocalVideoButtonText(Device.DisplayConfig.LocalVideoOpenTime);
            LocalVideoButton.ToolTip = Device.DisplayConfig.LocalVideoOpenTime > 0
                ? $"上次打开耗时: {FormatDuration(Device.DisplayConfig.LocalVideoOpenTime)}"
                : "打开本地视频";
        }

        private static string BuildLocalVideoButtonText(double elapsedMilliseconds)
        {
            if (elapsedMilliseconds <= 0)
            {
                return "LocalVideo";
            }

            return $"LocalVideo ({FormatDuration(elapsedMilliseconds)})";
        }

        private static string FormatDuration(double elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 1000)
            {
                return $"{elapsedMilliseconds:F0} ms";
            }

            return $"{elapsedMilliseconds / 1000:F1} s";
        }

        double articulation;
        ulong QHYCCDProcCallBackFunction(int enumImgType, IntPtr pData, int width, int height, int lss, int bpp, int channels, IntPtr buffer)
        {
            Application.Current?.Dispatcher.Invoke(new Action(() =>
            {
                bool enablePseudo = Device.View.ImageView.Config.IsPseudo;
                bool enableArticulation = VideoConfig.IsCalArtculation;
                bool shouldProcess = VideoConfig.IsUseCacheFile && (enablePseudo || enableArticulation);

                if (shouldProcess)
                {
                    Rect rect = DVRectangleText.Rect;

                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        rect = new Rect(0, 0, width, height);
                    }

                    int frameBytes = width * height * channels * Math.Max(1, bpp / 8);
                    var request = new VideoFrameProcessingRequest
                    {
                        EnableArticulation = enableArticulation,
                        FocusAlgorithm = VideoConfig.EvaFunc,
                        Roi = new RoiRect(rect),
                        EnablePseudoColor = enablePseudo,
                        PseudoMin = enablePseudo ? (uint)Device.View.ImageView.PseudoSlider.ValueStart : 0,
                        PseudoMax = enablePseudo ? (uint)Device.View.ImageView.PseudoSlider.ValueEnd : 0,
                        ColormapTypes = Device.View.ImageView.Config.ColormapTypes,
                        PseudoChannel = 0
                    };
                    _localFrameProcessor?.SubmitFrame(pData, frameBytes, width, height, channels, bpp, width * channels * Math.Max(1, bpp / 8), request);
                }

                // Normal display (non-pseudo color)
                if (!enablePseudo)
                {
                    WriteableBitmap writeableBitmap = Device.View.ImageView.ImageShow.Source as WriteableBitmap;
                    bool needNewBitmap = writeableBitmap == null
                        || writeableBitmap.PixelWidth != width
                        || writeableBitmap.PixelHeight != height
                        || GetPixelFormat(channels, bpp) != writeableBitmap.Format;

                    if (needNewBitmap)
                    {
                        writeableBitmap = new WriteableBitmap(
                            width,
                            height,
                            96, 96,
                            GetPixelFormat(channels, bpp),
                            null);
                        Device.View.ImageView.ImageShow.Source = writeableBitmap;
                    }
                    writeableBitmap!.Lock();

                    OpenCvSharp.MatType matType = writeableBitmap.Format.GetPixelFormat();

                    // 2. 包装源数据 (pData) -> Zero Copy
                    using var srcMat = OpenCvSharp.Mat.FromPixelData(height, width, matType, pData);

                    using var dstMat = OpenCvSharp.Mat.FromPixelData(height, width, matType, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);

                    if (Device.DisplayConfig.FlipMode == CVImageFlipMode.None)
                    {
                        srcMat.CopyTo(dstMat);
                    }
                    else
                    {
                        OpenCvSharp.Cv2.Flip(srcMat, dstMat, (OpenCvSharp.FlipMode)Device.DisplayConfig.FlipMode);
                    }

                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));

                    writeableBitmap.Unlock();
                }

                Interlocked.Increment(ref frameCount);
                if (fpsTimer.ElapsedMilliseconds >= 1000)
                {
                    lastFps = (double)frameCount * 1000 / fpsTimer.ElapsedMilliseconds;
                    logger.Info($"Current FPS: {lastFps:F2}");
                    Interlocked.Exchange(ref frameCount, 0);
                    fpsTimer.Restart();
                }

                if (!enablePseudo)
                {
                    DVText.Attribute.Text = $"fps:{lastFps:F1} Articulation: {articulation:F5}";
                    if (first)
                    {
                        first = false;
                        Device.View.ImageView.Zoombox1.ZoomUniform();
                    }
                }
            }));
            return 0;
        }


        cvCameraCSLib.QHYCCDProcCallBack callback;
        private int frameCount;
        private readonly Stopwatch fpsTimer = new Stopwatch();
        private double lastFps;
        bool first = true;

        private async void Video1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            if (Device.DisplayConfig.IsLocalVideoOpen)
            {
                cvCameraCSLib.CM_UnregisterCallBack(m_hCamHandle);
                cvCameraCSLib.CM_Close(m_hCamHandle);
                _localFrameProcessor?.Dispose();
                _localFrameProcessor = null;
                Device.DisplayConfig.IsLocalVideoOpen = false;
                fpsTimer.Stop();
                // Unsubscribe from pseudo-color changes
                Device.View.ImageView.Config.PseudoChanged -= VideoConfig_PseudoChanged;

                // Cleanup visuals
                if (_visualsAdded)
                {
                    Device.View.ImageView.ImageShow.RemoveVisualCommand(DVRectangleText);
                    Device.View.ImageView.ImageShow.RemoveVisualCommand(DVText);
                    _visualsAdded = false;
                }

                UpdateLocalVideoButtonState();

                return;
            }

            if (_isOpeningLocalVideo)
            {
                return;
            }

            _isOpeningLocalVideo = true;
            button.IsEnabled = false;
            ButtonProgressBarLocalVideo.TargetTime = Math.Max(500, Device.DisplayConfig.LocalVideoOpenTime);
            ButtonProgressBarLocalVideo.Start();
            logger.Info("初始化视频模式");
            bool localVideoOpened = false;

            try
            {
                _localFrameProcessor ??= new VideoFrameProcessor(HandleLocalFrameProcessed);

                (bool isSuccess, string errorMessage) = await Task.Run(OpenLocalVideoInternal);
                if (!isSuccess)
                {
                    _localFrameProcessor?.Dispose();
                    _localFrameProcessor = null;
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
                    }
                    return;
                }

                if (!_visualsAdded)
                {
                    Device.View.ImageView.ImageShow.AddVisualCommand(DVRectangleText);
                    Device.View.ImageView.ImageShow.AddVisualCommand(DVText);
                    _visualsAdded = true;
                }

                if (Device.View.ImageView.Config.IsPseudo)
                {
                    VideoConfig.IsUseCacheFile = true;
                    VideoConfig.IsCalArtculation = true;
                }

                Device.View.ImageView.Config.PseudoChanged -= VideoConfig_PseudoChanged;
                Device.View.ImageView.Config.PseudoChanged += VideoConfig_PseudoChanged;

                button.Content = "Close Video";
                first = true;
                articulation = 0;
                Interlocked.Exchange(ref frameCount, 0);
                lastFps = 0;
                fpsTimer.Restart();
                Device.DisplayConfig.IsLocalVideoOpen = true;
                localVideoOpened = true;
                logger.Info("视频模式初始化结束");
            }
            finally
            {
                ButtonProgressBarLocalVideo.Stop();
                if (localVideoOpened)
                {
                    Device.DisplayConfig.LocalVideoOpenTime = ButtonProgressBarLocalVideo.Elapsed;
                    ConfigHandler.GetInstance().Save<DisplayConfigManager>();
                }

                UpdateLocalVideoButtonState();
                button.IsEnabled = true;
                _isOpeningLocalVideo = false;
            }
        }

        private void VideoConfig_PseudoChanged(object? sender, EventArgs e)
        {
            if (Device.View.ImageView.Config.IsPseudo)
                VideoConfig.IsUseCacheFile = true;
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

            cvCameraCSLib.CM_SetFlip(m_hCamHandle, (int)Device.DisplayConfig.FlipMode);
            cvCameraCSLib.CM_SetExpTime(m_hCamHandle, (float)Device.DisplayConfig.ExpTime);
            cvCameraCSLib.CM_SetGain(m_hCamHandle, Device.DisplayConfig.Gain);
            callback ??= new cvCameraCSLib.QHYCCDProcCallBack(QHYCCDProcCallBackFunction);
            cvCameraCSLib.CM_SetCallBack(m_hCamHandle, callback, IntPtr.Zero);

            return (true, string.Empty);
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

        private void HandleLocalFrameProcessed(VideoFrameProcessingResult result)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!Device.DisplayConfig.IsLocalVideoOpen)
                {
                    if (result.PseudoImage is HImage staleImage)
                    {
                        staleImage.Dispose();
                    }
                    return;
                }

                if (result.Articulation is double value)
                {
                    articulation = value;
                    logger.Info($"Video Articulation: {articulation}");
                }

                if (result.PseudoImage is HImage pseudoImage)
                {
                    if (Device.View.ImageView.Config.IsPseudo)
                    {
                        VideoFrameUiHelper.ApplyPseudoImage(Device.View.ImageView, pseudoImage);
                        if (first)
                        {
                            first = false;
                            Device.View.ImageView.Zoombox1.ZoomUniform();
                        }
                    }
                    else
                    {
                        pseudoImage.Dispose();
                    }
                }

                DVText.Attribute.Text = $"fps:{lastFps:F1} Articulation: {articulation:F5}";
            }));
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
            //cvCameraCSLib.CM_SetFlip(m_hCamHandle, (int)Device.DisplayConfig.FlipMode);

        }
    }
}

