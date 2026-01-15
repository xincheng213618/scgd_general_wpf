using ColorVision.Engine.Services.Devices.SMU.Views;
using log4net;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Batch.IVL
{
    /// <summary>
    /// IV Curve Plot Window
    /// Plots Voltage (V) vs Current (I) curves for SMU scan data
    /// </summary>
    public partial class IVPlotWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(IVPlotWindow));

        private Dictionary<string, List<IVDataPoint>> _groupedData;
        private Dictionary<string, Scatter> _scatterPlots;
        private List<string> _seriesNames;
        private bool _isVIMode = true; // true for V-I (X=Voltage, Y=Current), false for I-V (X=Current, Y=Voltage)
        private bool _sortData = false; // true to sort data points, false to preserve original sequence (for round-trip)
        private Crosshair _crosshair;
        private Marker _highlightMarker;
        private Text _highlightText;
        private double _dpiRatio = 1;

        public IVPlotWindow(ObservableCollection<ViewResultSMU> scanResults)
        {
            InitializeComponent();
            _groupedData = new Dictionary<string, List<IVDataPoint>>();
            _scatterPlots = new Dictionary<string, Scatter>();
            _seriesNames = new List<string>();

            LoadData(scanResults);
            InitializePlot();
            SetupMouseInteraction();
        }

        private void LoadData(ObservableCollection<ViewResultSMU> scanResults)
        {
            using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            _dpiRatio = graphics.DpiY / 96.0;

            log.Info($"Loading data for IV plot: Scan count = {scanResults.Count}");

            if (scanResults.Count == 0)
            {
                MessageBox.Show("No scan data available to plot.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if all results are single-point data (GetData type from SMUResultModel)
            // If so, combine them into one VI curve line (like IVL does)
            // Note: scanResults.Count == 0 case is already handled above, so All() won't run on empty collection
            bool allSinglePoint = scanResults.All(r => r.Type == "GetData" && r.SMUDatas.Count == 1);

            if (allSinglePoint)
            {
                // Combine all single-point data into one series (VI Curve)
                const string seriesName = "VI Curve";
                _groupedData[seriesName] = new List<IVDataPoint>();
                _seriesNames.Add(seriesName);

                foreach (var result in scanResults)
                {
                    if (result.SMUDatas.Count > 0)
                    {
                        var smuData = result.SMUDatas[0];
                        _groupedData[seriesName].Add(new IVDataPoint
                        {
                            Voltage = smuData.Voltage,
                            Current = smuData.Current
                        });
                    }
                }
            }
            else
            {
                // Original behavior: each scan result is a separate series (for Scan type data)
                int scanIndex = 1;
                foreach (var result in scanResults)
                {
                    string seriesName = $"Scan {scanIndex}";
                    if (result.Id > 0)
                    {
                        seriesName = $"Scan {result.Id}";
                    }
                    else if (result.CreateTime.HasValue)
                    {
                        seriesName = $"Scan {result.CreateTime.Value:HH:mm:ss}";
                    }

                    if (!_groupedData.ContainsKey(seriesName))
                    {
                        _groupedData[seriesName] = new List<IVDataPoint>();
                        _seriesNames.Add(seriesName);
                    }

                    foreach (var smuData in result.SMUDatas)
                    {
                        _groupedData[seriesName].Add(new IVDataPoint
                        {
                            Voltage = smuData.Voltage,
                            Current = smuData.Current
                        });
                    }

                    scanIndex++;
                }
            }

            // Remove empty series
            var emptyKeys = _groupedData.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
            foreach (var key in emptyKeys)
            {
                _groupedData.Remove(key);
                _seriesNames.Remove(key);
            }

            // Populate list box with series names
            foreach (var name in _seriesNames)
            {
                ScanSeriesList.Items.Add(name);
            }

            // Select all series by default
            if (_seriesNames.Count > 0)
            {
                ScanSeriesList.SelectAll();
            }
        }

        private void InitializePlot()
        {
            wpfPlot.Plot.Clear();

            if (_groupedData.Count == 0)
            {
                string modeText = _isVIMode ? "V-I" : "I-V";
                wpfPlot.Plot.Title($"{modeText} Curve (No Data)");
                wpfPlot.Refresh();
                TxtLegendInfo.Text = "No valid data to display";
                return;
            }

            // Set labels based on display mode
            string modeLabel = _isVIMode ? "V-I" : "I-V";
            string xLabel = _isVIMode ? "Voltage (V)" : "Current (A)";
            string yLabel = _isVIMode ? "Current (A)" : "Voltage (V)";

            wpfPlot.Plot.Title($"{modeLabel} Characteristics Curve");
            wpfPlot.Plot.XLabel(xLabel);
            wpfPlot.Plot.YLabel(yLabel);
            wpfPlot.Plot.Legend.FontName = Fonts.Detect("中文");

            // Update title text block
            TxtTitle.Text = $"{modeLabel} Curve Analysis";

            // Set font for labels
            string fontSample = $"{modeLabel} Characteristics Curve {xLabel} {yLabel}";
            wpfPlot.Plot.Axes.Title.Label.FontName = Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Left.Label.FontName = Fonts.Detect(fontSample);
            wpfPlot.Plot.Axes.Bottom.Label.FontName = Fonts.Detect(fontSample);

            // Enable grid
            wpfPlot.Plot.Grid.MajorLineColor = Color.FromColor(System.Drawing.Color.LightGray);
            wpfPlot.Plot.Grid.MajorLineWidth = 1;

            PlotAllSeries();
            wpfPlot.Refresh();

            UpdateLegendInfo();
            UpdateDataTable();
            wpfPlot.Plot.Axes.AutoScale();
        }

        private void PlotAllSeries()
        {
            // Clear existing plots
            foreach (var plot in _scatterPlots.Values)
            {
                wpfPlot.Plot.Remove(plot);
            }
            _scatterPlots.Clear();

            // Color palette
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
                if (!_groupedData.TryGetValue(seriesName, out List<IVDataPoint>? dataPoints) || dataPoints.Count == 0)
                    continue;

                // Apply sorting if enabled (for monotonic curves)
                // Otherwise preserve original sequence (for round-trip/hysteresis visualization)
                List<IVDataPoint> sortedData;
                if (_sortData)
                {
                    sortedData = new List<IVDataPoint>(dataPoints);
                    if (_isVIMode)
                    {
                        sortedData.Sort((a, b) => a.Voltage.CompareTo(b.Voltage));
                    }
                    else
                    {
                        sortedData.Sort((a, b) => a.Current.CompareTo(b.Current));
                    }
                }
                else
                {
                    // Preserve original measurement sequence
                    sortedData = dataPoints;
                }

                // Select X and Y data based on display mode
                double[] x = _isVIMode
                    ? sortedData.Select(p => p.Voltage).ToArray()
                    : sortedData.Select(p => p.Current).ToArray();
                double[] y = _isVIMode
                    ? sortedData.Select(p => p.Current).ToArray()
                    : sortedData.Select(p => p.Voltage).ToArray();

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
                if (ScanSeriesList.SelectedItems.Contains(seriesName))
                {
                    wpfPlot.Plot.PlottableList.Add(scatter);
                }

                colorIndex++;
            }

            // Show legend
            wpfPlot.Plot.ShowLegend(Alignment.UpperLeft);
        }

        private void ScanSeriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
            foreach (var item in ScanSeriesList.SelectedItems)
            {
                string seriesName = item.ToString() ?? "";
                if (_scatterPlots.ContainsKey(seriesName))
                {
                    wpfPlot.Plot.PlottableList.Add(_scatterPlots[seriesName]);
                }
            }
            wpfPlot.Refresh();
            UpdateLegendInfo();
            UpdateDataTable();
            wpfPlot.Plot.Axes.AutoScale();
        }

        private void UpdateDataTable()
        {
            var dataList = new ObservableCollection<IVDataTableRow>();

            foreach (var item in ScanSeriesList.SelectedItems)
            {
                string seriesName = item.ToString() ?? "";
                if (_groupedData.ContainsKey(seriesName))
                {
                    var dataPoints = _groupedData[seriesName];
                    for (int i = 0; i < dataPoints.Count; i++)
                    {
                        dataList.Add(new IVDataTableRow
                        {
                            SeriesName = seriesName,
                            Index = i + 1,
                            Voltage = dataPoints[i].Voltage,
                            Current = dataPoints[i].Current
                        });
                    }
                }
            }

            DataGridValues.ItemsSource = dataList;
        }

        private void UpdateLegendInfo()
        {
            if (ScanSeriesList.SelectedItems.Count == 0)
            {
                TxtLegendInfo.Text = "No series selected";
                return;
            }

            var info = new StringBuilder();
            info.AppendLine($"Selected: {ScanSeriesList.SelectedItems.Count} series");
            info.AppendLine();

            foreach (var item in ScanSeriesList.SelectedItems)
            {
                string seriesName = item.ToString() ?? "";
                if (_groupedData.ContainsKey(seriesName))
                {
                    var data = _groupedData[seriesName];
                    if (data.Count > 0)
                    {
                        info.AppendLine($"{seriesName}:");
                        info.AppendLine($"  Points: {data.Count}");
                        info.AppendLine($"  V: {data.Min(p => p.Voltage):F4} - {data.Max(p => p.Voltage):F4} V");
                        info.AppendLine($"  I: {data.Min(p => p.Current):F6} - {data.Max(p => p.Current):F6} A");
                        info.AppendLine();
                    }
                }
            }

            TxtLegendInfo.Text = info.ToString();
        }

        private void ChkShowAll_Checked(object sender, RoutedEventArgs e)
        {
            ScanSeriesList.SelectAll();
        }

        private void ChkShowAll_Unchecked(object sender, RoutedEventArgs e)
        {
            ScanSeriesList.UnselectAll();
        }

        private void DisplayMode_Changed(object sender, RoutedEventArgs e)
        {
            if (RbVI == null) return;
            if (_groupedData == null) return;

            _isVIMode = RbVI.IsChecked == true;
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
            string modeText = _isVIMode ? "VI" : "IV";
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
            string modeText = _isVIMode ? "VI" : "IV";
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
                    csv.AppendLine("Series,Index,Voltage (V),Current (A)");

                    // Add data from selected series
                    foreach (var item in ScanSeriesList.SelectedItems)
                    {
                        string seriesName = item.ToString() ?? "";
                        if (_groupedData.ContainsKey(seriesName))
                        {
                            var dataPoints = _groupedData[seriesName];
                            for (int i = 0; i < dataPoints.Count; i++)
                            {
                                csv.AppendLine($"{seriesName},{i + 1},{dataPoints[i].Voltage:F6},{dataPoints[i].Current:F6}");
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

            _highlightMarker = wpfPlot.Plot.Add.Marker(0, 0);
            _highlightMarker.Shape = MarkerShape.OpenCircle;
            _highlightMarker.Size = 17;
            _highlightMarker.LineWidth = 2;
            _highlightMarker.Color = Color.FromColor(System.Drawing.Color.Gray);

            _highlightText = wpfPlot.Plot.Add.Text("", 0, 0);
            _highlightText.LabelAlignment = Alignment.LowerLeft;
            _highlightText.LabelBold = true;
            _highlightText.OffsetX = 7;
            _highlightText.OffsetY = -7;
            _highlightText.LabelFontColor = Color.FromColor(System.Drawing.Color.Gray);

            wpfPlot.MouseMove += WpfPlot_MouseMove;
            wpfPlot.MouseLeave += WpfPlot_MouseLeave;
        }

        private void WpfPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var position = e.GetPosition(wpfPlot);
            var scaledPosition = new System.Windows.Point(position.X * _dpiRatio, position.Y * _dpiRatio);

            var pixel = new Pixel((float)scaledPosition.X, (float)scaledPosition.Y);
            var coords = wpfPlot.Plot.GetCoordinates(pixel);

            // Find the nearest data point
            double minDistance = double.MaxValue;
            IVDataPoint? nearestPoint = null;

            foreach (var seriesName in _seriesNames)
            {
                // Only check visible series
                if (!ScanSeriesList.SelectedItems.Contains(seriesName))
                    continue;

                if (!_groupedData.TryGetValue(seriesName, out var dataPoints))
                    continue;

                foreach (var point in dataPoints)
                {
                    double x = _isVIMode ? point.Voltage : point.Current;
                    double y = _isVIMode ? point.Current : point.Voltage;

                    double dx = x - coords.X;
                    double dy = y - coords.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestPoint = point;
                    }
                }
            }

            // Show crosshair if a point is found
            if (nearestPoint != null)
            {
                double x = _isVIMode ? nearestPoint.Voltage : nearestPoint.Current;
                double y = _isVIMode ? nearestPoint.Current : nearestPoint.Voltage;
                var coords1 = new Coordinates(x, y);

                _crosshair.Position = coords1;
                _crosshair.IsVisible = true;

                _highlightMarker.IsVisible = true;
                _highlightMarker.Location = coords1;

                _highlightText.IsVisible = true;
                _highlightText.Location = coords1;
                _highlightText.LabelText = $"V: {nearestPoint.Voltage:F4} V\nI: {nearestPoint.Current:F6} A";
                _highlightText.LabelFontColor = Color.FromColor(System.Drawing.Color.Red);

                wpfPlot.Refresh();
            }
            else
            {
                _crosshair.IsVisible = false;
                _highlightMarker.IsVisible = false;
                _highlightText.IsVisible = false;
                wpfPlot.Refresh();
            }
        }

        private void WpfPlot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _crosshair.IsVisible = false;
            _highlightMarker.IsVisible = false;
            _highlightText.IsVisible = false;
            wpfPlot.Refresh();
        }

        public class IVDataPoint
        {
            public double Voltage { get; set; }
            public double Current { get; set; }
        }

        private sealed class IVDataTableRow
        {
            public string SeriesName { get; set; } = string.Empty;
            public int Index { get; set; }
            public double Voltage { get; set; }
            public double Current { get; set; }
        }
    }
}
