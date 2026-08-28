using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.BlobAnalysis
{
    public partial class BlobAnalysisResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private readonly IDisposable _overlaySession;
        private bool _disposed;

        public BlobAnalysisResultWindow(AlgorithmResult result, ImageProcessingContext image, DrawEditorContext draw)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmTableArtifact components = result.GetArtifact<AlgorithmTableArtifact>("blob-components")
                ?? throw new ArgumentException("The result has no blob component table.", nameof(result));
            InitializeComponent();
            _result = result;
            _overlaySession = AlgorithmOverlayRenderer.Apply(image, draw, result);
            ComponentsGrid.ItemsSource = ToTable(components).DefaultView;
            SummaryText.Text = $"候选：{Measurement("blob.candidate_count"):N0}；接受：{Measurement("blob.accepted_count"):N0}；"
                + $"拒绝：{Measurement("blob.rejected_count"):N0}；前景像素：{Measurement("blob.foreground_pixel_count"):N0}。"
                + " Confidence 为区域填充率，并非分类概率。";
            Closed += (_, _) => DisposeOwnedState();
        }

        private double Measurement(string name)
            => _result.GetArtifact<AlgorithmMeasurementArtifact>("blob-summary")!.Measurements.Single(item => item.Name == name).Value;

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
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "blob-components.csv", AddExtension = true };
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
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "blob-components.json", AddExtension = true };
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
