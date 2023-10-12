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

namespace ColorVision.Device.Camera
{

    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class CameraDisplayLocal : UserControl    {

        public CameraService Service { get => Device.Service; }

        public DeviceCamera Device { get; set; }

        public ImageView View { get; set; }


        public CameraDisplayLocal(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Service;

            StackPanelOpen.Visibility = Visibility.Collapsed;
            StackPanelImage.Visibility = Visibility.Collapsed;
            CameraOpenButton.Visibility = Visibility.Collapsed;

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.SelectedValue = Service.Config.TakeImageMode;
            ComboxCameraTakeImageMode.SelectionChanged += (s, e) =>
            {
                CameraVideoSetButton.Visibility = ComboxCameraTakeImageMode.SelectedValue is TakeImageMode mode && mode == TakeImageMode.Live ? Visibility.Visible : Visibility.Collapsed;
            };


            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            };
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

            if (Service.DeviceStatus == DeviceStatus.Init)
            {
                StackPanelOpen.Visibility = Visibility.Visible;
                CameraOpenButton.Visibility = Visibility.Visible;
                CamerInitButton.Content = "断开初始化";
            }
            Service.DeviceStatusChanged += (e) =>
            {
                switch (e)
                {
                    case DeviceStatus.Closed:
                        CameraOpenButton.Visibility = Visibility.Visible;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        CameraOpenButton.Content = "打开";
                        break;
                    case DeviceStatus.Closing:
                        break;
                    case DeviceStatus.Opened:
                        StackPanelImage.Visibility = Visibility.Visible;

                        CameraOpenButton.Content = "关闭";
                        break;
                    case DeviceStatus.Opening:
                        break;
                    case DeviceStatus.UnInit:
                        StackPanelOpen.Visibility = Visibility.Collapsed;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        CameraOpenButton.Visibility = Visibility.Collapsed;
                        CamerInitButton.Content = "连接";
                        CameraOpenButton.Content = "打开";
                        ViewGridManager.GetInstance().RemoveView(View);
                        break;
                    case DeviceStatus.Init:
                        StackPanelOpen.Visibility = Visibility.Visible;
                        CameraOpenButton.Visibility = Visibility.Visible;
                        CamerInitButton.Content = "断开连接";

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


        private void MQTTCamera_Init_Click(object sender, RoutedEventArgs e)
        {
            if (Service.DeviceStatus == DeviceStatus.UnInit)
            {
                Service.Init();
                CamerInitButton.Content = "连接中";
            }
            else
            {
                Service.UnInit();
                CamerInitButton.Content = "断开连接中";
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (Service.DeviceStatus == DeviceStatus.Init || Service.DeviceStatus == DeviceStatus.Closed)
            {
                if (ComboxCameraTakeImageMode.SelectedValue is TakeImageMode takeImageMode)
                {
                    if (takeImageMode == TakeImageMode.Live)
                    {
                        Button4_Click(sender, e);
                    }
                    else
                    {
                        Service.Open(Service.Config.ID, takeImageMode, (int)Service.Config.ImageBpp);
                        CameraOpenButton.Content = "打开中";
                    }
                }
            }
            else
            {
                Button4_Click(sender, e);
                Service.Close();
                CameraOpenButton.Content = "关闭中";
            }
        }

        private void SendDemo3_Click(object sender, RoutedEventArgs e)
        {
            string filename = DateTime.Now.ToString("yyyyMMddHHmmss") + ".tif";
            Service.GetData(SliderexpTime.Value, SliderGain.Value, filename);
        }

        private void SendDemo4_Click(object sender, RoutedEventArgs e)
        {
            Service.Close();
        }


        private void FilterWheelSetPort_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxFilterWheelChannel.SelectedIndex > -1)
            {
                Service.FilterWheelSetPort(0, ComboxFilterWheelChannel.SelectedIndex + 0x30, (int)Service.CurrentCameraType);
            }
        }


        private void FilterWheelSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Service.FilterWheelSetPort(ComboxFilterWheelChannel1.SelectedIndex + 1, ComboxFilterWheelChannel2.SelectedIndex + 1, (int)Service.CurrentCameraType);
            }
            catch
            {

            }
        }

        private void FilterWheelReset_Click(object sender, RoutedEventArgs e)
        {
            Service.FilterWheelSetPort(0, 0x30, (int)Service.CurrentCameraType);
            ComboxFilterWheelChannel.SelectedIndex = 0;
        }

        private void StackPanelFilterWheel_Initialized(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
                ComboxFilterWheelChannel.Items.Add(new ComboBoxItem() { Content = i });
            ComboxFilterWheelChannel.SelectedIndex = 0;
        }

        private void AutoExplose_Click(object sender, RoutedEventArgs e)
        {
            Service.GetAutoExpTime();
        }

        public CameraVideoControl CameraVideoControl { get; set; }

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                CameraVideoControl??= new CameraVideoControl();
                if (Service.DeviceStatus == DeviceStatus.Init|| Service.DeviceStatus == DeviceStatus.Closed)
                {
                    button.Content = "正在获取推流";
                    CameraVideoControl.Open(Service.Config.VideoConfig.Host, Service.Config.VideoConfig.Port);
                    Service.Open(Service.Config.ID, TakeImageMode.Live, (int)Service.Config.ImageBpp);

                    CameraVideoControl.CameraVideoFrameReceived += (bmp) =>
                    {
                        button.Content = "关闭视频";
                        if (View.ImageShow.Source is WriteableBitmap bitmap)
                        {
                            ImageUtil.BitmapCopyToWriteableBitmap(bmp, bitmap, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.PixelFormat);
                        }
                        else
                        {
                            WriteableBitmap writeableBitmap = ImageUtil.BitmapToWriteableBitmap(bmp);
                            View.ImageShow.Source = writeableBitmap;
                        }
                    };
                }
                else
                {
                    Service.Close();
                    CameraVideoControl.Close();
                }
            }
        }

        private void ButtonCV_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.custom) | *.custom";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                View.OpenCVImage(filePath);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                View.OpenImage(filePath);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            //using var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            //saveFileDialog.Filter = "Image files (*.tif) | *.tif";
            //saveFileDialog.DefaultExt = "1.tif";
            //saveFileDialog.RestoreDirectory = true;
            //if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    string filePath = openFileDialog.FileName;
            //    View.OpenCVImage(filePath);
            //}
        }

        private void VideSetting_Click(object sender, RoutedEventArgs e)
        {
            new CameraVideoConnect(Service.Config.VideoConfig) { Owner =Application.Current.MainWindow,WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void AutoFocus_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// FSR
        /// </summary>
        private void FSR_Click(object sender, RoutedEventArgs e)
        {

        }
        /// <summary>
        /// GetData
        /// </summary>
        private void FOV_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// 鬼影
        /// </summary>
        private void GhostShadow_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
