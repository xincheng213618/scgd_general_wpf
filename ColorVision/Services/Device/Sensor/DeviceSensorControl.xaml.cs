using ColorVision.Device.PG;
using ColorVision.Services;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Sensor
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceSensorControl : UserControl
    {
        public DeviceSensor DeviceSensor { get; set; }

        public DeviceSensorControl(DeviceSensor deviceSensor)
        {
            DeviceSensor = deviceSensor;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DeviceSensor;

            ComboxSensorType.ItemsSource = from e1 in Enum.GetValues(typeof(CommunicateType)).Cast<CommunicateType>()
                                          select new KeyValuePair<CommunicateType, string>(e1, e1.ToString()); 

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
