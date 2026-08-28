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

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ContourAnalysis
{
    public partial class ContourAnalysisResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private readonly IDisposable _overlaySession;
        private bool _disposed;

        public ContourAnalysisResultWindow(AlgorithmResult result, ImageProcessingContext image, DrawEditorContext draw)
        {
            ArgumentNullException.ThrowIfNull(result);
            AlgorithmTableArtifact contours = result.GetArtifact<AlgorithmTableArtifact>("contours")
                ?? throw new ArgumentException("The result has no contour table.", nameof(result));
            InitializeComponent();
            _result = result;
            _overlaySession = AlgorithmOverlayRenderer.Apply(image, draw, result);
            ContoursGrid.ItemsSource = ToTable(contours).DefaultView;
            SummaryText.Text = $"候选：{Measurement("contour.candidate_count"):N0}；接受：{Measurement("contour.accepted_count"):N0}；"
                + $"拒绝：{Measurement("contour.rejected_count"):N0}；结构化点：{Measurement("contour.structured_point_count"):N0}。"
                + " Confidence 为轮廓实心度，并非分类概率。";
            Closed += (_, _) => DisposeOwnedState();
        }

        private double Measurement(string name)
            => _result.GetArtifact<AlgorithmMeasurementArtifact>("contour-summary")!.Measurements.Single(item => item.Name == name).Value;

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
            SaveFileDialog dialog = new() { Filter = "CSV 文件 (*.csv)|*.csv", FileName = "contours.csv", AddExtension = true };
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
            SaveFileDialog dialog = new() { Filter = "JSON 文件 (*.json)|*.json", FileName = "contours.json", AddExtension = true };
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
