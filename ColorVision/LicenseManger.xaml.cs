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
        public string Sn { get => _Sn; set { _Sn = value; NotifyPropertyChanged(); } }
        private string _Sn;

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
            ListViewLicense.ItemsSource = LicenseConfigs;
            LicenseConfigs.Add(new LicenseConfig() { Name = "ColorVision", Sn = "4060005EAD286752E9BF44AD08D2325E", Tag = $"序列号：4060005EAD286752E9BF44AD08D2325E\n\r许可给 fuzzes ally\n\r订阅将于 July 2, 2023过期\n\r您拥有此版本的永久回退许可证" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "VIDCamera", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r许可给 fuzzes ally\n\r订阅将于 July 9, 2023过期\n\r您拥有此版本的永久回退许可证" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "CameraTest", Sn = "409D2B7555605C0B7ABABD5D31ECA47D",Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r未注册" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "CameraTest", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r未注册" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "CameraTest", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r未注册" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "CameraTest", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r未注册" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "CameraTest", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r未注册" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "CameraTest", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r未注册" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "CameraTest", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r未注册" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "CameraTest", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r未注册" });

            ListViewLicense.SelectedIndex = 0;
        }



        DateTime dateTime = DateTime.Now;
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            dateTime = dateTime.AddYears(1);
            MessageBox.Show("导入");
            LicenseConfigs[ListViewLicense.SelectedIndex].Tag = $"序列号：409D2B7555605C0B7ABABD5D31ECA47D\n\r许可给 fuzzes ally\n\r订阅将于{dateTime:yyyy年MM月dd日}过期\n\r您拥有此版本的永久回退许可证";
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {

        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewLicense.SelectedIndex > -1)
            {
                GridContent.DataContext = LicenseConfigs[ListViewLicense.SelectedIndex];
            }
        }
    }
}
