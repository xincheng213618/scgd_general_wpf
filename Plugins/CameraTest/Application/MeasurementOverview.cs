using ColorVision.Core;
using System.Globalization;

namespace CameraTest.Application;

/// <summary>A view of measured rows. Filtering or metric selection never changes the recorded analysis.</summary>
public sealed record MeasurementOverview(string Target, string Edge, IReadOnlyList<MetricRow> Rows, int Metric, double Frequency)
{
    public string EdgeLabel => Direction(Edge);
    public string Y => CellText(Find("Y (L)"), Metric, Frequency);
    public string R => CellText(Find("R"), Metric, Frequency);
    public string G => CellText(Find("G"), Metric, Frequency);
    public string B => CellText(Find("B"), Metric, Frequency);
    public string YHint => Describe(Find("Y (L)"), Metric, Frequency);
    public string RHint => Describe(Find("R"), Metric, Frequency);
    public string GHint => Describe(Find("G"), Metric, Frequency);
    public string BHint => Describe(Find("B"), Metric, Frequency);
    public double? YValue => ChannelValue("Y (L)");
    public double? RValue => ChannelValue("R");
    public double? GValue => ChannelValue("G");
    public double? BValue => ChannelValue("B");
    public string Status => string.Join("；", Rows.Select(r => Explain(r.Status)).Where(s => s.Length > 0).Distinct());
    public string DiagnosticText => string.Join("\n", Rows.Select(row => Describe(row, Metric, Frequency)));
    public MetricRow? Find(string channel) => Rows.FirstOrDefault(r => r.Channel == channel);
    private double? ChannelValue(string channel) => Find(channel) is { } row ? Value(row, Metric, Frequency) : null;

    public static double? Value(MetricRow row, int metric, double frequency)
    {
        if (metric < 2) return metric == 1 ? row.Mtf10 : row.Mtf50;
        string channel = row.Channel == "Y (L)" ? "L" : row.Channel;
        var measured = row.Analysis?.Channels.FirstOrDefault(c => c.Channel == channel && c.Valid);
        return measured == null ? null : SfrCurveQueries.AtFrequency(measured.Frequencies, measured.Mtf, metric == 3 ? .5 : frequency);
    }
    public static string Format(MetricRow? row, int metric, double frequency) => row != null && Value(row, metric, frequency) is { } value && double.IsFinite(value)
        ? value.ToString(metric >= 2 ? "P1" : "F4", CultureInfo.CurrentCulture) : "—";

    private static string CellText(MetricRow? row, int metric, double frequency) => row?.ChannelAnalysis is { Valid: false }
        ? "未通过" : Format(row, metric, frequency);
    public static string Direction(string edge) => edge switch { "Top" => "上", "Bottom" => "下", "Left" => "左", "Right" => "右", _ => edge };
    public static bool Missing(MetricRow row, int metric, double frequency) => Value(row, metric, frequency) is not { } value || !double.IsFinite(value);

    public static string Describe(MetricRow? row, int metric, double frequency)
    {
        if (row == null) return "没有该通道的测量结果。";
        string identity = $"{row.Target} · {Direction(row.Edge)}边 · {(row.Channel == "Y (L)" ? "Y" : row.Channel)}";
        if (row.ChannelAnalysis is { Valid: false }) return $"{identity}：未通过测量质量检查\n{Diagnostic(row)}";
        if (row.ChannelAnalysis == null) return $"{identity}：{Diagnostic(row)}";
        if (Missing(row, metric, frequency))
            return metric < 2
                ? $"{identity}：曲线有效，但在已测频率范围内未穿过 {(metric == 1 ? "10%" : "50%")}，无法确定 MTF{(metric == 1 ? "10" : "50")}。可切换指标或查看曲线。"
                : $"{identity}：曲线有效，但不覆盖所选频率，无法给出该频率的响应。";
        return $"{identity}：{Format(row, metric, frequency)}\n{Diagnostic(row)}";
    }

    public static string Diagnostic(MetricRow row)
    {
        var channel = row.ChannelAnalysis;
        string evidence = channel?.Reason switch
        {
            "edge_angle_out_of_range" when channel.FitAvailable => $"实测倾角 {Math.Abs(channel.AngleDegrees):F2}°；允许 1°–15°。",
            "edge_fit_residual_too_large" when channel.FitAvailable => $"实测拟合残差 {channel.FitRms:F3} px；上限 {row.Options.MaximumFitRms:G} px。",
            "low_contrast_or_no_edge" when channel.PlateausAvailable => $"实测边缘跨度 {channel.Contrast:P1}；下限 {row.Options.MinimumContrast:P1}。",
            "textured_or_noisy_plateaus" when channel.PlateausAvailable => $"实测边缘信噪比 {channel.Snr:F2}；下限 {row.Options.MinimumSnr:G}。",
            "insufficient_subpixel_coverage" when channel.SamplingAvailable => $"实测采样覆盖率 {channel.BinCoverage:P1}；下限 95%。",
            _ => ""
        };
        string reason = Explain(row.Status);
        if (reason.Length == 0 && channel is { Valid: false }) reason = "测量未通过，未返回具体原因。";
        return evidence.Length == 0 ? reason : $"{evidence}\n{reason}";
    }

    public static MeasurementOverview[] Create(IEnumerable<MetricRow> rows, int metric, double frequency) => rows
        .GroupBy(row => (row.Target, row.Edge)).Select(group => new MeasurementOverview(group.Key.Target, group.Key.Edge, group.ToArray(), metric, frequency)).ToArray();

    public static MetricRow? Lowest(IEnumerable<MetricRow> rows, int metric, double frequency) => rows
        .Where(row => Value(row, metric, frequency) is { } value && double.IsFinite(value)).MinBy(row => Value(row, metric, frequency));

    public static string Explain(string status) => string.Join("；", status.Split([',', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(code => code switch
    {
        "ok" => "",
        "target_not_found" => "未找到完整靶标。请保留周围白边，可扩大选框后重试。",
        "checkerboard_corner_not_found" => "未找到可测棋盘交叉点。请将交叉点放在框中心，四侧保留足够格面。",
        "checkerboard_insufficient_edge_support" => "交叉点已定位，但这侧刃边范围不足。请向该侧增大外部选框。",
        "ambiguous_checkerboard_corners" => "多个棋盘交叉点同样接近框中心。请移动或缩小外框。",
        "checkerboard_roi_crosses_junction" => "小框超出该段棋盘边的安全范围。请减小尺寸或调整中心距离。",
        "multiple_targets_in_search_roi" => "框内有多个靶标，请每个靶标单独画框。",
        "invalid_search_roi" => "选框无效，请检查是否完整位于原图内。",
        "edge_roi_out_of_bounds" => "刃边测量框超出原图，请减小测量框尺寸或调整中心距离。",
        "unknown_input_encoding" => "输入编码未确认，数值仅供调试；出厂判定前请在“输入信号与质量”中确认。",
        "channel_missing" => "图像不包含该通道。",
        "insufficient_contrast" or "low_contrast" => "刃边对比度不足，请检查选框、曝光与照明。",
        "clipping_detected" or "clipped_highlights" => "检测到像素截断，请检查曝光。",
        "clipped_pixels" => "部分像素存在削顶风险，请检查曝光。",
        "roi_too_small" => "测量框太小，请在测量设置中增大尺寸。",
        "low_contrast_or_no_edge" => "对比度不足或没有完整刃边，请检查选框和曝光。",
        "textured_or_noisy_plateaus" => "平台纹理或噪声过强，请检查摩尔纹和曝光。",
        "multiple_or_noisy_edges" => "测量框包含多条刃边或明显噪声。",
        "edge_fit_failed" => "刃边拟合失败，请检查测量框位置。",
        "insufficient_edge_support" => "刃边离测量框边界过近，请调整测量框。",
        "edge_angle_out_of_range" => "刃边倾角超出 1°–15°，建议调整至约 5°。",
        "edge_fit_residual_too_large" => "刃边弯曲或拟合不稳定，请检查靶标和测量框。",
        "insufficient_subpixel_coverage" => "亚像素采样覆盖不足，请检查刃边倾角。",
        "invalid_lsf_dc" => "没有可归一化的有效刃边信号。",
        _ => code
    }).Where(s => s.Length > 0));
}
