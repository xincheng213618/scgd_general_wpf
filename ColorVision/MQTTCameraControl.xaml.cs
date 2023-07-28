using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.Template;
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

namespace ColorVision
{
    /// <summary>
    /// MQTTCameraControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTCameraControl : UserControl
    {
        private MQTTCamera MQTTCamera { get; set; }

        public MQTTCameraControl(MQTTCamera mQTTCamera)
        {
            MQTTCamera = mQTTCamera;
            InitializeComponent();
            StackPanelCamera.DataContext = MQTTCamera;
            MQTTCamera = mQTTCamera;
        }

        private void StackPanelCamera_Initialized(object sender, EventArgs e)
        {
            //MQTTCamera.FileHandler += OpenImage;

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());
            ComboxCameraType.SelectedIndex = 1;

            ComboxCameraType.SelectionChanged += (s, e) =>
            {
                if (ComboxCameraType.SelectedItem is KeyValuePair<CameraType, string> KeyValue)
                {
                    if (KeyValue.Key == CameraType.BVQ)
                    {
                        StackPanelFilterWheel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        if (StackPanelFilterWheel.Visibility == Visibility.Visible)
                        {
                            StackPanelFilterWheel.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            };


            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());
            ComboxCameraTakeImageMode.SelectedIndex = 0;



            StackPanelOpen.Visibility = Visibility.Collapsed;
            StackPanelImage.Visibility = Visibility.Collapsed;
            CameraCloseButton.Visibility = Visibility.Collapsed;
            CameraOpenButton.Visibility = Visibility.Collapsed;

            MQTTCamera.InitCameraSuccess += (s, e) =>
            {
                ComboxCameraID.ItemsSource = MQTTCamera.CameraIDList?.IDs;
                ComboxCameraID.SelectedIndex = 0;
                StackPanelOpen.Visibility = Visibility.Visible;
                CameraOpenButton.Visibility = Visibility.Visible;
                CameraCloseButton.Visibility = Visibility.Collapsed;
                CamerInitButton.Content = "断开初始化";
            };
            MQTTCamera.OpenCameraSuccess += (s, e) =>
            {
                CameraCloseButton.Visibility = Visibility.Visible;
                CameraOpenButton.Visibility = Visibility.Collapsed;
                StackPanelImage.Visibility = Visibility.Visible;
            };
            MQTTCamera.CloseCameraSuccess += (s, e) =>
            {
                CameraCloseButton.Visibility = Visibility.Collapsed;
                CameraOpenButton.Visibility = Visibility.Visible;
                StackPanelImage.Visibility = Visibility.Collapsed;
            };
        }

        private void MQTTCamera_Init_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "初始化")
                {
                    if (ComboxCameraType.SelectedItem is KeyValuePair<CameraType, string> KeyValue && KeyValue.Key is CameraType cameraType)
                    {
                        MQTTCamera.Init(cameraType);
                        CamerInitButton.Content = "正在初始化";
                    }
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
            if (ComboxCameraTakeImageMode.SelectedItem is KeyValuePair<TakeImageMode, string> KeyValue && KeyValue.Key is TakeImageMode takeImageMode)
            {
                if (string.IsNullOrEmpty(ComboxCameraID.Text))
                {
                    MessageBox.Show("找不到相机");
                    return;
                }
                MQTTCamera.Open(ComboxCameraID.Text.ToString(), takeImageMode, int.Parse(ComboxCameraImageBpp.Text));
            }
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
