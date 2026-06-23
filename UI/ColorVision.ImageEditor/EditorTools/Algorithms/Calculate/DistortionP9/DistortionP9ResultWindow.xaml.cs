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

            SummaryText.Text = $"状态: {(_result.Success ? "OK" : "异常")}    点数: {_result.Count} / 候选: {_result.CandidateCount}";
            StatusText.Text = BuildStatusText();
            MetricsGrid.ItemsSource = BuildMetricRows(_result);
            PointsGrid.ItemsSource = _result.Points.Count > 0 ? _result.Points : _result.CandidatePoints;
            JsonText.Text = _json;
        }

        private string BuildStatusText()
        {
            if (!_result.Success)
            {
                return BuildFailureHint(_result);
            }

            if (_result.Metrics == null)
            {
                return _result.Message ?? string.Empty;
            }

            string warningText = _result.Warnings.Count > 0 ? $"    Warning: {string.Join("; ", _result.Warnings)}" : string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "Horizontal TV: {0:F5}%    Vertical TV: {1:F5}%    Formula: ((edge1 + edge2) / 2 - center) / center{2}",
                _result.Metrics.HorizontalTvPercent,
                _result.Metrics.VerticalTvPercent,
                warningText);
        }

        private static string BuildFailureHint(DistortionP9NativeResult result)
        {
            DistortionP9Diagnostics? diagnostics = result.Diagnostics;
            return result.StatusCode switch
            {
                "no_candidates" => "没有找到有效候选点。优先检查 Rect 是否框到图、阈值是否过高、亮点是否太弱、brightTarget 是否与图像极性一致。",
                "too_few_candidates" => $"候选点不足: 找到 {result.CandidateCount} 个，期望 {result.ExpectedCount} 个。可能有弱光点、少点、ROI 没框全，或点尺寸被过滤。",
                "grid_sort_failed" => "候选点数量足够，但无法稳定排成 3x3。可能有漏光/反光误检、点阵倾斜过大，或 ROI 混入其他亮区。",
                "invalid_image" => "图像为空或通道格式不支持。",
                "invalid_grid_size" => "点阵行列配置无效。",
                _ when diagnostics != null && diagnostics.ExtraCount > 0 => $"候选点过多: 多出 {diagnostics.ExtraCount} 个。可能有漏光、反光或噪声亮区。",
                _ => result.Message ?? "9点畸变计算异常。"
            };
        }

        private static DistortionP9MetricRow[] BuildMetricRows(DistortionP9NativeResult result)
        {
            DistortionP9Metrics? metrics = result.Metrics;
            var rows = new System.Collections.Generic.List<DistortionP9MetricRow>
            {
                RowText("Status", result.Success ? "OK" : "NG", string.Empty, BuildFailureHint(result)),
                RowText("Candidates", result.CandidateCount.ToString(CultureInfo.InvariantCulture), "count", $"期望 {result.ExpectedCount} 个候选点"),
            };

            if (result.Diagnostics != null)
            {
                rows.Add(RowText("Missing", result.Diagnostics.MissingCount.ToString(CultureInfo.InvariantCulture), "count", "候选点不足数量"));
                rows.Add(RowText("Extra", result.Diagnostics.ExtraCount.ToString(CultureInfo.InvariantCulture), "count", "候选点超出数量"));
            }

            foreach (string warning in result.Warnings)
            {
                rows.Add(RowText("Warning", warning, string.Empty, "异常提示"));
            }

            if (metrics == null)
            {
                return rows.ToArray();
            }

            rows.Add(Row("Horizontal TV", metrics.HorizontalTvPercent, "%", "横向 TV 畸变"));
            rows.Add(Row("Vertical TV", metrics.VerticalTvPercent, "%", "竖向 TV 畸变"));
            rows.Add(Row("Top", metrics.TopPercent, "%", "上边中点相对上下边弦的弯曲"));
            rows.Add(Row("Bottom", metrics.BottomPercent, "%", "下边中点相对上下边弦的弯曲"));
            rows.Add(Row("Left", metrics.LeftPercent, "%", "左边中点相对左右边弦的弯曲"));
            rows.Add(Row("Right", metrics.RightPercent, "%", "右边中点相对左右边弦的弯曲"));
            rows.Add(Row("Keystone H", metrics.KeystoneHorizontalPercent, "%", "上/下边宽度差"));
            rows.Add(Row("Keystone V", metrics.KeystoneVerticalPercent, "%", "左/右边高度差"));
            rows.Add(Row("Top Width", metrics.TopWidth, "px", "上边宽度"));
            rows.Add(Row("Middle Width", metrics.MiddleWidth, "px", "中边宽度"));
            rows.Add(Row("Bottom Width", metrics.BottomWidth, "px", "下边宽度"));
            rows.Add(Row("Left Height", metrics.LeftHeight, "px", "左边高度"));
            rows.Add(Row("Center Height", metrics.CenterHeight, "px", "中边高度"));
            rows.Add(Row("Right Height", metrics.RightHeight, "px", "右边高度"));
            return rows.ToArray();
        }

        private static DistortionP9MetricRow Row(string name, double value, string unit, string description)
            => new(name, value.ToString("F5", CultureInfo.InvariantCulture), unit, description);

        private static DistortionP9MetricRow RowText(string name, string value, string unit, string description)
            => new(name, value, unit, description);

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
            foreach (DistortionP9MetricRow row in BuildMetricRows(_result))
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

            if (_result.CandidatePoints.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Section,Id,Name,Row,Col,X,Y,Area,BoundingRect");
                foreach (DistortionP9Point point in _result.CandidatePoints)
                {
                    AppendCsvLine(
                        builder,
                        "Candidate",
                        point.Id.ToString(CultureInfo.InvariantCulture),
                        point.Name ?? string.Empty,
                        point.Row.ToString(CultureInfo.InvariantCulture),
                        point.Col.ToString(CultureInfo.InvariantCulture),
                        point.X.ToString("F5", CultureInfo.InvariantCulture),
                        point.Y.ToString("F5", CultureInfo.InvariantCulture),
                        point.Area.ToString(CultureInfo.InvariantCulture),
                        point.BoundingRectDisplay);
                }
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
