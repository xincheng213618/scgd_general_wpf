using ColorVision.Core;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.GridDistortion
{
    public partial class GridDistortionResultWindow : Window
    {
        private readonly GridDistortionResult _result;
        private readonly GridDistortionAnalysis? _analysis;
        private readonly IReadOnlyList<GridDistortionMetricRow> _rows;
        private readonly string _analysisJson;

        public GridDistortionResultWindow(GridDistortionResult result, GridDistortionAnalysis? analysis)
        {
            if (result.Success && analysis == null) throw new ArgumentException("成功结果需要完整的指标分析。", nameof(analysis));
            _analysis = result.Success ? analysis : null;
            _result = result;
            InitializeComponent();
            this.ApplyCaption();
            SummaryText.Text = result.Success
                ? $"测量完成 · {result.ExpectedRows} × {result.ExpectedCols} · {result.SelectedCount} 个点 · 检测 {result.Timings.TotalMs:F2} ms"
                : $"测量失败 · {result.StatusCode}\n{result.Message}";
            _rows = BuildMetricRows(result, _analysis);
            MetricsGrid.ItemsSource = _rows;
            PointsGrid.ItemsSource = result.Points.OrderBy(p => p.Row).ThenBy(p => p.Col).Select(p => new
            {
                p.Id, p.Row, p.Col, p.X, p.Y, p.Area, p.Contrast, Reference = result.ReferencePointIds.Contains(p.Id) ? "是" : string.Empty
            });
            JsonText.Text = result.RawJson;
            _analysisJson = CreateAnalysisJson(result, _analysis);
            AnalysisJsonText.Text = _analysisJson;
            OpticalGrid.ItemsSource = _analysis?.Optical.Samples;
            GridDistortionQuality quality = result.Quality;
            GridDistortionTimings times = result.Timings;
            DiagnosticsText.Text = FormattableString.Invariant($"""
                状态：{result.StatusCode}
                {result.Message}

                候选点：{result.CandidateCount}；选中：{result.SelectedCount}
                质量分数：{quality.Score:F4}（启发式质量分数，不是正确概率）
                最小对比度：{quality.MinimumContrast:F5}
                点阵残差 / 网格间距：{quality.GridResidualFraction:F5}
                处理缩放：{quality.ProcessingScale:F5}

                预处理：{times.PreprocessMs:F3} ms
                候选检测：{times.CandidatesMs:F3} ms
                点阵排序：{times.GridMs:F3} ms
                原图精修：{times.RefineMs:F3} ms
                指标计算：{times.MetricsMs:F3} ms
                原生检测合计：{times.TotalMs:F3} ms

                警告：{string.Join("; ", result.Warnings)}
                互操作诊断：{result.InteropDiagnostic}

                对边均值 9 点的 Keystone：水平 = (左高 − 右高) / 对边均值 × 100%；垂直 = (上宽 − 下宽) / 对边均值 × 100%。
                旧 P9 方案保留旧口径：三跨度均值作为分母，Keystone 水平/垂直命名与对边均值方案相反。
                单张图没有左右眼配对信息，本次不生成 DIFF_H / DIFF_V。
                """);
            if (_analysis != null)
            {
                GridDistortionOpticalEstimate optical = _analysis.Optical;
                DiagnosticsText.Text += FormattableString.Invariant($"""


                    光学相对估计：{(optical.IsAvailable ? "可用" : "不可用")}
                    方法：{optical.Method}
                    中央节距相对估计，非已标定镜头畸变。
                    {optical.ReferenceDescription}
                    参考中心：({optical.Origin.X:F4}, {optical.Origin.Y:F4}) px
                    列节距：({optical.ColumnPitch.X:F4}, {optical.ColumnPitch.Y:F4}) px
                    行节距：({optical.RowPitch.X:F4}, {optical.RowPitch.Y:F4}) px
                    {string.Join(Environment.NewLine, optical.Warnings)}
                    """);
            }
        }

        internal static IReadOnlyList<GridDistortionMetricRow> BuildMetricRows(GridDistortionResult result, GridDistortionAnalysis? analysis)
        {
            if (!result.Success || result.Metrics is not GridDistortionMetrics m || analysis == null)
                return new[] { new GridDistortionMetricRow("状态", "测量状态", "无有效指标", string.Empty, result.Message) };
            List<GridDistortionMetricRow> rows = new()
            {
                Row("标准 TV", "Horizontal TV", analysis.StandardTv.HorizontalPercent, "%", "(上下宽均值 − 中间宽) / 中间宽 × 100"),
                Row("标准 TV", "Vertical TV", analysis.StandardTv.VerticalPercent, "%", "(左右高均值 − 中间高) / 中间高 × 100"),
                Row("半值 TV", "Horizontal TV", analysis.HalfTv.HorizontalPercent, "%", "标准 Horizontal TV / 2"),
                Row("半值 TV", "Vertical TV", analysis.HalfTv.VerticalPercent, "%", "标准 Vertical TV / 2")
            };
            AddPoint9Rows(rows, "对边均值 9 点", analysis.ReferencePoint9, false);
            AddPoint9Rows(rows, "旧 P9 三跨度", analysis.LegacyPoint9, true);
            GridDistortionOpticalEstimate optical = analysis.Optical;
            if (optical.IsAvailable && optical.OpticRatioPercent.HasValue && optical.MaxAbsoluteRatioPercent.HasValue)
            {
                rows.Add(Row("中央节距估计", "最大径向偏差（带符号）", optical.OpticRatioPercent.Value, "%", "中央节距相对估计，非已标定镜头畸变"));
                rows.Add(Row("中央节距估计", "最大绝对径向偏差", optical.MaxAbsoluteRatioPercent.Value, "%", "max(|实际半径 − 参考半径| / 参考半径) × 100"));
                rows.Add(new("中央节距估计", "最大偏差点 ID", optical.MaxErrorPointId?.ToString(CultureInfo.InvariantCulture) ?? "无", string.Empty, optical.Method));
            }
            else rows.Add(new("中央节距估计", "估计状态", "不可用", string.Empty, string.Join("；", optical.Warnings)));
            rows.AddRange(new[]
            {
                Row("参考跨度", "上边宽度", m.TopWidth, "px", "左上到右上"), Row("参考跨度", "中间宽度", m.MiddleWidth, "px", "左中到右中"),
                Row("参考跨度", "下边宽度", m.BottomWidth, "px", "左下到右下"), Row("参考跨度", "左边高度", m.LeftHeight, "px", "左上到左下"),
                Row("参考跨度", "中间高度", m.CenterHeight, "px", "上中到下中"), Row("参考跨度", "右边高度", m.RightHeight, "px", "右上到右下")
            });
            return rows;
        }

        private static void AddPoint9Rows(List<GridDistortionMetricRow> rows, string method, GridDistortionPoint9Metrics metrics, bool legacy)
        {
            string heightMean = legacy ? "左中右高均值" : "左右高均值";
            string widthMean = legacy ? "上中下宽均值" : "上下宽均值";
            rows.AddRange(new[]
            {
                Row(method, "上边畸变", metrics.TopPercent, "%", $"上中点到上边弦有符号距离 / {heightMean} × 100；向内为正"),
                Row(method, "下边畸变", metrics.BottomPercent, "%", $"下中点到下边弦有符号距离 / {heightMean} × 100；向内为正"),
                Row(method, "左边畸变", metrics.LeftPercent, "%", $"左中点到左边弦有符号距离 / {widthMean} × 100；向内为正"),
                Row(method, "右边畸变", metrics.RightPercent, "%", $"右中点到右边弦有符号距离 / {widthMean} × 100；向内为正"),
                Row(method, "Keystone Horizontal", metrics.KeystoneHorizontalPercent, "%", legacy
                    ? "(上宽 − 下宽) / 上中下宽均值 × 100；保留旧名称" : "(左高 − 右高) / 左右高均值 × 100"),
                Row(method, "Keystone Vertical", metrics.KeystoneVerticalPercent, "%", legacy
                    ? "(左高 − 右高) / 左中右高均值 × 100；保留旧名称" : "(上宽 − 下宽) / 上下宽均值 × 100")
            });
        }

        internal static string CreateAnalysisJson(GridDistortionResult result, GridDistortionAnalysis? analysis)
        {
            JsonElement? nativeResult = null;
            try
            {
                using JsonDocument document = JsonDocument.Parse(result.RawJson);
                nativeResult = document.RootElement.Clone();
            }
            catch (JsonException) { }
            return JsonSerializer.Serialize(new
            {
                formulaVersion = analysis?.FormulaVersion, analysis, nativeResult,
                rawNativeJson = nativeResult.HasValue ? null : result.RawJson,
                invocation = new { result.Success, result.StatusCode, result.Message, result.NativeReturnCode, result.InteropDiagnostic }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
        }

        private static GridDistortionMetricRow Row(string method, string name, double value, string unit, string description) =>
            new(method, name, value.ToString("F6", CultureInfo.InvariantCulture), unit, description);

        private void CopyMetrics_Click(object sender, RoutedEventArgs e) => CopyText("方案\t指标\t数值\t单位\t定义\n" +
            string.Join("\n", _rows.Select(row => $"{row.Method}\t{row.Name}\t{row.Value}\t{row.Unit}\t{row.Description}")));
        private void CopyJson_Click(object sender, RoutedEventArgs e) => CopyText(_result.RawJson);
        private void CopyAnalysisJson_Click(object sender, RoutedEventArgs e) => CopyText(_analysisJson);
        private static void CopyText(string text)
        {
            try { Clipboard.SetText(text); }
            catch (Exception ex) { MessageBox.Show($"复制失败：{ex.Message}", "点阵畸变", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    public sealed record GridDistortionMetricRow(string Method, string Name, string Value, string Unit, string Description);
}
