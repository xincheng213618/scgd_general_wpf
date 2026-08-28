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

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.GeometricTransform
{
    public partial class GeometricTransformResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private bool _disposed;

        public GeometricTransformResultWindow(AlgorithmResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmImageArtifact transformed = result.GetArtifact<AlgorithmImageArtifact>("transformed-image")
                ?? throw new ArgumentException("The result has no transformed image.", nameof(result));
            AlgorithmImageArtifact mask = result.GetArtifact<AlgorithmImageArtifact>("valid-region-mask")
                ?? throw new ArgumentException("The result has no validity mask.", nameof(result));
            AlgorithmMeasurementArtifact measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("geometric-transform-summary")
                ?? throw new ArgumentException("The result has no transform measurements.", nameof(result));
            AlgorithmTableArtifact matrix = result.GetArtifact<AlgorithmTableArtifact>("geometric-transform-matrix")
                ?? throw new ArgumentException("The result has no transform matrix table.", nameof(result));
            InitializeComponent();
            _result = result;
            TransformedPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(transformed.Image);
            MaskPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(mask.Image);
            MatrixGrid.ItemsSource = ToTable(matrix).DefaultView;
            double width = Value(measurements, "transform.output_width");
            double height = Value(measurements, "transform.output_height");
            double valid = Value(measurements, "transform.valid_fraction");
            double condition = Value(measurements, "transform.condition_number");
            double residual = Value(measurements, "transform.inverse_residual");
            SummaryText.Text = $"输出 {width:G0} × {height:G0}；有效像素 {valid:P2}；条件数 {condition:G8}；正逆矩阵残差 {residual:G6}。结果已通过 ImageView session 提交，mask 与矩阵可分别查看/导出。";
            Closed += (_, _) => DisposeOwnedState();
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e) => SavePng(TransformedPreview.Source as BitmapSource, "geometric-transform.png");

        private void SaveMask_Click(object sender, RoutedEventArgs e) => SavePng(MaskPreview.Source as BitmapSource, "geometric-transform-mask.png");

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

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "geometric-transform.json", AddExtension = true, OverwritePrompt = false };
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
            TransformedPreview.Source = null;
            MaskPreview.Source = null;
            MatrixGrid.ItemsSource = null;
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
                    row[column.Name] = source.TryGetValue(column.Name, out JsonElement value) && value.TryGetDouble(out double number)
                        ? number.ToString("G12", CultureInfo.InvariantCulture)
                        : string.Empty;
                }
                table.Rows.Add(row);
            }
            return table;
        }
    }
}
