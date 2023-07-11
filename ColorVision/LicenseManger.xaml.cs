using ColorVision.Controls;
using ColorVision.MVVM;
using ColorVision.Template;
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

namespace ColorVision
{
    public class LicenseConfig : ViewModelBase
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Tag { get => _Tag; set { _Tag = value; NotifyPropertyChanged(); } }
        private string _Tag;

        public object? Value { set; get; }
    }

    /// <summary>
    /// LicenseManger.xaml 的交互逻辑
    /// </summary>
    public partial class LicenseManger : BaseWindow
    {
        public ObservableCollection<LicenseConfig> LicenseConfigs { get; set; } = new ObservableCollection<LicenseConfig>();
        public LicenseManger()
        {
            InitializeComponent();
            ListViewMySql.ItemsSource = LicenseConfigs;
            LicenseConfigs.Add(new LicenseConfig() { Name = "12312313" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "12312313" });
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导入");
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {

        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
