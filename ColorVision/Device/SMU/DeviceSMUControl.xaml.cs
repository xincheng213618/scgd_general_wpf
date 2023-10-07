using ColorVision.MQTT.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.SMU
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceSMUControl : UserControl, IDisposable
    {
        public DeviceSMU MQTTDeviceSMU { get; set; }
        public ServiceControl ServiceControl { get; set; }


        public DeviceSMUControl(DeviceSMU mqttDeviceSMU)
        {
            this.MQTTDeviceSMU = mqttDeviceSMU;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this.MQTTDeviceSMU;
        }

        private void Button_Click_Edit(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
        }

        private void Button_Click_Submit(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
        }


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
