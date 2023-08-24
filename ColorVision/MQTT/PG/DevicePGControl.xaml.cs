using ColorVision.Extension;
using ColorVision.MQTT.Camera;
using ColorVision.MQTT.Service;
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

namespace ColorVision.MQTT.PG
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DevicePGControl : UserControl
    {
        public DevicePG DevicePG { get; set; }
        public ServiceControl ServiceControl { get; set; }

        public DevicePGControl(DevicePG devicePG)
        {
            DevicePG = devicePG;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceControl.GetInstance();
            this.DataContext = DevicePG;
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
