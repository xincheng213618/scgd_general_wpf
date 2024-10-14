using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Scheduler;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using cvColorVision;
using CVCommCore;
using log4net;
using MQTTMessageLib.Camera;
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

namespace ColorVision.Engine.Services.Devices.Camera
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
    public partial class DisplayCamera : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayCamera));
        public DeviceCamera Device { get; set; }
        public MQTTCamera DService { get => Device.DService; }

        public ViewCamera View { get; set; }
        public string DisPlayName => Device.Config.Name;

        public DisplayCamera(DeviceCamera device)
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

            void UpdateTemplate()
            {
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams.CreateEmpty();
                ComboxCalibrationTemplate.SelectedIndex = 0;

            }
            UpdateTemplate();
            Device.ConfigChanged += (s, e) => UpdateTemplate();
            PhyCameraManager.GetInstance().Loaded += (s, e) => UpdateTemplate();
            ComboxAutoExpTimeParamTemplate.ItemsSource = TemplateAutoExpTimeParam.Params;
            ComboxAutoExpTimeParamTemplate.SelectedIndex = 0;

            ComboxAutoExpTimeParamTemplate1.ItemsSource = TemplateAutoExpTimeParam.Params.CreateEmpty();
            ComboxAutoExpTimeParamTemplate1.SelectedIndex = 0;




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
                    case DeviceStatusType.Closed:
                        SetVisibility(ButtonOpen, Visibility.Visible);
                        CroppedBitmaps.Clear();
                        smallWindowImages.Clear();
                        break;
                    case DeviceStatusType.LiveOpened:
                        SetVisibility(StackPanelOpen, Visibility.Visible);
                        SetVisibility(ButtonClose, Visibility.Visible);

                        Device.CameraVideoControl ??= new CameraVideoControl();
                        if (!DService.IsVideoOpen)
                        {
                            DService.CurrentTakeImageMode = TakeImageMode.Live;
                            string host = Device.Config.VideoConfig.Host;
                            int port = Tool.GetFreePort(Device.Config.VideoConfig.Port);
                            port = Device.CameraVideoControl.Open(host, port);
                            if (port > 0)
                            {
                                View.ImageView.ImageShow.Source = null;
                                Device.CameraVideoControl.CameraVideoFrameReceived -= CameraVideoFrameReceived;
                                Device.CameraVideoControl.CameraVideoFrameReceived += CameraVideoFrameReceived;
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
                            Device.CameraVideoControl ??= new CameraVideoControl();
                            Device.CameraVideoControl.Start(pm_live.IsLocal, mapName, pm_live.FrameInfo.width, pm_live.FrameInfo.height);
                            break;
                        default:
                            break;
                    }
                }

            };
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
                    StackPanelOpen.Visibility = Visibility.Visible;
                    msgRecord.MsgSucessed -= msgRecordStateChangedHandler;
                };
                msgRecord.MsgSucessed += msgRecordStateChangedHandler;

            }
        }

        private void GetData_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return;

                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    if (param.Id != -1)
                    {
                        if (Device.PhyCamera != null && Device.PhyCamera.CameraLicenseModel?.DevCaliId == null)
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), "使用校正模板需要先配置校正服务", "ColorVision");
                            return;
                        }
                    }

                    double[] expTime = null;
                    if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
                    else expTime = new double[] { Device.Config.ExpTime };
                    MsgRecord msgRecord = DService.GetData(expTime, param, autoExpTimeParam);
                    ServicesHelper.SendCommand(button,msgRecord);
                }
                else
                {
                    double[] expTime = null;
                    if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
                    else expTime = new double[] { Device.Config.ExpTime };
                    MsgRecord msgRecord = DService.GetData(expTime, new CalibrationParam() { Id = -1,Name ="Empty" }, autoExpTimeParam);
                    ServicesHelper.SendCommand(button, msgRecord);
                    msgRecord.MsgRecordStateChanged += (s) =>
                    {
                        if (s == MsgRecordState.Timeout)
                        {
                            MessageBox1.Show("取图失败,请检查是否为物理相机配置校正");
                        }
                    };
                }  

            }
        }


        public void GetData()
        {
            if (ComboxAutoExpTimeParamTemplate1.SelectedValue is not AutoExpTimeParam autoExpTimeParam) return;
            if (ComboxCalibrationTemplate.SelectedValue is not CalibrationParam param) return;

            double[] expTime = null;
            if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
            else expTime = new double[] { Device.Config.ExpTime };
            MsgRecord msgRecord = DService.GetData(expTime, param, autoExpTimeParam);
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
                Device.CameraVideoControl ??= new CameraVideoControl();
                if (!DService.IsVideoOpen)
                {
                    DService.CurrentTakeImageMode = TakeImageMode.Live;
                    string host = Device.Config.VideoConfig.Host;
                    int port = Tool.GetFreePort(Device.Config.VideoConfig.Port);
                    port = Device.CameraVideoControl.Open(host, port);
                    if (port > 0)
                    {
                        MsgRecord msg = DService.OpenVideo(host, port);
                        msg.MsgRecordStateChanged += (s) =>
                        {
                            if (s == MsgRecordState.Fail)
                            {
                                Device.CameraVideoControl.CameraVideoFrameReceived -= CameraVideoFrameReceived;
                                Device.CameraVideoControl.Close();
                                DService.Close();
                            }
                            else
                            {
                                ButtonOpen.Visibility = Visibility.Collapsed;
                                ButtonClose.Visibility = Visibility.Visible;
                                StackPanelOpen.Visibility = Visibility.Visible;
                            }
                        };
                        ServicesHelper.SendCommand(button, msg);
                        View.ImageView.ImageShow.Source = null;
                        Device.CameraVideoControl.CameraVideoFrameReceived -= CameraVideoFrameReceived;
                        Device.CameraVideoControl.CameraVideoFrameReceived += CameraVideoFrameReceived;
                    }
                    else
                    {
                        MessageBox1.Show("视频模式下，本地端口打开失败");
                        logger.Debug($"Local socket open failed.{host}:{port}");
                    }
                }
            }

        }

        List<CroppedBitmap> CroppedBitmaps = new List<CroppedBitmap>();
        List<ImageView> smallWindowImages = new List<ImageView>();

        public void CameraVideoFrameReceived(WriteableBitmap bmp)
        {
            View.ImageView.ImageShow.Source = bmp;

            if (CroppedBitmaps.Count == 0)
            {
                foreach (var item in Device.Config.ROIParams)
                {
                    CroppedBitmap croppedBitmap = new CroppedBitmap(bmp, new Int32Rect(item.X, item.Y, item.Width, item.Height));
                    ImageView smallWindowImage = new ImageView() { };
                    smallWindowImage.ImageShow.Source = croppedBitmap;
                    CroppedBitmaps.Add(croppedBitmap);
                    smallWindowImages.Add(smallWindowImage);
                    Window window = new Window() { Content = smallWindowImage ,Height =300,Width =300 ,Owner =Application.Current.MainWindow};
                    window.Show();
                }
            }
            else
            {
                for (int i = 0; i < CroppedBitmaps.Count; i++)
                {
                    CroppedBitmap croppedBitmap = new CroppedBitmap(bmp, Device.Config.ROIParams[i].ToInt32Rect());
                    smallWindowImages[i].ImageShow.Source = croppedBitmap;
                }
            }

        }


        private void AutoFocus_Click(object sender, RoutedEventArgs e)
        {
            MsgRecord msgRecord = DService.AutoFocus();
            ServicesHelper.SendCommand(msgRecord, "自动聚焦",false);
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
        }

        private void EditAutoExpTime(object sender, RoutedEventArgs e)
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoExpTimeParam(), ComboxAutoExpTimeParamTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow() };
            windowTemplate.ShowDialog();
        }

        private void EditAutoExpTime1(object sender, RoutedEventArgs e)
        {
            var windowTemplate = new TemplateEditorWindow(new TemplateAutoExpTimeParam(), ComboxAutoExpTimeParamTemplate1.SelectedIndex - 1) { Owner = Application.Current.GetActiveWindow() };
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
    }
}

