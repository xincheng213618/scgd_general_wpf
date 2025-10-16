using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ProfileChartWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProfileChartWindow : Window
    {
        private List<double> _profileData;
        private LineSeries<double> _series;

        public ProfileChartWindow(List<double> profileData,string title)
        {
            _profileData = profileData;
            InitializeComponent();
            this.Title = title;
            
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var redValues = new List<double>(_profileData);

            double MaxY = redValues.Max();
            ProfileChart.XAxes = new Axis[]
{
                new Axis
                {
                    MaxLimit = redValues.Count,
                    MinLimit =0,
                    Labels = Enumerable.Range(0, redValues.Count).Select(x => x.ToString()).ToArray() // 确保显示0到255的每个标签
                }
};
            ProfileChart.YAxes = new Axis[]
            {
                new Axis(){
                    IsVisible =true ,
                    MaxLimit = MaxY ,
                    MinLimit =0,
                    Labeler = value => value.ToString("F0")
                }
            };

            ProfileChart.ZoomMode = ZoomAndPanMode.Both | ZoomAndPanMode.Y | ZoomAndPanMode.X;



            var Serieschannel1 = new LineSeries<double>
            {
                Values = redValues,
                Name = "Profile",
                Fill = new SolidColorPaint(new SKColor(255, 0, 0, 60)),
                Stroke = new SolidColorPaint(new SKColor(255, 0, 0)),
                LineSmoothness = 10,
                GeometrySize = 0,
            };

            _series = Serieschannel1;

            var SeriesCollection = new ISeries[] { Serieschannel1};
            ProfileChart.Series = SeriesCollection;
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
                    // Render the chart to a bitmap
                    var bounds = VisualTreeHelper.GetDescendantBounds(ProfileChart);
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        MessageBox.Show("Chart is not visible or has no size.");
                        return;
                    }

                    // Create a RenderTargetBitmap
                    var dpi = 96d;  // Standard DPI; increase for higher resolution
                    var renderTarget = new RenderTargetBitmap(
                        (int)bounds.Width,
                        (int)bounds.Height,
                        dpi,
                        dpi,
                        PixelFormats.Pbgra32);

                    // Render the chart
                    var drawingVisual = new DrawingVisual();
                    using (var context = drawingVisual.RenderOpen())
                    {
                        var visualBrush = new VisualBrush(ProfileChart);
                        context.DrawRectangle(visualBrush, null, new Rect(new Point(), bounds.Size));
                    }
                    renderTarget.Render(drawingVisual);

                    // Save to desktop as PNG
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var fileName = $"CartesianChart_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var fullPath = Path.Combine(desktopPath, fileName);

                    using (var fileStream = new FileStream(fullPath, FileMode.Create))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                        encoder.Save(fileStream);
                    }

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
                    csv.AppendLine("Index,Value");
                    
                    // Add data
                    for (int i = 0; i < _profileData.Count; i++)
                    {
                        csv.AppendLine($"{i},{_profileData[i]}");
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

        private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Render the chart to a bitmap
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(
                    (int)ProfileChart.ActualWidth,
                    (int)ProfileChart.ActualHeight,
                    144,
                    144,
                    PixelFormats.Pbgra32);
                renderTargetBitmap.Render(ProfileChart);


                // Render the chart to a bitmap
                var bounds = VisualTreeHelper.GetDescendantBounds(ProfileChart);
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    MessageBox.Show("Chart is not visible or has no size.");
                    return;
                }

                // Create a RenderTargetBitmap
                var dpi = 96d;  // Standard DPI; increase for higher resolution
                var renderTarget = new RenderTargetBitmap(
                    (int)bounds.Width,
                    (int)bounds.Height,
                    dpi,
                    dpi,
                    PixelFormats.Pbgra32);

                // Render the chart
                var drawingVisual = new DrawingVisual();
                using (var context = drawingVisual.RenderOpen())
                {
                    var visualBrush = new VisualBrush(ProfileChart);
                    context.DrawRectangle(visualBrush, null, new Rect(new Point(), bounds.Size));
                }
                renderTarget.Render(drawingVisual);

                // Copy to clipboard
                Clipboard.SetImage(renderTarget);
                MessageBox.Show("Chart copied to clipboard!", "Copy to Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy chart: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowPointsCheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (_series != null)
            {
                _series.GeometrySize = ShowPointsCheckBox.IsChecked == true ? 4 : 0;
            }
        }

        private void SmoothLineCheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (_series != null)
            {
                _series.LineSmoothness = SmoothLineCheckBox.IsChecked == true ? 10 : 0;
            }
        }

    }
}
