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
            StatusText.Text = Properties.Resources.PleaseSelectRGBImages;
        }

        private void SelectRed_Click(object sender, RoutedEventArgs e)
        {
            SelectImage(Properties.Resources.SelectRImage, measurement =>
            {
                redMeasurement = measurement;
                RedFileTextBox.Text = measurement.FilePath;
                RedChromaticityText.Text = FormatChromaticity(measurement);
            });
        }

        private void SelectGreen_Click(object sender, RoutedEventArgs e)
        {
            SelectImage(Properties.Resources.SelectGImage, measurement =>
            {
                greenMeasurement = measurement;
                GreenFileTextBox.Text = measurement.FilePath;
                GreenChromaticityText.Text = FormatChromaticity(measurement);
            });
        }

        private void SelectBlue_Click(object sender, RoutedEventArgs e)
        {
            SelectImage(Properties.Resources.SelectBImage, measurement =>
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
                Filter = Properties.Resources.CVCIEFileFilter,
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
                StatusText.Text = string.Format(Properties.Resources.FileRead, measurement.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.RGBColorGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CaptureFocusPoint(string channelName, Action<ImageMeasurement> applyMeasurement)
        {
            global::Conoscope.ConoscopeView? activeView = GetActiveView();
            if (activeView == null)
            {
                MessageBox.Show(this, Properties.Resources.NoActiveConoscopeView, Properties.Resources.RGBColorGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!activeView.TryGetLatestFocusPointMeasurement(out ImageMeasurement measurement, out string? errorMessage))
            {
                MessageBox.Show(this, errorMessage ?? Properties.Resources.CurrentFocusPointUnavailable, Properties.Resources.RGBColorGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            applyMeasurement(measurement);
            AreaText.Text = string.Empty;
            CoverageText.Text = string.Empty;
            StatusText.Text = string.Format(Properties.Resources.FocusPointRecorded, channelName, measurement.FileName);
        }

        private static global::Conoscope.ConoscopeView? GetActiveView()
        {
            return global::Conoscope.ConoscopeWindow.Instance?.ActiveView;
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (redMeasurement == null || greenMeasurement == null || blueMeasurement == null)
            {
                MessageBox.Show(this, Properties.Resources.PleaseSelectRGBFirst, Properties.Resources.RGBColorGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StandardComboBox.SelectedItem is not ColorGamutStandard standard)
            {
                MessageBox.Show(this, Properties.Resources.PleaseSelectGamutStandard, Properties.Resources.RGBColorGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ColorGamutResult result = colorGamutCalculator.Calculate(redMeasurement, greenMeasurement, blueMeasurement, standard);
                AreaText.Text = string.Format(Properties.Resources.SampleAreaFormat, result.SampleArea.ToString("F6"), result.Standard.Name, result.StandardArea.ToString("F6"));
                CoverageText.Text = $"{result.CoveragePercent:F2}% {result.Standard.Name}";
                StatusText.Text = string.Format(Properties.Resources.CalculationCompleteByStandard, result.Standard.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.RGBColorGamutCalc, MessageBoxButton.OK, MessageBoxImage.Warning);
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
            StatusText.Text = Properties.Resources.PleaseSelectRGBImages;
        }

        private static string FormatChromaticity(ImageMeasurement measurement)
        {
            return string.Format(Properties.Resources.ChromaticityLuminanceFormat,
                measurement.Chromaticity.x.ToString("F6"),
                measurement.Chromaticity.y.ToString("F6"),
                measurement.Luminance.ToString("F4"),
                measurement.FileName);
        }
    }
}
