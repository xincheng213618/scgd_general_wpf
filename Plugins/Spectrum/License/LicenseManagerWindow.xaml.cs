using ColorVision.UI.Menus;
using LicenseGenerator;
using Microsoft.Win32;
using Newtonsoft.Json;
using Spectrum.License;
using Spectrum.Menus;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        /// <summary>
        /// Parsed license info (decoded from base64 content).
        /// </summary>
        public EnhancedLicenseModel? LicenseInfo { get; set; }

        /// <summary>
        /// Brief display of license status.
        /// </summary>
        public string StatusDisplay
        {
            get
            {
                if (LicenseInfo == null) return "未知";
                if (LicenseInfo.IsExpired()) return "已过期";
                return $"有效 ({LicenseInfo.GetRemainingDays()}天)";
            }
        }

        /// <summary>
        /// Color for status display.
        /// </summary>
        public Brush StatusBrush
        {
            get
            {
                if (LicenseInfo == null) return Brushes.Gray;
                if (LicenseInfo.IsExpired()) return Brushes.Red;
                if (LicenseInfo.GetRemainingDays() < 30) return Brushes.Orange;
                return Brushes.Green;
            }
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

                    var item = new LicenseFileItem
                    {
                        FileName = fileName,
                        FileSize = fi.Length,
                        ImportedAt = dbRecord?.ImportedAt ?? fi.CreationTime
                    };

                    // Try to parse license content from base64
                    item.LicenseInfo = TryParseLicenseFile(file);

                    items.Add(item);
                }
            }

            LicenseListView.ItemsSource = items;
        }

        /// <summary>
        /// Try to parse a .lic file as base64-encoded JSON license.
        /// </summary>
        private static EnhancedLicenseModel? TryParseLicenseFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath).Trim();
                return LicenseHelper.ParseEnhancedLicense(content);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Format license details for display.
        /// </summary>
        private static string FormatLicenseDetails(string fileName, LicenseFileItem item)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"文件: {fileName}");
            sb.AppendLine($"大小: {item.FileSizeDisplay}");
            sb.AppendLine($"导入时间: {item.ImportedAtDisplay}");

            if (item.LicenseInfo != null)
            {
                var lic = item.LicenseInfo;
                sb.AppendLine();
                sb.AppendLine("── 许可证信息 ──");
                if (!string.IsNullOrEmpty(lic.DeviceMode))
                    sb.AppendLine($"设备型号: {lic.DeviceMode}");
                if (!string.IsNullOrEmpty(lic.Licensee))
                    sb.AppendLine($"被许可人: {lic.Licensee}");
                if (!string.IsNullOrEmpty(lic.IssuingAuthority))
                    sb.AppendLine($"签发机构: {lic.IssuingAuthority}");
                sb.AppendLine($"签发日期: {lic.IssueDateDateTime:yyyy-MM-dd}");
                sb.AppendLine($"过期日期: {lic.ExpiryDateTime:yyyy-MM-dd}");
                sb.AppendLine($"状态: {item.StatusDisplay}");
                if (!string.IsNullOrEmpty(lic.LicenseeSignature))
                    sb.AppendLine($"机器码: {lic.LicenseeSignature}");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("(无法解析许可证内容)");
            }

            return sb.ToString();
        }

        private void LicenseListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LicenseListView.SelectedItem is LicenseFileItem item)
            {
                DetailPanel.Visibility = Visibility.Visible;
                DetailText.Text = FormatLicenseDetails(item.FileName, item);
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
                    string fileName = Path.GetFileName(file);
                    long fileSize = new FileInfo(file).Length;
                    string sizeDisplay = fileSize < 1024 ? $"{fileSize} B" : $"{fileSize / 1024.0:F1} KB";

                    // Try to decode the license and show details in confirmation
                    var licenseInfo = TryParseLicenseFile(file);

                    var confirmMsg = new StringBuilder();
                    confirmMsg.AppendLine("确认导入许可证?");
                    confirmMsg.AppendLine();
                    confirmMsg.AppendLine($"文件名: {fileName}");
                    confirmMsg.AppendLine($"大小: {sizeDisplay}");

                    if (licenseInfo != null)
                    {
                        confirmMsg.AppendLine();
                        confirmMsg.AppendLine("── 许可证信息 ──");
                        if (!string.IsNullOrEmpty(licenseInfo.DeviceMode))
                            confirmMsg.AppendLine($"设备型号: {licenseInfo.DeviceMode}");
                        if (!string.IsNullOrEmpty(licenseInfo.Licensee))
                            confirmMsg.AppendLine($"被许可人: {licenseInfo.Licensee}");
                        if (!string.IsNullOrEmpty(licenseInfo.IssuingAuthority))
                            confirmMsg.AppendLine($"签发机构: {licenseInfo.IssuingAuthority}");
                        confirmMsg.AppendLine($"签发日期: {licenseInfo.IssueDateDateTime:yyyy-MM-dd}");
                        confirmMsg.AppendLine($"过期日期: {licenseInfo.ExpiryDateTime:yyyy-MM-dd}");
                        if (licenseInfo.IsExpired())
                            confirmMsg.AppendLine("⚠ 此许可证已过期!");
                        else
                            confirmMsg.AppendLine($"剩余有效天数: {licenseInfo.GetRemainingDays()}天");
                    }
                    else
                    {
                        confirmMsg.AppendLine();
                        confirmMsg.AppendLine("(无法解析许可证内容)");
                    }

                    var result = MessageBox.Show(
                        confirmMsg.ToString(),
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
