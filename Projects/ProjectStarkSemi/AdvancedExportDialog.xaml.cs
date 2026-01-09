using System;
using System.Collections.Generic;
using System.Windows;

namespace ProjectStarkSemi
{
    /// <summary>
    /// AdvancedExportDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AdvancedExportDialog : Window
    {
        public AdvancedExportSettings Settings { get; private set; }

        public AdvancedExportDialog()
        {
            InitializeComponent();
            Settings = new AdvancedExportSettings();
        }

        private void chkEnableCrossSection_Changed(object sender, RoutedEventArgs e)
        {
            if (panelCrossSection != null)
            {
                panelCrossSection.IsEnabled = chkEnableCrossSection.IsChecked == true;
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (!ValidateInputs())
                {
                    return;
                }

                // Collect settings
                Settings.FilePrefix = txtFilePrefix.Text.Trim();

                // Channels
                Settings.Channels = new List<Conoscope.ExportChannel>();
                if (chkChannelR.IsChecked == true) Settings.Channels.Add(Conoscope.ExportChannel.R);
                if (chkChannelG.IsChecked == true) Settings.Channels.Add(Conoscope.ExportChannel.G);
                if (chkChannelB.IsChecked == true) Settings.Channels.Add(Conoscope.ExportChannel.B);
                if (chkChannelX.IsChecked == true) Settings.Channels.Add(Conoscope.ExportChannel.X);
                if (chkChannelY.IsChecked == true) Settings.Channels.Add(Conoscope.ExportChannel.Y);
                if (chkChannelZ.IsChecked == true) Settings.Channels.Add(Conoscope.ExportChannel.Z);

                if (Settings.Channels.Count == 0)
                {
                    MessageBox.Show("请至少选择一个通道", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Export modes
                Settings.ExportAzimuth = chkExportAzimuth.IsChecked == true;
                Settings.ExportPolar = chkExportPolar.IsChecked == true;

                if (!Settings.ExportAzimuth && !Settings.ExportPolar)
                {
                    MessageBox.Show("请至少选择一种导出模式", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Step sizes
                Settings.AzimuthStep = double.Parse(txtAzimuthStep.Text);
                Settings.RadialStep = double.Parse(txtRadialStep.Text);
                Settings.PolarStep = double.Parse(txtPolarStep.Text);
                Settings.CircumferentialStep = double.Parse(txtCircumferentialStep.Text);

                // Cross-section
                Settings.EnableCrossSection = chkEnableCrossSection.IsChecked == true;
                if (Settings.EnableCrossSection)
                {
                    Settings.CrossSectionType = rbCrossSectionAzimuth.IsChecked == true ? 
                        CrossSectionType.Azimuth : CrossSectionType.Polar;
                    
                    if (Settings.CrossSectionType == CrossSectionType.Azimuth)
                    {
                        Settings.CrossSectionAngle = double.Parse(txtCrossSectionAzimuthAngle.Text);
                    }
                    else
                    {
                        Settings.CrossSectionAngle = double.Parse(txtCrossSectionPolarAngle.Text);
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateInputs()
        {
            // Validate file prefix
            if (string.IsNullOrWhiteSpace(txtFilePrefix.Text))
            {
                MessageBox.Show("请输入文件前缀", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Validate azimuth step
            if (!double.TryParse(txtAzimuthStep.Text, out double azimuthStep) || azimuthStep < 0.01 || azimuthStep > 180)
            {
                MessageBox.Show("方位角步进必须是0.01-180之间的数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Validate radial step
            if (!double.TryParse(txtRadialStep.Text, out double radialStep) || radialStep < 0.01 || radialStep > 80)
            {
                MessageBox.Show("径向采样步进必须是0.01-80之间的数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Validate polar step
            if (!double.TryParse(txtPolarStep.Text, out double polarStep) || polarStep < 0.01 || polarStep > 80)
            {
                MessageBox.Show("圆环步进必须是0.01-80之间的数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Validate circumferential step
            if (!double.TryParse(txtCircumferentialStep.Text, out double circumStep) || circumStep < 0.01 || circumStep > 360)
            {
                MessageBox.Show("圆周角步进必须是0.01-360之间的数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Validate cross-section settings if enabled
            if (chkEnableCrossSection.IsChecked == true)
            {
                if (rbCrossSectionAzimuth.IsChecked == true)
                {
                    if (!double.TryParse(txtCrossSectionAzimuthAngle.Text, out double angle) || angle < 0 || angle > 180)
                    {
                        MessageBox.Show("方位角截面角度必须是0-180之间的数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
                else
                {
                    if (!double.TryParse(txtCrossSectionPolarAngle.Text, out double angle) || angle < 0 || angle > 80)
                    {
                        MessageBox.Show("极角截面角度必须是0-80之间的数值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// 高级导出设置
    /// </summary>
    public class AdvancedExportSettings
    {
        public string FilePrefix { get; set; } = "Conoscope_Export";
        public List<Conoscope.ExportChannel> Channels { get; set; } = new List<Conoscope.ExportChannel>();
        public bool ExportAzimuth { get; set; } = true;
        public bool ExportPolar { get; set; } = false;
        public double AzimuthStep { get; set; } = 1;
        public double RadialStep { get; set; } = 1;
        public double PolarStep { get; set; } = 1;
        public double CircumferentialStep { get; set; } = 1;
        public bool EnableCrossSection { get; set; } = false;
        public CrossSectionType CrossSectionType { get; set; } = CrossSectionType.Azimuth;
        public double CrossSectionAngle { get; set; } = 0;
    }

    /// <summary>
    /// 截面类型
    /// </summary>
    public enum CrossSectionType
    {
        Azimuth,  // 方位角截面
        Polar     // 极角截面
    }
}
