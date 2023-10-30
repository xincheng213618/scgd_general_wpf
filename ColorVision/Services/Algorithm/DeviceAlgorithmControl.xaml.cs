using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Algorithm
{
    /// <summary>
    /// DeviceAlgorithmControl.xaml 的交互逻辑s
    /// </summary>
    public partial class DeviceAlgorithmControl : UserControl
    {
        public DeviceAlgorithm Device { get; set; }
        public DeviceAlgorithmControl(DeviceAlgorithm device)
        {
            this.Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }
    }
}
