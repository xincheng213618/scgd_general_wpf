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
        /// 使用当前机器码
        /// </summary>
        private void UseCurrentMachineCode_Click(object sender, RoutedEventArgs e)
        {
            txtInputMachineCode.Text = txtCurrentMachineCode.Text;
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
        /// 生成许可证
        /// </summary>
        private void GenerateLicense_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInputMachineCode.Text))
            {
                UpdateStatus("请输入机器码", false);
                txtInputMachineCode.Focus();
                return;
            }

            try
            {
                string machineCode = txtInputMachineCode.Text.Trim();
                string license = LicenseHelper.CreateLicense(machineCode);
                txtLicense.Text = license;

                // 验证生成的许可证
                bool isValid = LicenseHelper.VerifyLicense(license, machineCode);
                UpdateStatus($"许可证生成成功 (验证: {(isValid ? "通过 ✓" : "失败 ✗")})", isValid);
            }
            catch (Exception ex)
            {
                UpdateStatus($"生成许可证失败: {ex.Message}", false);
                txtLicense.Text = string.Empty;
            }
        }

        /// <summary>
        /// 复制许可证
        /// </summary>
        private void CopyLicense_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLicense.Text))
            {
                UpdateStatus("没有可复制的许可证", false);
                return;
            }

            try
            {
                Clipboard.SetText(txtLicense.Text);
                UpdateStatus("许可证已复制到剪贴板", true);
            }
            catch (Exception ex)
            {
                UpdateStatus($"复制失败: {ex.Message}", false);
            }
        }

        /// <summary>
        /// 保存许可证到文件
        /// </summary>
        private void SaveLicense_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLicense.Text))
            {
                UpdateStatus("没有可保存的许可证", false);
                return;
            }

            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "许可证文件 (*.license)|*.license|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    DefaultExt = "license",
                    FileName = $"license_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, txtLicense.Text);
                    UpdateStatus($"许可证已保存到: {saveFileDialog.FileName}", true);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存失败: {ex.Message}", false);
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
        /// 输入机器码变化时清空许可证
        /// </summary>
        private void TxtInputMachineCode_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            txtLicense.Text = string.Empty;
        }
    }
}
