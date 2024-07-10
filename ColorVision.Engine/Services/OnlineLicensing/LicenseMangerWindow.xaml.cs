using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.OnlineLicensing
{

    public class ExportLincense : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "Lincense";
        public override int Order => 10003;
        public override string Header => Properties.Resources.MyLicense_R;


        public override void Execute()
        {
            new LicenseMangerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }


    /// <summary>
    /// LicenseManger.xaml 的交互逻辑
    /// </summary>
    public partial class LicenseMangerWindow : BaseWindow
    {
        public LicenseMangerWindow()
        {
            InitializeComponent();
            IsBlurEnabled = ThemeConfig.Instance.TransparentWindow && IsBlurEnabled;
            Background = IsBlurEnabled ? Background : Brushes.Gray;
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


        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                await button.ChangeButtonContentAsync("已复制",() => Common.NativeMethods.Clipboard.SetText(TextBoxSn.Text));
        }

        private async void ButtonContentChange(Button button,string Content)
        {
            if (button.Content.ToString() != Content)
            {
                Common.NativeMethods.Clipboard.SetText(TextBoxSn.Text);
                var temp = button.Content;
                button.Content = Content;
                await Task.Delay(1000);
                button.Content = temp;
            }
        }
    }
}
