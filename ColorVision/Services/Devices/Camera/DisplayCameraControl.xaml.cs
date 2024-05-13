using ColorVision.Common.Utilities;
using ColorVision.Extension;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Video;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.Services.Msg;
using ColorVision.Services.PhyCameras;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.Themes;
using ColorVision.UI;
using cvColorVision;
using log4net;
using MQTTMessageLib;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            PreviewMouseDown += UserControl_PreviewMouseDown;
        }

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Parent is StackPanel stackPanel)
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
            DataContext = Device;

            this.AddViewConfig(View, ComboxView);

            ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
            ComboxCalibrationTemplate.SelectedIndex = 0;
            Device.ConfigChanged += (s, e) =>
            {
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
                ComboxCalibrationTemplate.SelectedIndex = 0;
            };
            PhyCameraManager.GetInstance().Loaded += (s, e) =>
            {
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
                ComboxCalibrationTemplate.SelectedIndex = 0;
            };

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
                        ButtonClose.Visibility = Visibility.Collapsed;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        break;
                    case DeviceStatusType.Closing:
                        break;
                    case DeviceStatusType.LiveOpened:
                    case DeviceStatusType.Opened:
                        ButtonInit.Visibility = Visibility.Collapsed;
                        StackPanelOpen.Visibility = Visibility.Visible;
                        if (!DService.IsVideoOpen)
                            StackPanelImage.Visibility = Visibility.Visible;

                        ButtonOpen.Visibility = Visibility.Collapsed;
                        ButtonClose.Visibility = Visibility.Visible;

                        break;
                    case DeviceStatusType.Opening:
                        break;
                    default:
                        break;
                }
            };

            DService.MsgReturnReceived += (msg) =>
            {
                if (msg.Code == 0)
                {
                    switch (msg.EventName)
                    {
                        case MQTTCameraEventEnum.Event_OpenLive:
                            DeviceOpenLiveResult pm_live = JsonConvert.DeserializeObject<DeviceOpenLiveResult>(JsonConvert.SerializeObject(msg.Data));
                            string mapName = Device.Code;
                            if (pm_live.IsLocal) mapName = pm_live.MapName;
                            CameraVideoControl ??= new CameraVideoControl();
                            CameraVideoControl.Start(pm_live.IsLocal, mapName, pm_live.FrameInfo.width, pm_live.FrameInfo.height);
                            break;
                        default:
                            break;
                    }
                }

            };
            SelectChanged += (s, e) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }






        private void CameraInit_Click(object sender, RoutedEventArgs e)
        {
            ServicesHelper.SendCommandEx(sender, DService.GetAllCameraID);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.Open(DService.Config.CameraID, Device.Config.TakeImageMode, (int)DService.Config.ImageBpp);
                ServicesHelper.SendCommand(button,msgRecord);
                MsgRecordSucessChangedHandler msgRecordStateChangedHandler = null;
                msgRecordStateChangedHandler = (e) =>
                {
                    ButtonOpen.Visibility = Visibility.Collapsed;
                    ButtonClose.Visibility = Visibility.Visible;
                    StackPanelImage.Visibility = Visibility.Visible;
                    msgRecord.MsgSucessed -= msgRecordStateChangedHandler;
                };
                msgRecord.MsgSucessed += msgRecordStateChangedHandler;

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
                    ServicesHelper.SendCommand(button,msgRecord);
                }
                else
                {
                    double[] expTime = null;
                    if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
                    else expTime = new double[] { Device.Config.ExpTime };
                    MsgRecord msgRecord = DService.GetData(expTime, new CalibrationParam() { Id = -1,Name ="Empty" });
                    ServicesHelper.SendCommand(button, msgRecord);
                }  

            }
        }

        private void AutoExplose_Click(object sender, RoutedEventArgs e)
        {
            ServicesHelper.SendCommandEx(sender, DService.GetAutoExpTime);
        }

        public CameraVideoControl CameraVideoControl { get; set; }

        private void Video_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
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
                        MsgRecord msg = DService.OpenVideo(host, port);
                        msg.MsgRecordStateChanged += (s) =>
                        {
                            if (s == MsgRecordState.Fail)
                            {
                                CameraVideoControl.CameraVideoFrameReceived -= CameraVideoFrameReceived;
                                CameraVideoControl.Close();
                                DService.Close();
                            }
                            else
                            {
                                ButtonOpen.Visibility = Visibility.Collapsed;
                                ButtonClose.Visibility = Visibility.Visible;
                            }
                        };
                        ServicesHelper.SendCommand(button, msg);
                        CameraVideoControl.CameraVideoFrameReceived -= CameraVideoFrameReceived;
                        CameraVideoControl.CameraVideoFrameReceived += CameraVideoFrameReceived;
                    }
                    else
                    {
                        MessageBox.Show("视频模式下，本地端口打开失败");
                        logger.ErrorFormat("Local socket open failed.{0}:{1}", host, ConfigHandler.GetInstance().SoftwareConfig.VideoConfig.Port);
                    }
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
            ServicesHelper.SendCommandEx(sender, DService.AutoFocus);
        }

        private void SetChannel()
        {
            MsgSend msg = new()
            {
                EventName = "SetParam",
                Params = new Dictionary<string, object>() { { "Func",new List<ParamFunction> (){
                    new() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 0 }, { "nPort", DService.Config.CFW.ChannelCfgs[0].Cfwport },{ "eImgChlType", (int)DService.Config.CFW.ChannelCfgs[0].Chtype } } },
                    new() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 1 }, { "nPort", DService.Config.CFW.ChannelCfgs[1].Cfwport },{ "eImgChlType", (int)DService.Config.CFW.ChannelCfgs[1].Chtype } } },
                    new() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 2 }, { "nPort", DService.Config.CFW.ChannelCfgs[2].Cfwport },{ "eImgChlType", (int)DService.Config.CFW.ChannelCfgs[2].Chtype } } },
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
            if (Device.PhyCamera == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "在使用校正前，请先配置对映的物理相机", "ColorVision");
                return;
            }

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

                        if (Device.PhyCamera.CalibrationParams.Count > 0)
                        {
                            calibration = new CalibrationControl(Device.PhyCamera, Device.PhyCamera.CalibrationParams[0].Value);
                        }
                        else
                        {
                            calibration = new CalibrationControl(Device.PhyCamera);
                        }
                        var ITemplate = new TemplateCalibrationParam() { Device = Device.PhyCamera, TemplateParams = Device.PhyCamera.CalibrationParams, CalibrationControl = calibration, Code = ModMasterType.Calibration, Title = "校正参数设置" };

                        windowTemplate = new WindowTemplate(ITemplate, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
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
                CameraVideoControl.Close();
            MsgRecord msgRecord = ServicesHelper.SendCommandEx(sender, () => DService.Close());
            if (msgRecord != null)
            {
                MsgRecordStateChangedHandler msgRecordStateChangedHandler = null;
                msgRecordStateChangedHandler = (e) =>
                {
                    DService.IsVideoOpen = false;
                    ButtonOpen.Visibility = Visibility.Visible;
                    ButtonClose.Visibility = Visibility.Collapsed;
                    StackPanelImage.Visibility = Visibility.Collapsed;
                    msgRecord.MsgRecordStateChanged -= msgRecordStateChangedHandler;
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

