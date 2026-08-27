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

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageProfile
{
    public partial class ImageProfileResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private readonly IDisposable _overlaySession;
        private bool _disposed;

        public ImageProfileResultWindow(AlgorithmResult result, ImageProcessingContext image, DrawEditorContext draw)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmTableArtifact samples = result.GetArtifact<AlgorithmTableArtifact>("image-profile-samples")
                ?? throw new ArgumentException("The result has no image profile sample table.", nameof(result));
            AlgorithmMeasurementArtifact measurements = result.GetArtifact<AlgorithmMeasurementArtifact>("image-profile")
                ?? throw new ArgumentException("The result has no image profile measurements.", nameof(result));
            InitializeComponent();
            _result = result;
            _overlaySession = AlgorithmOverlayRenderer.Apply(image, draw, result);
            SamplesGrid.ItemsSource = ToTable(samples).DefaultView;
            double count = measurements.Measurements.Single(item => item.Name == "profile.sample_count").Value;
            double length = measurements.Measurements.Single(item => item.Name == "profile.path_length_pixels").Value;
            double millimetres = measurements.Measurements.Single(item => item.Name == "profile.path_length_millimetres").Value;
            SummaryText.Text = $"采样点：{count:N0}；路径：{length:G8} px / {millimetres:G8} mm。非有限值在表格中保留状态，在曲线中显示为间断。";
            Render(samples);
            Closed += (_, _) => DisposeOwnedState();
        }

        private void Render(AlgorithmTableArtifact table)
        {
            ProfilePlot.Plot.Clear();
            double[] distances = table.Rows.Select(row => row["DistancePixels"].GetDouble()).ToArray();
            string[] channels = table.Columns
                .Select(column => column.Name)
                .Where(name => table.Columns.Any(column => column.Name == name + "Status"))
                .ToArray();
            foreach (string channel in channels)
            {
                double[] values = table.Rows.Select(row => row[channel].ValueKind == JsonValueKind.Number ? row[channel].GetDouble() : double.NaN).ToArray();
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

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "image-profile.csv", AddExtension = true };
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
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "image-profile.json", AddExtension = true };
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

        private void DisposeOwnedState()
        {
            if (_disposed) return;
            _disposed = true;
            _overlaySession.Dispose();
            _result.Dispose();
        }
    }
}
