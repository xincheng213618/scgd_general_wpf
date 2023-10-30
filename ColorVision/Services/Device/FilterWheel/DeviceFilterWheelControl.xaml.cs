using ColorVision.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.FilterWheel
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceFilterWheelControl : UserControl, IDisposable
    {
        public DeviceFilterWheel Device { get; set; }
        public ServiceManager ServiceControl { get; set; }


        public DeviceFilterWheelControl(DeviceFilterWheel deviceFilterWheel)
        {
            this.Device = deviceFilterWheel;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this.Device;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }
    }
}
