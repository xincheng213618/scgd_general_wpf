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
    /// HistogramChartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HistogramChartWindow : Window
    {
        private HistogramData _histogramData;
        private bool _isLogScale = false;
        
        private BarPlot? _redBars;
        private BarPlot? _greenBars;
        private BarPlot? _blueBars;
        private BarPlot? _grayBars;

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
                    _redBars = WpfPlot.Plot.Add.Bars(positions, redValues);
                    _redBars.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(100, 255, 0, 0));
                    _redBars.LegendText = "Red";
                }

                // Green channel
                if (ShowGreenCheckBox.IsChecked == true)
                {
                    double[] greenValues = PrepareHistogramValues(_histogramData.GreenChannel);
                    _greenBars = WpfPlot.Plot.Add.Bars(positions, greenValues);
                    _greenBars.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(100, 0, 255, 0));
                    _greenBars.LegendText = "Green";
                }

                // Blue channel
                if (ShowBlueCheckBox.IsChecked == true)
                {
                    double[] blueValues = PrepareHistogramValues(_histogramData.BlueChannel);
                    _blueBars = WpfPlot.Plot.Add.Bars(positions, blueValues);
                    _blueBars.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(100, 0, 0, 255));
                    _blueBars.LegendText = "Blue";
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
                    _grayBars = WpfPlot.Plot.Add.Bars(positions, grayValues);
                    _grayBars.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(150, 128, 128, 128));
                    _grayBars.LegendText = "Gray";
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
                return histogram.Select(v => Math.Log(v + 1)).ToArray();
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
