using ColorVision.Device.Camera.Video;
using ColorVision.Util;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.Device.Camera
{

    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class MQTTCameraControl1 : UserControl
    {
        public CameraService Service { get => Device.Service; }

        public DeviceCamera Device { get; set; }

        public ImageView View { get; set; }


        public MQTTCameraControl1(DeviceCamera device)
        {
            Device = device;
            View = Device.View;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Service;
        }

        private void StackPanelCamera_Initialized(object sender, EventArgs e)
        {
            StackPanelOpen.Visibility = Visibility.Collapsed;
            StackPanelImage.Visibility = Visibility.Collapsed;
            CameraCloseButton.Visibility = Visibility.Collapsed;
            CameraOpenButton.Visibility = Visibility.Collapsed;


            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>("独立窗口", -2));
                KeyValues.Add(new KeyValuePair<string, int>("隐藏", -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                //ComboxView.SelectedIndex = View.View.ViewIndex + 2;
            };
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);

                }
            };


            if (Service.DeviceStatus == DeviceStatus.Init)
            {
                ComboxCameraID.ItemsSource = CameraService.CameraIDs;
                ComboxCameraID.SelectedIndex = 0;
                StackPanelOpen.Visibility = Visibility.Visible;
                CameraOpenButton.Visibility = Visibility.Visible;
                CameraCloseButton.Visibility = Visibility.Collapsed;
                CamerInitButton.Content = "断开初始化";
            }
            Service.DeviceStatusChanged += (e) =>
            {
                switch (e)
                {
                    case DeviceStatus.Closed:
                        CameraCloseButton.Visibility = Visibility.Collapsed;
                        CameraOpenButton.Visibility = Visibility.Visible;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        ViewGridManager.GetInstance().RemoveView(View);
                        break;
                    case DeviceStatus.Closing:
                        break;
                    case DeviceStatus.Opened:
                        CameraCloseButton.Visibility = Visibility.Visible;
                        CameraOpenButton.Visibility = Visibility.Collapsed;
                        StackPanelImage.Visibility = Visibility.Visible;
                        ViewGridManager.GetInstance().AddView(View);
                        if (ViewGridManager.GetInstance().ViewMax > 4 || ViewGridManager.GetInstance().ViewMax == 3)
                        {
                            ViewGridManager.GetInstance().SetViewNum(-1);
                        }
                        break;
                    case DeviceStatus.Opening:
                        break;
                    case DeviceStatus.UnInit:
                        StackPanelOpen.Visibility = Visibility.Collapsed;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        CameraOpenButton.Visibility = Visibility.Collapsed;
                        CamerInitButton.Content = "初始化";
                        break;
                    case DeviceStatus.Init:
                        ComboxCameraID.ItemsSource = CameraService.CameraIDs;
                        ComboxCameraID.SelectedIndex = 0;
                        StackPanelOpen.Visibility = Visibility.Visible;
                        CameraOpenButton.Visibility = Visibility.Visible;
                        CameraCloseButton.Visibility = Visibility.Collapsed;
                        CamerInitButton.Content = "断开初始化";
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
            if (sender is Button button)
            {
                if (Service.DeviceStatus == DeviceStatus.UnInit)
                {
                    Service.Init();
                }
                else
                {
                    Service.UnInit();

                }

            }
        }


        private void SendDemo2_Click(object sender, RoutedEventArgs e)
        {
            Service.Open(Service.Config.ID, Service.Config.TakeImageMode, Service.Config.ImageBpp);
        }

        private void SendDemo3_Click(object sender, RoutedEventArgs e)
        {
            Service.GetData(SliderexpTime.Value, SliderGain.Value);
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

        private void SendDemo5_Click(object sender, RoutedEventArgs e)
        {
            Service.SetCfwport();
        }

        bool CameraOpen;

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                CameraVideoControl control = CameraVideoControl.GetInstance();
                if (!CameraOpen)
                {
                    button.Content = "正在获取推流";
                    control.Open();
                    Service.Open(Service.Config.ID,TakeImageMode.Live, Service.Config.ImageBpp);

                    control.CameraVideoFrameReceived += (bmp) =>
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
                    button.Content = "启用视频模式";
                    Service.Close();
                    control.Close();
                }
                CameraOpen = !CameraOpen;
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
    }
}
