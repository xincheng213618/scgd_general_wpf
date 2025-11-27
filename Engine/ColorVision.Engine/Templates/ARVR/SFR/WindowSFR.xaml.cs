using ColorVision.Engine.Templates.SFR;
using Microsoft.Win32;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.Engine.Templates.ARVR.SFR
{
    /// <summary>
    /// WindowSFR.xaml 的交互逻辑
    /// </summary>
    public partial class WindowSFR : Window
    {
        private const int DefaultChartWidth = 800;
        private const int DefaultChartHeight = 450;
        private const int DefaultDataPoints = 48;
        private const double Epsilon = 1e-9;
        
        private static readonly string DetectedFont = ScottPlot.Fonts.Detect("频率");

        public List<AlgResultSFRModel> AlgResultSFRModels { get; set; }
        
        private double[] _frequencies;
        private double[] _sfrValues;
        private Scatter _scatter;

        public WindowSFR(List<AlgResultSFRModel> algResultSFRModels)
        {
            AlgResultSFRModels = algResultSFRModels;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            InitializePlot();
            Render(0);
        }

        private void InitializePlot()
        {
            // Clear any existing data
            WpfPlot.Plot.Clear();
            ConfigurePlotAppearance();
        }

        private void ConfigurePlotAppearance()
        {
            // Configure plot appearance
            WpfPlot.Plot.Title("SFR (MTF vs Frequency)");
            WpfPlot.Plot.XLabel("Pdfrequency");
            WpfPlot.Plot.YLabel("PdomainSamplingData");

            WpfPlot.Plot.Axes.Title.Label.FontName = DetectedFont;
            WpfPlot.Plot.Axes.Left.Label.FontName = DetectedFont;
            WpfPlot.Plot.Axes.Bottom.Label.FontName = DetectedFont;

            // Configure grid
            WpfPlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
        }

        public void Render(int index)
        {
            if (AlgResultSFRModels == null || index >= AlgResultSFRModels.Count) return;

            var pdfrequencys = JsonConvert.DeserializeObject<float[]>(AlgResultSFRModels[index].Pdfrequency);
            var pdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(AlgResultSFRModels[index].PdomainSamplingData);

            if (pdfrequencys == null || pdomainSamplingDatas == null) return;

            // Limit data points
            int n = Math.Min(DefaultDataPoints, Math.Min(pdfrequencys.Length, pdomainSamplingDatas.Length));
            
            _frequencies = pdfrequencys.Take(n).Select(f => (double)f).ToArray();
            _sfrValues = pdomainSamplingDatas.Take(n).Select(s => (double)s).ToArray();

            // Clear existing plots
            WpfPlot.Plot.Clear();

            // Re-apply configuration after clear
            ConfigurePlotAppearance();

            // Add scatter plot
            _scatter = WpfPlot.Plot.Add.Scatter(_frequencies, _sfrValues);
            _scatter.Color = ScottPlot.Color.FromColor(System.Drawing.Color.FromArgb(33, 150, 243));
            _scatter.LineWidth = 4f;
            _scatter.MarkerSize = 0;
            _scatter.LegendText = "MTF";

            // Auto-scale axes
            WpfPlot.Plot.Axes.AutoScale();

            // Refresh the plot
            WpfPlot.Refresh();
        }

        /// <summary>
        /// Find frequency at a given MTF threshold.
        /// Implementation based on find_freq_at_threshold in slanted.cpp
        /// </summary>
        private double FindFreqAtThreshold(double[] freqAxis, double[] sfrData, double threshold)
        {
            if (freqAxis == null || sfrData == null || freqAxis.Length != sfrData.Length || sfrData.Length == 0)
                return 0.0;

            // Find the first pair of points that bracket the threshold
            // We need to find where y1 >= threshold && y2 < threshold
            for (int i = 0; i < sfrData.Length - 1; i++)
            {
                double y1 = sfrData[i];
                double y2 = sfrData[i + 1];

                if (y1 >= threshold && y2 < threshold)
                {
                    // Get the corresponding bracketing values for x (frequency)
                    double x1 = freqAxis[i];
                    double x2 = freqAxis[i + 1];

                    // Perform linear interpolation
                    if (Math.Abs(y2 - y1) < Epsilon)
                    {
                        return x1;
                    }
                    return x1 + (threshold - y1) * (x2 - x1) / (y2 - y1);
                }
            }

            return 0.0;
        }

        private void BtnFreqAtMtf_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtMtf.Text, out var mtf))
            {
                Resultextbox.Text = "MTF输入错误";
                return;
            }

            double freq = FindFreqAtThreshold(_frequencies, _sfrValues, mtf);
            if (freq > 0)
                Resultextbox.Text = $"Freq(MTF={mtf:F4}) = {freq:F5}";
            else
                Resultextbox.Text = "未找到对应频率";
        }

        private void BtnSaveChart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog dlg = new()
                {
                    Filter = "PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|BMP Image|*.bmp",
                    FileName = $"SFR_Chart_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                    DefaultExt = ".png"
                };

                if (dlg.ShowDialog() == true)
                {
                    int width = WpfPlot.ActualWidth > 0 ? (int)WpfPlot.ActualWidth : DefaultChartWidth;
                    int height = WpfPlot.ActualHeight > 0 ? (int)WpfPlot.ActualHeight : DefaultChartHeight;
                    
                    WpfPlot.Plot.Save(dlg.FileName, width, height);
                    MessageBox.Show("图表保存成功!", "保存图表", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog dlg = new()
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = $"SFR_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    DefaultExt = ".csv"
                };

                if (dlg.ShowDialog() == true)
                {
                    var csv = new StringBuilder();
                    csv.AppendLine("# SFR Data Export");
                    csv.AppendLine();
                    csv.AppendLine("Frequency,MTF");

                    if (_frequencies != null && _sfrValues != null)
                    {
                        int n = Math.Min(_frequencies.Length, _sfrValues.Length);
                        for (int i = 0; i < n; i++)
                        {
                            csv.AppendLine($"{_frequencies[i]:F6},{_sfrValues[i]:F6}");
                        }
                    }

                    File.WriteAllText(dlg.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show($"数据已成功导出到:\n{dlg.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }  
}
