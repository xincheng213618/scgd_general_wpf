using ColorVision.Device.Algorithm;
using ColorVision.MQTT.Service;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
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
            if (ComboxPoiTemplate.SelectedValue is PoiParam poiParam)
            {
                string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                var model = ServiceControl.GetInstance().GetResultBatch(sn);
                Service.GetData(poiParam.ID, model.Id);
            }
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
