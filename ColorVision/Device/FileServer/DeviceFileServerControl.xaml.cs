using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.FileServer
{
    /// <summary>
    /// DeviceImageControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceImageControl : UserControl
    {
        public DeviceFileServer DeviceImg { get; set; }
        public DeviceImageControl(DeviceFileServer device)
        {
            DeviceImg = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DeviceImg;
        }


        private void Button_Click_Edit(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_Save(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }
    }
}
