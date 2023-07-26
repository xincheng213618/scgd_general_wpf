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
using System.Windows.Shapes;

namespace ColorVision.MQTT
{
    /// <summary>
    /// HeartbeatWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HeartbeatWindow : Window
    {
        public HeartbeatWindow()
        {
            InitializeComponent();
            this.DataContext = MQTTManager.GetInstance().ServiceHeartbeats;
            ListView1.ItemsSource = MQTTManager.GetInstance().ServiceHeartbeats;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {

        }

        private void ListView1_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
