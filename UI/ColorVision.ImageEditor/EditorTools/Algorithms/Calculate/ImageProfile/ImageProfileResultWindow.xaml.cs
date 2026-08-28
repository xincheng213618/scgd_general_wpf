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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageProfile
{
    public partial class ImageProfileResultWindow : Window, IDisposable
    {
        internal const int MaximumPreviewRows = 2_000;
        internal const int MaximumChartPoints = 2_000;

        private readonly AlgorithmResult _result;
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
                int[] previewRows = PreviewIndices(samples.Rows.Count, MaximumPreviewRows);
                int[] chartRows = PreviewIndices(samples.Rows.Count, MaximumChartPoints);
                SamplesGrid.ItemsSource = ToTable(samples, previewRows).DefaultView;
                double count = measurements.Measurements.Single(item => item.Name == "profile.sample_count").Value;
                double length = measurements.Measurements.Single(item => item.Name == "profile.path_length_pixels").Value;
                double millimetres = measurements.Measurements.Single(item => item.Name == "profile.path_length_millimetres").Value;
                SummaryText.Text = $"采样点：{count:N0}；界面预览：{previewRows.Length:N0}；路径：{length:G8} px / {millimetres:G8} mm。完整数据请显式导出；非有限值在曲线中显示为间断。";
                Render(samples, chartRows);
                _overlaySession = AlgorithmOverlayRenderer.Apply(image, draw, result);
                Closed += (_, _) => DisposeOwnedState();
            }
            catch
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_lifetimeCancellation.Cancel, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _exportCancellation?.Cancel(), ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _overlaySession?.Dispose(), ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(result.Dispose, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_lifetimeCancellation.Dispose, ref ignored);
                throw;
            }
        }

        private void Render(AlgorithmTableArtifact table, IReadOnlyList<int> rowIndices)
        {
            ProfilePlot.Plot.Clear();
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
                scatter.Color = channel switch
                {
                    "R" => Colors.Red,
                    "G" => Colors.Green,
                    "B" => Colors.Blue,
                    "A" => Colors.Gray,
                    "Luminance" => Colors.Orange,
                    _ => Colors.Black,
                };
            }
            ProfilePlot.Plot.XLabel("Distance (px)");
            ProfilePlot.Plot.YLabel("Value");
            ProfilePlot.Plot.ShowLegend(Alignment.UpperRight);
            ProfilePlot.Plot.Axes.AutoScale();
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
            JsonValueKind.Number when value.TryGetDouble(out double number) => number.ToString("G10", CultureInfo.InvariantCulture),
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
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_lifetimeCancellation.Cancel, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _exportCancellation?.Cancel(), ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _overlaySession?.Dispose(), ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_result.Dispose, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_lifetimeCancellation.Dispose, ref _disposeFailure);
            return _disposeFailure;
        }
    }
}
