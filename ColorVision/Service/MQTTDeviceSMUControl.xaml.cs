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

namespace ColorVision.Service
{
    /// <summary>
    /// MQTTDeviceSMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTDeviceSMUControl : UserControl
    {
        public MQTTDeviceSMU MQTTDeviceSMU { get; set; }
        public ServiceControl ServiceControl { get; set; }
        public MQTTDeviceSMUControl(MQTTDeviceSMU mqttDeviceSMU)
        {
            this.MQTTDeviceSMU = mqttDeviceSMU;
            InitializeComponent();
        }
    }
}
