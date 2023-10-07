using ColorVision.MQTT.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.Sensor
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceSensorControl : UserControl
    {
        public DeviceSensor DeviceSensor { get; set; }
        public ServiceControl ServiceControl { get; set; }

        public DeviceSensorControl(DeviceSensor deviceSensor)
        {
            DeviceSensor = deviceSensor;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceControl.GetInstance();
            this.DataContext = DeviceSensor;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
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
