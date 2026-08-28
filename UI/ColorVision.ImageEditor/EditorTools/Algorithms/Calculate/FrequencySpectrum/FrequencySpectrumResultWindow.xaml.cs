using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using Microsoft.Win32;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FrequencySpectrum
{
    public partial class FrequencySpectrumResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private bool _disposed;

        public FrequencySpectrumResultWindow(AlgorithmResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmImageArtifact magnitude = result.GetArtifact<AlgorithmImageArtifact>("magnitude-spectrum")
                ?? throw new ArgumentException("The result has no magnitude spectrum.", nameof(result));
            AlgorithmImageArtifact power = result.GetArtifact<AlgorithmImageArtifact>("power-spectrum")
                ?? throw new ArgumentException("The result has no power spectrum.", nameof(result));
            AlgorithmMeasurementArtifact measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("frequency-spectrum-summary")
                ?? throw new ArgumentException("The result has no frequency measurements.", nameof(result));
            AlgorithmTableArtifact radial = result.GetArtifact<AlgorithmTableArtifact>("frequency-radial-spectrum")
                ?? throw new ArgumentException("The result has no radial spectrum.", nameof(result));
            AlgorithmTableArtifact directional = result.GetArtifact<AlgorithmTableArtifact>("frequency-directional-spectrum")
                ?? throw new ArgumentException("The result has no directional spectrum.", nameof(result));
            AlgorithmTableArtifact peaks = result.GetArtifact<AlgorithmTableArtifact>("frequency-peaks")
                ?? throw new ArgumentException("The result has no peak table.", nameof(result));

            InitializeComponent();
            _result = result;
            MagnitudePreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(magnitude.Image);
            PowerPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(power.Image);
            PeaksGrid.ItemsSource = ToTable(peaks).DefaultView;
            RenderRadial(radial);
            RenderDirectional(directional);
            double inverse = Value(measurements, "frequency.inverse_rmse");
            AlgorithmMeasurement? dominant = measurements.Measurements.FirstOrDefault(value => value.Name == "frequency.dominant.cycles_per_pixel");
            SummaryText.Text = dominant == null
                ? $"未检测到超过阈值的非直流峰值；逆变换 RMSE={inverse:G8}。频谱图是显示归一化结果，数值结果请使用表格/导出。"
                : $"主频={dominant.Value:G8} cycles/pixel；周期={Value(measurements, "frequency.dominant.period_pixels"):G8}px；频率方向={Value(measurements, "frequency.dominant.frequency_direction_degrees"):G6}°；空间纹理方向={Value(measurements, "frequency.dominant.spatial_direction_degrees"):G6}°；逆变换 RMSE={inverse:G8}。";
            Closed += (_, _) => DisposeOwnedState();
        }

        private void RenderRadial(AlgorithmTableArtifact table)
        {
            double[] x = Numbers(table, "CenterFrequency");
            double[] y = Numbers(table, "MeanPower");
            var plot = RadialPlot.Plot.Add.Scatter(x, y);
            plot.MarkerSize = 0;
            plot.LineWidth = 1.5f;
            RadialPlot.Plot.XLabel("Frequency (cycles/pixel)");
            RadialPlot.Plot.YLabel("Mean power (nominal DN²)");
            RadialPlot.Plot.Axes.AutoScale();
            RadialPlot.Refresh();
        }

        private void RenderDirectional(AlgorithmTableArtifact table)
        {
            double[] x = Numbers(table, "CenterDirection");
            double[] y = Numbers(table, "MeanPower");
            var plot = DirectionalPlot.Plot.Add.Scatter(x, y);
            plot.MarkerSize = 0;
            plot.LineWidth = 1.5f;
            DirectionalPlot.Plot.XLabel("Frequency direction (degree)");
            DirectionalPlot.Plot.YLabel("Mean power (nominal DN²)");
            DirectionalPlot.Plot.Axes.AutoScale();
            DirectionalPlot.Refresh();
        }

        private static double[] Numbers(AlgorithmTableArtifact table, string column)
            => table.Rows.Select(row => row[column].GetDouble()).ToArray();

        private void SaveMagnitude_Click(object sender, RoutedEventArgs e) => SavePng(MagnitudePreview.Source as BitmapSource, "frequency-magnitude.png");

        private void SavePower_Click(object sender, RoutedEventArgs e) => SavePng(PowerPreview.Source as BitmapSource, "frequency-power.png");

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
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "frequency-spectrum.csv", AddExtension = true, OverwritePrompt = false };
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
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "frequency-spectrum.json", AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try { AlgorithmResultExporter.ExportJson(_result, dialog.FileName); }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void DisposeOwnedState()
        {
            if (_disposed) return;
            _disposed = true;
            MagnitudePreview.Source = null;
            PowerPreview.Source = null;
            PeaksGrid.ItemsSource = null;
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
