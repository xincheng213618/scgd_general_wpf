using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using Microsoft.Win32;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.RoiStatistics
{
    public partial class RoiStatisticsResultWindow : Window, IDisposable
    {
        private readonly AlgorithmResult _result;
        private IDisposable? _overlaySession;
        private bool _disposed;
        private Exception? _disposeFailure;

        public RoiStatisticsResultWindow(AlgorithmResult result, ImageProcessingContext image, DrawEditorContext draw)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmTableArtifact summary = result.GetArtifact<AlgorithmTableArtifact>("roi-statistics-summary")
                ?? throw new ArgumentException("The result has no ROI statistics summary.", nameof(result));
            AlgorithmTableArtifact histogram = result.GetArtifact<AlgorithmTableArtifact>("roi-histogram")
                ?? throw new ArgumentException("The result has no ROI histogram.", nameof(result));
            AlgorithmTableArtifact candidates = result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")
                ?? throw new ArgumentException("The result has no bad-pixel candidate table.", nameof(result));
            _result = result;
            try
            {
                InitializeComponent();
                SummaryGrid.ItemsSource = ToTable(summary).DefaultView;
                HistogramGrid.ItemsSource = ToTable(histogram).DefaultView;
                CandidatesGrid.ItemsSource = ToTable(candidates).DefaultView;
                double pixelCount = Measurement("roi.pixel_count");
                double badPixels = Measurement("roi.bad_pixel_candidate_count");
                SummaryText.Text = $"ROI 像素：{pixelCount:N0}；通道：{summary.Rows.Count}；坏点候选：{badPixels:N0}。统计值排除 NaN/Infinity，StdDev 为总体标准差。";
                RenderHistogram(histogram);
                _overlaySession = AlgorithmOverlayRenderer.Apply(image, draw, result);
                Closed += (_, _) => DisposeOwnedState();
            }
            catch
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _overlaySession?.Dispose(), ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(result.Dispose, ref ignored);
                throw;
            }
        }

        private void RenderHistogram(AlgorithmTableArtifact table)
        {
            HistogramPlot.Plot.Clear();
            foreach (IGrouping<int, IReadOnlyDictionary<string, JsonElement>> channel in table.Rows.GroupBy(row => row["Channel"].GetInt32()))
            {
                IReadOnlyDictionary<string, JsonElement>[] rows = channel.OrderBy(row => row["BinIndex"].GetInt32()).ToArray();
                double[] positions = rows.Select(row => (row["LowerInclusive"].GetDouble() + row["Upper"].GetDouble()) / 2).ToArray();
                double[] counts = rows.Select(row => (double)row["Count"].GetInt64()).ToArray();
                if (positions.Length == 0) continue;
                var plot = HistogramPlot.Plot.Add.Scatter(positions, counts);
                plot.MarkerSize = 0;
                plot.LineWidth = 1.5f;
                plot.LegendText = rows[0]["ChannelName"].GetString() ?? channel.Key.ToString(CultureInfo.InvariantCulture);
                plot.Color = channel.Key switch
                {
                    0 when table.Rows.Any(row => row["ChannelName"].GetString() == "B") => Colors.Blue,
                    1 => Colors.Green,
                    2 => Colors.Red,
                    3 => Colors.Gray,
                    _ => Colors.Black,
                };
            }
            HistogramPlot.Plot.XLabel("Pixel value");
            HistogramPlot.Plot.YLabel("Count");
            HistogramPlot.Plot.ShowLegend(Alignment.UpperRight);
            HistogramPlot.Plot.Axes.AutoScale();
            HistogramPlot.Refresh();
        }

        private double Measurement(string name)
            => _result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements.Single(item => item.Name == name && item.Channel == null).Value;

        private static DataTable ToTable(AlgorithmTableArtifact artifact)
        {
            DataTable table = new(artifact.Name);
            foreach (AlgorithmTableColumn column in artifact.Columns) table.Columns.Add(column.Name, typeof(string));
            foreach (IReadOnlyDictionary<string, JsonElement> source in artifact.Rows)
            {
                DataRow row = table.NewRow();
                foreach (AlgorithmTableColumn column in artifact.Columns)
                    row[column.Name] = source.TryGetValue(column.Name, out JsonElement value) ? Display(value) : string.Empty;
                table.Rows.Add(row);
            }
            return table;
        }

        private static string Display(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number when value.TryGetDouble(out double number) => number.ToString("G10", CultureInfo.InvariantCulture),
            JsonValueKind.True => "True",
            JsonValueKind.False => "False",
            _ => value.GetRawText(),
        };

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "roi-statistics.csv", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                IReadOnlyList<string> paths = AlgorithmResultExporter.ExportCsvBundle(_result, dialog.FileName);
                MessageBox.Show(this, $"已导出 {paths.Count} 个文件。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "roi-statistics.json", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                AlgorithmResultExporter.ExportJson(_result, dialog.FileName);
                MessageBox.Show(this, "导出完成。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _overlaySession?.Dispose(), ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_result.Dispose, ref _disposeFailure);
            return _disposeFailure;
        }
    }
}
