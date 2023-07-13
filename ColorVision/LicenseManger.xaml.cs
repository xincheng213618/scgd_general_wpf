using ColorVision.Controls;
using ColorVision.MVVM;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

        public string ActivationCode { get => _ActivationCode; set { _ActivationCode = value; NotifyPropertyChanged(); } }
        private string _ActivationCode;

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
            LicenseConfigs.Add(new LicenseConfig() { Name = "ColorVision", Sn = "4060005EAD286752E9BF44AD08D2325E", Tag = $"许可给 fuzzes ally\n\r订阅将于 July 2, 2023过期\n\r您拥有此版本的永久回退许可证" });
            LicenseConfigs.Add(new LicenseConfig() { Name = "VIDCamera", Sn = "409D2B7555605C0B7ABABD5D31ECA47D", Tag = $"许可给 fuzzes ally\n\r订阅将于 July 9, 2023过期\n\r您拥有此版本的永久回退许可证" });
            ListViewLicense.SelectedIndex = 0;
        }



        DateTime dateTime = DateTime.Now;
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string Key = File.ReadAllText(openFileDialog.FileName);
            }



            dateTime = dateTime.AddYears(1);
            LicenseConfigs[ListViewLicense.SelectedIndex].Tag = $"许可给 fuzzes ally\n\r订阅将于{dateTime:yyyy年MM月dd日}过期\n\r您拥有此版本的永久回退许可证";
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                Clipboard.SetText(TextBoxSn.Text);
                var temp = button.Content;
                button.Content = "已复制";
                await Task.Delay(1000);
                button.Content = temp;
            }
        }
    }
}
