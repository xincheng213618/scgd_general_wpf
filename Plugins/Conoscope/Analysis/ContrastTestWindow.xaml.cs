using Microsoft.Win32;
using System;
using System.Windows;

namespace Conoscope.Analysis
{
    public partial class ContrastTestWindow : Window
    {
        private readonly DefaultContrastCalculator contrastCalculator = new();
        private ImageMeasurement? whiteMeasurement;
        private ImageMeasurement? blackMeasurement;

        public ContrastTestWindow()
        {
            InitializeComponent();
            StatusText.Text = Properties.Resources.PleaseSelectWhiteBlackImages;
        }

        private void SelectWhite_Click(object sender, RoutedEventArgs e)
        {
            SelectImage(Properties.Resources.SelectWhiteImage, measurement =>
            {
                whiteMeasurement = measurement;
                WhiteFileTextBox.Text = measurement.FilePath;
                WhiteLuminanceText.Text = FormatLuminance(measurement);
            });
        }

        private void SelectBlack_Click(object sender, RoutedEventArgs e)
        {
            SelectImage(Properties.Resources.SelectBlackImage, measurement =>
            {
                blackMeasurement = measurement;
                BlackFileTextBox.Text = measurement.FilePath;
                BlackLuminanceText.Text = FormatLuminance(measurement);
            });
        }

        private void CaptureWhiteFocusPoint_Click(object sender, RoutedEventArgs e)
        {
            CaptureFocusPoint(Properties.Resources.WhitePoint, measurement =>
            {
                whiteMeasurement = measurement;
                WhiteFileTextBox.Text = measurement.FilePath;
                WhiteLuminanceText.Text = FormatLuminance(measurement);
            });
        }

        private void CaptureBlackFocusPoint_Click(object sender, RoutedEventArgs e)
        {
            CaptureFocusPoint(Properties.Resources.BlackPoint, measurement =>
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
                StatusText.Text = string.Format(Properties.Resources.FileRead, measurement.FileName);
                ContrastText.Text = string.Empty;
                UpdateChromaticityText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.ContrastTestTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CaptureFocusPoint(string slotName, Action<ImageMeasurement> applyMeasurement)
        {
            global::Conoscope.ConoscopeView? activeView = GetActiveView();
            if (activeView == null)
            {
                MessageBox.Show(this, Properties.Resources.NoActiveConoscopeView, Properties.Resources.ContrastTestTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!activeView.TryGetLatestFocusPointMeasurement(out ImageMeasurement measurement, out string? errorMessage))
            {
                MessageBox.Show(this, errorMessage ?? Properties.Resources.CurrentFocusPointUnavailable, Properties.Resources.ContrastTestTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            applyMeasurement(measurement);
            StatusText.Text = string.Format(Properties.Resources.SlotRecorded, slotName, measurement.FileName);
            ContrastText.Text = string.Empty;
            UpdateChromaticityText();
        }

        private static global::Conoscope.ConoscopeView? GetActiveView()
        {
            return global::Conoscope.ConoscopeWindow.Instance?.ActiveView;
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (whiteMeasurement == null || blackMeasurement == null)
            {
                MessageBox.Show(this, Properties.Resources.PleaseSelectWhiteBlackFirst, Properties.Resources.ContrastTestTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ContrastResult result = contrastCalculator.Calculate(blackMeasurement, whiteMeasurement);
                ContrastText.Text = result.RatioText;
                StatusText.Text = string.Format(Properties.Resources.WhiteBlackLuminanceFormat, whiteMeasurement.Luminance.ToString("F4"), blackMeasurement.Luminance.ToString("F4"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.ContrastTestTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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
            StatusText.Text = Properties.Resources.PleaseSelectWhiteBlackImages;
        }

        private void UpdateChromaticityText()
        {
            string whiteText = whiteMeasurement == null
                ? Properties.Resources.WhiteImageNotSelected
                : string.Format(Properties.Resources.WhiteImageFormat, whiteMeasurement.Chromaticity.x.ToString("F6"), whiteMeasurement.Chromaticity.y.ToString("F6"));
            string blackText = blackMeasurement == null
                ? Properties.Resources.BlackImageNotSelected
                : string.Format(Properties.Resources.BlackImageFormat, blackMeasurement.Chromaticity.x.ToString("F6"), blackMeasurement.Chromaticity.y.ToString("F6"));
            ChromaticityText.Text = $"{whiteText}\n{blackText}";
        }

        private static string FormatLuminance(ImageMeasurement measurement)
        {
            return string.Format(Properties.Resources.LuminanceFileNameFormat, measurement.Luminance.ToString("F4"), measurement.FileName);
        }
    }
}
