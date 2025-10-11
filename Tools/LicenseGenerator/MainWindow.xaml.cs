using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace LicenseGenerator
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadCurrentMachineInfo();
            InitializeEnhancedLicenseDefaults();
        }

        /// <summary>
        /// 初始化增强许可证的默认值
        /// </summary>
        private void InitializeEnhancedLicenseDefaults()
        {
            // 设置默认过期日期为一年后
            dpExpiryDate.SelectedDate = DateTime.Now.AddYears(1);
            UpdateRemainingDays();
        }

        /// <summary>
        /// 更新剩余天数显示
        /// </summary>
        private void UpdateRemainingDays()
        {
            if (dpExpiryDate.SelectedDate.HasValue)
            {
                TimeSpan remaining = dpExpiryDate.SelectedDate.Value - DateTime.Now;
                int days = Math.Max(0, (int)remaining.TotalDays);
                txtRemainingDays.Text = $"剩余天数: {days}";
                
                if (days <= 0)
                {
                    txtRemainingDays.Foreground = System.Windows.Media.Brushes.Red;
                }
                else if (days <= 30)
                {
                    txtRemainingDays.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    txtRemainingDays.Foreground = System.Windows.Media.Brushes.Green;
                }
            }
        }

        /// <summary>
        /// 加载当前机器信息
        /// </summary>
        private void LoadCurrentMachineInfo()
        {
            try
            {
                txtMachineName.Text = Environment.MachineName;
                txtCurrentMachineCode.Text = LicenseHelper.GetMachineCode();
                UpdateStatus("当前机器信息加载成功", true);
            }
            catch (Exception ex)
            {
                UpdateStatus($"加载机器信息失败: {ex.Message}", false);
            }
        }

        /// <summary>
        /// 复制当前机器码
        /// </summary>
        private void CopyCurrentMachineCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtCurrentMachineCode.Text);
                UpdateStatus("机器码已复制到剪贴板", true);
            }
            catch (Exception ex)
            {
                UpdateStatus($"复制失败: {ex.Message}", false);
            }
        }

        /// <summary>
        /// 更新状态信息
        /// </summary>
        private void UpdateStatus(string message, bool isSuccess)
        {
            txtStatus.Text = message;
            txtStatus.Foreground = isSuccess ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        /// <summary>
        /// 使用当前机器码（增强许可证）
        /// </summary>
        private void UseCurrentMachineCodeEnhanced_Click(object sender, RoutedEventArgs e)
        {
            txtEnhancedMachineCode.Text = txtCurrentMachineCode.Text;
        }

        /// <summary>
        /// 增强许可证输入变化时清空许可证
        /// </summary>
        private void TxtEnhancedInput_TextChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if(txtEnhancedLicense != null)
            {
                txtEnhancedLicense.Text = string.Empty;
                UpdateRemainingDays();
            }


        }

        /// <summary>
        /// 生成增强许可证
        /// </summary>
        private void GenerateEnhancedLicense_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(txtEnhancedMachineCode.Text))
            {
                UpdateStatus("请输入机器码", false);
                txtEnhancedMachineCode.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtLicensee.Text))
            {
                UpdateStatus("请输入客户名称", false);
                txtLicensee.Focus();
                return;
            }

            if (!dpExpiryDate.SelectedDate.HasValue)
            {
                UpdateStatus("请选择有效期", false);
                dpExpiryDate.Focus();
                return;
            }

            if (dpExpiryDate.SelectedDate.Value <= DateTime.Now)
            {
                UpdateStatus("有效期必须晚于当前时间", false);
                dpExpiryDate.Focus();
                return;
            }

            try
            {
                string machineCode = txtEnhancedMachineCode.Text.Trim();
                string licensee = txtLicensee.Text.Trim();
                string deviceMode = txtDeviceMode.Text.Trim();
                DateTime expiryDate = dpExpiryDate.SelectedDate.Value;
                string issuingAuthority = txtIssuingAuthority.Text.Trim();

                // 生成增强许可证
                string enhancedLicense = LicenseHelper.CreateEnhancedLicense(
                    machineCode, 
                    licensee, 
                    deviceMode, 
                    expiryDate, 
                    issuingAuthority);
                
                txtEnhancedLicense.Text = enhancedLicense;

                // 验证生成的许可证
                bool isValid = LicenseHelper.VerifyEnhancedLicense(enhancedLicense, machineCode);
                
                // 解析许可证以显示详细信息
                var parsedLicense = LicenseHelper.ParseEnhancedLicense(enhancedLicense);
                if (parsedLicense != null)
                {
                    int remainingDays = parsedLicense.GetRemainingDays();
                    UpdateStatus($"增强许可证生成成功 (验证: {(isValid ? "通过 ✓" : "失败 ✗")}, 有效期: {remainingDays} 天)", isValid);
                }
                else
                {
                    UpdateStatus($"增强许可证生成成功 (验证: {(isValid ? "通过 ✓" : "失败 ✗")})", isValid);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"生成增强许可证失败: {ex.Message}", false);
                txtEnhancedLicense.Text = string.Empty;
            }
        }

        /// <summary>
        /// 复制增强许可证
        /// </summary>
        private void CopyEnhancedLicense_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtEnhancedLicense.Text))
            {
                UpdateStatus("没有可复制的增强许可证", false);
                return;
            }

            try
            {
                Clipboard.SetText(txtEnhancedLicense.Text);
                UpdateStatus("增强许可证已复制到剪贴板", true);
            }
            catch (Exception ex)
            {
                UpdateStatus($"复制失败: {ex.Message}", false);
            }
        }

        /// <summary>
        /// 保存增强许可证到文件
        /// </summary>
        private void SaveEnhancedLicense_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtEnhancedLicense.Text))
            {
                UpdateStatus("没有可保存的增强许可证", false);
                return;
            }

            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "许可证文件 (*.lic)|*.lic|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    DefaultExt = "lic",
                    FileName = $"enhanced_license_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, txtEnhancedLicense.Text);
                    UpdateStatus($"增强许可证已保存到: {saveFileDialog.FileName}", true);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存失败: {ex.Message}", false);
            }
        }
    }
}
