using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using log4net;
using Microsoft.Win32;
using NPOI.SS.Formula.Functions;
using NPOI.Util;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Batch.IVL
{
    /// <summary>
    /// Display mode for ILvPlotWindow
    /// </summary>
    public enum ILvDisplayMode
    {
        IL,  // Current (I) vs Luminance (Lv)
        VL,  // Voltage (V) vs Luminance (Lv)
        IV   // Current (I) vs Voltage (V)
    }

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
        private ILvDisplayMode _displayMode = ILvDisplayMode.IL; // Display mode: IL, VL, or IV
        private bool _sortData = false; // true to sort data points, false to preserve original sequence (for round-trip)
        private Crosshair _crosshair;

        ScottPlot.Plottables.Marker MyHighlightMarker;
        ScottPlot.Plottables.Text MyHighlightText;

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

        double dpiRadio = 1;

        private void LoadData(List<SMUResultModel> smuResults, List<PoiResultCIExyuvData> poixyuvDatas, ObservableCollection<ViewResultSpectrum> spectrumResults)
        {

            using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            dpiRadio = graphics.DpiY / 96.0;



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
                string spectrumSeriesName = ColorVision.Engine.Properties.Resources.Spectrometer;

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

            // Note: Data sorting is now controlled by _sortData flag and applied on-demand
            // This allows preserving original measurement sequence for round-trip (hysteresis) visualization

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
                string modeText = _displayMode switch
                {
                    ILvDisplayMode.IL => "I-Lv",
                    ILvDisplayMode.VL => "V-Lv",
                    ILvDisplayMode.IV => "I-V",
                    _ => "I-Lv"
                };
                wpfPlot.Plot.Title($"{modeText} Curve (No Data)");
                wpfPlot.Refresh();
                TxtLegendInfo.Text = "No valid data to display";
                return;
            }


            // Set labels with proper formatting based on display mode
            string modeLabel = _displayMode switch
            {
                ILvDisplayMode.IL => "I-Lv",
                ILvDisplayMode.VL => "V-Lv",
                ILvDisplayMode.IV => "I-V",
                _ => "I-Lv"
            };
            string xLabel = _displayMode switch
            {
                ILvDisplayMode.IL => "Current (mA)",
                ILvDisplayMode.VL => "Voltage (V)",
                ILvDisplayMode.IV => "Current (mA)",
                _ => "Current (mA)"
            };
            string yLabel = _displayMode switch
            {
                ILvDisplayMode.IL => "Luminance (cd/m²)",
                ILvDisplayMode.VL => "Luminance (cd/m²)",
                ILvDisplayMode.IV => "Voltage (V)",
                _ => "Luminance (cd/m²)"
            };

            wpfPlot.Plot.Title($"{modeLabel} Characteristics Curve");
            wpfPlot.Plot.XLabel(xLabel);
            wpfPlot.Plot.YLabel(yLabel);
            wpfPlot.Plot.Legend.FontName = Fonts.Detect("中文");


            // Update title text block
            TxtTitle.Text = $"{modeLabel} Curve Analysis";

            // Set font for labels to support international characters
            // Use a consistent string for font detection
            string fontSample = $"{modeLabel} Characteristics Curve {xLabel} {yLabel}";
            wpfPlot.Plot.Axes.Title.Label.FontName = Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Left.Label.FontName = Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(fontSample);

            // Enable grid for better readability
            wpfPlot.Plot.Grid.MajorLineColor = Color.FromColor(System.Drawing.Color.LightGray);
            wpfPlot.Plot.Grid.MajorLineWidth = 1;
            PlotAllSeries();
            wpfPlot.Plot.Axes.AutoScale();
            wpfPlot.Refresh();

            UpdateLegendInfo();
            UpdateDataTable();
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

                // Apply sorting if enabled (for monotonic curves)
                // Otherwise preserve original sequence (for round-trip/hysteresis visualization)
                List<ILvDataPoint> sortedData;
                if (_sortData)
                {
                    sortedData = new List<ILvDataPoint>(dataPoints);
                    switch (_displayMode)
                    {
                        case ILvDisplayMode.IL:
                        case ILvDisplayMode.IV:
                            sortedData.Sort((a, b) => a.Current.CompareTo(b.Current));
                            break;
                        case ILvDisplayMode.VL:
                            sortedData.Sort((a, b) => a.Voltage.CompareTo(b.Voltage));
                            break;
                    }
                }
                else
                {
                    // Preserve original measurement sequence
                    sortedData = dataPoints;
                }

                // Select X-axis and Y-axis data based on display mode
                double[] x;
                double[] y;
                switch (_displayMode)
                {
                    case ILvDisplayMode.IL:
                        x = sortedData.Select(p => p.Current).ToArray();
                        y = sortedData.Select(p => p.Luminance).ToArray();
                        break;
                    case ILvDisplayMode.VL:
                        x = sortedData.Select(p => p.Voltage).ToArray();
                        y = sortedData.Select(p => p.Luminance).ToArray();
                        break;
                    case ILvDisplayMode.IV:
                        x = sortedData.Select(p => p.Current).ToArray();
                        y = sortedData.Select(p => p.Voltage).ToArray();
                        break;
                    default:
                        x = sortedData.Select(p => p.Current).ToArray();
                        y = sortedData.Select(p => p.Luminance).ToArray();
                        break;
                }

                // Create scatter plot with line and markers
                var scatter = new Scatter(new ScatterSourceDoubleArray(x, y))
                {
                    Color = Color.FromColor(colors[colorIndex % colors.Length]),
                    LineWidth = 2,
                    MarkerSize = 6,
                    MarkerShape = MarkerShape.FilledCircle,
                    LegendText = seriesName,
                    Smooth = false  // Disable smoothing to avoid strange curvature with non-monotonic data
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
            UpdateDataTable();
        }

        private void UpdateDataTable()
        {
            var dataList = new ObservableCollection<DataTableRow>();

            foreach (var item in PoiSeriesList.SelectedItems)
            {
                string seriesName = item.ToString();
                if (_groupedData.ContainsKey(seriesName))
                {
                    var dataPoints = _groupedData[seriesName];
                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        dataList.Add(new DataTableRow
                        {
                            SeriesName = seriesName,
                            Index = i + 1,
                            Current = dataPoints[i].Current,
                            Voltage = dataPoints[i].Voltage,
                            Luminance = dataPoints[i].Luminance
                        });
                    }
                }
            }

            DataGridValues.ItemsSource = dataList;
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

            string xAxisLabel;
            string xAxisUnit;
            string yAxisLabel;
            string yAxisUnit;

            switch (_displayMode)
            {
                case ILvDisplayMode.IL:
                    xAxisLabel = "I";
                    xAxisUnit = "mA";
                    yAxisLabel = "Lv";
                    yAxisUnit = "cd/m²";
                    break;
                case ILvDisplayMode.VL:
                    xAxisLabel = "V";
                    xAxisUnit = "V";
                    yAxisLabel = "Lv";
                    yAxisUnit = "cd/m²";
                    break;
                case ILvDisplayMode.IV:
                    xAxisLabel = "I";
                    xAxisUnit = "mA";
                    yAxisLabel = "V";
                    yAxisUnit = "V";
                    break;
                default:
                    xAxisLabel = "I";
                    xAxisUnit = "mA";
                    yAxisLabel = "Lv";
                    yAxisUnit = "cd/m²";
                    break;
            }

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

                        switch (_displayMode)
                        {
                            case ILvDisplayMode.IL:
                            case ILvDisplayMode.IV:
                                info.AppendLine($"  {xAxisLabel}: {data.Min(p => p.Current):F2} - {data.Max(p => p.Current):F2} {xAxisUnit}");
                                break;
                            case ILvDisplayMode.VL:
                                info.AppendLine($"  {xAxisLabel}: {data.Min(p => p.Voltage):F2} - {data.Max(p => p.Voltage):F2} {xAxisUnit}");
                                break;
                        }

                        switch (_displayMode)
                        {
                            case ILvDisplayMode.IL:
                            case ILvDisplayMode.VL:
                                info.AppendLine($"  {yAxisLabel}: {data.Min(p => p.Luminance):F2} - {data.Max(p => p.Luminance):F2} {yAxisUnit}");
                                break;
                            case ILvDisplayMode.IV:
                                info.AppendLine($"  {yAxisLabel}: {data.Min(p => p.Voltage):F2} - {data.Max(p => p.Voltage):F2} {yAxisUnit}");
                                break;
                        }

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
            if (RbILv.IsChecked == true)
                _displayMode = ILvDisplayMode.IL;
            else if (RbVLv.IsChecked == true)
                _displayMode = ILvDisplayMode.VL;
            else if (RbIV.IsChecked == true)
                _displayMode = ILvDisplayMode.IV;

            // Re-initialize the plot with new mode
            InitializePlot();
        }

        private void SortMode_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkSortData == null) return;
            if (_groupedData == null) return;

            // Update the sort flag
            _sortData = ChkSortData.IsChecked == true;

            // Re-plot with new sort mode
            PlotAllSeries();
            wpfPlot.Refresh();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string modeText = _displayMode switch
            {
                ILvDisplayMode.IL => "ILv",
                ILvDisplayMode.VL => "VLv",
                ILvDisplayMode.IV => "IV",
                _ => "ILv"
            };
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

        private void BtnSaveData_Click(object sender, RoutedEventArgs e)
        {
            string modeText = _displayMode switch
            {
                ILvDisplayMode.IL => "ILv",
                ILvDisplayMode.VL => "VLv",
                ILvDisplayMode.IV => "IV",
                _ => "ILv"
            };
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files|*.csv",
                Title = $"Save {modeText} Data to CSV",
                FileName = $"{modeText}_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var csv = new StringBuilder();

                    // Add header
                    csv.AppendLine("Series,Index,Current (mA),Voltage (V),Luminance (cd/m²)");

                    // Add data from selected series
                    foreach (var item in PoiSeriesList.SelectedItems)
                    {
                        string seriesName = item.ToString();
                        if (_groupedData.ContainsKey(seriesName))
                        {
                            var dataPoints = _groupedData[seriesName];
                            for (int i = 0; i < dataPoints.Count; i++)
                            {
                                csv.AppendLine($"{seriesName},{i + 1},{dataPoints[i].Current:F4},{dataPoints[i].Voltage:F4},{dataPoints[i].Luminance:F2}");
                            }
                        }
                    }

                    File.WriteAllText(saveFileDialog.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Data saved to:\n{saveFileDialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    log.Error("Failed to save CSV file", ex);
                    MessageBox.Show($"Failed to save CSV file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

            MyHighlightMarker = wpfPlot.Plot.Add.Marker(0, 0);
            MyHighlightMarker.Shape = MarkerShape.OpenCircle;
            MyHighlightMarker.Size = 17;
            MyHighlightMarker.LineWidth = 2;
            MyHighlightMarker.Color = Color.FromColor(System.Drawing.Color.Gray);

            // Create a text label to place near the highlighted value
            MyHighlightText = wpfPlot.Plot.Add.Text("", 0, 0);
            MyHighlightText.LabelAlignment = Alignment.LowerLeft;
            MyHighlightText.LabelBold = true;
            MyHighlightText.OffsetX = 7;
            MyHighlightText.OffsetY = -7;
            MyHighlightText.LabelFontColor = Color.FromColor(System.Drawing.Color.Gray);
            // Subscribe to mouse move events
            wpfPlot.MouseMove += WpfPlot_MouseMove;
            wpfPlot.MouseLeave += WpfPlot_MouseLeave;
        }



        private void WpfPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Get mouse position in plot coordinates
            var position = e.GetPosition(wpfPlot);

            position.X = position.X * dpiRadio;
            position.Y = position.Y * dpiRadio;

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
                    double x, y;
                    switch (_displayMode)
                    {
                        case ILvDisplayMode.IL:
                            x = point.Current;
                            y = point.Luminance;
                            break;
                        case ILvDisplayMode.VL:
                            x = point.Voltage;
                            y = point.Luminance;
                            break;
                        case ILvDisplayMode.IV:
                            x = point.Current;
                            y = point.Voltage;
                            break;
                        default:
                            x = point.Current;
                            y = point.Luminance;
                            break;
                    }

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
            if (nearestPoint != null)
            {
                double x, y;
                switch (_displayMode)
                {
                    case ILvDisplayMode.IL:
                        x = nearestPoint.Current;
                        y = nearestPoint.Luminance;
                        break;
                    case ILvDisplayMode.VL:
                        x = nearestPoint.Voltage;
                        y = nearestPoint.Luminance;
                        break;
                    case ILvDisplayMode.IV:
                        x = nearestPoint.Current;
                        y = nearestPoint.Voltage;
                        break;
                    default:
                        x = nearestPoint.Current;
                        y = nearestPoint.Luminance;
                        break;
                }
                var coords1 = new Coordinates(x, y);

                _crosshair.Position = coords1;
                _crosshair.IsVisible = true;

                string xLabel, xUnit, yLabel, yUnit;
                switch (_displayMode)
                {
                    case ILvDisplayMode.IL:
                        xLabel = "I";
                        xUnit = "mA";
                        yLabel = "Lv";
                        yUnit = "cd/m²";
                        break;
                    case ILvDisplayMode.VL:
                        xLabel = "V";
                        xUnit = "V";
                        yLabel = "Lv";
                        yUnit = "cd/m²";
                        break;
                    case ILvDisplayMode.IV:
                        xLabel = "I";
                        xUnit = "mA";
                        yLabel = "V";
                        yUnit = "V";
                        break;
                    default:
                        xLabel = "I";
                        xUnit = "mA";
                        yLabel = "Lv";
                        yUnit = "cd/m²";
                        break;
                }

                MyHighlightMarker.IsVisible = true;
                MyHighlightMarker.Location = coords1;

                MyHighlightText.IsVisible = true;
                MyHighlightText.Location = coords1;
                MyHighlightText.LabelText = $"{xLabel}: {x:F2} {xUnit}\n{yLabel}: {y:F2} {yUnit}";
                MyHighlightText.LabelFontColor = Color.FromColor(System.Drawing.Color.Red);
                //MyHighlightText.LabelBorderColor = Color.FromColor(System.Drawing.Color.Red);

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


        public class ILvDataPoint
        {
            public double Current { get; set; }
            public double Luminance { get; set; }
            public double Voltage { get; set; }
        }

        private class DataTableRow
        {
            public string SeriesName { get; set; }
            public int Index { get; set; }
            public double Current { get; set; }
            public double Voltage { get; set; }
            public double Luminance { get; set; }
        }
    }
}
