using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageComparison
{
    public partial class ImageComparisonResultWindow : Window, IDisposable
    {
        private readonly AlgorithmResult _result;
        private readonly BitmapSource _reference;
        private readonly BitmapSource _candidate;
        private readonly Dictionary<string, AlgorithmImageArtifact> _visualizations;
        private readonly DispatcherTimer _blinkTimer;
        private IDisposable? _overlaySession;
        private string? _currentDifferenceName;
        private bool _showCandidate;
        private bool _disposed;
        private Exception? _disposeFailure;

        public ImageComparisonResultWindow(
            AlgorithmResult result,
            BitmapSource reference,
            BitmapSource candidate,
            string candidateName,
            ImageProcessingContext image,
            DrawEditorContext draw)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmMeasurementArtifact measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("image-comparison")
                ?? throw new ArgumentException("The result has no comparison measurements.", nameof(result));
            AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("image-comparison-channels")
                ?? throw new ArgumentException("The result has no comparison channel table.", nameof(result));
            AlgorithmTableArtifact alignment = result.GetArtifact<AlgorithmTableArtifact>("image-comparison-alignment")
                ?? throw new ArgumentException("The result has no alignment precheck table.", nameof(result));
            _result = result;
            _reference = reference;
            _candidate = candidate;
            _visualizations = result.Artifacts.OfType<AlgorithmImageArtifact>()
                .Where(artifact => artifact.Name.EndsWith("visualization", StringComparison.Ordinal) || artifact.Name == "difference-heatmap")
                .ToDictionary(artifact => artifact.Name, StringComparer.Ordinal);
            _blinkTimer = new DispatcherTimer();
            try
            {
                InitializeComponent();
                _blinkTimer.Interval = TimeSpan.FromMilliseconds(BlinkInterval.Value);
                _blinkTimer.Tick += (_, _) => ToggleBlink();
                SplitReference.Source = _reference;
                SplitCandidate.Source = _candidate;
                BlinkImage.Source = _reference;
                BlinkLabel.Text = "当前图像";
                MetricsGrid.ItemsSource = ToTable(table).DefaultView;
                AlignmentGrid.ItemsSource = ToTable(alignment).DefaultView;
                DifferenceKind_SelectionChanged(this, null!);
                double mse = measurements.Measurements.Single(item => item.Name == "comparison.mse").Value;
                double rmse = measurements.Measurements.Single(item => item.Name == "comparison.rmse").Value;
                double psnr = measurements.Measurements.Single(item => item.Name == "comparison.psnr_db").Value;
                double? ssim = measurements.Measurements.FirstOrDefault(item => item.Name == "comparison.ssim")?.Value;
                IReadOnlyDictionary<string, JsonElement> alignmentRow = alignment.Rows.Single();
                string alignmentStatus = Display(alignmentRow["Status"]);
                string shiftX = Display(alignmentRow["EstimatedShiftX"]);
                string shiftY = Display(alignmentRow["EstimatedShiftY"]);
                string confidence = Display(alignmentRow["Confidence"]);
                string ssimText = ssim.HasValue ? ssim.Value.ToString("G10", CultureInfo.InvariantCulture) : "N/A";
                SummaryText.Text = $"候选：{candidateName}；MSE={mse:G10}，RMSE={rmse:G10}，PSNR={(double.IsPositiveInfinity(psnr) ? "Infinity" : psnr.ToString("G10", CultureInfo.InvariantCulture))} dB，SSIM={ssimText}；对齐预检={alignmentStatus}，候选偏移=({shiftX}, {shiftY}) px，置信度={confidence}。差分数值 artifact 保持原始位深；对齐预检只报告，不修改图像。";
                _overlaySession = AlgorithmOverlayRenderer.Apply(image, draw, result);
                Closed += (_, _) => DisposeOwnedState();
            }
            catch
            {
                Exception? ignored = null;
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_blinkTimer.Stop, ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _overlaySession?.Dispose(), ref ignored);
                AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(result.Dispose, ref ignored);
                throw;
            }
        }

        private void DifferenceKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || DifferenceKind.SelectedItem is not ComboBoxItem item || item.Tag is not string name) return;
            if (_currentDifferenceName == name || _visualizations == null || !_visualizations.TryGetValue(name, out AlgorithmImageArtifact? artifact)) return;
            DifferenceImage.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(artifact.Image);
            _currentDifferenceName = name;
        }

        private void BlinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_blinkTimer.IsEnabled)
            {
                _blinkTimer.Stop();
                BlinkButton.Content = "开始";
            }
            else
            {
                _blinkTimer.Start();
                BlinkButton.Content = "停止";
            }
        }

        private void BlinkInterval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_blinkTimer != null) _blinkTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, e.NewValue));
        }

        private void ToggleBlink()
        {
            _showCandidate = !_showCandidate;
            BlinkImage.Source = _showCandidate ? _candidate : _reference;
            BlinkLabel.Text = _showCandidate ? "候选图像" : "当前图像";
        }

        private void ViewTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewTabs.SelectedIndex != 1 && _blinkTimer?.IsEnabled == true)
            {
                _blinkTimer.Stop();
                BlinkButton.Content = "开始";
            }
        }

        private void SplitPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateSplitClip();

        private void SplitViewport_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSplitClip();

        private void UpdateSplitClip()
        {
            if (SplitCandidate == null || SplitViewport == null || SplitPosition == null) return;
            SplitCandidate.Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, SplitViewport.ActualWidth * SplitPosition.Value, SplitViewport.ActualHeight));
        }

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
            _ => value.GetRawText(),
        };

        private void SaveDifference_Click(object sender, RoutedEventArgs e)
        {
            if (DifferenceImage.Source is not BitmapSource source) return;
            SaveFileDialog dialog = new() { Filter = "PNG 文件 (*.png)|*.png", FileName = "image-comparison.png", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (File.Exists(dialog.FileName)) throw new IOException($"拒绝覆盖已有文件：{dialog.FileName}");
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using FileStream stream = File.Open(dialog.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                encoder.Save(stream);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "image-comparison.csv", AddExtension = true };
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
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "image-comparison.json", AddExtension = true };
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
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_blinkTimer.Stop, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => DifferenceImage.Source = null, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => BlinkImage.Source = null, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => SplitReference.Source = null, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => SplitCandidate.Source = null, ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(() => _overlaySession?.Dispose(), ref _disposeFailure);
            AlgorithmAnalysisResultWindowTransaction.CaptureCleanupFailure(_result.Dispose, ref _disposeFailure);
            return _disposeFailure;
        }
    }
}
