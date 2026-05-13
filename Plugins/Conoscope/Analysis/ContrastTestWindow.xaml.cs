using Microsoft.Win32;
using System;
using System.Windows;

namespace Conoscope.Analysis
{
    public partial class ContrastTestWindow : Window
    {
        private readonly IContrastCalculator contrastCalculator = new DefaultContrastCalculator();
        private ImageMeasurement? whiteMeasurement;
        private ImageMeasurement? blackMeasurement;

        public ContrastTestWindow()
        {
            InitializeComponent();
            StatusText.Text = "请选择白图和黑图后计算对比度";
        }

        private void SelectWhite_Click(object sender, RoutedEventArgs e)
        {
            SelectImage("选择白图", measurement =>
            {
                whiteMeasurement = measurement;
                WhiteFileTextBox.Text = measurement.FilePath;
                WhiteLuminanceText.Text = FormatLuminance(measurement);
            });
        }

        private void SelectBlack_Click(object sender, RoutedEventArgs e)
        {
            SelectImage("选择黑图", measurement =>
            {
                blackMeasurement = measurement;
                BlackFileTextBox.Text = measurement.FilePath;
                BlackLuminanceText.Text = FormatLuminance(measurement);
            });
        }

        private void SelectImage(string title, Action<ImageMeasurement> applyMeasurement)
        {
            OpenFileDialog openFileDialog = new()
            {
                Title = title,
                Filter = "CVCIE 文件|*.cvcie|所有文件|*.*",
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                ImageMeasurement measurement = ImageMeasurementProviderRegistry.Read(openFileDialog.FileName);
                applyMeasurement(measurement);
                StatusText.Text = $"已读取 {measurement.FileName}";
                ContrastText.Text = string.Empty;
                UpdateChromaticityText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "对比度测试", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (whiteMeasurement == null || blackMeasurement == null)
            {
                MessageBox.Show(this, "请先选择白图和黑图", "对比度测试", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ContrastResult result = contrastCalculator.Calculate(blackMeasurement, whiteMeasurement);
                ContrastText.Text = result.RatioText;
                StatusText.Text = $"白亮度 / 黑亮度 = {whiteMeasurement.Luminance:F4} / {blackMeasurement.Luminance:F4}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "对比度测试", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            whiteMeasurement = null;
            blackMeasurement = null;
            WhiteFileTextBox.Clear();
            BlackFileTextBox.Clear();
            WhiteLuminanceText.Text = string.Empty;
            BlackLuminanceText.Text = string.Empty;
            ContrastText.Text = string.Empty;
            ChromaticityText.Text = string.Empty;
            StatusText.Text = "请选择白图和黑图后计算对比度";
        }

        private void UpdateChromaticityText()
        {
            string whiteText = whiteMeasurement == null ? "白图: 未选择" : $"白图: x={whiteMeasurement.Chromaticity.x:F6}, y={whiteMeasurement.Chromaticity.y:F6}";
            string blackText = blackMeasurement == null ? "黑图: 未选择" : $"黑图: x={blackMeasurement.Chromaticity.x:F6}, y={blackMeasurement.Chromaticity.y:F6}";
            ChromaticityText.Text = $"{whiteText}\n{blackText}";
        }

        private static string FormatLuminance(ImageMeasurement measurement)
        {
            return $"Y={measurement.Luminance:F4}  ({measurement.FileName})";
        }
    }
}