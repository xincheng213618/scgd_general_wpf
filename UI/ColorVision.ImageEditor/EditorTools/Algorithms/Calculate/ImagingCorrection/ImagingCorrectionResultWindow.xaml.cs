using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImagingCorrection
{
    public partial class ImagingCorrectionResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private bool _disposed;

        public ImagingCorrectionResultWindow(AlgorithmResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmImageArtifact corrected = result.GetArtifact<AlgorithmImageArtifact>("corrected-image") ?? throw new ArgumentException("The result has no corrected image.", nameof(result));
            AlgorithmImageArtifact mask = result.GetArtifact<AlgorithmImageArtifact>("correction-validity-mask") ?? throw new ArgumentException("The result has no validity mask.", nameof(result));
            AlgorithmMeasurementArtifact measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("imaging-correction-summary") ?? throw new ArgumentException("The result has no correction measurements.", nameof(result));
            AlgorithmTableArtifact stages = result.GetArtifact<AlgorithmTableArtifact>("imaging-correction-stages") ?? throw new ArgumentException("The result has no stage table.", nameof(result));
            AlgorithmTableArtifact provenance = result.GetArtifact<AlgorithmTableArtifact>("imaging-correction-provenance") ?? throw new ArgumentException("The result has no provenance table.", nameof(result));
            InitializeComponent();
            _result = result;
            CorrectedPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(corrected.Image);
            MaskPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(mask.Image);
            StageGrid.ItemsSource = ToTable(stages).DefaultView;
            ProvenanceGrid.ItemsSource = ToTable(provenance).DefaultView;
            SummaryText.Text = $"有效像素={Value(measurements, "imaging-correction.valid_fraction"):P2}；坏点 {Value(measurements, "imaging-correction.bad_pixels_corrected"):G0}/{Value(measurements, "imaging-correction.bad_pixels_marked"):G0} 已校正；低/高端裁剪样本={Value(measurements, "imaging-correction.clipped_low_samples"):G0}/{Value(measurements, "imaging-correction.clipped_high_samples"):G0}。结果已通过 ImageView session 提交。";
            Closed += (_, _) => DisposeOwnedState();
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e) => SavePng(CorrectedPreview.Source as BitmapSource, "imaging-corrected.png");
        private void SaveMask_Click(object sender, RoutedEventArgs e) => SavePng(MaskPreview.Source as BitmapSource, "imaging-correction-mask.png");

        private void SavePng(BitmapSource? source, string name)
        {
            if (source == null) return;
            SaveFileDialog dialog = new() { Filter = "PNG 文件 (*.png)|*.png", FileName = name, AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (File.Exists(dialog.FileName)) throw new IOException($"拒绝覆盖已有文件：{dialog.FileName}");
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using FileStream stream = File.Open(dialog.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                encoder.Save(stream);
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "imaging-correction.csv", AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try { AlgorithmResultExporter.ExportCsvBundle(_result, dialog.FileName); }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "imaging-correction.json", AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try { AlgorithmResultExporter.ExportJson(_result, dialog.FileName); }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void DisposeOwnedState()
        {
            if (_disposed) return;
            _disposed = true;
            CorrectedPreview.Source = null;
            MaskPreview.Source = null;
            StageGrid.ItemsSource = null;
            ProvenanceGrid.ItemsSource = null;
            _result.Dispose();
        }

        private static double Value(AlgorithmMeasurementArtifact artifact, string name) => artifact.Measurements.Single(value => value.Name == name).Value;

        private static DataTable ToTable(AlgorithmTableArtifact artifact)
        {
            DataTable table = new(artifact.Name);
            foreach (AlgorithmTableColumn column in artifact.Columns) table.Columns.Add(column.Name, typeof(string));
            foreach (IReadOnlyDictionary<string, JsonElement> source in artifact.Rows)
            {
                DataRow row = table.NewRow();
                foreach (AlgorithmTableColumn column in artifact.Columns)
                    row[column.Name] = source.TryGetValue(column.Name, out JsonElement value)
                        ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
                        : string.Empty;
                table.Rows.Add(row);
            }
            return table;
        }
    }
}
