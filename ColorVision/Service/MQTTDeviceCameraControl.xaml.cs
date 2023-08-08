using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.MQTT.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Service
{
    /// <summary>
    /// MQTTDeviceCameraControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTDeviceCameraControl : UserControl
    {
        public MQTTDeviceCamera MQTTDeviceCamera { get; set; }
        public ServiceControl ServiceControl { get; set; }

        public MQTTDeviceCameraControl(MQTTDeviceCamera mQTTDeviceCamera)
        {
            MQTTDeviceCamera = mQTTDeviceCamera;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceControl.GetInstance();
            this.DataContext = MQTTDeviceCamera;

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());



            ComboxCameraChannel.Text = MQTTDeviceCamera.CameraConfig.Channel.ToString();
            ComboxCameraImageBpp.Text = MQTTDeviceCamera.CameraConfig.ImageBpp.ToString();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTDeviceCamera.CameraConfig.Channel = int.Parse(ComboxCameraChannel.Text.ToString());
            MQTTDeviceCamera.CameraConfig.ImageBpp = int.Parse(ComboxCameraImageBpp.Text.ToString());

            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;

        }
    }
}
