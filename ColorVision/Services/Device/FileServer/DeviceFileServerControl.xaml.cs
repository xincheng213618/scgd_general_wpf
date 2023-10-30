using ColorVision.Device.Spectrum;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.FileServer
{
    /// <summary>
    /// DeviceImageControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceFileServerControl : UserControl
    {
        public DeviceFileServer DeviceFileServer { get; set; }
        public DeviceFileServerControl(DeviceFileServer device)
        {
            DeviceFileServer = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DeviceFileServer;
        }

        private void Button_Click_Save(object sender, RoutedEventArgs e)
        {

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
