using ColorVision.ImageEditor.Draw.Line;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ProfileChartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProfileChartWindow : Window
    {
        private ProfileData _profileData;
        private Scatter? _redScatter;
        private Scatter? _greenScatter;
        private Scatter? _blueScatter;
        private Scatter? _grayScatter;

        public ProfileChartWindow(ProfileData profileData, string title)
        {
            _profileData = profileData;
            InitializeComponent();
            this.Title = title;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            // Configure channel visibility checkboxes
            if (_profileData.IsMultiChannel)
            {
                ShowRedCheckBox.Visibility = Visibility.Visible;
                ShowGreenCheckBox.Visibility = Visibility.Visible;
                ShowBlueCheckBox.Visibility = Visibility.Visible;
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
            WpfPlot.Plot.Title("Profile Chart");
            WpfPlot.Plot.XLabel("Sample Point");
            WpfPlot.Plot.YLabel("Value");

            // Set Chinese font support
            string fontSample = "Profile";
            string detectedFont = ScottPlot.Fonts.Detect(fontSample);
            WpfPlot.Plot.Axes.Title.Label.FontName = detectedFont;
            WpfPlot.Plot.Axes.Left.Label.FontName = detectedFont;
            WpfPlot.Plot.Axes.Bottom.Label.FontName = detectedFont;

            // Configure grid
            WpfPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
        }

        private void UpdatePlot()
        {
            if (WpfPlot == null) return;
            // Clear existing plottables
            WpfPlot.Plot.Clear();

            if (_profileData.SampleCount == 0)
                return;

            // Prepare X-axis data (sample indices)
            double[] xData = Enumerable.Range(0, _profileData.SampleCount).Select(i => (double)i).ToArray();

            // Add channel plots based on visibility and data availability
            if (_profileData.IsMultiChannel)
            {
                // Red channel
                if (ShowRedCheckBox.IsChecked == true && _profileData.RedChannel.Count > 0)
                {
                    _redScatter = WpfPlot.Plot.Add.Scatter(xData, _profileData.RedChannel.ToArray());
                    _redScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Red);
                    _redScatter.LineWidth = 1.5f;
                    _redScatter.LegendText = "Red";
                    _redScatter.MarkerSize = 0;
                }

                // Green channel
                if (ShowGreenCheckBox.IsChecked == true && _profileData.GreenChannel.Count > 0)
                {
                    _greenScatter = WpfPlot.Plot.Add.Scatter(xData, _profileData.GreenChannel.ToArray());
                    _greenScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Green);
                    _greenScatter.LineWidth = 1.5f;
                    _greenScatter.LegendText = "Green";
                    _greenScatter.MarkerSize = 0;
                }

                // Blue channel
                if (ShowBlueCheckBox.IsChecked == true && _profileData.BlueChannel.Count > 0)
                {
                    _blueScatter = WpfPlot.Plot.Add.Scatter(xData, _profileData.BlueChannel.ToArray());
                    _blueScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Blue);
                    _blueScatter.LineWidth = 1.5f;
                    _blueScatter.LegendText = "Blue";
                    _blueScatter.MarkerSize = 0;
                }
            }

            // Gray channel (always available)
            if (ShowGrayCheckBox.IsChecked == true && _profileData.GrayChannel.Count > 0)
            {
                _grayScatter = WpfPlot.Plot.Add.Scatter(xData, _profileData.GrayChannel.ToArray());
                _grayScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
                _grayScatter.LineWidth = 1.5f;
                _grayScatter.LegendText = _profileData.IsMultiChannel ? "Gray" : "Profile";
                _grayScatter.MarkerSize = 0;
            }

            // Show legend if multi-channel
            if (_profileData.IsMultiChannel)
            {
                WpfPlot.Plot.ShowLegend(Alignment.UpperRight);
            }

            // Auto-scale axes
            WpfPlot.Plot.Axes.AutoScale();
            // Refresh the plot
            WpfPlot.Refresh();
        }

        private void ChannelCheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdatePlot();
        }

        private void SaveChartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog dlg = new()
                {
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|BMP Image|*.bmp",
                    FileName = $"ProfileChart_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                    DefaultExt = ".png"
                };

                if (dlg.ShowDialog() == true)
                {
                    // Save using ScottPlot's built-in save functionality
                    WpfPlot.Plot.Save(dlg.FileName, (int)WpfPlot.ActualWidth, (int)WpfPlot.ActualHeight);
                    MessageBox.Show("Chart saved successfully!", "Save Chart", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save chart: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog dlg = new()
                {
                    Filter = "CSV File|*.csv|Text File|*.txt",
                    FileName = $"ProfileData_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    DefaultExt = ".csv"
                };

                if (dlg.ShowDialog() == true)
                {
                    StringBuilder csv = new StringBuilder();

                    // Add header
                    if (_profileData.IsMultiChannel)
                    {
                        csv.AppendLine("Index,Red,Green,Blue,Gray");

                        // Add data
                        for (int i = 0; i < _profileData.SampleCount; i++)
                        {
                            csv.AppendLine($"{i},{_profileData.RedChannel[i]},{_profileData.GreenChannel[i]},{_profileData.BlueChannel[i]},{_profileData.GrayChannel[i]}");
                        }
                    }
                    else
                    {
                        csv.AppendLine("Index,Value");

                        // Add data
                        for (int i = 0; i < _profileData.SampleCount; i++)
                        {
                            csv.AppendLine($"{i},{_profileData.GrayChannel[i]}");
                        }
                    }

                    File.WriteAllText(dlg.FileName, csv.ToString());
                    MessageBox.Show("Data saved successfully!", "Save Data", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
