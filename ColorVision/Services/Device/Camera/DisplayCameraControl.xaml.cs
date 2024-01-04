using ColorVision.Device.Camera.Video;
using ColorVision.Util;
using ColorVision.Extension;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using cvColorVision;
using ColorVision.Templates;
using ColorVision.Services.Msg;
using ColorVision.Services.Device;
using ColorVision.Services.Device.Camera;
using ColorVision.Net;
using ColorVision.Solution;
using Panuon.WPF.UI;
using MQTTMessageLib.Camera;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using log4net;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using ColorVision.Services.Device.Camera.Views;

namespace ColorVision.Device.Camera
{

    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class CameraDisplayControl : UserControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(CameraDisplayControl));
        public DeviceCamera Device { get; set; }
        public DeviceServiceCamera DService { get => Device.DeviceService; }

        public ViewCamera View { get; set; }

        private NetFileUtil netFileUtil;
        private IPendingHandler? handler { get; set; }

        private ResultService resultService { get; set; }

        public CameraDisplayControl(DeviceCamera device)
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
        }

        private void View_OnCurSelectionChanged(ViewResultCamera data)
        {
            if (data.ResultCode == 0 && data.FilePath!=null)
            {
                switch (data.FileType)
                {
                    case CameraFileType.SrcFile:
                        doOpen(data.FilePath, FileExtType.Raw);
                        break;
                    case CameraFileType.CIEFile:
                        doOpen(data.FilePath, FileExtType.CIE);
                        break;
                    default:
                        break;
                }
            }
        }

        private void doOpen(string fileName, FileExtType extType)
        {
            string localName = netFileUtil.GetCacheFileFullName(fileName);
            if (string.IsNullOrEmpty(localName) || !System.IO.File.Exists(localName))
            {
                DService.DownloadFile(fileName, extType);
            }
            else  
            {
                netFileUtil.OpenLocalFile(localName, extType);
            }
        }

        private void CameraService_OnMessageRecved(object sender, Services.MessageRecvArgs arg)
        {
            switch (arg.EventName)
            {
                case MQTTCameraEventEnum.Event_GetData:
                    ShowResultFromDB(arg.SerialNumber, Convert.ToInt32(arg.Data.MasterId));
                    break;
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    FileDownload(pm_dl);
                    break;
                case MQTTCameraEventEnum.Event_Calibration_UploadFile:
                    DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    FileUpload(pm_up);
                    break;
            }
        }

        private void FileUpload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName)) netFileUtil.TaskStartUploadFile(param.IsLocal, param.ServerEndpoint, param.FileName);
        }

        private void FileDownload(DeviceFileUpdownParam param)
        {
            if (!string.IsNullOrWhiteSpace(param.FileName))
            {
                FileExtType extType = FileExtType.Raw;
                if (param.FileName.EndsWith("cvcie",StringComparison.OrdinalIgnoreCase)) extType = FileExtType.CIE;
                netFileUtil.TaskStartDownloadFile(param.IsLocal, param.ServerEndpoint, param.FileName, extType);
            }
        }

        private void ShowResultFromDB(string serialNumber, int masterId)
        {
            List<MeasureImgResultModel> resultMaster = null;
            if (masterId > 0)
            {
                resultMaster = new List<MeasureImgResultModel>();
                MeasureImgResultModel model = resultService.GetCameraImgResultById(masterId);
                resultMaster.Add(model);
            }
            else
            {
                resultMaster = resultService.GetCameraImgResultBySN(serialNumber);
            }
            if (resultMaster != null)
            {
                foreach (MeasureImgResultModel result in resultMaster)
                {
                    ShowResult(result);
                }
            }

            handler?.Close();
        }

        private void ShowResult(MeasureImgResultModel result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Device.View.ShowResult(result);
            });
        }

        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    View.OpenImage(arg.FileData);
                });
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

            CalibrationParams = new ObservableCollection<TemplateModel<CalibrationParam>>();
            CalibrationParams.Insert(0, new TemplateModel<CalibrationParam>("Empty", new CalibrationParam()));

            foreach (var item in TemplateControl.GetInstance().CalibrationParams)
                CalibrationParams.Add(item);
            TemplateControl.GetInstance().CalibrationParams.CollectionChanged += (s, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        // 处理添加项
                        foreach (TemplateModel<CalibrationParam> newItem in e.NewItems)
                        {
                            CalibrationParams.Add(newItem);
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        // 处理移除项
                        foreach (TemplateModel<CalibrationParam> oldItem in e.OldItems)
                        {
                            CalibrationParams.Remove(oldItem);
                        }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        // 处理替换项
                        // ...
                        break;
                    case NotifyCollectionChangedAction.Move:
                        // 处理移动项
                        // ...
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        // 处理清空集合
                        CalibrationParams.Clear();
                        CalibrationParams.Insert(0, new TemplateModel<CalibrationParam>("Empty", new CalibrationParam()) { ID = -1 });
                        break;
                }
            };


            ComboxCalibrationTemplate.ItemsSource = CalibrationParams;
            ComboxCalibrationTemplate.SelectedIndex = 0;  

            StackPanelOpen.Visibility = Visibility.Collapsed;
            StackPanelImage.Visibility = Visibility.Collapsed;
            ButtonOpen.Visibility = Visibility.Collapsed;


            ViewMaxChangedEvent(ViewGridManager.GetInstance().ViewMax);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += ViewMaxChangedEvent;

            void ViewMaxChangedEvent(int max)
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < max; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            }
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    View.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);
                }
            };
            View.View.ViewIndex = -1;

            if (DService.DeviceStatus == DeviceStatus.Init)
            {
                StackPanelOpen.Visibility = Visibility.Visible;
                ButtonOpen.Visibility = Visibility.Visible;
                ButtonInit.Visibility = Visibility.Collapsed;
            }
            DService.DeviceStatusChanged += (e) =>
            {
                switch (e)
                {
                    case DeviceStatus.Closed:
                        ButtonOpen.Visibility = Visibility.Visible;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        ButtonClose.Visibility = Visibility.Collapsed;
                        View.ImageView.Clear();
                        break;
                    case DeviceStatus.Closing:
                        break;
                    case DeviceStatus.Opened:
                        ButtonOpen.Visibility = Visibility.Collapsed;
                        ButtonClose.Visibility = Visibility.Visible;
                        if (!DService.IsVideoOpen)
                        {
                            StackPanelImage.Visibility = Visibility.Visible;
                        }
                        break;
                    case DeviceStatus.Opening:
                        break;
                    case DeviceStatus.UnInit:
                        StackPanelOpen.Visibility = Visibility.Collapsed;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        ButtonOpen.Visibility = Visibility.Collapsed;
                        ButtonClose.Visibility = Visibility.Collapsed;
                        ButtonInit.Content = "连接";
                        ViewGridManager.GetInstance().RemoveView(View);
                        break;
                    case DeviceStatus.Init:
                        StackPanelOpen.Visibility = Visibility.Visible;
                        ButtonOpen.Visibility = Visibility.Collapsed;
                        ButtonInit.Content = "断开连接";
                        ViewGridManager.GetInstance().AddView(View);
                        if (ViewGridManager.GetInstance().ViewMax > 4 || ViewGridManager.GetInstance().ViewMax == 3)
                        {
                            ViewGridManager.GetInstance().SetViewNum(-1);
                        }
                        break;
                    case DeviceStatus.UnConnected:
                        break;
                    default:
                        break;
                }
            };

            resultService = new ResultService();
        }

        private void CameraInit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (DService.DeviceStatus == DeviceStatus.UnInit && button.Content.ToString() == "连接")
                {
                    var msgRecord = DService.Init();
                    Helpers.SendCommand(button, msgRecord,false);
                }
                else if (DService.DeviceStatus != DeviceStatus.UnInit || button.Content.ToString() == "断开连接")
                {
                    var msgRecord = DService.UnInit();
                    Helpers.SendCommand(button, msgRecord, false);
                }
                else
                {
                    MessageBox.Show(Application.Current.MainWindow, "指令已经发送请稍等", "ColorVision");
                }
            }

        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = DService.Open(DService.Config.ID, Device.Config.TakeImageMode, (int)DService.Config.ImageBpp);
            }
        }

        private void SendDemo3_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string filename = DateTime.Now.ToString("yyyyMMddHHmmss") + ".tif";

                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    double[] expTime = null;
                    if (Device.Config.IsExpThree) { expTime = new double[] { Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB }; }
                    else expTime = new double[] { Device.Config.ExpTime };
                    MsgRecord msgRecord = DService.GetData(expTime, param);
                    Helpers.SendCommand(msgRecord, msgRecord.MsgRecordState.ToDescription());
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
                string host = GlobalSetting.GetInstance().SoftwareConfig.VideoConfig.Host;
                int port = GlobalSetting.GetInstance().SoftwareConfig.VideoConfig.Port;
                port = CameraVideoControl.Open(host, port);
                if (port > 0)
                {
                    CameraVideoControl.Start();
                    MsgRecord msg= DService.OpenVideo(host, port, DService.Config.ExpTime);
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
                    logger.ErrorFormat("Local socket open failed.{0}:{1}", host, GlobalSetting.GetInstance().SoftwareConfig.VideoConfig.Port);
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
                //    MsgRecord msgRecord = DService.Calibration(param);
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
                SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show(Application.Current.MainWindow, "数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "Calibration":
                        Calibration calibration;
                        if (TemplateControl.CalibrationParams.Count>0) 
                        {
                             calibration = new Calibration(TemplateControl.CalibrationParams[0].Value);
                        }
                        else
                        {
                             calibration = new Calibration();
                        }
                        windowTemplate = new WindowTemplate(TemplateType.Calibration, calibration, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    default:
                        HandyControl.Controls.Growl.Info("开发中");
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
            if (DService.IsVideoOpen)
            {
                DService.Close();
                CameraVideoControl.Close();
            }
            else
            {
                DService.Close();
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

