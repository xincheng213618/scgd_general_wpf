using ColorVision.Engine.Services.Dao;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Dao
{
    /// <summary>
    /// TemperatureChart.xaml 的交互逻辑
    /// </summary>
    public partial class TemperatureChartWindow : Window
    {
        private const int MaxTickCount = 10;
        private const int DefaultChartWidth = 800;
        private const int DefaultChartHeight = 450;

        public TemperatureChartWindow(List<CameraTempModel> data)
        {
            this.data = data;
            InitializeComponent();
        }
        public List<CameraTempModel> data { get; set; } = new List<CameraTempModel>();
        private Scatter? _temperatureScatter;
        private string _detectedFont = string.Empty;

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (data != null && data.Count > 0)
            {
                // 设置窗口标题
                this.Title = $"温度曲线图 - {data.First().CreateDate?.ToString("yyyy-MM-dd")}";
                TempatureText.Text = $"{data.Last()?.CreateDate}  {data.Last()?.TempValue} °C";
            }
            else
            {
                this.Title = "温度曲线图";
            }

            // Initialize the plot
            InitializePlot();
            UpdatePlot();
        }

        private void InitializePlot()
        {
            // Clear any existing data
            WpfPlot.Plot.Clear();

            // Set Chinese font support (cache for reuse)
            string fontSample = "温度";
            _detectedFont = ScottPlot.Fonts.Detect(fontSample);

            // Configure plot appearance
            WpfPlot.Plot.Title("温度曲线图");
            WpfPlot.Plot.XLabel("时间");
            WpfPlot.Plot.YLabel("温度 (°C)");

            WpfPlot.Plot.Axes.Title.Label.FontName = _detectedFont;
            WpfPlot.Plot.Axes.Left.Label.FontName = _detectedFont;
            WpfPlot.Plot.Axes.Bottom.Label.FontName = _detectedFont;

            // Configure grid
            WpfPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);

            // Configure Y-axis limits
            WpfPlot.Plot.Axes.Left.Min = 10;
            WpfPlot.Plot.Axes.Left.Max = 70;
        }

        private void UpdatePlot()
        {
            if (WpfPlot == null || data == null || data.Count == 0) return;

            // Remove existing scatter plot if any
            if (_temperatureScatter != null)
            {
                WpfPlot.Plot.Remove(_temperatureScatter);
            }

            // Prepare X-axis data (sample indices)
            double[] xData = Enumerable.Range(0, data.Count).Select(i => (double)i).ToArray();

            // Prepare Y-axis data (temperature values)
            double[] yData = data.Select(d => (double)(d.TempValue ?? 0)).ToArray();

            // Add temperature scatter plot
            _temperatureScatter = WpfPlot.Plot.Add.Scatter(xData, yData);
            _temperatureScatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.OrangeRed);
            _temperatureScatter.LineWidth = 1.5f;
            _temperatureScatter.LegendText = "Temperature";
            _temperatureScatter.MarkerSize = 0;

            // Setup custom tick labels for X-axis with time labels
            var timeLabels = data.Select(d => d.CreateDate?.ToString("HH:mm") ?? "").ToArray();
            
            // Create tick positions and labels
            List<Tick> ticks = new List<Tick>();
            int tickInterval = Math.Max(1, data.Count / MaxTickCount);
            for (int i = 0; i < data.Count; i += tickInterval)
            {
                ticks.Add(new Tick(i, timeLabels[i]));
            }
            // Always include the last point
            if (data.Count > 0 && (data.Count - 1) % tickInterval != 0)
            {
                ticks.Add(new Tick(data.Count - 1, timeLabels[data.Count - 1]));
            }
            
            WpfPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());

            // Auto-scale X axis
            WpfPlot.Plot.Axes.AutoScaleX();

            // Refresh the plot
            WpfPlot.Refresh();
        }

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = "Save as CSV",
                FileName = DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv" // 设置默认文件名
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var filePath = saveFileDialog.FileName;
                var csv = new StringBuilder();

                // 添加CSV文件头
                csv.AppendLine("TempValue,PwmValue,CreateDate");

                // 添加数据行
                foreach (var item in data)
                {
                    var line = $"{item.TempValue},{item.PwmValue},{item.CreateDate?.ToString("yyyy-MM-dd HH:mm:ss")},{item.RescourceId}";
                    csv.AppendLine(line);
                }

                // 写入文件
                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
            }
        }

        private void ButtonSaveChart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog dlg = new()
                {
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|BMP Image|*.bmp",
                    FileName = $"TemperatureChart_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                    DefaultExt = ".png"
                };

                if (dlg.ShowDialog() == true)
                {
                    // Use actual dimensions if available, otherwise use defaults
                    int width = WpfPlot.ActualWidth > 0 ? (int)WpfPlot.ActualWidth : DefaultChartWidth;
                    int height = WpfPlot.ActualHeight > 0 ? (int)WpfPlot.ActualHeight : DefaultChartHeight;
                    
                    // Save using ScottPlot's built-in save functionality
                    WpfPlot.Plot.Save(dlg.FileName, width, height);
                    MessageBox.Show("Chart saved successfully!", "Save Chart", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save chart: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
