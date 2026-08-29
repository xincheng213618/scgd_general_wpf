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

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.MoireAnalysis
{
    public partial class MoireAnalysisResultWindow : Window
    {
        private readonly AlgorithmResult _result;
        private bool _disposed;

        public MoireAnalysisResultWindow(AlgorithmResult result)
        {
            AlgorithmImageArtifact spectrum = result.GetArtifact<AlgorithmImageArtifact>("moire-magnitude-spectrum") ?? throw new ArgumentException("Missing spectrum.", nameof(result));
            AlgorithmImageArtifact heatmap = result.GetArtifact<AlgorithmImageArtifact>("moire-frequency-heatmap") ?? throw new ArgumentException("Missing heatmap.", nameof(result));
            AlgorithmMeasurementArtifact summary = result.GetArtifact<AlgorithmMeasurementArtifact>("moire-analysis-summary") ?? throw new ArgumentException("Missing summary.", nameof(result));
            AlgorithmTableArtifact suggestions = result.GetArtifact<AlgorithmTableArtifact>("moire-notch-suggestions") ?? throw new ArgumentException("Missing suggestions.", nameof(result));
            InitializeComponent();
            _result = result;
            SpectrumPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(spectrum.Image);
            HeatmapPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(heatmap.Image);
            AlgorithmImageArtifact? filtered = result.GetArtifact<AlgorithmImageArtifact>("moire-filtered-luminance");
            FilteredTab.Visibility = filtered == null ? Visibility.Collapsed : Visibility.Visible;
            if (filtered != null) FilteredPreview.Source = ImageAlgorithmInputFactory.ToWriteableBitmap(filtered.Image);
            SuggestionsGrid.ItemsSource = ToTable(suggestions).DefaultView;
            double score = Value(summary, "moire.score");
            SummaryText.Text = $"周期频谱证据评分={score:F2}/100（{Classification(score)}）；候选={Value(summary, "moire.candidate_count"):N0}；候选功率比例={Value(summary, "moire.candidate_power_fraction"):P2}；最大突出度={Value(summary, "moire.maximum_prominence"):G6}。评分是频谱证据，不是摩尔纹成因证明。";
            Closed += (_, _) => DisposeOwnedState();
        }

        private void SaveFiltered_Click(object sender, RoutedEventArgs e)
        {
            if (FilteredPreview.Source is not BitmapSource source) return;
            SaveFileDialog dialog = new() { Filter = "TIFF 文件 (*.tif)|*.tif", FileName = "moire-filtered-luminance.tif", AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (File.Exists(dialog.FileName)) throw new IOException($"拒绝覆盖已有文件：{dialog.FileName}");
                TiffBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(source));
                using FileStream stream = File.Open(dialog.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None); encoder.Save(stream);
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e) => Export(false);
        private void ExportJson_Click(object sender, RoutedEventArgs e) => Export(true);
        private void Export(bool json)
        {
            SaveFileDialog dialog = new() { Filter = json ? "JSON 文件 (*.json)|*.json" : "CSV 文件 (*.csv)|*.csv", FileName = json ? "moire-analysis.json" : "moire-analysis.csv", AddExtension = true, OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                if (json) AlgorithmResultExporter.ExportJson(_result, dialog.FileName);
                else AlgorithmResultExporter.ExportCsvBundle(_result, dialog.FileName);
            }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void DisposeOwnedState()
        {
            if (_disposed) return; _disposed = true;
            SpectrumPreview.Source = HeatmapPreview.Source = FilteredPreview.Source = null;
            SuggestionsGrid.ItemsSource = null;
            _result.Dispose();
        }

        private static string Classification(double score) => score switch { < 20 => "低", < 50 => "中", < 75 => "高", _ => "很高" };
        private static double Value(AlgorithmMeasurementArtifact artifact, string name) => artifact.Measurements.Single(value => value.Name == name).Value;
        private static DataTable ToTable(AlgorithmTableArtifact artifact)
        {
            DataTable table = new(artifact.Name);
            foreach (AlgorithmTableColumn column in artifact.Columns) table.Columns.Add(column.Name, typeof(string));
            foreach (IReadOnlyDictionary<string, JsonElement> source in artifact.Rows)
            {
                DataRow row = table.NewRow();
                foreach (AlgorithmTableColumn column in artifact.Columns)
                    row[column.Name] = source.TryGetValue(column.Name, out JsonElement value) ? Format(value) : string.Empty;
                table.Rows.Add(row);
            }
            return table;
        }

        private static string Format(JsonElement value)
            => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)
                ? number.ToString("G12", CultureInfo.InvariantCulture)
                : value.ToString();
    }
}
