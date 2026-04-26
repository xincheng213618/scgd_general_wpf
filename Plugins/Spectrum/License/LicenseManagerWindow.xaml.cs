using ColorVision.UI.Menus;
using Microsoft.Win32;
using Newtonsoft.Json;
using Spectrum.Menus;
using System.IO;
using System.Security.Cryptography;
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
    /// 许可证助手类 - 包含许可证生成和验证的核心逻辑
    /// 这是从 ColorVision.UI.ACE.License 类中提取的核心功能
    /// 注意: 简单许可证方法已弃用，请使用增强型许可证方法
    /// </summary>
    public static class LicenseHelper
    {
        /// <summary>
        /// RSA 公钥（用于验证许可证签名）
        /// </summary>
        private const string PublicKeyXml = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>
        /// RSA 私钥（仅用于许可证生成，不应包含在客户端应用程序中）
        /// </summary>
        private const string PrivateKeyXml = "<RSAKeyValue><Modulus>5sf/agoe+/hryIfvt7v6o9aNldWSkUoPkW6se8VbEo7B4JBT0vIUQqku635RU+0vhaF/IJ7TQw6pYerHacA83XYBy90KEN4twOBs1Gy3XfEBcjYheQO919Hif1gENzqzQEg47G36VdmWzmhjreq2YQQQN+p/ezIbYtrPXGNU4fE=</Modulus><Exponent>AQAB</Exponent><P>/OfgYc6H7sSiFUrwkTVtQEyuSm309+Whwuvuul/3zLkNJlvorGC2D5ksTz3Q0XFehHWgWNc0jQ3MRyKp2EHxgw==</P><Q>6ZrTQbe25FVr92pxAlBeO1iONdbLRM+/VmuwrZVgeHvu++8ChAidQT13rcVfqvLDuGq5/q2bgQgmraqdgRNIew==</Q><DP>0sEQ1bDcyncGcyQOMZQKRSkhnVjgaaztDpi6Sooq4GndsXep/+xgC8Ojjy1+VOtazpuPUjmUy28SKr2SOGtLrQ==</DP><DQ>b7mMsDGdVzdDm+Fciy7E4r1HxpgkP5TcfgijR2HZ8cXUVsnI+jzkeP9c7c8oIipZUSo6KoP9i4jKduTSz5jZYQ==</DQ><InverseQ>2kXWXpMpHplGwG/eHR17tVNyfaxjl2Hu2QWnlg5Jf/vLDMcA9MspGS5mS5uCNTTPh34T9PEtmCdA5L5i8kakwg==</InverseQ><D>EmVOzr0PyzX6IXn0ecjaKcUodBEaJcqpgwY3aYZJxCjs+2GFzQLO6qFhxBPFl9MIPrao04jVfjrk9ZEpZByWvUmq79tlzpBjeZW2wcjeUrZYK0/b0D7NRelf6InSJaOb9QKw/hhSPsl3x+nXPyhUFfz6q8bThGDSriC/eb3aSyE=</D></RSAKeyValue>";

        /// <summary>
        /// 获取机器码（基于机器名的十六进制编码）
        /// </summary>
        /// <returns>机器码的十六进制字符串表示</returns>
        public static string GetMachineCode()
        {
            byte[] code = Encoding.UTF8.GetBytes(Environment.MachineName);
            StringBuilder reg = new StringBuilder(code.Length * 2);

            foreach (byte a in code)
            {
                reg.Append(a.ToString("x2"));
            }

            return reg.ToString();
        }



        /// <summary>
        /// 使用 RSA 私钥对文本进行签名
        /// </summary>
        /// <param name="text">要签名的文本</param>
        /// <param name="privateKey">RSA 私钥（XML 格式）</param>
        /// <returns>Base64 编码的签名字符串</returns>
        private static string SignData(string text, string privateKey)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentNullException(nameof(text), "待签名文本不能为空");
            }

            if (string.IsNullOrWhiteSpace(privateKey))
            {
                throw new ArgumentNullException(nameof(privateKey), "私钥不能为空");
            }

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKey);
                byte[] dataBytes = Encoding.UTF8.GetBytes(text);
                // 使用 SHA256 替代已弃用的 MD5
                byte[] signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return Convert.ToBase64String(signatureBytes);
            }
        }

        /// <summary>
        /// 创建增强型许可证
        /// </summary>
        /// <param name="machineCode">机器码</param>
        /// <param name="licensee">被许可人（客户名称）</param>
        /// <param name="deviceMode">设备型号</param>
        /// <param name="expiryDate">过期日期</param>
        /// <param name="issuingAuthority">签发机构</param>
        /// <returns>Base64 编码的 JSON 许可证字符串</returns>
        public static string CreateEnhancedLicense(
            string machineCode,
            string licensee,
            string deviceMode,
            DateTime expiryDate,
            string issuingAuthority = "ColorVision")
        {
            if (string.IsNullOrWhiteSpace(machineCode))
            {
                throw new ArgumentNullException(nameof(machineCode), "机器码不能为空");
            }

            if (string.IsNullOrWhiteSpace(licensee))
            {
                throw new ArgumentNullException(nameof(licensee), "被许可人不能为空");
            }

            // 创建增强许可证对象
            var enhancedLicense = new EnhancedLicenseModel
            {
                LicenseeSignature = machineCode,
                Licensee = licensee,
                DeviceMode = deviceMode,
                IssuingAuthority = issuingAuthority,
                IssueDateDateTime = DateTime.Now,
                ExpiryDateTime = expiryDate
            };

            // 生成授权签名（对机器码+过期时间戳进行签名）
            string dataToSign = $"{machineCode}:{enhancedLicense.ExpiryDate}";
            enhancedLicense.AuthoritySignature = SignData(dataToSign, PrivateKeyXml);

            // 序列化为JSON
            string jsonLicense = JsonConvert.SerializeObject(enhancedLicense, Formatting.None);

            // Base64 编码
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonLicense);
            return Convert.ToBase64String(jsonBytes);
        }

        /// <summary>
        /// 验证增强型许可证
        /// </summary>
        /// <param name="base64License">Base64 编码的许可证</param>
        /// <param name="machineCode">要验证的机器码</param>
        /// <returns>如果许可证有效返回 true，否则返回 false</returns>
        public static bool VerifyEnhancedLicense(string base64License, string machineCode)
        {
            if (string.IsNullOrWhiteSpace(base64License) || string.IsNullOrWhiteSpace(machineCode))
            {
                return false;
            }

            try
            {
                // Base64 解码
                byte[] jsonBytes = Convert.FromBase64String(base64License);
                string jsonLicense = Encoding.UTF8.GetString(jsonBytes);

                // 反序列化
                var license = JsonConvert.DeserializeObject<EnhancedLicenseModel>(jsonLicense);
                if (license == null)
                {
                    return false;
                }

                // 验证机器码
                if (license.LicenseeSignature != machineCode)
                {
                    return false;
                }

                // 验证是否过期
                if (license.IsExpired())
                {
                    return false;
                }

                // 验证授权签名
                string dataToVerify = $"{machineCode}:{license.ExpiryDate}";
                byte[] dataBytes = Encoding.UTF8.GetBytes(dataToVerify);
                byte[] signatureBytes = Convert.FromBase64String(license.AuthoritySignature);

                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(PublicKeyXml);
                    return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 解析增强型许可证（不验证）
        /// </summary>
        /// <param name="base64License">Base64 编码的许可证</param>
        /// <returns>许可证对象，如果解析失败返回 null</returns>
        public static EnhancedLicenseModel? ParseEnhancedLicense(string base64License)
        {
            try
            {
                byte[] jsonBytes = Convert.FromBase64String(base64License);
                string jsonLicense = Encoding.UTF8.GetString(jsonBytes);
                return JsonConvert.DeserializeObject<EnhancedLicenseModel>(jsonLicense);
            }
            catch
            {
                return null;
            }
        }
    }

    public class EnhancedLicenseModel
    {
        /// <summary>
        /// 授权机构签名
        /// </summary>
        [JsonProperty("authority_signature")]
        public string AuthoritySignature { get; set; } = string.Empty;

        /// <summary>
        /// 设备型号
        /// </summary>
        [JsonProperty("device_mode")]
        public string DeviceMode { get; set; } = string.Empty;

        /// <summary>
        /// 过期日期（Unix时间戳，秒）
        /// </summary>
        [JsonProperty("expiry_date")]
        public string ExpiryDate { get; set; } = string.Empty;

        /// <summary>
        /// 过期日期的DateTime表示
        /// </summary>
        [JsonIgnore]
        public DateTime ExpiryDateTime
        {
            get
            {
                if (string.IsNullOrEmpty(ExpiryDate))
                    return DateTime.Now.AddYears(1);

                try
                {
                    long timestamp = long.Parse(ExpiryDate);
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                }
                catch
                {
                    return DateTime.Now.AddYears(1);
                }
            }
            set
            {
                DateTimeOffset offset = new DateTimeOffset(value);
                ExpiryDate = offset.ToUnixTimeSeconds().ToString();
            }
        }

        /// <summary>
        /// 签发日期（Unix时间戳，秒）
        /// </summary>
        [JsonProperty("issue_date")]
        public string IssueDate { get; set; } = string.Empty;

        /// <summary>
        /// 签发日期的DateTime表示
        /// </summary>
        [JsonIgnore]
        public DateTime IssueDateDateTime
        {
            get
            {
                if (string.IsNullOrEmpty(IssueDate))
                    return DateTime.Now;

                try
                {
                    long timestamp = long.Parse(IssueDate);
                    return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                }
                catch
                {
                    return DateTime.Now;
                }
            }
            set
            {
                DateTimeOffset offset = new DateTimeOffset(value);
                IssueDate = offset.ToUnixTimeSeconds().ToString();
            }
        }

        /// <summary>
        /// 签发机构
        /// </summary>
        [JsonProperty("issuing_authority")]
        public string IssuingAuthority { get; set; } = "ColorVision";

        /// <summary>
        /// 被许可人（客户名称）
        /// </summary>
        [JsonProperty("licensee")]
        public string Licensee { get; set; } = string.Empty;

        /// <summary>
        /// 被许可人签名（机器码）
        /// </summary>
        [JsonProperty("licensee_signature")]
        public string LicenseeSignature { get; set; } = string.Empty;

        /// <summary>
        /// 创建默认的增强许可证
        /// </summary>
        public EnhancedLicenseModel()
        {
            IssueDateDateTime = DateTime.Now;
            ExpiryDateTime = DateTime.Now.AddYears(1);
        }

        /// <summary>
        /// 验证许可证是否已过期
        /// </summary>
        /// <returns>如果已过期返回true</returns>
        public bool IsExpired()
        {
            return ExpiryDateTime < DateTime.Now;
        }

        /// <summary>
        /// 获取剩余有效天数
        /// </summary>
        /// <returns>剩余天数</returns>
        public int GetRemainingDays()
        {
            TimeSpan remaining = ExpiryDateTime - DateTime.Now;
            return Math.Max(0, (int)remaining.TotalDays);
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
