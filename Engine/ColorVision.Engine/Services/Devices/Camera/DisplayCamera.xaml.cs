using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons.HDR;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using FlowEngineLib.Algorithm;
using log4net;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices.Camera
{


    public class DisplayCameraConfig: IDisplayConfigBase
    {
        public double TakePictureDelay { get; set; }
        public int CalibrationTemplateIndex { get; set; }
        public int ExpTimeParamTemplateIndex { get; set; }
        public int ExpTimeParamTemplate1Index { get; set; }
        public int HDRTemplateIndex { get; set; }

        public int AutoFocusTemplateIndex { get; set; }

        public double OpenTime { get; set; } = 10;
        public double CloseTime { get; set; } = 10;

        public ReferenceLineParam ReferenceLineParam { get => _ReferenceLineParam; set { _ReferenceLineParam = value; OnPropertyChanged(); } }
        private ReferenceLineParam _ReferenceLineParam = new ReferenceLineParam();

    }


    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class DisplayCamera : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayCamera));
        public DeviceCamera Device { get; set; }
        public MQTTCamera DService { get => Device.DService; }
        public DisplayCameraConfig DisplayCameraConfig => Device.DisplayConfig;

        public ViewCamera View { get; set; }
        public string DisPlayName => Device.Config.Name;

        public DisplayCamera(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
            CommandBindings.Add(new CommandBinding(EngineCommands.TakePhotoCommand, GetData_Click, (s, e) => e.CanExecute = Device.DService.DeviceStatus == DeviceStatusType.Opened));
        }


        ButtonProgressBar ButtonProgressBarGetData { get; set; }
        ButtonProgressBar ButtonProgressBarOpen { get; set; }
        ButtonProgressBar ButtonProgressBarClose { get; set; }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            this.AddViewConfig(View, ComboxView);

            this.ContextMenu = Device.ContextMenu;


            ButtonProgressBarGetData = new ButtonProgressBar(ProgressBar, TakePhotoButton);
            ButtonProgressBarOpen = new ButtonProgressBar(ProgressBarOpen, OpenButton);
            ButtonProgressBarClose = new ButtonProgressBar(ProgressBarClose, CloseButton);

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


            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; };
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
                HideAllButtons();

                switch (status)
                {
                    case DeviceStatusType.Unauthorized:
                        SetVisibility(ButtonUnauthorized, Visibility.Visible);
                        break;
                    case DeviceStatusType.Unknown:
                        SetVisibility(TextBlockUnknow, Visibility.Visible);
                        break;
                    case DeviceStatusType.OffLine:
                        SetVisibility(ButtonOffline, Visibility.Visible);
                        break;
                    case DeviceStatusType.UnInit:
                        SetVisibility(ButtonInit, Visibility.Visible);
                        break;
                    case DeviceStatusType.Closing:
                    case DeviceStatusType.Closed:
                        SetVisibility(ButtonOpen, Visibility.Visible);
                        break;
                    case DeviceStatusType.LiveOpened:
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
                        SetVisibility(StackPanelOpen, Visibility.Visible);
                        SetVisibility(ButtonClose, Visibility.Visible);
                        TakePhotoButton.Visibility = Visibility.Visible;
                        break;
                    default:
                        // No specific action needed
                        break;
                }
            }
            UpdateUI(DService.DeviceStatus);
            DService.DeviceStatusChanged += UpdateUI;
            this.ApplyChangedSelectedColor(DisPlayBorder);

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
                ServicesHelper.SendCommand(button,msgRecord);
                MsgRecordStateChangedHandler msgRecordStateChangedHandler = null;
                msgRecordStateChangedHandler = (e) =>
                {
                    ButtonOpen.Visibility = Visibility.Collapsed;
                    ButtonClose.Visibility = Visibility.Visible;
                    StackPanelOpen.Visibility = Visibility.Visible;
                    msgRecord.MsgRecordStateChanged -= msgRecordStateChangedHandler;
                    ButtonProgressBarOpen.Stop();
                    DisplayCameraConfig.OpenTime = ButtonProgressBarOpen.Elapsed;
                };
                msgRecord.MsgRecordStateChanged += msgRecordStateChangedHandler;

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
            if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
            else expTime = new double[] { Device.Config.ExpTime };






            if (ComboBoxHDRTemplate.SelectedValue is not ParamBase HDRparamBase) return;
            TakePhotoButton.Visibility = Visibility.Hidden;

            MsgRecord msgRecord = DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);

            ButtonProgressBarGetData.Start();
            ButtonProgressBarGetData.TargetTime = Device.Config.ExpTime + DisplayCameraConfig.TakePictureDelay;
            logger.Info($"正在取图：ExpTime{Device.Config.ExpTime} othertime{DisplayCameraConfig.TakePictureDelay}");
            Device.SetMsgRecordChanged(msgRecord);

            ServicesHelper.SendCommand(TakePhotoButton, msgRecord);
            msgRecord.MsgRecordStateChanged += (s) =>
            {
                ButtonProgressBarGetData.Stop();
                DisplayCameraConfig.TakePictureDelay = ButtonProgressBarGetData.Elapsed - Device.Config.ExpTime;
                if (s == MsgRecordState.Timeout)
                {
                    MessageBox1.Show("取图超时,请重设超时时间或者是否为物理相机配置校正");
                }
                if (s== MsgRecordState.Fail)
                {
                    View.SearchAll();
                }
            };
        }

        public MsgRecord? TakePhoto(double exp =0)
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
                if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
                else expTime = new double[] { Device.Config.ExpTime };
            }
            else
            {
                if (Device.Config.IsExpThree) { expTime = new double[] { exp, exp, exp }; }
                else expTime = new double[] { exp };
            }

            if (ComboBoxHDRTemplate.SelectedValue is not ParamBase HDRparamBase) return null;

            return DService.GetData(expTime, param, autoExpTimeParam,HDRparamBase);

        }


        public void GetData()
        {
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return;
            if (ComboxCalibrationTemplate.SelectedValue is not CalibrationParam param) return;

            double[] expTime = null;
            if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
            else expTime = new double[] { Device.Config.ExpTime };


            if (ComboBoxHDRTemplate.SelectedValue is not ParamBase HDRparamBase) return;

            MsgRecord msgRecord = DService.GetData(expTime, param, autoExpTimeParam, HDRparamBase);
        }

        private void AutoExplose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxAutoExpTimeParamTemplate.SelectedValue is AutoExpTimeParam param)
                {
                    var msgRecord = DService.GetAutoExpTime(param);
                    msgRecord.MsgRecordStateChanged += (e) =>
                    {
                        if (e == MsgRecordState.Timeout)
                        {
                            MessageBox1.Show("自动曝光超时，请检查服务日志", "ColorVision");
                        };
                        if (e == MsgRecordState.Fail)
                        {
                            MessageBox1.Show($"自动曝光失败，请检查服务日志{Environment.NewLine}{msgRecord.MsgReturn.ToString()}" , "ColorVision");
                        };
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
                        MsgRecord msg = DService.OpenVideo(host, port);
                        msg.MsgRecordStateChanged += (s) =>
                        {
                            if (s == MsgRecordState.Fail)
                            {
                                Device.CameraVideoControl.Close();
                                DService.Close();
                                DService.IsVideoOpen = false;
                            }
                            else
                            {
                                DeviceOpenLiveResult pm_live = JsonConvert.DeserializeObject<DeviceOpenLiveResult>(JsonConvert.SerializeObject(msg.MsgReturn.Data));
                                string mapName = Device.Code;
                                if (pm_live.IsLocal) mapName = pm_live.MapName;
                                Device.CameraVideoControl.Startup(mapName, View.ImageView);

                                DService.IsVideoOpen = true;
                                ButtonOpen.Visibility = Visibility.Collapsed;
                                ButtonClose.Visibility = Visibility.Visible;
                                StackPanelOpen.Visibility = Visibility.Visible;
                            }
                        };
                        ServicesHelper.SendCommand(button, msg);
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
            MsgRecord msg = DService.AutoFocus(param);
            ServicesHelper.SendCommand(sender, msg);
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
                MsgRecordStateChangedHandler msgRecordStateChangedHandler = null;
                msgRecordStateChangedHandler = (e) =>
                {
                    if(e == MsgRecordState.Timeout)
                    {
                        MessageBox.Show("关闭相机超时,请查看日志并排查问题");
                        return;
                    }

                    DService.IsVideoOpen = false;
                    ButtonOpen.Visibility = Visibility.Visible;
                    ButtonClose.Visibility = Visibility.Collapsed;
                    StackPanelOpen.Visibility = Visibility.Collapsed;
                    msgRecord.MsgRecordStateChanged -= msgRecordStateChangedHandler;
                    ButtonProgressBarClose.Stop();
                    DisplayCameraConfig.CloseTime = ButtonProgressBarClose.Elapsed;
                };
                msgRecord.MsgRecordStateChanged += msgRecordStateChangedHandler;
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
            msgRecord.MsgRecordStateChanged += (e) =>
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
    }
}

