using ColorVision.Device.Algorithm;
using ColorVision.Template;
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

namespace ColorVision.Device.POI
{
    /// <summary>
    /// AlgorithmDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmDisplayControl : UserControl
    {
        public DeviceAlgorithm Device { get; set; }

        public AlgorithmService Service { get => Device.Service; }


        public AlgorithmDisplayControl(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ComboxPoiTemplate.ItemsSource = TemplateControl.GetInstance().PoiParams;
            ComboxPoiTemplate.SelectedIndex = 0;
        }

        private void PoiClick(object sender, RoutedEventArgs e)
        {
            Service.GetData(1,1);
        }

        private void Algorithm_INI(object sender, RoutedEventArgs e)
        {
            Service.Init();
        }

        private void Algorithm_GET(object sender, RoutedEventArgs e)
        {
            Service.GetAllSnID();
        }
    }
}
