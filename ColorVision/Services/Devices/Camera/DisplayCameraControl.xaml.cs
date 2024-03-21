using ColorVision.Common.Utilities;
using ColorVision.Extension;
using ColorVision.Net;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Calibration.Templates;
using ColorVision.Services.Devices.Camera.Video;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.Services.Msg;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.Solution;
using ColorVision.Themes;
using cvColorVision;
using log4net;
using MQTTMessageLib;
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Services.Devices.Camera
{
    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class DisplayCameraControl : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayCameraControl));
        public DeviceCamera Device { get; set; }
        public MQTTCamera DService { get => Device.DeviceService; }

        public ViewCamera View { get; set; }


        public DisplayCameraControl(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick; 
            this.PreviewMouseDown += UserControl_PreviewMouseDown;
        }
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; 
                DisPlayBorder.BorderBrush = value ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");    } }
        private bool _IsSelected;

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
        }

        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; }  

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;

            this.AddViewConfig(View, ComboxView);

            CalibrationParamInit();
            Device.ConfigChanged +=(s,e)=> CalibrationParamInit();

            StackPanelOpen.Visibility = Visibility.Visible;
            StackPanelImage.Visibility = Visibility.Collapsed;
            ButtonOpen.Visibility = Visibility.Collapsed;

            if (DService.DeviceStatus == DeviceStatusType.Unknown)
            {
                StackPanelOpen.Visibility = Visibility.Visible;
                ButtonOpen.Visibility = Visibility.Visible;
                ButtonInit.Visibility = Visibility.Collapsed;
            }
            DService.DeviceStatusChanged += (e) =>
            {
                switch (e)
                {
                    case DeviceStatusType.OffLine:
                        ButtonInit.Visibility = Visibility.Visible;
                        StackPanelOpen.Visibility = Visibility.Collapsed;
                        break;
                    case DeviceStatusType.Closed:
                        ButtonInit.Visibility = Visibility.Collapsed;
                        StackPanelOpen.Visibility = Visibility.Visible;
                        ButtonOpen.Visibility = Visibility.Visible;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        ButtonClose.Visibility = Visibility.Collapsed;
                        break;
                    case DeviceStatusType.Closing:
                        break;
                    case DeviceStatusType.LiveOpened:
                    case DeviceStatusType.Opened:
                        ButtonInit.Visibility = Visibility.Collapsed;
                        ButtonOpen.Visibility = Visibility.Collapsed;
                        ButtonClose.Visibility = Visibility.Visible;
                        if (!DService.IsVideoOpen)
                        {
                            StackPanelImage.Visibility = Visibility.Visible;
                        }
                        break;
                    case DeviceStatusType.Opening:
                        break;
                    default:
                        break;
                }
            };


            DService.OnMessageRecved += (s,e) =>
            {
                if (e.ResultCode == 0)
                {
                    switch (e.EventName)
                    {
                        case MQTTCameraEventEnum.Event_OpenLive:
                            DeviceOpenLiveResult pm_live = JsonConvert.DeserializeObject<DeviceOpenLiveResult>(JsonConvert.SerializeObject(e.Data));
                            string mapName = Device.Code;
                            if (pm_live.IsLocal) mapName = pm_live.MapName;
                            CameraVideoControl.Start(pm_live.IsLocal, mapName, pm_live.FrameInfo.width, pm_live.FrameInfo.height);
                            break;
                        default:
                            break;
                    }
                }

            };
        }



        private void CalibrationParamInit()
        {
            CalibrationParams = new ObservableCollection<TemplateModel<CalibrationParam>>();
            CalibrationParams.Insert(0, new TemplateModel<CalibrationParam>("Empty", new CalibrationParam() { Id = -1 }));

            if (Device.DeviceCalibration != null)
            {
                foreach (var item in Device.DeviceCalibration.CalibrationParams)
                    CalibrationParams.Add(item);

                Device.DeviceCalibration.CalibrationParams.CollectionChanged -= CalibrationParams_CollectionChanged;
                Device.DeviceCalibration.CalibrationParams.CollectionChanged += CalibrationParams_CollectionChanged;
            }

            ComboxCalibrationTemplate.ItemsSource = CalibrationParams;
            ComboxCalibrationTemplate.SelectedIndex = 0;
        }

        private void CalibrationParams_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                        foreach (TemplateModel<CalibrationParam> newItem in e.NewItems)
                            CalibrationParams.Add(newItem);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                        foreach (TemplateModel<CalibrationParam> newItem in e.OldItems)
                            CalibrationParams.Remove(newItem);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    CalibrationParams.Clear();
                    CalibrationParams.Insert(0, new TemplateModel<CalibrationParam>("Empty", new CalibrationParam()) { Id = -1 });
                    break;
            }
        }

        private void CameraInit_Click(object sender, RoutedEventArgs e)
        {
            DService.GetAllCameraID();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.Open(DService.Config.CameraID, Device.Config.TakeImageMode, (int)DService.Config.ImageBpp);
                Helpers.SendCommand(button,msgRecord);
            }
        }

        private void GetData_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    double[] expTime = null;
                    if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
                    else expTime = new double[] { Device.Config.ExpTime };
                    MsgRecord msgRecord = DService.GetData(expTime, param);
                    Helpers.SendCommand(button,msgRecord);
                }
            }
        }

        private void AutoExplose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                MsgRecord msgRecord = DService.GetAutoExpTime();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        public CameraVideoControl CameraVideoControl { get; set; }

        private void Video_Click(object sender, RoutedEventArgs e)
        {
            CameraVideoControl ??= new CameraVideoControl();
            if (!DService.IsVideoOpen)
            {
                DService.CurrentTakeImageMode = TakeImageMode.Live;
                string host = ConfigHandler.GetInstance().SoftwareConfig.VideoConfig.Host;
                int port = ConfigHandler.GetInstance().SoftwareConfig.VideoConfig.Port;
                //bool IsLocal = (host == "127.0.0.1");
                port = CameraVideoControl.Open(host, port);
                if (port > 0)
                {
                    MsgRecord msg = DService.OpenVideo(host, port, DService.Config.ExpTime);
                    msg.MsgRecordStateChanged += (s) =>
                    {
                        if (s == MsgRecordState.Fail)
                        {
                            CameraVideoControl.CameraVideoFrameReceived -= CameraVideoFrameReceived;
                            DService.Close();
                            CameraVideoControl.Close();
                        }
                    };
                    CameraVideoControl.CameraVideoFrameReceived -= CameraVideoFrameReceived;
                    CameraVideoControl.CameraVideoFrameReceived += CameraVideoFrameReceived;
                    StackPanelImage.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MessageBox.Show("视频模式下，本地端口打开失败");
                    logger.ErrorFormat("Local socket open failed.{0}:{1}", host, ConfigHandler.GetInstance().SoftwareConfig.VideoConfig.Port);
                }
            }
        }

        public void CameraVideoFrameReceived(System.Drawing.Bitmap bmp)
        {
            if (View.ImageView.ImageShow.Source is WriteableBitmap bitmap)
            {
                if(bitmap.Width!= bmp.Width)
                {
                    WriteableBitmap writeableBitmap = ImageUtil.BitmapToWriteableBitmap(bmp);
                    View.ImageView.ImageShow.Source = writeableBitmap;
                }
                else
                {
                    ImageUtil.BitmapCopyToWriteableBitmap(bmp, bitmap, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.PixelFormat);
                }
            }
            else
            {
                WriteableBitmap writeableBitmap = ImageUtil.BitmapToWriteableBitmap(bmp);
                View.ImageView.ImageShow.Source = writeableBitmap;
            }
        }


        private void AutoFocus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                MsgRecord msgRecord = DService.AutoFocus();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void SetChannel()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params = new Dictionary<string, object>() { { "Func",new List<ParamFunction> (){
                    new ParamFunction() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 0 }, { "nPort", DService.Config.CFW.ChannelCfgs[0].Cfwport },{ "eImgChlType", (int)DService.Config.CFW.ChannelCfgs[0].Chtype } } },
                    new ParamFunction() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 1 }, { "nPort", DService.Config.CFW.ChannelCfgs[1].Cfwport },{ "eImgChlType", (int)DService.Config.CFW.ChannelCfgs[1].Chtype } } },
                    new ParamFunction() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 2 }, { "nPort", DService.Config.CFW.ChannelCfgs[2].Cfwport },{ "eImgChlType", (int)DService.Config.CFW.ChannelCfgs[2].Chtype } } },
                } } }
            };
            DService.PublishAsyncClient(msg);
        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }

        TemplateControl TemplateControl { get; set; }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                TemplateControl = TemplateControl.GetInstance();
                SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show(Application.Current.MainWindow, Properties.Resource.DatabaseConnectionFailed, "ColorVision");
                    return;
                }
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "Calibration":
                        CalibrationControl calibration;
                        if (Device.DeviceCalibration != null)
                        {
                            if (Device.DeviceCalibration.CalibrationParams.Count > 0)
                            {
                                calibration = new CalibrationControl(Device.DeviceCalibration, Device.DeviceCalibration.CalibrationParams[0].Value);
                            }
                            else
                            {
                                calibration = new CalibrationControl(Device.DeviceCalibration);
                            }
                            windowTemplate = new WindowTemplate(TemplateType.Calibration, calibration, Device.DeviceCalibration, false);
                            windowTemplate.Owner = Window.GetWindow(this);
                            windowTemplate.ShowDialog();
                        }
                        else
                        {
                            MessageBox.Show("在使用校正前，请先配置对映的校正服务");
                        }

                        break;
                    default:
                        HandyControl.Controls.Growl.Info(Properties.Resource.UnderDevelopment);
                        break;
                }
            }
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(TextPos.Text, out int pos))
                {
                    var msgRecord = DService.Move(pos, CheckBoxIsAbs.IsChecked ?? true);
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }


        private void GoHome_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(TextPos.Text, out int pos))
                {
                    var msgRecord = DService.GoHome();
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }

        private void GetPosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.GetPosition();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void Move1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (double.TryParse(TextDiaphragm.Text, out double pos))
                {
                    var msgRecord = DService.MoveDiaphragm(pos);
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (DService.IsVideoOpen)
                    CameraVideoControl.Close();
                MsgRecord msgRecord = DService.Close();
                Helpers.SendCommand(button,msgRecord);

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




        private void SliderexpTime_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }


    }
}

