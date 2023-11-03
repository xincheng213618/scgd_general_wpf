using ColorVision.Services;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Device.Motor
{
    /// <summary>
    /// DeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceMotorControl : UserControl, IDisposable
    {
        public DeviceMotor Device { get; set; }
        public ServiceManager ServiceControl { get; set; }


        public DeviceMotorControl(DeviceMotor deviceMotor)
        {
            this.Device = deviceMotor;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this.Device;

            ComboxMotorType.ItemsSource = from e1 in Enum.GetValues(typeof(FOCUS_COMMUN)).Cast<FOCUS_COMMUN>()
                                                    select new KeyValuePair<FOCUS_COMMUN, string>(e1, e1.ToString());


            List<string> strings = new List<string>();
            TextBaudRate.ItemsSource = strings;
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
