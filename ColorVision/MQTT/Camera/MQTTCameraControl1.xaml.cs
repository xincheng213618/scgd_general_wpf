using ColorVision.Extension;
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

namespace ColorVision.MQTT.Camera
{

    /// <summary>
    /// 根据服务的MQTT相机
    /// </summary>
    public partial class MQTTCameraControl1 : UserControl
    {
        private CameraService Service { get; set; }

        public MQTTCameraControl1(CameraService service)
        {
            Service = service;
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
                    case DeviceStatus.Close:
                        CameraCloseButton.Visibility = Visibility.Collapsed;
                        CameraOpenButton.Visibility = Visibility.Visible;
                        StackPanelImage.Visibility = Visibility.Collapsed;
                        break;
                    case DeviceStatus.Closing:
                        break;
                    case DeviceStatus.Open:
                        CameraCloseButton.Visibility = Visibility.Visible;
                        CameraOpenButton.Visibility = Visibility.Collapsed;
                        StackPanelImage.Visibility = Visibility.Visible;
                        break;
                    case DeviceStatus.Opening:
                        break;
                    case DeviceStatus.UnInit:
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
                if (button.Content.ToString() == "初始化")
                {
                    Service.Init(Service.Config.CameraType, Service.Config.ID);
                }
                else
                {
                    Service.UnInit();
                    button.Content = "初始化";
                    StackPanelOpen.Visibility = Visibility.Collapsed;
                    StackPanelImage.Visibility = Visibility.Collapsed;
                    CameraOpenButton.Visibility = Visibility.Collapsed;
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


    }
}
