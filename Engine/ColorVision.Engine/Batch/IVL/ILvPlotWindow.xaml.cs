using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Batch.IVL
{
    /// <summary>
    /// I-Lv Curve Plot Window
    /// Plots Current (I) vs Luminance (Lv) curves grouped by POI name
    /// </summary>
    public partial class ILvPlotWindow : Window
    {
        private Dictionary<string, List<ILvDataPoint>> _groupedData;
        private Dictionary<string, Scatter> _scatterPlots;
        private List<string> _seriesNames;

        public ILvPlotWindow(List<SMUResultModel> smuResults, List<PoiResultCIExyuvData> poixyuvDatas)
        {
            InitializeComponent();
            _groupedData = new Dictionary<string, List<ILvDataPoint>>();
            _scatterPlots = new Dictionary<string, Scatter>();
            _seriesNames = new List<string>();
            
            LoadData(smuResults, poixyuvDatas);
            InitializePlot();
        }

        private void LoadData(List<SMUResultModel> smuResults, List<PoiResultCIExyuvData> poixyuvDatas)
        {
            // Group data by POI name
            int smuCount = smuResults.Count;
            int poiCount = poixyuvDatas.Count;
            
            if (smuCount == 0 || poiCount == 0)
            {
                MessageBox.Show("No data available to plot.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Calculate how many POIs per SMU measurement
            int poisPerMeasurement = poiCount / smuCount;
            if (poisPerMeasurement == 0)
                poisPerMeasurement = 1;

            for (int i = 0; i < poiCount; i++)
            {
                int smuIndex = i / poisPerMeasurement;
                if (smuIndex >= smuCount)
                    smuIndex = smuCount - 1;

                var smu = smuResults[smuIndex];
                var poi = poixyuvDatas[i];
                
                string poiName = poi.POIPointResultModel?.PoiName ?? $"POI_{i}";
                
                if (!_groupedData.ContainsKey(poiName))
                {
                    _groupedData[poiName] = new List<ILvDataPoint>();
                    _seriesNames.Add(poiName);
                }

                // Add data point: Current (IResult) vs Luminance (Y)
                if (smu.IResult.HasValue && poi.Y > 0)
                {
                    _groupedData[poiName].Add(new ILvDataPoint
                    {
                        Current = smu.IResult.Value,
                        Luminance = poi.Y,
                        Voltage = smu.VResult ?? 0
                    });
                }
            }

            // Sort data points within each series by current
            foreach (var series in _groupedData.Values)
            {
                series.Sort((a, b) => a.Current.CompareTo(b.Current));
            }

            // Populate list box
            foreach (var name in _seriesNames)
            {
                PoiSeriesList.Items.Add(name);
            }

            // Select all by default
            PoiSeriesList.SelectAll();
        }

        private void InitializePlot()
        {
            WpfPlot.Plot.Clear();
            
            // Set labels
            WpfPlot.Plot.Title("I-Lv Characteristics Curve");
            WpfPlot.Plot.XLabel("Current (mA)");
            WpfPlot.Plot.YLabel("Luminance (cd/m²)");
            
            // Set font for labels
            string title = "I-Lv Curve";
            WpfPlot.Plot.Axes.Title.Label.FontName = Fonts.Detect(title);
            WpfPlot.Plot.Axes.Left.Label.FontName = Fonts.Detect(title);
            WpfPlot.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(title);

            PlotAllSeries();
            WpfPlot.Refresh();
            
            UpdateLegendInfo();
        }

        private void PlotAllSeries()
        {
            // Clear existing plots
            foreach (var plot in _scatterPlots.Values)
            {
                WpfPlot.Plot.Remove(plot);
            }
            _scatterPlots.Clear();

            // Color palette for different series
            var colors = new[]
            {
                System.Drawing.Color.Red,
                System.Drawing.Color.Blue,
                System.Drawing.Color.Green,
                System.Drawing.Color.Orange,
                System.Drawing.Color.Purple,
                System.Drawing.Color.Brown,
                System.Drawing.Color.Pink,
                System.Drawing.Color.Gray,
                System.Drawing.Color.Cyan,
                System.Drawing.Color.Magenta
            };

            int colorIndex = 0;
            foreach (var seriesName in _seriesNames)
            {
                if (!_groupedData.ContainsKey(seriesName) || _groupedData[seriesName].Count == 0)
                    continue;

                var dataPoints = _groupedData[seriesName];
                double[] x = dataPoints.Select(p => (double)p.Current).ToArray();
                double[] y = dataPoints.Select(p => p.Luminance).ToArray();

                var scatter = new Scatter(new ScatterSourceDoubleArray(x, y))
                {
                    Color = Color.FromColor(colors[colorIndex % colors.Length]),
                    LineWidth = 2,
                    MarkerSize = 5,
                    MarkerShape = MarkerShape.FilledCircle,
                    Label = seriesName
                };

                _scatterPlots[seriesName] = scatter;
                
                // Only add if selected
                if (PoiSeriesList.SelectedItems.Contains(seriesName))
                {
                    WpfPlot.Plot.PlottableList.Add(scatter);
                }

                colorIndex++;
            }

            // Show legend
            WpfPlot.Plot.ShowLegend(Alignment.UpperLeft);
        }

        private void PoiSeriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePlotVisibility();
        }

        private void UpdatePlotVisibility()
        {
            // Remove all plots
            foreach (var plot in _scatterPlots.Values)
            {
                WpfPlot.Plot.Remove(plot);
            }

            // Add only selected series
            foreach (var item in PoiSeriesList.SelectedItems)
            {
                string seriesName = item.ToString();
                if (_scatterPlots.ContainsKey(seriesName))
                {
                    WpfPlot.Plot.PlottableList.Add(_scatterPlots[seriesName]);
                }
            }

            WpfPlot.Refresh();
            UpdateLegendInfo();
        }

        private void UpdateLegendInfo()
        {
            if (PoiSeriesList.SelectedItems.Count == 0)
            {
                TxtLegendInfo.Text = "No series selected";
                return;
            }

            var info = new System.Text.StringBuilder();
            info.AppendLine($"Selected: {PoiSeriesList.SelectedItems.Count} series");
            info.AppendLine();

            foreach (var item in PoiSeriesList.SelectedItems)
            {
                string seriesName = item.ToString();
                if (_groupedData.ContainsKey(seriesName))
                {
                    var data = _groupedData[seriesName];
                    if (data.Count > 0)
                    {
                        info.AppendLine($"{seriesName}:");
                        info.AppendLine($"  Points: {data.Count}");
                        info.AppendLine($"  I: {data.Min(p => p.Current):F2} - {data.Max(p => p.Current):F2} mA");
                        info.AppendLine($"  Lv: {data.Min(p => p.Luminance):F2} - {data.Max(p => p.Luminance):F2} cd/m²");
                        info.AppendLine();
                    }
                }
            }

            TxtLegendInfo.Text = info.ToString();
        }

        private void ChkShowAll_Checked(object sender, RoutedEventArgs e)
        {
            PoiSeriesList.SelectAll();
        }

        private void ChkShowAll_Unchecked(object sender, RoutedEventArgs e)
        {
            PoiSeriesList.UnselectAll();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG Files|*.png|JPEG Files|*.jpg|BMP Files|*.bmp",
                Title = "Save I-Lv Plot Image",
                FileName = $"ILv_Curve_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                WpfPlot.Plot.Save(filePath, 1200, 800);
                MessageBox.Show($"Plot saved to:\n{filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            WpfPlot.Plot.Axes.AutoScale();
            WpfPlot.Refresh();
        }

        private class ILvDataPoint
        {
            public float Current { get; set; }
            public double Luminance { get; set; }
            public float Voltage { get; set; }
        }
    }
}
