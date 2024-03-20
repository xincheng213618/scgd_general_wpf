using ColorVision.Common.Extension;
using ColorVision.Common.Utilities;
using ColorVision.Net;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Calibration.Templates;
using ColorVision.Services.Devices.Camera.Video;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.Services.Core;
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
using ColorVision.Extension;

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

        private NetFileUtil netFileUtil;
        private IPendingHandler? handler { get; set; }


        public DisplayCameraControl(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            InitializeComponent();

            netFileUtil = new NetFileUtil(SolutionManager.GetInstance().CurrentSolution.FullName+"\\Cache");
            netFileUtil.handler += NetFileUtil_handler;

            DService.OnMessageRecved += CameraService_OnMessageRecved;
            View.OnCurSelectionChanged += View_OnCurSelectionChanged;


            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500); // 设置延时时间，这里是500毫秒
            _timer.Tick += Timer_Tick; // 设置Tick事件处理程序

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
        private MeasureImgResultDao measureImgResultDao = new MeasureImgResultDao();


        private void View_OnCurSelectionChanged(ViewResultCamera data)
        {
            if (data.ResultCode == 0 && data.FilePath!=null)
            {
                string localName = netFileUtil.GetCacheFileFullName(data.FilePath);
                FileExtType fileExt = FileExtType.Src;
                switch (data.FileType)
                {
                    case CameraFileType.SrcFile:
                        fileExt = FileExtType.Src;
                        break;
                    case CameraFileType.RawFile:
                        fileExt = FileExtType.Raw;
                        break;
                    case CameraFileType.CIEFile:
                        fileExt = FileExtType.CIE;
                        break;
                    default:
                        break;
                }
                if (string.IsNullOrEmpty(localName) || !System.IO.File.Exists(localName))
                {
                    DService.DownloadFile(data.FilePath, fileExt);
                }
                else
                {
                    netFileUtil.OpenLocalFile(localName, fileExt);
                }
            }
        }


        private void CameraService_OnMessageRecved(object sender, MessageRecvArgs arg)
        {
            if (arg.ResultCode == 0)
            {
                switch (arg.EventName)
                {
                    case MQTTCameraEventEnum.Event_GetData:
                        int masterId = Convert.ToInt32(arg.Data.MasterId);
                        List<MeasureImgResultModel> resultMaster = null;
                        if (masterId > 0)
                        {
                            resultMaster = new List<MeasureImgResultModel>();
                            MeasureImgResultModel model = measureImgResultDao.GetById(masterId);
                            if (model != null)
                                resultMaster.Add(model);
                        }
                        else
                        {
                            resultMaster = measureImgResultDao.GetAllByBatchCode(arg.SerialNumber);
                        }
                        if (resultMaster != null)
                        {
                            foreach (MeasureImgResultModel result in resultMaster)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    View.ShowResult(result);
                                });
                            }
                        }
                        break;
                    case MQTTFileServerEventEnum.Event_File_Download:
                        //DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                        //FileDownload(pm_dl);
                        break;
                    case MQTTCameraEventEnum.Event_GetData_Channel:
                        DeviceGetChannelResult pm_dl_ch = JsonConvert.DeserializeObject<DeviceGetChannelResult>(JsonConvert.SerializeObject(arg.Data));
                        FileDownload(pm_dl_ch);
                        break;
                    case MQTTCameraEventEnum.Event_OpenLive:
                        DeviceOpenLiveResult pm_live = JsonConvert.DeserializeObject<DeviceOpenLiveResult>(JsonConvert.SerializeObject(arg.Data));
                        string mapName = Device.Code;
                        if (pm_live.IsLocal) mapName = pm_live.MapName;
                        CameraVideoControl.Start(pm_live.IsLocal, mapName, pm_live.FrameInfo.width, pm_live.FrameInfo.height);
                        break;
                }
            }
            else if (arg.ResultCode == 102)
            {
                switch (arg.EventName)
                {
                    case MQTTFileServerEventEnum.Event_File_Upload:
                        DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                        FileUpload(pm_up);
                        break;
                    case MQTTFileServerEventEnum.Event_File_Download:
                        DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                        FileDownload(pm_dl);
                        break;
                    case MQTTCameraEventEnum.Event_GetData_Channel:
                        DeviceGetChannelResult pm_dl_ch = JsonConvert.DeserializeObject<DeviceGetChannelResult>(JsonConvert.SerializeObject(arg.Data));
                        FileDownload(pm_dl_ch);
                        break;
                }
            }
            else
            {
                switch (arg.EventName)
                {
                    case MQTTCameraEventEnum.Event_GetData:
                        int masterId = Convert.ToInt32(arg.Data.MasterId);
                        List<MeasureImgResultModel> resultMaster = null;
                        if (masterId > 0)
                        {
                            resultMaster = new List<MeasureImgResultModel>();
                            MeasureImgResultModel model = measureImgResultDao.GetById(masterId);
                            if (model != null)
                                resultMaster.Add(model);
                        }
                        else
                        {
                            resultMaster = measureImgResultDao.GetAllByBatchCode(arg.SerialNumber);
                        }
                        if (resultMaster != null)
                        {
                            foreach (MeasureImgResultModel result in resultMaster)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Device.View.ShowResult(result);
                                });
                            }
                        }
                        break;
                    case MQTTFileServerEventEnum.Event_File_Download:
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("文件下载失败");
                        });
                        break;
                    case MQTTCameraEventEnum.Event_GetData_Channel:
                        break;
                    case MQTTCameraEventEnum.Event_OpenLive:
                        break;
                }
            }

        }

        private void FileDownload(DeviceGetChannelResult param)
        {
            netFileUtil.TaskStartDownloadFile(param);
        }

        private void FileUpload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName)) netFileUtil.TaskStartUploadFile(param.IsLocal, param.ServerEndpoint, param.FileName);
        }

        private void FileDownload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName))
            {
                netFileUtil.TaskStartDownloadFile(param.IsLocal, param.ServerEndpoint, param.FileName, param.FileExtType);
            }
        }


        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0)
            {
                if (arg.EventName == FileEvent.FileDownload && arg.FileData.data != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        View.OpenImage(arg.FileData);
                    });
                }

                handler?.Close();
            }
            else
            {
                handler?.Close();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, "文件打开失败", "ColorVision");
                });
            }
        }

        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; }



        

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;

            this.AddViewConfig(View, ComboxView);

            Device_ConfigChanged();
            Device.ConfigChanged +=(s,e)=> Device_ConfigChanged();

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
        }



        private void Device_ConfigChanged()
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

        private void Calibration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                //if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                //{
                //    MsgRecord msgRecord = MQTTFileServer.CalibrationControl(param);
                //    Helpers.SendCommand(button, msgRecord);

                //}

                DService.UploadCalibrationFile("111","D:\\img\\20230407175926_1_src.tif", 38);
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
                MsgRecord msgRecord = DService.Close();
                Helpers.SendCommand(button,msgRecord);
                if (DService.IsVideoOpen)
                {
                    CameraVideoControl.Close();
                }
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

