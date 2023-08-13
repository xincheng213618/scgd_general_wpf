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
using ColorVision.MQTT.Service;

namespace ColorVision.Service
{
    /// <summary>
    /// MQTTDeviceControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTDeviceControl : UserControl
    {
        public MQTTDevice MQTTDevice { get; set; }
        public ServiceControl ServiceControl { get; set; }

        public MQTTDeviceControl(MQTTDevice mQTTDevice)
        {
            MQTTDevice = mQTTDevice;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceControl.GetInstance();
            this.DataContext = MQTTDevice;
        }
    }
}
