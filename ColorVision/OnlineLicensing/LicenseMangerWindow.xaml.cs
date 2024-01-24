using ColorVision.SettingUp;
using ColorVision.Themes.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.OnlineLicensing
{


    /// <summary>
    /// LicenseManger.xaml 的交互逻辑
    /// </summary>
    public partial class LicenseMangerWindow : BaseWindow
    {
        public LicenseMangerWindow()
        {
            InitializeComponent();
            IsBlurEnabled = ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.TransparentWindow && IsBlurEnabled;
            this.Background = IsBlurEnabled ? this.Background : Brushes.Gray;
        }

        public ObservableCollection<LicenseConfig> Licenses { get; set; }


        private void BaseWindow_Initialized(object sender, EventArgs e)
        {
            Licenses = LicenseManager.GetInstance().Licenses;
            ListViewLicense.ItemsSource = Licenses;
            ListViewLicense.SelectedIndex = 0;
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string Key = File.ReadAllText(openFileDialog.FileName);
                Licenses[ListViewLicense.SelectedIndex].Tag = $"{Key}";
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {

        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewLicense.SelectedIndex > -1)
            {
                GridContent.DataContext = Licenses[ListViewLicense.SelectedIndex];
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
