using ColorVision.Controls;
using ColorVision.MQTT;
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

        public bool IsCanImport { get => _IsCanImport; set { _IsCanImport = value; NotifyPropertyChanged(); } }
        private bool _IsCanImport = true;

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

        }
        private async void BaseWindow_Initialized(object sender, EventArgs e)
        {
            ListViewLicense.ItemsSource = LicenseConfigs;
            MQTTManager.GetInstance().MQTTCameras[0].Value.GetAllCameraID();

            await Task.Delay(1000);

            LicenseConfigs.Add(new LicenseConfig() { Name = "ColorVision", Sn = "0000005EAD286752E9BF44AD08D23250", Tag = $"免费版\n\r永久有效", IsCanImport = false });

            MQTT.MQTTManager.GetInstance().MQTTCameras[0].Value.MD5.ForEach(x =>
            {
                LicenseConfigs.Add(new LicenseConfig() { Name = "相机", Sn = x, Tag = $"业务还在开发中" });
            });
            ListViewLicense.SelectedIndex = 0;
        }


        private void Import_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string Key = File.ReadAllText(openFileDialog.FileName);
                LicenseConfigs[ListViewLicense.SelectedIndex].Tag = $"{Key}";
                MQTT.MQTTManager.GetInstance().MQTTCameras[0].Value.SetLicense(LicenseConfigs[ListViewLicense.SelectedIndex].Sn, Key);

            }

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

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                ButtonContentChange(button, "已复制");
        }

        private async void ButtonContentChange(Button button,string Content)
        {
            if (button.Content.ToString() != Content)
            {
                NativeMethods.Clipboard.SetText(TextBoxSn.Text);
                var temp = button.Content;
                button.Content = Content;
                await Task.Delay(1000);
                button.Content = temp;
            }
        }


    }
}
