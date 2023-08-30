using ColorVision.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.Camera
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCameraControl : UserControl
    {
        public DeviceCamera MQTTDeviceCamera { get; set; }

        public CameraService Service { get => MQTTDeviceCamera.Service; }


        public DeviceCameraControl(DeviceCamera mQTTDeviceCamera)
        {
            MQTTDeviceCamera = mQTTDeviceCamera;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = MQTTDeviceCamera;

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());



            ComboxCameraChannel.Text = MQTTDeviceCamera.Config.Channel.ToString();
            ComboxCameraImageBpp.Text = MQTTDeviceCamera.Config.ImageBpp.ToString();


            CameraID.ItemsSource = CameraService.CameraIDs;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTDeviceCamera.Config.Channel = int.Parse(ComboxCameraChannel.Text.ToString());
            MQTTDeviceCamera.Config.ImageBpp = int.Parse(ComboxCameraImageBpp.Text.ToString());

            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
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
}
