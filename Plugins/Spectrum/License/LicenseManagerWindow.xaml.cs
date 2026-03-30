using ColorVision.UI.Menus;
using Microsoft.Win32;
using Spectrum.License;
using Spectrum.Menus;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Spectrum.License
{

    public class MenuLicenseManager : SpectrumMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;

        public override int Order => 10003;
        public override string Header => "许可证管理";

        public override void Execute()
        {
            new LicenseManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    /// <summary>
    /// View model for license list display.
    /// </summary>
    public class LicenseFileItem
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime ImportedAt { get; set; }
        public string FileSizeDisplay => FileSize < 1024 ? $"{FileSize} B" : $"{FileSize / 1024.0:F1} KB";
        public string ImportedAtDisplay => ImportedAt.ToString("yyyy-MM-dd HH:mm:ss");
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
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LicenseDirText.Text = $"许可证目录: {licenseDir}";
            LoadLicenseFiles();
        }

        private void LoadLicenseFiles()
        {
            var items = new List<LicenseFileItem>();

            // Get records from DB
            var dbRecords = LicenseDatabase.Instance.GetAllRecords();

            // Also scan local directory for files not in DB
            if (Directory.Exists(licenseDir))
            {
                var files = Directory.GetFiles(licenseDir, "*.lic");
                foreach (var file in files)
                {
                    var fi = new FileInfo(file);
                    string fileName = fi.Name;
                    var dbRecord = dbRecords.FirstOrDefault(r => r.FileName == fileName);

                    items.Add(new LicenseFileItem
                    {
                        FileName = fileName,
                        FileSize = fi.Length,
                        ImportedAt = dbRecord?.ImportedAt ?? fi.CreationTime
                    });
                }
            }

            LicenseListView.ItemsSource = items;
        }

        private void LicenseListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LicenseListView.SelectedItem is LicenseFileItem item)
            {
                DetailPanel.Visibility = Visibility.Visible;
                string fullPath = Path.Combine(licenseDir, item.FileName);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        string content = File.ReadAllText(fullPath);
                        // Show first 500 chars of license content
                        string preview = content.Length > 500 ? content[..500] + "..." : content;
                        DetailText.Text = $"文件: {item.FileName}\n大小: {item.FileSizeDisplay}\n导入时间: {item.ImportedAtDisplay}\n\n内容预览:\n{preview}";
                    }
                    catch
                    {
                        DetailText.Text = $"文件: {item.FileName}\n无法读取文件内容";
                    }
                }
                else
                {
                    DetailText.Text = $"文件: {item.FileName}\n文件不存在";
                }
            }
            else
            {
                DetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "License Files (*.lic)|*.lic|All Files|*.*",
                Multiselect = true,
                Title = "选择许可证文件"
            };
            if (ofd.ShowDialog() == true)
            {
                foreach (var file in ofd.FileNames)
                {
                    // Show confirmation with file info before importing
                    string fileName = Path.GetFileName(file);
                    long fileSize = new FileInfo(file).Length;
                    string sizeDisplay = fileSize < 1024 ? $"{fileSize} B" : $"{fileSize / 1024.0:F1} KB";

                    string previewContent = "";
                    try
                    {
                        string content = File.ReadAllText(file);
                        previewContent = content.Length > 300 ? content[..300] + "..." : content;
                    }
                    catch { }

                    var result = MessageBox.Show(
                        $"确认导入许可证?\n\n文件名: {fileName}\n大小: {sizeDisplay}\n\n内容预览:\n{previewContent}",
                        "导入确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        LicenseDatabase.Instance.ImportLicense(file);
                    }
                }
                LoadLicenseFiles();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (LicenseListView.SelectedItem is LicenseFileItem item)
            {
                var result = MessageBox.Show(
                    $"确认删除许可证 {item.FileName}?",
                    "删除确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    LicenseDatabase.Instance.RemoveLicense(item.FileName);
                    LoadLicenseFiles();
                    DetailPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            LicenseSync.SyncLicenses();
            LicenseDatabase.Instance.SyncToLocal();
            LoadLicenseFiles();
            MessageBox.Show("许可证同步完成", "同步", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
