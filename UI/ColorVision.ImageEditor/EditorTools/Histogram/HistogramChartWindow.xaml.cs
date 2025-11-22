using ColorVision.ImageEditor.EditorTools.Histogram;
using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public class HistogramChartConfig : ViewModelBase, IConfig
    {
        public static HistogramChartConfig Instance => ConfigService.Instance.GetRequiredService<HistogramChartConfig>();
    }

    /// <summary>
    /// Interaction logic for HistogramChartWindow.xaml
    /// </summary>
    public partial class HistogramChartWindow : Window
    {
        private HistogramData _histogramData;
        private bool _isLogScale = false;
        private bool _isOptimized = false;
        private bool _isEditMode = false;
        private ToneCurve _toneCurve;
        private CurvePoint? _selectedPoint;
        private BitmapSource? _originalImage;
        private ImageView? _imageView;

        public HistogramChartWindow(int[] redHistogram, int[] greenHistogram, int[] blueHistogram, ImageView? imageView = null)
        {
            _histogramData = HistogramData.CreateMultiChannel(redHistogram, greenHistogram, blueHistogram);
            _toneCurve = new ToneCurve();
            _imageView = imageView;
            if (_imageView != null)
            {
                _originalImage = _imageView.ViewBitmapSource as BitmapSource;
            }
            InitializeComponent();
            this.ApplyCaption();
        }

        public HistogramChartWindow(int[] grayHistogram, ImageView? imageView = null)
        {
            _histogramData = HistogramData.CreateSingleChannel(grayHistogram);
            _toneCurve = new ToneCurve();
            _imageView = imageView;
            if (_imageView != null)
            {
                _originalImage = _imageView.ViewBitmapSource as BitmapSource;
            }
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            // Configure channel visibility checkboxes
            if (_histogramData.IsMultiChannel)
            {
                ShowRedCheckBox.Visibility = Visibility.Visible;
                ShowGreenCheckBox.Visibility = Visibility.Visible;
                ShowBlueCheckBox.Visibility = Visibility.Visible;
                ShowGrayCheckBox.Visibility = Visibility.Visible; // Show gray for multi-channel too
                ShowGrayCheckBox.IsChecked = false;
            }
            else
            {
                ShowGrayCheckBox.Visibility = Visibility.Visible;
            }

            // Initialize the plot
            InitializePlot();
            UpdatePlot();

            // Setup mouse event handlers for curve editing
            WpfPlot.MouseDown += WpfPlot_MouseDown;
            WpfPlot.MouseMove += WpfPlot_MouseMove;
            WpfPlot.MouseUp += WpfPlot_MouseUp;
            WpfPlot.MouseRightButtonDown += WpfPlot_MouseRightButtonDown;
        }

        private void InitializePlot()
        {
            // Clear any existing data
            WpfPlot.Plot.Clear();

            // Configure plot appearance
            WpfPlot.Plot.Title("Histogram");
            WpfPlot.Plot.XLabel("Pixel Value (0-255)");
            WpfPlot.Plot.YLabel(_isLogScale ? "Count (Log Scale)" : "Count");

            // Set Chinese font support
            string detectedFont = ScottPlot.Fonts.Detect("直方图");
            WpfPlot.Plot.Axes.Title.Label.FontName = detectedFont;
            WpfPlot.Plot.Axes.Left.Label.FontName = detectedFont;
            WpfPlot.Plot.Axes.Bottom.Label.FontName = detectedFont;

            // Configure grid
            WpfPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);

            // Set X axis limits
            WpfPlot.Plot.Axes.SetLimitsX(0, 256);
        }

        private void UpdatePlot()
        {
            if (WpfPlot == null) return;
            // Clear existing plottables
            WpfPlot.Plot.Clear();

            // Prepare X-axis data (bin positions 0-255)
            double[] positions = Enumerable.Range(0, 256).Select(i => (double)i).ToArray();

            double maxYValue = 0;

            if (_histogramData.IsMultiChannel)
            {
                // Multi-channel histogram
                // Red channel
                if (ShowRedCheckBox.IsChecked == true)
                {
                    double[] redValues = PrepareHistogramValues(_histogramData.RedChannel);
                    var redPlot = WpfPlot.Plot.Add.Scatter(positions, redValues);
                    redPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red);
                    redPlot.FillY = true;
                    redPlot.FillYColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(64, 255, 0, 0));
                    redPlot.MarkerSize = 0;
                    redPlot.LineWidth = 1;
                    redPlot.LegendText = "Red";
                    maxYValue = Math.Max(maxYValue, redValues.Max());
                }

                // Green channel
                if (ShowGreenCheckBox.IsChecked == true)
                {
                    double[] greenValues = PrepareHistogramValues(_histogramData.GreenChannel);
                    var greenPlot = WpfPlot.Plot.Add.Scatter(positions, greenValues);
                    greenPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Lime);
                    greenPlot.FillY = true;
                    greenPlot.FillYColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(64, 0, 255, 0));
                    greenPlot.MarkerSize = 0;
                    greenPlot.LineWidth = 1;
                    greenPlot.LegendText = "Green";
                    maxYValue = Math.Max(maxYValue, greenValues.Max());
                }

                // Blue channel
                if (ShowBlueCheckBox.IsChecked == true)
                {
                    double[] blueValues = PrepareHistogramValues(_histogramData.BlueChannel);
                    var bluePlot = WpfPlot.Plot.Add.Scatter(positions, blueValues);
                    bluePlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Blue);
                    bluePlot.FillY = true;
                    bluePlot.FillYColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(64, 0, 0, 255));
                    bluePlot.MarkerSize = 0;
                    bluePlot.LineWidth = 1;
                    bluePlot.LegendText = "Blue";
                    maxYValue = Math.Max(maxYValue, blueValues.Max());
                }

                // Gray channel for multi-channel
                if (ShowGrayCheckBox.IsChecked == true)
                {
                    double[] grayValues = PrepareHistogramValues(_histogramData.GrayChannel);
                    var grayPlot = WpfPlot.Plot.Add.Scatter(positions, grayValues);
                    grayPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
                    grayPlot.FillY = true;
                    grayPlot.FillYColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(64, 128, 128, 128));
                    grayPlot.MarkerSize = 0;
                    grayPlot.LineWidth = 1;
                    grayPlot.LegendText = "Gray";
                    maxYValue = Math.Max(maxYValue, grayValues.Max());
                }

                // Show legend for multi-channel
                WpfPlot.Plot.ShowLegend(Alignment.UpperRight);
            }
            else
            {
                // Single-channel (grayscale) histogram
                if (ShowGrayCheckBox.IsChecked == true)
                {
                    double[] grayValues = PrepareHistogramValues(_histogramData.GrayChannel);
                    var grayPlot = WpfPlot.Plot.Add.Scatter(positions, grayValues);
                    grayPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
                    grayPlot.FillY = true;
                    grayPlot.FillYColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(128, 128, 128, 128));
                    grayPlot.MarkerSize = 0;
                    grayPlot.LineWidth = 1;
                    grayPlot.LegendText = "Gray";
                    maxYValue = Math.Max(maxYValue, grayValues.Max());
                }
            }

            // Update Y label
            string yLabel = "Count";
            if (_isLogScale)
                yLabel = "Count (Log Scale)";
            else if (_isOptimized)
                yLabel = "Count (Optimized)";
            WpfPlot.Plot.YLabel(yLabel);

            // Set axis limits
            WpfPlot.Plot.Axes.SetLimitsX(0, 256);
            
            if (_isOptimized && !_isLogScale)
            {
                // In optimize mode, calculate Y max based on second highest value
                double optimizedYMax = CalculateOptimizedYMax();
                WpfPlot.Plot.Axes.SetLimitsY(0, optimizedYMax);
            }
            else
            {
                // Normal mode: ensure Y starts at 0
                WpfPlot.Plot.Axes.SetLimitsY(0, maxYValue * 1.1); // Add 10% padding
            }

            // Draw the tone curve if in edit mode
            if (_isEditMode)
            {
                DrawCurve();
            }

            // Refresh the plot
            WpfPlot.Refresh();
        }

        private double CalculateOptimizedYMax()
        {
            // Collect all values from enabled channels
            List<double> allValues = new List<double>();

            if (_histogramData.IsMultiChannel)
            {
                if (ShowRedCheckBox.IsChecked == true)
                {
                    var doubles = PrepareHistogramValues(_histogramData.RedChannel);
                    var sortedValues1 = doubles.OrderByDescending(v => v).Distinct().ToList();
                    allValues.Add(sortedValues1[1] * 1.2);
                }
                if (ShowGreenCheckBox.IsChecked == true)
                {
                    var doubles = PrepareHistogramValues(_histogramData.GreenChannel);
                    var sortedValues1 = doubles.OrderByDescending(v => v).Distinct().ToList();
                    allValues.Add(sortedValues1[1] * 1.1);
                }
                if (ShowBlueCheckBox.IsChecked == true)
                {
                    var doubles = PrepareHistogramValues(_histogramData.BlueChannel);
                    var sortedValues1 = doubles.OrderByDescending(v => v).Distinct().ToList();
                    allValues.Add(sortedValues1[1] * 1.1);
                }
                if (ShowGrayCheckBox.IsChecked == true)
                {
                    var doubles = PrepareHistogramValues(_histogramData.GrayChannel);
                    var sortedValues1 = doubles.OrderByDescending(v => v).Distinct().ToList();
                    allValues.Add(sortedValues1[1] * 1.1);
                }
                if (allValues.Count == 0)
                    return 100;
                return allValues.Max();
            }
            else
            {
                if (ShowGrayCheckBox.IsChecked == true)
                    allValues.AddRange(PrepareHistogramValues(_histogramData.GrayChannel));
                if (allValues.Count == 0)
                    return 100;

                // Sort and find second highest value
                var sortedValues = allValues.OrderByDescending(v => v).Distinct().ToList();

                if (sortedValues.Count < 2)
                    return sortedValues.First() * 1.1;

                // Use second highest value with 120% multiplier for better visibility
                double secondHighest = sortedValues[1];
                return secondHighest * 1.1;
            }
        }

        private double[] PrepareHistogramValues(int[] histogram)
        {
            if (_isLogScale)
            {
                // Apply log scale: log(value + 1)
                return histogram.Select(v => Math.Log((double)(v + 1),10)).ToArray();
            }
            else
            {
                // Normal scale
                return histogram.Select(v => (double)v).ToArray();
            }
        }

        private void LogScaleButton_Click(object sender, RoutedEventArgs e)
        {
            _isLogScale = !_isLogScale;
            
            // Update button text
            if (sender is System.Windows.Controls.Button button)
            {
                button.Content = _isLogScale ? "Linear Scale" : "Log Scale";
            }

            // Reset optimize mode when switching to log scale
            if (_isLogScale)
            {
                _isOptimized = false;
                OptimizeButton.Content = "Optimize";
            }

            UpdatePlot();
        }

        private void OptimizeButton_Click(object sender, RoutedEventArgs e)
        {
            _isOptimized = !_isOptimized;
            
            // Update button text
            if (sender is System.Windows.Controls.Button button)
            {
                button.Content = _isOptimized ? "Normal" : "Optimize";
            }

            // Reset log scale when switching to optimize mode
            if (_isOptimized)
            {
                _isLogScale = false;
                LogScaleButton.Content = "Log Scale";
            }

            UpdatePlot();
        }

        private void ChannelCheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdatePlot();
        }

        private void EditModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = !_isEditMode;

            if (_isEditMode)
            {
                EditModeButton.Content = "查看模式";
                ResetCurveButton.Visibility = Visibility.Visible;
                ApplyButton.Visibility = Visibility.Visible;
                
                // Disable log scale and optimize mode in edit mode
                _isLogScale = false;
                _isOptimized = false;
                LogScaleButton.Content = "Log Scale";
                OptimizeButton.Content = "Optimize";
                LogScaleButton.IsEnabled = false;
                OptimizeButton.IsEnabled = false;
            }
            else
            {
                EditModeButton.Content = "编辑模式";
                ResetCurveButton.Visibility = Visibility.Collapsed;
                ApplyButton.Visibility = Visibility.Collapsed;
                LogScaleButton.IsEnabled = true;
                OptimizeButton.IsEnabled = true;
                
                // Reset to original image
                if (_imageView != null && _originalImage != null)
                {
                    _imageView.ImageShow.Source = _originalImage;
                    _imageView.FunctionImage = null;
                }
            }

            UpdatePlot();
        }

        private void ResetCurveButton_Click(object sender, RoutedEventArgs e)
        {
            _toneCurve.Reset();
            
            // Reset to original image
            if (_imageView != null && _originalImage != null)
            {
                _imageView.ImageShow.Source = _originalImage;
                _imageView.FunctionImage = null;
            }
            
            UpdatePlot();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_imageView != null && _imageView.FunctionImage is WriteableBitmap functionImage)
            {
                // Apply the changes permanently
                _imageView.ViewBitmapSource = functionImage;
                _imageView.ImageShow.Source = _imageView.ViewBitmapSource;
                _originalImage = functionImage;
                _imageView.FunctionImage = null;
            }
        }

        private void WpfPlot_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;

            var position = e.GetPosition(WpfPlot);
            var coordinates = WpfPlot.Plot.GetCoordinates((float)position.X, (float)position.Y);
            
            int inputValue = (int)Math.Clamp(Math.Round(coordinates.X), 0, 255);
            int outputValue = 255 - (int)Math.Clamp(Math.Round(coordinates.Y), 0, 255); // Invert Y axis

            // Try to find an existing point nearby
            _selectedPoint = _toneCurve.FindClosestPoint(inputValue, threshold: 10);

            if (_selectedPoint == null)
            {
                // Add a new point
                _toneCurve.AddOrUpdatePoint(inputValue, outputValue);
                _selectedPoint = _toneCurve.FindClosestPoint(inputValue, threshold: 1);
                ApplyCurveToImage();
            }

            UpdatePlot();
        }

        private void WpfPlot_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isEditMode || _selectedPoint == null || e.LeftButton != MouseButtonState.Pressed) return;

            var position = e.GetPosition(WpfPlot);
            var coordinates = WpfPlot.Plot.GetCoordinates((float)position.X, (float)position.Y);
            
            int inputValue = (int)Math.Clamp(Math.Round(coordinates.X), 0, 255);
            int outputValue = 255 - (int)Math.Clamp(Math.Round(coordinates.Y), 0, 255); // Invert Y axis

            // Update the selected point
            _toneCurve.AddOrUpdatePoint(inputValue, outputValue);
            _selectedPoint = _toneCurve.FindClosestPoint(inputValue, threshold: 1);
            
            ApplyCurveToImage();
            UpdatePlot();
        }

        private void WpfPlot_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _selectedPoint = null;
        }

        private void WpfPlot_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;

            var position = e.GetPosition(WpfPlot);
            var coordinates = WpfPlot.Plot.GetCoordinates((float)position.X, (float)position.Y);
            
            int inputValue = (int)Math.Clamp(Math.Round(coordinates.X), 0, 255);

            // Try to find and remove a point
            var point = _toneCurve.FindClosestPoint(inputValue, threshold: 10);
            if (point != null && point.Input != 0 && point.Input != 255)
            {
                _toneCurve.RemovePoint(point.Input);
                ApplyCurveToImage();
                UpdatePlot();
            }

            e.Handled = true;
        }

        private void DrawCurve()
        {
            if (!_isEditMode) return;

            // Draw the tone curve
            double[] curveX = new double[256];
            double[] curveY = new double[256];

            for (int i = 0; i < 256; i++)
            {
                curveX[i] = i;
                curveY[i] = 255 - _toneCurve.GetOutput(i); // Invert Y for display
            }

            var curvePlot = WpfPlot.Plot.Add.Scatter(curveX, curveY);
            curvePlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.White);
            curvePlot.LineWidth = 2;
            curvePlot.MarkerSize = 0;

            // Draw control points
            foreach (var point in _toneCurve.Points)
            {
                var marker = WpfPlot.Plot.Add.Marker(point.Input, 255 - point.Output);
                marker.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Yellow);
                marker.Size = point == _selectedPoint ? 12 : 8;
                marker.Shape = MarkerShape.FilledCircle;
            }
        }

        private void ApplyCurveToImage()
        {
            if (_imageView == null || _originalImage == null) return;

            try
            {
                // Get the lookup table
                int[] lut = _toneCurve.GetLUT();

                // Apply LUT to the image
                var adjustedImage = ApplyLUTToImage(_originalImage, lut);
                
                if (adjustedImage != null)
                {
                    _imageView.FunctionImage = adjustedImage;
                    _imageView.ImageShow.Source = adjustedImage;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying curve: {ex.Message}");
            }
        }

        private WriteableBitmap? ApplyLUTToImage(BitmapSource source, int[] lut)
        {
            if (source == null) return null;

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * ((source.Format.BitsPerPixel + 7) / 8);
            byte[] pixelData = new byte[height * stride];
            source.CopyPixels(pixelData, stride, 0);

            // Apply LUT to pixel data
            if (source.Format == System.Windows.Media.PixelFormats.Gray8)
            {
                for (int i = 0; i < pixelData.Length; i++)
                {
                    pixelData[i] = (byte)lut[pixelData[i]];
                }
            }
            else if (source.Format == System.Windows.Media.PixelFormats.Bgr24)
            {
                for (int i = 0; i < pixelData.Length; i += 3)
                {
                    pixelData[i] = (byte)lut[pixelData[i]];         // Blue
                    pixelData[i + 1] = (byte)lut[pixelData[i + 1]]; // Green
                    pixelData[i + 2] = (byte)lut[pixelData[i + 2]]; // Red
                }
            }
            else if (source.Format == System.Windows.Media.PixelFormats.Bgra32 || source.Format == System.Windows.Media.PixelFormats.Bgr32)
            {
                for (int i = 0; i < pixelData.Length; i += 4)
                {
                    pixelData[i] = (byte)lut[pixelData[i]];         // Blue
                    pixelData[i + 1] = (byte)lut[pixelData[i + 1]]; // Green
                    pixelData[i + 2] = (byte)lut[pixelData[i + 2]]; // Red
                    // Alpha channel (i+3) is not modified
                }
            }
            else if (source.Format == System.Windows.Media.PixelFormats.Rgb48)
            {
                for (int i = 0; i < pixelData.Length; i += 6)
                {
                    ushort red = BitConverter.ToUInt16(pixelData, i);
                    ushort green = BitConverter.ToUInt16(pixelData, i + 2);
                    ushort blue = BitConverter.ToUInt16(pixelData, i + 4);

                    // Apply LUT to 8-bit values and scale back to 16-bit
                    byte red8 = (byte)(red >> 8);
                    byte green8 = (byte)(green >> 8);
                    byte blue8 = (byte)(blue >> 8);

                    red8 = (byte)lut[red8];
                    green8 = (byte)lut[green8];
                    blue8 = (byte)lut[blue8];

                    ushort newRed = (ushort)(red8 << 8);
                    ushort newGreen = (ushort)(green8 << 8);
                    ushort newBlue = (ushort)(blue8 << 8);

                    BitConverter.GetBytes(newRed).CopyTo(pixelData, i);
                    BitConverter.GetBytes(newGreen).CopyTo(pixelData, i + 2);
                    BitConverter.GetBytes(newBlue).CopyTo(pixelData, i + 4);
                }
            }

            // Create a new WriteableBitmap with the modified pixel data
            var writeableBitmap = new WriteableBitmap(width, height, source.DpiX, source.DpiY, source.Format, source.Palette);
            writeableBitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixelData, stride, 0);

            return writeableBitmap;
        }
    }
}