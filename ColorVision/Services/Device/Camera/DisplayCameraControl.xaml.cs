using ColorVision.Device.Camera.Video;
using ColorVision.Util;
using ColorVision.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using cvColorVision;
using ColorVision.Templates;
using ColorVision.Services.Msg;
using ColorVision.Services.Device.Camera.Video;
using System.Collections.ObjectModel;

namespace ColorVision.Device.Camera
{

    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class CameraDisplayControl : UserControl
    {
        public DeviceServiceCamera Service { get => Device.DeviceService; }

        public DeviceCamera Device { get; set; }

        public ImageView View { get; set; }


        public CameraDisplayControl(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;

            ComboxCalibrationTemplate.ItemsSource = TemplateControl.GetInstance().CalibrationParams;
            ComboxCalibrationTemplate.SelectedIndex = 0;

            StackPanelOpen.Visibility = Visibility.Collapsed;
            StackPanelImage.Visibility = Visibility.Collapsed;
            ButtonOpen.Visibility = Visibility.Collapsed;

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());
            ComboxCameraTakeImageMode.SelectedValue = Service.Config.TakeImageMode;


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

            if (Service.DeviceStatus == DeviceStatus.Init)
            {
                StackPanelOpen.Visibility = Visibility.Visible;
                ButtonOpen.Visibility = Visibility.Visible;
                ButtonInit.Visibility = Visibility.Collapsed;
            }
            Service.DeviceStatusChanged += (e) =>
            {
                switch (e)
                {
                    case DeviceStatus.Closed:
                        ButtonOpen.Visibility = Visibility.Visible;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        ButtonOpen.Content = "打开";
                        TakeImageModePanel.Visibility = Visibility.Visible;
                        break;
                    case DeviceStatus.Closing:
                        break;
                    case DeviceStatus.Opened:
                        StackPanelImage.Visibility = Visibility.Visible;
                        ButtonOpen.Content = "关闭";
                        TakeImageModePanel.Visibility = Visibility.Collapsed;
                        break;
                    case DeviceStatus.Opening:
                        break;
                    case DeviceStatus.UnInit:
                        StackPanelOpen.Visibility = Visibility.Collapsed;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        ButtonOpen.Visibility = Visibility.Collapsed;
                        ButtonInit.Content = "连接";
                        ButtonOpen.Content = "打开";
                        ViewGridManager.GetInstance().RemoveView(View);
                        break;
                    case DeviceStatus.Init:
                        StackPanelOpen.Visibility = Visibility.Visible;
                        ButtonOpen.Visibility = Visibility.Visible;
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
        }





        private void CameraInit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (Service.DeviceStatus == DeviceStatus.UnInit && button.Content.ToString() == "连接")
                {
                    var msgRecord = Service.Init();
                    Helpers.SendCommand(button, msgRecord,false);
                }
                else if (Service.DeviceStatus != DeviceStatus.UnInit || button.Content.ToString() == "断开连接")
                {
                    var msgRecord = Service.UnInit();
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
                if (ComboxCameraTakeImageMode.SelectedValue is TakeImageMode takeImageMode)
                {
                    if ((Service.DeviceStatus == DeviceStatus.Init || Service.DeviceStatus == DeviceStatus.Closed))
                    {
                        if (takeImageMode == TakeImageMode.Live)
                        {
                            Button4_Click(sender, e);
                        }
                        else
                        {
                            var msgRecord = Service.Open(Service.Config.ID, takeImageMode, (int)Service.Config.ImageBpp);
                            Helpers.SendCommand(button, msgRecord, false);
                        }
                    }
                    else
                    {
                        if (takeImageMode == TakeImageMode.Live)
                        {
                            Button4_Click(sender, e);
                        }
                        else
                        {
                            Helpers.SendCommand(button, Service.Close(), false);
                        }
                        ButtonOpen.Content = "关闭中";
                    }
                }
            }
        }

        private void SendDemo3_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (SliderexpTime.Value <= 0)
                {
                    MessageBox.Show(Application.Current.MainWindow, "曝光时间小于0");
                    return;
                }
                string filename = DateTime.Now.ToString("yyyyMMddHHmmss") + ".tif";

                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    MsgRecord msgRecord = Service.GetData(SliderexpTime.Value, SliderGain.Value);
                    Helpers.SendCommand(msgRecord, msgRecord.MsgRecordState.ToDescription());
                }
            }
        }

        private void AutoExplose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                MsgRecord msgRecord = Service.GetAutoExpTime();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        public CameraVideoControl1 CameraVideoControl { get; set; }

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                CameraVideoControl??= new CameraVideoControl1();
                if (Service.DeviceStatus == DeviceStatus.Init|| Service.DeviceStatus == DeviceStatus.Closed)
                {
                    button.Content = "正在获取推流";
                    CameraVideoControl.Open(Service.Config.VideoConfig.Host, Service.Config.VideoConfig.Port);
                    Service.Open(Service.Config.ID, TakeImageMode.Live, (int)Service.Config.ImageBpp);
                    CameraVideoControl.CameraVideoFrameReceived -= CameraVideoFrameReceived;
                    CameraVideoControl.CameraVideoFrameReceived += CameraVideoFrameReceived;
                }
                else
                {
                    Service.Close();
                    CameraVideoControl.Close();
                }
            }
        }

        public void CameraVideoFrameReceived(System.Drawing.Bitmap bmp)
        {
            if (View.ImageShow.Source is WriteableBitmap bitmap)
            {
                ImageUtil.BitmapCopyToWriteableBitmap(bmp, bitmap, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.PixelFormat);
            }
            else
            {
                WriteableBitmap writeableBitmap = ImageUtil.BitmapToWriteableBitmap(bmp);
                View.ImageShow.Source = writeableBitmap;
            }
        }


        private void VideSetting_Click(object sender, RoutedEventArgs e)
        {
            new CameraVideoConnect(Service.Config.VideoConfig) { Owner =Application.Current.MainWindow,WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void AutoFocus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                MsgRecord msgRecord = Service.AutoFocus();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void ComboxCalibrationTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }


        private void Calibration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    MsgRecord msgRecord = Service.Calibration(param);
                    Helpers.SendCommand(button, msgRecord);

                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void SetChannel()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params = new Dictionary<string, object>() { { "Func",new List<ParamFunction> (){
                    new ParamFunction() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 0 }, { "nPort", Service.Config.CFW.ChannelCfgs[0].Cfwport },{ "eImgChlType", (int)Service.Config.CFW.ChannelCfgs[0].Chtype } } },
                    new ParamFunction() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 1 }, { "nPort", Service.Config.CFW.ChannelCfgs[1].Cfwport },{ "eImgChlType", (int)Service.Config.CFW.ChannelCfgs[1].Chtype } } },
                    new ParamFunction() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", 2 }, { "nPort", Service.Config.CFW.ChannelCfgs[2].Cfwport },{ "eImgChlType", (int)Service.Config.CFW.ChannelCfgs[2].Chtype } } },
                } } }
            };
            Service.PublishAsyncClient(msg);
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
                        Calibration calibration = new Calibration(TemplateControl.CalibrationParams[0].Value);
                        windowTemplate = new WindowTemplate(TemplateType.Calibration, calibration) { Title = "校正参数设置" };
                        TemplateAbb(windowTemplate, TemplateControl.CalibrationParams);
                        break;
                    default:
                        HandyControl.Controls.Growl.Info("开发中");
                        break;
                }
            }
        }
        private void TemplateAbb<T>(WindowTemplate windowTemplate, ObservableCollection<TemplateModel<T>> keyValuePairs) where T : ParamBase
        {
            windowTemplate.Owner = Window.GetWindow(this);
            windowTemplate.ListConfigs.Clear();
            foreach (var item in keyValuePairs)
            {
                if (item.Value is PoiParam poiParam)
                {
                    item.Tag = $"{poiParam.Width}*{poiParam.Height}{(GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql ? "" : $"_{poiParam.PoiPoints.Count}")}";
                }

                windowTemplate.ListConfigs.Add(item);
            }
            windowTemplate.ShowDialog();
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (int.TryParse(TextPos.Text, out int pos))
                {
                    var msgRecord = Service.Move(pos, CheckBoxIsAbs.IsChecked ?? true);
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
                    var msgRecord = Service.GoHome();
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }

        private void GetPosition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = Service.GetPosition();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void Move1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (double.TryParse(TextDiaphragm.Text, out double pos))
                {
                    var msgRecord = Service.MoveDiaphragm(pos);
                    Helpers.SendCommand(button, msgRecord);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var msgRecord = Service.Close();
                Helpers.SendCommand(button, msgRecord);
            }
        }

        private void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Service.DeviceStatus == DeviceStatus.Opened)
            {
                if (Service.CurrentTakeImageMode == TakeImageMode.Live)
                {
                    Service.SetExp();
                }
            }
        }
    }
}

