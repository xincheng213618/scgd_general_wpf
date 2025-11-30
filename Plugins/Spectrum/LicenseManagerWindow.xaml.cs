using ColorVision.UI.Menus;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace Spectrum
{

    public class MenuLicenseManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;

        public override int Order => 10003;
        public override string Header => "许可证管理";

        public override void Execute()
        {
            new LicenseManagerWindow() { Owner =Application.Current.GetActiveWindow(),WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
    /// <summary>
    /// LicenseManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LicenseManagerWindow : Window
    {
        private readonly string licenseDir = LicenseSync.LocalLicenseDir;

        public LicenseManagerWindow()
        {
            InitializeComponent();
            LoadLicenseFiles();
        }

        private void LoadLicenseFiles()
        {
            LicenseListBox.Items.Clear();
            if (Directory.Exists(licenseDir))
            {
                var files = Directory.GetFiles(licenseDir, "*.lic");
                foreach (var file in files)
                {
                    LicenseListBox.Items.Add(System.IO.Path.GetFileName(file));
                }
            }
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "License Files (*.lic)|*.lic",
                Multiselect = true
            };
            if (ofd.ShowDialog() == true)
            {
                Directory.CreateDirectory(licenseDir);
                foreach (var file in ofd.FileNames)
                {
                    var dest = System.IO.Path.Combine(licenseDir, System.IO.Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }
                LoadLicenseFiles();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (LicenseListBox.SelectedItem != null)
            {
                var selectedFile = LicenseListBox.SelectedItem.ToString();
                var fullPath = System.IO.Path.Combine(licenseDir, selectedFile);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    LoadLicenseFiles();
                }
            }
        }
    }
}
