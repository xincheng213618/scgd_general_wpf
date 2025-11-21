using ColorVision.ImageEditor.EditorTools.Histogram;
using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Linq;
using System.Windows;

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
        
        // Removed BarPlot fields as we are now using local Scatter variables in UpdatePlot
        // and clearing the plot on each update.

        public HistogramChartWindow(int[] redHistogram, int[] greenHistogram, int[] blueHistogram)
        {
            _histogramData = HistogramData.CreateMultiChannel(redHistogram, greenHistogram, blueHistogram);
            InitializeComponent();
            this.ApplyCaption();
        }

        public HistogramChartWindow(int[] grayHistogram)
        {
            _histogramData = HistogramData.CreateSingleChannel(grayHistogram);
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
            }
            else
            {
                ShowGrayCheckBox.Visibility = Visibility.Visible;
            }

            // Initialize the plot
            InitializePlot();
            UpdatePlot();
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

            if (_histogramData.IsMultiChannel)
            {
                // Multi-channel histogram
                // Red channel
                if (ShowRedCheckBox.IsChecked == true)
                {
                    double[] redValues = PrepareHistogramValues(_histogramData.RedChannel);
                    // Use Scatter with FillY for continuous area look
                    var redPlot = WpfPlot.Plot.Add.Scatter(positions, redValues);
                    redPlot.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red); // Line Color
                    redPlot.FillY = true;
                    redPlot.FillYColor = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(64, 255, 0, 0)); // Semi-transparent fill
                    redPlot.MarkerSize = 0; // Hide points
                    redPlot.LineWidth = 1;
                    redPlot.LegendText = "Red";
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
                }

                // Show legend for multi-channel
                WpfPlot.Plot.ShowLegend();
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
                }
            }

            // Update Y label
            WpfPlot.Plot.YLabel(_isLogScale ? "Count (Log Scale)" : "Count");

            // Auto-scale axes
            WpfPlot.Plot.Axes.AutoScale();

            // Refresh the plot
            WpfPlot.Refresh();
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

            UpdatePlot();
        }

        private void ChannelCheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdatePlot();
        }
    }
}