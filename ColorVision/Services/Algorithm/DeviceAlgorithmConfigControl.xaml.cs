using ColorVision.Device.Algorithm;
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

namespace ColorVision.Services.Algorithm
{
    /// <summary>
    /// DeviceAlgorithmConfigControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceAlgorithmConfigControl : UserControl
    {
        public DeviceAlgorithm Device { get; set; }
        public DeviceAlgorithmConfigControl(DeviceAlgorithm device)
        {
            InitializeComponent();
            this.Device = device;
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

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;
        }
    }
}
