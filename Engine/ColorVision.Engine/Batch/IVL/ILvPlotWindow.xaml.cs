using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private static readonly ILog log = LogManager.GetLogger(nameof(ILvPlotWindow));
        
        private Dictionary<string, List<ILvDataPoint>> _groupedData;
        private Dictionary<string, Scatter> _scatterPlots;
        private List<string> _seriesNames;

        public ILvPlotWindow(List<SMUResultModel> smuResults, List<PoiResultCIExyuvData> poixyuvDatas)
            : this(smuResults, poixyuvDatas, null)
        {
        }

        public ILvPlotWindow(List<SMUResultModel> smuResults, List<PoiResultCIExyuvData> poixyuvDatas, ObservableCollection<ViewResultSpectrum> spectrumResults)
        {
            InitializeComponent();
            _groupedData = new Dictionary<string, List<ILvDataPoint>>();
            _scatterPlots = new Dictionary<string, Scatter>();
            _seriesNames = new List<string>();
            
            LoadData(smuResults, poixyuvDatas, spectrumResults);
            InitializePlot();
        }

        private void LoadData(List<SMUResultModel> smuResults, List<PoiResultCIExyuvData> poixyuvDatas, ObservableCollection<ViewResultSpectrum> spectrumResults)
        {
            // Group data by POI name
            int smuCount = smuResults.Count;
            int poiCount = poixyuvDatas.Count;
            
            // Check if we have at least some data to plot (POI or spectrum)
            bool hasPoiData = smuCount > 0 && poiCount > 0;
            bool hasSpectrumData = spectrumResults != null && spectrumResults.Count > 0 && smuCount > 0;

            log.Info($"Loading data for I-Lv plot: SMU count = {smuCount}, POI count = {poiCount}, Spectrum count = {(spectrumResults?.Count ?? 0)}");
            if (!(hasPoiData || hasSpectrumData))
            {
                MessageBox.Show("No data available to plot.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Process POI data if available
            if (hasPoiData)
            {
                // Calculate how many POIs per SMU measurement (same logic as IVLProcess)
                int poisPerMeasurement = poiCount / smuCount;
                if (poisPerMeasurement == 0)
                    poisPerMeasurement = 1;

                // Match the data pairing logic from IVLProcess.cs
                for (int i = 0; i < poiCount; i++)
                {
                // Calculate SMU index: z = i / cout (from IVLProcess.cs)
                int smuIndex = i / poisPerMeasurement;
                if (smuIndex >= smuCount)
                    continue; // Skip if no corresponding SMU data

                var smu = smuResults[smuIndex];
                var poi = poixyuvDatas[i];
                
                // Use POI name to group data points
                // Check if POIPointResultModel is null to detect data integrity issues
                string poiName;
                if (poi.POIPointResultModel == null)
                {
                    log.Warn($"POIPointResultModel is null for data point at index {i}. Using default name.");
                    poiName = $"POI_{i}";
                }
                else
                {
                    poiName = poi.POIPointResultModel.PoiName ?? $"POI_{i}";
                }
                
                if (!_groupedData.ContainsKey(poiName))
                {
                    _groupedData[poiName] = new List<ILvDataPoint>();
                    _seriesNames.Add(poiName);
                }

                // Add data point: Current (I) vs Luminance (Lv)
                // Only add valid data points with non-null current and positive luminance
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
            }

            // Add spectrum data if available
            if (hasSpectrumData)
            {
                string spectrumSeriesName = "光谱仪";
                
                if (!_groupedData.ContainsKey(spectrumSeriesName))
                {
                    _groupedData[spectrumSeriesName] = new List<ILvDataPoint>();
                    _seriesNames.Add(spectrumSeriesName);
                }

                // Match spectrum results with SMU data (similar to IVLProcess logic)
                for (int i = 0; i < spectrumResults.Count; i++)
                {
                    var spectrum = spectrumResults[i];
                    
                    // Try to get the Lv value from the string property
                    if (!string.IsNullOrEmpty(spectrum.Lv) && double.TryParse(spectrum.Lv, out double lvValue))
                    {
                        // Match with SMU data by index
                        if (i < smuResults.Count)
                        {
                            var smu = smuResults[i];
                            if (smu.IResult.HasValue && lvValue > 0)
                            {
                                _groupedData[spectrumSeriesName].Add(new ILvDataPoint
                                {
                                    Current = smu.IResult.Value,
                                    Luminance = lvValue,
                                    Voltage = smu.VResult ?? 0
                                });
                            }
                        }
                    }
                }
            }

            // Remove empty series
            var emptyKeys = _groupedData.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
            foreach (var key in emptyKeys)
            {
                _groupedData.Remove(key);
                _seriesNames.Remove(key);
            }

            // Sort data points within each series by current for proper line plotting
            foreach (var series in _groupedData.Values)
            {
                series.Sort((a, b) => a.Current.CompareTo(b.Current));
            }

            // Populate list box with series names
            foreach (var name in _seriesNames)
            {
                PoiSeriesList.Items.Add(name);
            }

            // Select all series by default for initial display
            if (_seriesNames.Count > 0)
            {
                PoiSeriesList.SelectAll();
            }
        }

        private void InitializePlot()
        {
            WpfPlot.Plot.Clear();
            
            // Check if there's any data to plot
            if (_groupedData.Count == 0)
            {
                WpfPlot.Plot.Title("I-Lv Curve (No Data)");
                WpfPlot.Refresh();
                TxtLegendInfo.Text = "No valid data to display";
                return;
            }
            
            // Set labels with proper formatting
            WpfPlot.Plot.Title("I-Lv Characteristics Curve");
            WpfPlot.Plot.XLabel("Current (mA)");
            WpfPlot.Plot.YLabel("Luminance (cd/m²)");
            
            // Set font for labels to support international characters
            // Use a consistent string for font detection
            string fontSample = "I-Lv Characteristics Curve Current Luminance";
            WpfPlot.Plot.Axes.Title.Label.FontName = Fonts.Detect(fontSample);
            WpfPlot.Plot.Axes.Left.Label.FontName = Fonts.Detect(fontSample);
            WpfPlot.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(fontSample);

            // Enable grid for better readability
            WpfPlot.Plot.Grid.MajorLineColor = Color.FromColor(System.Drawing.Color.LightGray);
            WpfPlot.Plot.Grid.MajorLineWidth = 1;

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

            // Enhanced color palette for better distinction
            var colors = new[]
            {
                System.Drawing.Color.Red,
                System.Drawing.Color.Blue,
                System.Drawing.Color.Green,
                System.Drawing.Color.DarkOrange,
                System.Drawing.Color.Purple,
                System.Drawing.Color.Brown,
                System.Drawing.Color.DeepPink,
                System.Drawing.Color.DarkCyan,
                System.Drawing.Color.Magenta,
                System.Drawing.Color.Teal
            };

            int colorIndex = 0;
            foreach (var seriesName in _seriesNames)
            {
                if (!_groupedData.ContainsKey(seriesName) || _groupedData[seriesName].Count == 0)
                    continue;

                var dataPoints = _groupedData[seriesName];
                double[] x = dataPoints.Select(p => (double)p.Current).ToArray();
                double[] y = dataPoints.Select(p => p.Luminance).ToArray();

                // Create scatter plot with line and markers
                var scatter = new Scatter(new ScatterSourceDoubleArray(x, y))
                {
                    Color = Color.FromColor(colors[colorIndex % colors.Length]),
                    LineWidth = 2,
                    MarkerSize = 6,
                    MarkerShape = MarkerShape.FilledCircle,
                    Label = seriesName,
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
            public double Current { get; set; }
            public double Luminance { get; set; }
            public double Voltage { get; set; }
        }
    }
}
