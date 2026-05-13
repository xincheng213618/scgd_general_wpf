using Microsoft.Win32;
using System;
using System.Windows;

namespace Conoscope.Analysis
{
    public partial class ColorGamutWindow : Window
    {
        private readonly DefaultColorGamutCalculator colorGamutCalculator = new();
        private ImageMeasurement? redMeasurement;
        private ImageMeasurement? greenMeasurement;
        private ImageMeasurement? blueMeasurement;

        public ColorGamutWindow()
        {
            InitializeComponent();
            StandardComboBox.ItemsSource = ColorGamutStandards.All;
            StandardComboBox.SelectedIndex = 0;
            StatusText.Text = "请选择 R/G/B 三张图后计算色域";
        }

        private void SelectRed_Click(object sender, RoutedEventArgs e)
        {
            SelectImage("选择 R 图", measurement =>
            {
                redMeasurement = measurement;
                RedFileTextBox.Text = measurement.FilePath;
                RedChromaticityText.Text = FormatChromaticity(measurement);
            });
        }

        private void SelectGreen_Click(object sender, RoutedEventArgs e)
        {
            SelectImage("选择 G 图", measurement =>
            {
                greenMeasurement = measurement;
                GreenFileTextBox.Text = measurement.FilePath;
                GreenChromaticityText.Text = FormatChromaticity(measurement);
            });
        }

        private void SelectBlue_Click(object sender, RoutedEventArgs e)
        {
            SelectImage("选择 B 图", measurement =>
            {
                blueMeasurement = measurement;
                BlueFileTextBox.Text = measurement.FilePath;
                BlueChromaticityText.Text = FormatChromaticity(measurement);
            });
        }

        private void CaptureRedFocusPoint_Click(object sender, RoutedEventArgs e)
        {
            CaptureFocusPoint("R", measurement =>
            {
                redMeasurement = measurement;
                RedFileTextBox.Text = measurement.FilePath;
                RedChromaticityText.Text = FormatChromaticity(measurement);
            });
        }

        private void CaptureGreenFocusPoint_Click(object sender, RoutedEventArgs e)
        {
            CaptureFocusPoint("G", measurement =>
            {
                greenMeasurement = measurement;
                GreenFileTextBox.Text = measurement.FilePath;
                GreenChromaticityText.Text = FormatChromaticity(measurement);
            });
        }

        private void CaptureBlueFocusPoint_Click(object sender, RoutedEventArgs e)
        {
            CaptureFocusPoint("B", measurement =>
            {
                blueMeasurement = measurement;
                BlueFileTextBox.Text = measurement.FilePath;
                BlueChromaticityText.Text = FormatChromaticity(measurement);
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
                AreaText.Text = string.Empty;
                CoverageText.Text = string.Empty;
                StatusText.Text = $"已读取 {measurement.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "RGB 色域计算", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CaptureFocusPoint(string channelName, Action<ImageMeasurement> applyMeasurement)
        {
            global::Conoscope.ConoscopeView? activeView = GetActiveView();
            if (activeView == null)
            {
                MessageBox.Show(this, "当前没有活动的 Conoscope 视图。", "RGB 色域计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!activeView.TryGetLatestFocusPointMeasurement(out ImageMeasurement measurement, out string? errorMessage))
            {
                MessageBox.Show(this, errorMessage ?? "当前关注点不可用。", "RGB 色域计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            applyMeasurement(measurement);
            AreaText.Text = string.Empty;
            CoverageText.Text = string.Empty;
            StatusText.Text = $"已记录 {channelName} 关注点: {measurement.FileName}";
        }

        private static global::Conoscope.ConoscopeView? GetActiveView()
        {
            return global::Conoscope.ConoscopeWindow.Instance?.ActiveView;
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (redMeasurement == null || greenMeasurement == null || blueMeasurement == null)
            {
                MessageBox.Show(this, "请先选择 R/G/B 三张图", "RGB 色域计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StandardComboBox.SelectedItem is not ColorGamutStandard standard)
            {
                MessageBox.Show(this, "请选择色域标准", "RGB 色域计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ColorGamutResult result = colorGamutCalculator.Calculate(redMeasurement, greenMeasurement, blueMeasurement, standard);
                AreaText.Text = $"样本面积={result.SampleArea:F6}，{result.Standard.Name} 标准面积={result.StandardArea:F6}";
                CoverageText.Text = $"{result.CoveragePercent:F2}% {result.Standard.Name}";
                StatusText.Text = $"按 {result.Standard.Name} 标准计算完成";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "RGB 色域计算", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            redMeasurement = null;
            greenMeasurement = null;
            blueMeasurement = null;
            RedFileTextBox.Clear();
            GreenFileTextBox.Clear();
            BlueFileTextBox.Clear();
            RedChromaticityText.Text = string.Empty;
            GreenChromaticityText.Text = string.Empty;
            BlueChromaticityText.Text = string.Empty;
            AreaText.Text = string.Empty;
            CoverageText.Text = string.Empty;
            StatusText.Text = "请选择 R/G/B 三张图后计算色域";
        }

        private static string FormatChromaticity(ImageMeasurement measurement)
        {
            return $"x={measurement.Chromaticity.x:F6}, y={measurement.Chromaticity.y:F6}, Y={measurement.Luminance:F4}  ({measurement.FileName})";
        }
    }
}