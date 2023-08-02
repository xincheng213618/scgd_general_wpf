using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.Template;
using cvColorVision;
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

namespace ColorVision.MQTT.Control
{

    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class MQTTCameraControl1 : UserControl
    {
        private MQTTCamera MQTTCamera { get; set; }

        public MQTTCameraControl1(MQTTCamera mQTTCamera)
        {
            MQTTCamera = mQTTCamera;
            InitializeComponent();

        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = MQTTCamera;
        }

        private void StackPanelCamera_Initialized(object sender, EventArgs e)
        {
            StackPanelOpen.Visibility = Visibility.Collapsed;
            StackPanelImage.Visibility = Visibility.Collapsed;
            CameraCloseButton.Visibility = Visibility.Collapsed;
            CameraOpenButton.Visibility = Visibility.Collapsed;

            MQTTCamera.InitCameraSuccess += (s, e) =>
            {
                if (e.CameraID == MQTTCamera.Config.CameraID)
                {
                    ComboxCameraID.ItemsSource = MQTTCamera.CameraIDs;
                    ComboxCameraID.SelectedIndex = 0;
                    StackPanelOpen.Visibility = Visibility.Visible;
                    CameraOpenButton.Visibility = Visibility.Visible;
                    CameraCloseButton.Visibility = Visibility.Collapsed;
                    CamerInitButton.Content = "断开初始化";
                }
            };
            MQTTCamera.OpenCameraSuccess += (s,e) =>
            {
                if (e.CameraID == MQTTCamera.Config.CameraID)
                {
                    CameraCloseButton.Visibility = Visibility.Visible;
                    CameraOpenButton.Visibility = Visibility.Collapsed;
                    StackPanelImage.Visibility = Visibility.Visible;
                }
            };
            MQTTCamera.CloseCameraSuccess += (s,e) =>
            {
                if (e.CameraID == MQTTCamera.Config.CameraID)
                {
                    CameraCloseButton.Visibility = Visibility.Collapsed;
                    CameraOpenButton.Visibility = Visibility.Visible;
                    StackPanelImage.Visibility = Visibility.Collapsed;
                }
            };
        }

        private void MQTTCamera_Init_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "初始化")
                {
                    MQTTCamera.Init(MQTTCamera.Config.CameraType, MQTTCamera.Config.CameraID);
                    CamerInitButton.Content = "正在初始化";
                }
                else
                {
                    MQTTCamera.UnInit();
                    button.Content = "初始化";
                    StackPanelOpen.Visibility = Visibility.Collapsed;
                    StackPanelImage.Visibility = Visibility.Collapsed;
                    CameraOpenButton.Visibility = Visibility.Collapsed;
                }
            }
        }


        private void SendDemo2_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.Open(MQTTCamera.Config.CameraID, MQTTCamera.Config.TakeImageMode, MQTTCamera.Config.ImageBpp);
        }

        private void SendDemo3_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.GetData(SliderexpTime.Value, SliderGain.Value);
        }

        private void SendDemo4_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.Close();
        }


        private void FilterWheelSetPort_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxFilterWheelChannel.SelectedIndex > -1)
            {
                MQTTCamera.FilterWheelSetPort(0, ComboxFilterWheelChannel.SelectedIndex + 0x30, (int)MQTTCamera.CurrentCameraType);
            }
        }


        private void FilterWheelSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MQTTCamera.FilterWheelSetPort(ComboxFilterWheelChannel1.SelectedIndex + 1, ComboxFilterWheelChannel2.SelectedIndex + 1, (int)MQTTCamera.CurrentCameraType);
            }
            catch
            {

            }
        }

        private void FilterWheelReset_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.FilterWheelSetPort(0, 0x30, (int)MQTTCamera.CurrentCameraType);
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
            MQTTCamera.SetCfwport();
        }


    }
}
