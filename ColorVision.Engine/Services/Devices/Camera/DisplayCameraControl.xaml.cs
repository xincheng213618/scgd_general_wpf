using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Scheduler;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Video;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.Services.Msg;
using ColorVision.Services.PhyCameras;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using cvColorVision;
using CVCommCore;
using log4net;
using MQTTMessageLib.Camera;
using Mysqlx.Crud;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Services.Devices.Camera
{
    public class CameraCaptureJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);
            schedulerInfo.RunCount++;
            Application.Current.Dispatcher.Invoke(() =>
            {
                schedulerInfo.Status = SchedulerStatus.Running;
            });
            // 定时任务逻辑
            Application.Current.Dispatcher.Invoke(() =>
            {
                var lsit = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();
                DeviceCamera deviceCamera = lsit.FirstOrDefault();
                deviceCamera?.DisplayCameraControlLazy.Value.GetData();


                schedulerInfo.Status = SchedulerStatus.Ready;
            });
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class DisplayCameraControl : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayCameraControl));
        public DeviceCamera Device { get; set; }
        public MQTTCamera DService { get => Device.DeviceService; }

        public ViewCamera View { get; set; }
        public string DisPlayName => Device.Config.Name;

        public LocalVideoConfig VideoConfig { get; set; }

        public DisplayCameraControl(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick; 
            PreviewMouseDown += UserControl_PreviewMouseDown;
            VideoConfig = LocalVideoConfig.Instance;
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

            void UpdateTemplate()
            {
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
                ComboxCalibrationTemplate.SelectedIndex = 0;
            }
            UpdateTemplate();
            Device.ConfigChanged += (s, e) => UpdateTemplate();
            PhyCameraManager.GetInstance().Loaded += (s, e) => UpdateTemplate();

            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) => element.Visibility = visibility;
                void HideAllButtons()
                {
                    SetVisibility(ButtonOpen, Visibility.Collapsed);
                    SetVisibility(ButtonInit, Visibility.Collapsed);
                    SetVisibility(ButtonOffline, Visibility.Collapsed);
                    SetVisibility(ButtonClose, Visibility.Collapsed);
                    SetVisibility(StackPanelImage, Visibility.Collapsed);
                }

                // Default state
                SetVisibility(StackPanelOpen, Visibility.Visible);
                HideAllButtons();

                switch (status)
                {
                    case DeviceStatusType.OffLine:
                        SetVisibility(StackPanelOpen, Visibility.Collapsed);
                        SetVisibility(ButtonOffline, Visibility.Visible);
                        break;
                    case DeviceStatusType.Unknown:
                    case DeviceStatusType.Unauthorized:
                    case DeviceStatusType.UnInit:
                        SetVisibility(StackPanelOpen, Visibility.Collapsed);
                        SetVisibility(ButtonInit, Visibility.Visible);
                        break;
                    case DeviceStatusType.Closed:
                        SetVisibility(ButtonOpen, Visibility.Visible);
                        break;
                    case DeviceStatusType.LiveOpened:
                    case DeviceStatusType.Opened:
                        if (!DService.IsVideoOpen)
                            SetVisibility(StackPanelImage, Visibility.Visible);
                        SetVisibility(ButtonClose, Visibility.Visible);
                        break;
                    case DeviceStatusType.Closing:
                    case DeviceStatusType.Opening:
                    default:
                        // No specific action needed
                        break;
                }
            }
            UpdateUI(DService.DeviceStatus);
            DService.DeviceStatusChanged += UpdateUI;

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
            void UpdateDisPlayBorder()
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            }
            UpdateDisPlayBorder();
            SelectChanged += (s, e) => UpdateDisPlayBorder();
            ThemeManager.Current.CurrentUIThemeChanged += (s) => UpdateDisPlayBorder();
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void CameraOffline_Click(object sender, RoutedEventArgs e)
        {
            ServicesHelper.SendCommandEx(sender, DService.GetAllCameraID);
        }

        private void CameraInit_Click(object sender, RoutedEventArgs e)
        {
            Device.EditCommand.RaiseExecute(sender);
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
                    msgRecord.MsgRecordStateChanged += (s) =>
                    {
                        if (s == MsgRecordState.Timeout)
                        {
                            MessageBox.Show("取图失败,请检查是否为物理相机配置校正");
                        }
                    };
                }  

            }
        }


        public void GetData()
        {
            if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
            {
                double[] expTime = null;
                if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
                else expTime = new double[] { Device.Config.ExpTime };
                MsgRecord msgRecord = DService.GetData(expTime, param);
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
                    string host = VideoConfig.Host;
                    int port = VideoConfig.Port;
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
                        logger.ErrorFormat("Local socket open failed.{0}:{1}", host, VideoConfig.Port);
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

                WindowTemplate windowTemplate;
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                {
                    MessageBox.Show(Application.Current.MainWindow, Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                    return;
                }
                var ITemplate = new TemplateCalibrationParam(Device.PhyCamera) ;
                windowTemplate = new WindowTemplate(ITemplate, ComboxCalibrationTemplate.SelectedIndex-1) { Owner = Application.Current.GetActiveWindow() };
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
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }


    }
}

