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
    /// IVL Curve Plot Window
    /// Plots Current (I) or Voltage (V) vs Luminance (Lv) curves grouped by POI name
    /// </summary>
    public partial class ILvPlotWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(ILvPlotWindow));
        
        private Dictionary<string, List<ILvDataPoint>> _groupedData;
        private Dictionary<string, Scatter> _scatterPlots;
        private List<string> _seriesNames;
        private bool _isILvMode = true; // true for I-Lv, false for V-Lv
        private Crosshair _crosshair;

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
            SetupMouseInteraction();
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
            wpfPlot.Plot.Clear();
            
            // Check if there's any data to plot
            if (_groupedData.Count == 0)
            {
                string modeText = _isILvMode ? "I-Lv" : "V-Lv";
                wpfPlot.Plot.Title($"{modeText} Curve (No Data)");
                wpfPlot.Refresh();
                TxtLegendInfo.Text = "No valid data to display";
                return;
            }
            
            // Set labels with proper formatting based on display mode
            string modeLabel = _isILvMode ? "I-Lv" : "V-Lv";
            string xLabel = _isILvMode ? "Current (mA)" : "Voltage (V)";
            
            wpfPlot.Plot.Title($"{modeLabel} Characteristics Curve");
            wpfPlot.Plot.XLabel(xLabel);
            wpfPlot.Plot.YLabel("Luminance (cd/m²)");
            
            // Update title text block
            TxtTitle.Text = $"{modeLabel} Curve Analysis";
            
            // Set font for labels to support international characters
            // Use a consistent string for font detection
            string fontSample = $"{modeLabel} Characteristics Curve {xLabel} Luminance Voltage";
            wpfPlot.Plot.Axes.Title.Label.FontName = Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Left.Label.FontName = Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(fontSample);

            // Enable grid for better readability
            wpfPlot.Plot.Grid.MajorLineColor = Color.FromColor(System.Drawing.Color.LightGray);
            wpfPlot.Plot.Grid.MajorLineWidth = 1;
            PlotAllSeries();
            wpfPlot.Refresh();
            
            UpdateLegendInfo();
        }

        private void PlotAllSeries()
        {
            // Clear existing plots
            foreach (var plot in _scatterPlots.Values)
            {
                wpfPlot.Plot.Remove(plot);
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
                if (!_groupedData.TryGetValue(seriesName, out List<ILvDataPoint>? value) || value.Count == 0)
                    continue;

                var dataPoints = _groupedData[seriesName];
                
                // Select X-axis data based on display mode (Current for I-Lv, Voltage for V-Lv)
                double[] x = _isILvMode 
                    ? dataPoints.Select(p => p.Current).ToArray()
                    : dataPoints.Select(p => p.Voltage).ToArray();
                double[] y = dataPoints.Select(p => p.Luminance).ToArray();

                // Create scatter plot with line and markers
                var scatter = new Scatter(new ScatterSourceDoubleArray(x, y))
                {
                    Color = Color.FromColor(colors[colorIndex % colors.Length]),
                    LineWidth = 2,
                    MarkerSize = 6,
                    MarkerShape = MarkerShape.FilledCircle,
                    LegendText = seriesName,
                    Smooth =true
                };

                _scatterPlots[seriesName] = scatter;
                
                // Only add if selected
                if (PoiSeriesList.SelectedItems.Contains(seriesName))
                {
                    wpfPlot.Plot.PlottableList.Add(scatter);
                }

                colorIndex++;
            }

            // Show legend
            wpfPlot.Plot.ShowLegend(Alignment.UpperLeft);
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
                wpfPlot.Plot.Remove(plot);
            }

            // Add only selected series
            foreach (var item in PoiSeriesList.SelectedItems)
            {
                string seriesName = item.ToString();
                if (_scatterPlots.ContainsKey(seriesName))
                {
                    wpfPlot.Plot.PlottableList.Add(_scatterPlots[seriesName]);
                }
            }

            wpfPlot.Refresh();
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

            string xAxisLabel = _isILvMode ? "I" : "V";
            string xAxisUnit = _isILvMode ? "mA" : "V";

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
                        
                        if (_isILvMode)
                        {
                            info.AppendLine($"  {xAxisLabel}: {data.Min(p => p.Current):F2} - {data.Max(p => p.Current):F2} {xAxisUnit}");
                        }
                        else
                        {
                            info.AppendLine($"  {xAxisLabel}: {data.Min(p => p.Voltage):F2} - {data.Max(p => p.Voltage):F2} {xAxisUnit}");
                        }
                        
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

        private void DisplayMode_Changed(object sender, RoutedEventArgs e)
        {
            if (RbILv == null) return;
            if (_groupedData == null) return;
            // Update the mode flag
            _isILvMode = RbILv.IsChecked == true;
            
            // Re-sort data based on the new X-axis
            foreach (var series in _groupedData.Values)
            {
                if (_isILvMode)
                {
                    series.Sort((a, b) => a.Current.CompareTo(b.Current));
                }
                else
                {
                    series.Sort((a, b) => a.Voltage.CompareTo(b.Voltage));
                }
            }
            
            // Re-initialize the plot with new mode
            InitializePlot();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string modeText = _isILvMode ? "ILv" : "VLv";
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG Files|*.png|JPEG Files|*.jpg|BMP Files|*.bmp",
                Title = $"Save {modeText} Plot Image",
                FileName = $"{modeText}_Curve_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                wpfPlot.Plot.Save(filePath, 1200, 800);
                MessageBox.Show($"Plot saved to:\n{filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            wpfPlot.Plot.Axes.AutoScale();
            wpfPlot.Refresh();
        }

        private void SetupMouseInteraction()
        {
            // Add crosshair for showing nearest data point
            _crosshair = wpfPlot.Plot.Add.Crosshair(0, 0);
            _crosshair.IsVisible = false;
            _crosshair.LineWidth = 1;
            _crosshair.LineColor = Color.FromColor(System.Drawing.Color.Gray);
            
            // Subscribe to mouse move events
            wpfPlot.MouseMove += WpfPlot_MouseMove;
            wpfPlot.MouseLeave += WpfPlot_MouseLeave;
        }

        private void WpfPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Get mouse position in plot coordinates
            var position = e.GetPosition(wpfPlot);
            var pixel = new Pixel((float)position.X, (float)position.Y);
            var coords = wpfPlot.Plot.GetCoordinates(pixel);

            // Find the nearest data point
            double minDistance = double.MaxValue;
            ILvDataPoint? nearestPoint = null;
            string nearestSeriesName = string.Empty;

            foreach (var seriesName in _seriesNames)
            {
                // Only check visible series
                if (!PoiSeriesList.SelectedItems.Contains(seriesName))
                    continue;

                if (!_groupedData.ContainsKey(seriesName))
                    continue;

                foreach (var point in _groupedData[seriesName])
                {
                    double x = _isILvMode ? point.Current : point.Voltage;
                    double y = point.Luminance;

                    // Calculate distance in plot coordinates
                    double dx = x - coords.X;
                    double dy = y - coords.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestPoint = point;
                        nearestSeriesName = seriesName;
                    }
                }
            }

            // Show crosshair if a point is close enough
            if (nearestPoint != null && minDistance < GetDistanceThreshold())
            {
                double x = _isILvMode ? nearestPoint.Current : nearestPoint.Voltage;
                double y = nearestPoint.Luminance;
                
                _crosshair.Position = new Coordinates(x, y);
                _crosshair.IsVisible = true;
                
                wpfPlot.Refresh();
            }
            else
            {
                _crosshair.IsVisible = false;
                wpfPlot.Refresh();
            }
        }

        private void WpfPlot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Hide crosshair when mouse leaves the plot
            _crosshair.IsVisible = false;
            wpfPlot.Refresh();
        }

        private double GetDistanceThreshold()
        {
            // Calculate a reasonable distance threshold based on the current axis ranges
            var xRange = wpfPlot.Plot.Axes.GetLimits().Rect.Width;
            var yRange = wpfPlot.Plot.Axes.GetLimits().Rect.Height;
            
            // Use 5% of the smaller range as threshold
            double threshold = Math.Min(xRange, yRange) * 0.05;
            return threshold;
        }

        private class ILvDataPoint
        {
            public double Current { get; set; }
            public double Luminance { get; set; }
            public double Voltage { get; set; }
        }
    }
}
