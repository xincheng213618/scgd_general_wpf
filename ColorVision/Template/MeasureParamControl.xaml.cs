using ColorVision.MVVM;
using ColorVision.MySql.DAO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Template
{
    public class MParamConfig : ViewModelBase
    {
        public MParamConfig(MeasureDetailModel model)
        {
            Name = model.Name;
            Type = model.Pcode;
        }

        public int ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }
    /// <summary>
    /// MeasureParamControl.xaml 的交互逻辑
    /// </summary>
    public partial class MeasureParamControl : UserControl
    {
        public MeasureParamControl()
        {
            InitializeComponent();
        }
        public ObservableCollection<MParamConfig> ListConfigs { get; set; } = new ObservableCollection<MParamConfig>();
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ListView1.ItemsSource = ListConfigs;
        }
    }
}
