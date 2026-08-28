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
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageRegistration
{
    public partial class ImageRegistrationResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private readonly IDisposable? _overlaySession;
        private bool _disposed;

        public ImageRegistrationResultWindow(
            AlgorithmResult result,
            string movingName,
            ImageProcessingContext? image = null,
            DrawEditorContext? draw = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmImageArtifact registered = result.GetArtifact<AlgorithmImageArtifact>("registered-image")
                ?? throw new ArgumentException("The result has no registered image.", nameof(result));
            AlgorithmImageArtifact mask = result.GetArtifact<AlgorithmImageArtifact>("valid-region-mask")
                ?? throw new ArgumentException("The result has no validity mask.", nameof(result));
            AlgorithmMeasurementArtifact measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("image-registration-summary")
                ?? throw new ArgumentException("The result has no registration measurements.", nameof(result));
            AlgorithmTableArtifact matrix = result.GetArtifact<AlgorithmTableArtifact>("image-registration-matrix")
                ?? throw new ArgumentException("The result has no registration matrix table.", nameof(result));
            AlgorithmTableArtifact matches = result.GetArtifact<AlgorithmTableArtifact>("image-registration-matches")
                ?? throw new ArgumentException("The result has no registration match table.", nameof(result));
            InitializeComponent();
            _result = result;
            RegisteredPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(registered.Image);
            MaskPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(mask.Image);
            MatrixGrid.ItemsSource = ToTable(matrix).DefaultView;
            MatchesGrid.ItemsSource = ToTable(matches).DefaultView;
            string method = result.GetArtifact<AlgorithmStructuredDataArtifact>("image-registration")?.Data.TryGetProperty("method", out JsonElement methodElement) == true
                ? methodElement.GetString() ?? "unknown"
                : "unknown";
            string estimateSummary = string.Equals(method, nameof(ImageRegistrationMethod.OrbHomography), StringComparison.Ordinal)
                ? $"匹配={Value(measurements, "registration.match_count"):G0}；内点={Value(measurements, "registration.inlier_count"):G0}；几何 RMSE={Value(measurements, "registration.geometric_rmse"):G8}px"
                : $"平移=({Value(measurements, "registration.phase_shift_x"):G8}, {Value(measurements, "registration.phase_shift_y"):G8}) px；相关损失={Value(measurements, "registration.correlation_loss"):G8}；峰唯一性={Value(measurements, "registration.phase_peak_uniqueness"):P2}";
            SummaryText.Text = $"moving：{movingName}；方法={method}；{estimateSummary}；光度 RMSE={Value(measurements, "registration.photometric_rmse"):G8}；置信度={Value(measurements, "registration.confidence"):P2}；有效区域={Value(measurements, "registration.valid_fraction"):P2}。矩阵方向为 moving 像素中心 → reference 像素中心。";
            if (image != null && draw != null) _overlaySession = AlgorithmOverlayRenderer.Apply(image, draw, result);
            Closed += (_, _) => DisposeOwnedState();
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e) => SavePng(RegisteredPreview.Source as BitmapSource, "image-registration.png");

        private void SaveMask_Click(object sender, RoutedEventArgs e) => SavePng(MaskPreview.Source as BitmapSource, "image-registration-mask.png");

        private void SavePng(BitmapSource? source, string fileName)
        {
            if (source == null) return;
            SaveFileDialog dialog = new() { Filter = "PNG 文件 (*.png)|*.png", FileName = fileName, AddExtension = true, OverwritePrompt = false };
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
                MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "image-registration.csv", AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                IReadOnlyList<string> paths = AlgorithmResultExporter.ExportCsvBundle(_result, dialog.FileName);
                MessageBox.Show(this, $"已导出 {paths.Count} 个文件。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "image-registration.json", AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                AlgorithmResultExporter.ExportJson(_result, dialog.FileName);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void DisposeOwnedState()
        {
            if (_disposed) return;
            _disposed = true;
            RegisteredPreview.Source = null;
            MaskPreview.Source = null;
            MatrixGrid.ItemsSource = null;
            MatchesGrid.ItemsSource = null;
            _overlaySession?.Dispose();
            _result.Dispose();
        }

        private static double Value(AlgorithmMeasurementArtifact artifact, string name)
            => artifact.Measurements.Single(value => value.Name == name).Value;

        private static DataTable ToTable(AlgorithmTableArtifact artifact)
        {
            DataTable table = new(artifact.Name);
            foreach (AlgorithmTableColumn column in artifact.Columns) table.Columns.Add(column.Name, typeof(string));
            foreach (IReadOnlyDictionary<string, JsonElement> source in artifact.Rows)
            {
                DataRow row = table.NewRow();
                foreach (AlgorithmTableColumn column in artifact.Columns)
                {
                    row[column.Name] = source.TryGetValue(column.Name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)
                        ? number.ToString("G12", CultureInfo.InvariantCulture)
                        : source.TryGetValue(column.Name, out value) ? value.ToString() : string.Empty;
                }
                table.Rows.Add(row);
            }
            return table;
        }
    }
}
