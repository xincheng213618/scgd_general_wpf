using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using Microsoft.Win32;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageProfile
{
    public partial class ImageProfileResultWindow : Window, IDisposable
    {
        internal const int MaximumPreviewRows = 2_000;
        internal const int MaximumChartPoints = 2_000;

        private readonly AlgorithmResult _result;
        private readonly ThemeManager _themeManager = ThemeManager.Current;
        private readonly Dictionary<string, ScottPlot.Plottables.Scatter> _curves = new();
        private IYAxis? _chromaticityAxis;
        private string _rawAxisLabel = "Value";
        private bool _selectingChannels;
        private IDisposable? _overlaySession;
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private CancellationTokenSource? _exportCancellation;
        private bool _disposed;
        private Exception? _disposeFailure;

        public ImageProfileResultWindow(AlgorithmResult result, ImageProcessingContext image, DrawEditorContext draw)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmTableArtifact samples = result.GetArtifact<AlgorithmTableArtifact>("image-profile-samples")
                ?? throw new ArgumentException("The result has no image profile sample table.", nameof(result));
            AlgorithmMeasurementArtifact measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("image-profile")
                ?? throw new ArgumentException("The result has no image profile measurements.", nameof(result));
            _result = result;
            try
            {
                InitializeComponent();
                this.ApplyCaption();
                int[] previewRows = PreviewIndices(samples.Rows.Count, MaximumPreviewRows);
                int[] chartRows = PreviewIndices(samples.Rows.Count, MaximumChartPoints);
                StatisticsGrid.ItemsSource = ToStatisticsTable(measurements).DefaultView;
                SamplesGrid.ItemsSource = ToTable(samples, previewRows).DefaultView;
                double count = measurements.Measurements.Single(item => item.Name == "profile.sample_count").Value;
                double length = measurements.Measurements.Single(item => item.Name == "profile.path_length_pixels").Value;
                double millimetres = measurements.Measurements.Single(item => item.Name == "profile.path_length_millimetres").Value;
                SummaryText.Text = $"采样点：{count:N0}；界面预览：{previewRows.Length:N0}；路径：{length:G8} px / {millimetres:G8} mm。统计按完整数据计算并排除非有限值；标准差为总体标准差。";
                string[] cieChannels = samples.Columns.Select(column => column.Name)
                    .Where(name => name.StartsWith("CIE ", StringComparison.Ordinal) && !name.EndsWith("Status", StringComparison.Ordinal)).ToArray();
                if (cieChannels.Length > 0)
                {
                    string sourceName = string.Join(" / ", cieChannels);
                    Title = $"灰度与颜色剖面 — {sourceName}";
                    SummaryText.Text = "RGB/Gray 为 DN；XYZ 单位未声明；x/y 为无量纲色度。" + SummaryText.Text;
                }
                Render(samples, chartRows);
                ApplyPlotTheme(_themeManager.CurrentUITheme);
                _themeManager.CurrentUIThemeChanged += OnThemeChanged;
                _overlaySession = AlgorithmOverlayRenderer.Apply(image, draw, result);
                Closed += (_, _) => DisposeOwnedState();
            }
            catch
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_lifetimeCancellation.Cancel, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _exportCancellation?.Cancel(), ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _overlaySession?.Dispose(), ref ignored);
                _themeManager.CurrentUIThemeChanged -= OnThemeChanged;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(result.Dispose, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_lifetimeCancellation.Dispose, ref ignored);
                throw;
            }
        }

        internal void ConfigureSources(IReadOnlyList<string> names, int selected, Action<int> select)
        {
            SourcePanel.Visibility = Visibility.Visible;
            SourceSelector.ItemsSource = names;
            SourceSelector.SelectedIndex = selected;
            SourceSelector.SelectionChanged += (_, _) =>
            {
                int requested = SourceSelector.SelectedIndex;
                if (requested < 0 || requested == selected || _disposed) return;
                // Keep the displayed source label truthful until the replacement result succeeds.
                SourceSelector.SelectedIndex = selected;
                select(requested);
            };
        }

        private void Render(AlgorithmTableArtifact table, IReadOnlyList<int> rowIndices)
        {
            ProfilePlot.Plot.Clear();
            _rawAxisLabel = table.Columns.Any(column => column.Unit == "DN") ? "RGB / Gray (DN)" : "Value";
            double[] distances = rowIndices.Select(index => table.Rows[index]["DistancePixels"].GetDouble()).ToArray();
            string[] channels = table.Columns
                .Select(column => column.Name)
                .Where(name => table.Columns.Any(column => column.Name == name + "Status"))
                .ToArray();
            foreach (string channel in channels)
            {
                double[] values = rowIndices.Select(index => table.Rows[index][channel].ValueKind == JsonValueKind.Number ? table.Rows[index][channel].GetDouble() : double.NaN).ToArray();
                var scatter = ProfilePlot.Plot.Add.Scatter(distances, values);
                scatter.MarkerSize = 0;
                scatter.LineWidth = 1.5f;
                scatter.LegendText = channel;
                _curves.Add(channel, scatter);
            }
            ProfilePlot.Plot.XLabel("Distance (px)");
            if (channels.Contains("CIE x")) _chromaticityAxis = ProfilePlot.Plot.Axes.AddRightAxis();
            ProfilePlot.Plot.ShowLegend(Alignment.UpperRight);
            ProfilePlot.Plot.Legend.ShowItemsFromHiddenPlottables = false;
            BuildChannelControls(channels);
            RefreshVisibleChannels();
        }

        private void BuildChannelControls(string[] channels)
        {
            ChannelPanel.Children.Add(new TextBlock { Text = "显示通道：", VerticalAlignment = System.Windows.VerticalAlignment.Center });
            foreach (string channel in channels)
            {
                bool selected = !channels.Contains("CIE Y") || channel is "R" or "G" or "B" or "Gray" or "CIE Y";
                CheckBox check = new() { Content = channel, Tag = channel, IsChecked = selected, Margin = new Thickness(0, 0, 12, 0), VerticalContentAlignment = System.Windows.VerticalAlignment.Center };
                check.Checked += ChannelVisibilityChanged;
                check.Unchecked += ChannelVisibilityChanged;
                ChannelPanel.Children.Add(check);
            }
            AddPreset("全部", _ => true);
            if (channels.Contains("R")) AddPreset("RGB", name => name is "B" or "G" or "R" or "Luminance");
            if (channels.Contains("CIE X")) AddPreset("XYZ", name => name is "CIE X" or "CIE Y" or "CIE Z");
            if (channels.Contains("CIE x")) AddPreset("x/y", name => name is "CIE x" or "CIE y");
        }

        private void AddPreset(string label, Func<string, bool> selected)
        {
            Button button = new() { Content = label, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(4, 0, 0, 0) };
            button.Click += (_, _) =>
            {
                _selectingChannels = true;
                try { foreach (CheckBox check in ChannelPanel.Children.OfType<CheckBox>()) check.IsChecked = selected((string)check.Tag); }
                finally { _selectingChannels = false; }
                RefreshVisibleChannels();
            };
            ChannelPanel.Children.Add(button);
        }

        private void ChannelVisibilityChanged(object sender, RoutedEventArgs e)
        {
            if (!_selectingChannels) RefreshVisibleChannels();
        }

        private void RefreshVisibleChannels()
        {
            foreach (CheckBox check in ChannelPanel.Children.OfType<CheckBox>()) _curves[(string)check.Tag].IsVisible = check.IsChecked == true;
            bool raw = _curves.Any(pair => pair.Value.IsVisible && !pair.Key.StartsWith("CIE ", StringComparison.Ordinal));
            bool xyz = _curves.Any(pair => pair.Value.IsVisible && pair.Key is "CIE X" or "CIE Y" or "CIE Z");
            bool xy = _curves.Any(pair => pair.Value.IsVisible && pair.Key is "CIE x" or "CIE y");
            var axes = ProfilePlot.Plot.Axes;
            IYAxis xyzAxis = raw ? axes.Right : axes.Left;
            IYAxis xyAxis = raw && xyz ? _chromaticityAxis! : raw || xyz ? axes.Right : axes.Left;
            foreach (var (name, curve) in _curves)
                curve.Axes.YAxis = name is "CIE x" or "CIE y" ? xyAxis : name.StartsWith("CIE ", StringComparison.Ordinal) ? xyzAxis : axes.Left;
            axes.Left.IsVisible = raw || xyz || xy;
            axes.Right.IsVisible = (raw && xyz) || ((raw || xyz) && xy);
            if (_chromaticityAxis != null) _chromaticityAxis.IsVisible = raw && xyz && xy;
            axes.Left.Label.Text = raw ? _rawAxisLabel : xyz ? "CIE XYZ" : "CIE x / y";
            if (xyz) xyzAxis.Label.Text = _curves.Any(pair => pair.Value.IsVisible && pair.Key is "CIE X" or "CIE Z") ? "CIE XYZ" : "CIE Y";
            if (xy) xyAxis.Label.Text = "CIE x / y";
            ProfilePlot.Plot.Axes.AutoScale();
            ProfilePlot.Refresh();
        }

        private void OnThemeChanged(Theme theme)
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(() => OnThemeChanged(theme));
                return;
            }
            if (!_disposed) ApplyPlotTheme(theme);
        }

        private void ApplyPlotTheme(Theme theme)
        {
            bool dark = theme == Theme.Dark;
            var plot = ProfilePlot.Plot;
            Color background = Color.FromHex(dark ? "#262626" : "#FFFFFF");
            Color foreground = Color.FromHex(dark ? "#E5E7EB" : "#252525");
            Color border = Color.FromHex(dark ? "#43464C" : "#D8DBDF");
            plot.FigureBackground.Color = background;
            plot.DataBackground.Color = Color.FromHex(dark ? "#1C1C1C" : "#FAFBFC");
            plot.Axes.Color(foreground);
            plot.Grid.MajorLineColor = border;
            plot.Grid.MinorLineColor = Color.FromHex(dark ? "#303237" : "#ECEEF0");
            plot.Legend.BackgroundColor = background;
            plot.Legend.FontColor = foreground;
            plot.Legend.OutlineColor = border;
            foreach (var (name, curve) in _curves)
            {
                curve.Color = name switch
                {
                    "R" => Color.FromHex(dark ? "#FF6868" : "#D52F3A"),
                    "G" => Color.FromHex(dark ? "#64D97B" : "#16803B"),
                    "B" => Color.FromHex(dark ? "#6FA8FF" : "#2563EB"),
                    "Luminance" => Color.FromHex(dark ? "#FFCD63" : "#9B6B00"),
                    "CIE X" => Color.FromHex(dark ? "#D69CFF" : "#9333B5"),
                    "CIE Y" => Color.FromHex(dark ? "#FF9E51" : "#D06612"),
                    "CIE Z" => Color.FromHex(dark ? "#5EDBD4" : "#0F817E"),
                    "CIE x" => Color.FromHex(dark ? "#FF91C8" : "#BE397C"),
                    "CIE y" => Color.FromHex(dark ? "#BDDD65" : "#687D13"),
                    _ => foreground,
                };
                CheckBox? check = ChannelPanel.Children.OfType<CheckBox>().FirstOrDefault(item => (string)item.Tag == name);
                if (check != null)
                {
                    var color = curve.Color;
                    check.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
                }
            }
            ProfilePlot.Refresh();
        }

        private static DataTable ToTable(AlgorithmTableArtifact artifact, IReadOnlyList<int> rowIndices)
        {
            DataTable table = new(artifact.Name);
            foreach (AlgorithmTableColumn column in artifact.Columns) table.Columns.Add(column.Name, typeof(string));
            foreach (int rowIndex in rowIndices)
            {
                IReadOnlyDictionary<string, JsonElement> source = artifact.Rows[rowIndex];
                DataRow row = table.NewRow();
                foreach (AlgorithmTableColumn column in artifact.Columns)
                    row[column.Name] = source.TryGetValue(column.Name, out JsonElement value) ? Display(value) : string.Empty;
                table.Rows.Add(row);
            }
            return table;
        }

        private static DataTable ToStatisticsTable(AlgorithmMeasurementArtifact artifact)
        {
            DataTable table = new("image-profile-statistics");
            foreach (string column in new[] { "通道", "单位", "有效点", "无效点", "最小值", "最大值", "平均值", "总体标准差" })
                table.Columns.Add(column, typeof(string));

            foreach (IGrouping<int, AlgorithmMeasurement> channel in artifact.Measurements
                .Where(item => item.Channel.HasValue)
                .GroupBy(item => item.Channel!.Value)
                .OrderBy(group => group.Key))
            {
                AlgorithmMeasurement[] measurements = channel.ToArray();
                AlgorithmMeasurement? named = measurements.FirstOrDefault(item => item.Qualifiers?.ContainsKey("channelName") == true);
                string channelName = named?.Qualifiers?["channelName"] ?? channel.Key.ToString(CultureInfo.InvariantCulture);
                string unit = measurements.FirstOrDefault(item => item.Name == "channel.mean")?.Unit ?? string.Empty;
                table.Rows.Add(
                    channelName,
                    unit,
                    MeasurementDisplay(measurements, "channel.finite_count"),
                    MeasurementDisplay(measurements, "channel.invalid_count"),
                    MeasurementDisplay(measurements, "channel.minimum"),
                    MeasurementDisplay(measurements, "channel.maximum"),
                    MeasurementDisplay(measurements, "channel.mean"),
                    MeasurementDisplay(measurements, "channel.stddev.population"));
            }
            return table;
        }

        private static string MeasurementDisplay(IEnumerable<AlgorithmMeasurement> measurements, string name)
        {
            AlgorithmMeasurement? measurement = measurements.FirstOrDefault(item => item.Name == name);
            return measurement == null ? string.Empty : measurement.Value.ToString("G8", CultureInfo.InvariantCulture);
        }

        internal static int[] PreviewIndices(int rowCount, int maximum)
        {
            if (rowCount <= 0 || maximum <= 0) return [];
            if (rowCount <= maximum) return Enumerable.Range(0, rowCount).ToArray();
            if (maximum == 1) return [0];
            int[] result = new int[maximum];
            for (int index = 0; index < maximum; index++)
                result[index] = (int)((long)index * (rowCount - 1) / (maximum - 1));
            return result;
        }

        private static string Display(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number when value.TryGetDouble(out double number) => number.ToString("G8", CultureInfo.InvariantCulture),
            _ => value.GetRawText(),
        };

        private async void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "image-profile.csv", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            await ExportAsync(async (token, progress) =>
            {
                IReadOnlyList<string> paths = await AlgorithmResultExporter.ExportCsvBundleAsync(
                    _result, dialog.FileName, cancellationToken: token, progress: progress);
                return $"已导出 {paths.Count} 个文件。";
            });
        }

        private async void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "image-profile.json", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            await ExportAsync(async (token, progress) =>
            {
                await AlgorithmResultExporter.ExportJsonAsync(_result, dialog.FileName, cancellationToken: token, progress: progress);
                return "导出完成。";
            });
        }

        private async Task ExportAsync(Func<CancellationToken, IProgress<AlgorithmProgress>, Task<string>> export)
        {
            if (_exportCancellation != null) return;
            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
            _exportCancellation = cancellation;
            Progress<AlgorithmProgress> progress = new(value => ExportProgress.Value = Math.Clamp(value.Fraction * 100, 0, 100));
            SetExporting(true);
            try
            {
                string message = await export(cancellation.Token, progress);
                if (!cancellation.IsCancellationRequested)
                    MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (ReferenceEquals(_exportCancellation, cancellation)) _exportCancellation = null;
                SetExporting(false);
            }
        }

        private void CancelExport_Click(object sender, RoutedEventArgs e) => _exportCancellation?.Cancel();

        private void SetExporting(bool exporting)
        {
            ExportCsvButton.IsEnabled = !exporting;
            ExportJsonButton.IsEnabled = !exporting;
            CancelExportButton.Visibility = exporting ? Visibility.Visible : Visibility.Collapsed;
            ExportProgress.Visibility = exporting ? Visibility.Visible : Visibility.Collapsed;
            if (!exporting) ExportProgress.Value = 0;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        public void Dispose()
        {
            Exception? failure = null;
            if (IsLoaded) AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(Close, ref failure);
            failure ??= DisposeOwnedState();
            GC.SuppressFinalize(this);
            if (failure != null) throw failure;
        }

        private Exception? DisposeOwnedState()
        {
            if (_disposed) return _disposeFailure;
            _disposed = true;
            _themeManager.CurrentUIThemeChanged -= OnThemeChanged;
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_lifetimeCancellation.Cancel, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _exportCancellation?.Cancel(), ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _overlaySession?.Dispose(), ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_result.Dispose, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_lifetimeCancellation.Dispose, ref _disposeFailure);
            return _disposeFailure;
        }
    }
}
