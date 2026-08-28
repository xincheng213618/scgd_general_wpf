using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
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

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.LensDistortionCorrection
{
    public partial class LensDistortionCorrectionResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private bool _disposed;

        public LensDistortionCorrectionResultWindow(AlgorithmResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmImageArtifact corrected = result.GetArtifact<AlgorithmImageArtifact>("corrected-image")
                ?? throw new ArgumentException("The result has no corrected image.", nameof(result));
            AlgorithmImageArtifact mask = result.GetArtifact<AlgorithmImageArtifact>("valid-region-mask")
                ?? throw new ArgumentException("The result has no validity mask.", nameof(result));
            AlgorithmMeasurementArtifact measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("lens-distortion-summary")
                ?? throw new ArgumentException("The result has no lens-distortion measurements.", nameof(result));
            AlgorithmTableArtifact matrices = result.GetArtifact<AlgorithmTableArtifact>("lens-distortion-camera-matrices")
                ?? throw new ArgumentException("The result has no camera matrices.", nameof(result));
            AlgorithmTableArtifact coefficients = result.GetArtifact<AlgorithmTableArtifact>("lens-distortion-coefficients")
                ?? throw new ArgumentException("The result has no distortion coefficients.", nameof(result));
            AlgorithmStructuredDataArtifact structured = result.GetArtifact<AlgorithmStructuredDataArtifact>("lens-distortion-correction")
                ?? throw new ArgumentException("The result has no structured calibration record.", nameof(result));
            InitializeComponent();
            _result = result;
            CorrectedPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(corrected.Image);
            MaskPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(mask.Image);
            MatrixGrid.ItemsSource = ToTable(matrices).DefaultView;
            CoefficientGrid.ItemsSource = ToTable(coefficients).DefaultView;
            JsonElement calibration = structured.Data.GetProperty("calibration");
            bool qualityAvailable = calibration.GetProperty("qualityAvailable").GetBoolean();
            string quality = qualityAvailable
                ? $"标定 RMS={Value(measurements, "lens-distortion.calibration_rms_error"):G8}px；标定置信度={Value(measurements, "lens-distortion.calibration_confidence"):P2}"
                : "未提供标定质量（结果不虚构置信度）";
            SummaryText.Text = $"有效像素={Value(measurements, "lens-distortion.valid_fraction"):P2}；平均位移={Value(measurements, "lens-distortion.mean_displacement"):G8}px；最大位移={Value(measurements, "lens-distortion.maximum_displacement"):G8}px；{quality}。标定来源={calibration.GetProperty("source").GetString()}；版本={calibration.GetProperty("version").GetString()}。结果已通过 ImageView session 提交。";
            Closed += (_, _) => DisposeOwnedState();
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e) => SavePng(CorrectedPreview.Source as BitmapSource, "lens-distortion-corrected.png");

        private void SaveMask_Click(object sender, RoutedEventArgs e) => SavePng(MaskPreview.Source as BitmapSource, "lens-distortion-mask.png");

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
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "lens-distortion-correction.csv", AddExtension = true, OverwritePrompt = false };
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
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "lens-distortion-correction.json", AddExtension = true, OverwritePrompt = false };
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
            CorrectedPreview.Source = null;
            MaskPreview.Source = null;
            MatrixGrid.ItemsSource = null;
            CoefficientGrid.ItemsSource = null;
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
                    row[column.Name] = source.TryGetValue(column.Name, out JsonElement value)
                        ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()
                        : string.Empty;
                }
                table.Rows.Add(row);
            }
            return table;
        }
    }
}
