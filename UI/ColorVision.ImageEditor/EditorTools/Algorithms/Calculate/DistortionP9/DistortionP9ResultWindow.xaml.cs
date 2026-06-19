#pragma warning disable CS8604
using ColorVision.Themes;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.DistortionP9
{
    public partial class DistortionP9ResultWindow : Window
    {
        private readonly DistortionP9NativeResult _result;
        private readonly string _json;

        public DistortionP9ResultWindow(DistortionP9NativeResult result, string rawJson)
        {
            InitializeComponent();
            this.ApplyCaption();

            _result = result;
            _json = FormatJson(rawJson);

            SummaryText.Text = $"点数: {_result.Count} / 候选: {_result.CandidateCount}";
            StatusText.Text = BuildStatusText();
            MetricsGrid.ItemsSource = BuildMetricRows(_result.Metrics);
            PointsGrid.ItemsSource = _result.Points;
            JsonText.Text = _json;
        }

        private string BuildStatusText()
        {
            if (_result.Metrics == null)
            {
                return _result.Message ?? string.Empty;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Horizontal TV: {0:F5}%    Vertical TV: {1:F5}%    Formula: ((edge1 + edge2) / 2 - center) / center",
                _result.Metrics.HorizontalTvPercent,
                _result.Metrics.VerticalTvPercent);
        }

        private static DistortionP9MetricRow[] BuildMetricRows(DistortionP9Metrics? metrics)
        {
            if (metrics == null)
            {
                return Array.Empty<DistortionP9MetricRow>();
            }

            return new[]
            {
                Row("Horizontal TV", metrics.HorizontalTvPercent, "%", "横向 TV 畸变"),
                Row("Vertical TV", metrics.VerticalTvPercent, "%", "竖向 TV 畸变"),
                Row("Top", metrics.TopPercent, "%", "上边中点相对上下边弦的弯曲"),
                Row("Bottom", metrics.BottomPercent, "%", "下边中点相对上下边弦的弯曲"),
                Row("Left", metrics.LeftPercent, "%", "左边中点相对左右边弦的弯曲"),
                Row("Right", metrics.RightPercent, "%", "右边中点相对左右边弦的弯曲"),
                Row("Keystone H", metrics.KeystoneHorizontalPercent, "%", "上/下边宽度差"),
                Row("Keystone V", metrics.KeystoneVerticalPercent, "%", "左/右边高度差"),
                Row("Top Width", metrics.TopWidth, "px", "上边宽度"),
                Row("Middle Width", metrics.MiddleWidth, "px", "中边宽度"),
                Row("Bottom Width", metrics.BottomWidth, "px", "下边宽度"),
                Row("Left Height", metrics.LeftHeight, "px", "左边高度"),
                Row("Center Height", metrics.CenterHeight, "px", "中边高度"),
                Row("Right Height", metrics.RightHeight, "px", "右边高度"),
            };
        }

        private static DistortionP9MetricRow Row(string name, double value, string unit, string description)
            => new(name, value.ToString("F5", CultureInfo.InvariantCulture), unit, description);

        private static string FormatJson(string json)
        {
            try
            {
                return JToken.Parse(json).ToString(Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"DistortionP9_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, _json, Encoding.UTF8);
            MessageBox.Show($"已导出:\n{dialog.FileName}", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"DistortionP9_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, BuildCsv(), Encoding.UTF8);
            MessageBox.Show($"已导出:\n{dialog.FileName}", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyJson_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_json);
        }

        private string BuildCsv()
        {
            StringBuilder builder = new();
            builder.AppendLine("Section,Name,Value,Unit,Description");
            foreach (DistortionP9MetricRow row in BuildMetricRows(_result.Metrics))
            {
                AppendCsvLine(builder, "Metric", row.Name, row.Value, row.Unit, row.Description);
            }

            builder.AppendLine();
            builder.AppendLine("Section,Id,Name,Row,Col,X,Y,Area,BoundingRect");
            foreach (DistortionP9Point point in _result.Points)
            {
                AppendCsvLine(
                    builder,
                    "Point",
                    point.Id.ToString(CultureInfo.InvariantCulture),
                    point.Name ?? string.Empty,
                    point.Row.ToString(CultureInfo.InvariantCulture),
                    point.Col.ToString(CultureInfo.InvariantCulture),
                    point.X.ToString("F5", CultureInfo.InvariantCulture),
                    point.Y.ToString("F5", CultureInfo.InvariantCulture),
                    point.Area.ToString(CultureInfo.InvariantCulture),
                    point.BoundingRectDisplay);
            }

            return builder.ToString();
        }

        private static void AppendCsvLine(StringBuilder builder, params string[] values)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(EscapeCsv(values[i]));
            }
            builder.AppendLine();
        }

        private static string EscapeCsv(string value)
        {
            value ??= string.Empty;
            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    public sealed record DistortionP9MetricRow(string Name, string Value, string Unit, string Description);
}
