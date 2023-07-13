using ColorVision.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace ColorVision.Template
{
    /// <summary>
    /// WindowFocusPointAdd.xaml 的交互逻辑
    /// </summary>
    public partial class WindowFocusPointAdd : BaseWindow
    {
        public ObservableCollection<ListConfig> ListConfigs { get; set; }
        public WindowFocusPointAdd(ObservableCollection<ListConfig> ListConfigs )
        {
            this.ListConfigs = ListConfigs;
            InitializeComponent();
            ListView1.ItemsSource = ListConfigs;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }
        public PoiParam SelectPoiParam { get; set; }
        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (ListConfigs[ListView1.SelectedIndex].Value is PoiParam poiParam)
                SelectPoiParam = poiParam;
                this.Close();
            }
        }
    }
}
