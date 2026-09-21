using ColorVision.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

internal static class BmwSfrPresentation
{
    public static readonly string[] ChannelNames = ["L", "R", "G", "B"];
    public static string EdgeName(BmwEdgeId id) => id switch { BmwEdgeId.Left => "左", BmwEdgeId.Top => "上", BmwEdgeId.Right => "右", _ => "下" };
    public static string Number(double? value) => value?.ToString("F4", CultureInfo.CurrentCulture) ?? "—";
    public static string Reason(string reason) => reason switch
    {
        "target_not_found" => "搜索框内未找到支持的靶标",
        "checkerboard_corner_not_found" => "未找到可测的棋盘交叉点，请将交叉点放在框中心并保留四侧格面",
        "checkerboard_insufficient_edge_support" => "交叉点已定位，但这侧刃边范围不足，请向该侧增大外部选框",
        "ambiguous_checkerboard_corners" => "多个棋盘交叉点同样接近框中心，请移动或缩小外框",
        "checkerboard_roi_crosses_junction" => "小框超出该段棋盘边的安全范围，请减小尺寸或调整距中心距离",
        "multiple_targets_in_search_roi" => "搜索框内有多个靶标，请分开绘制矩形",
        "invalid_search_roi" => "搜索矩形为空或超出原图",
        "edge_roi_out_of_bounds" => "边缘测量区域超出原图",
        "channel_missing" => "输入不包含该通道",
        "roi_changed" => "测量框已调整，等待重新计算",
        "ok" or "" => "",
        _ => SfrSimplePlotWindow.Explain(reason)
    };
    public static SfrChannelAnalysis? Channel(BmwEdgeAnalysis edge, string name) => edge.Analysis?.Channels.FirstOrDefault(c => c.Channel == name);
    public static double? Mtf50(BmwEdgeAnalysis edge, string name) => Channel(edge, name) is { Valid: true } c ? c.Mtf50 : null;
    public static string State(BmwEdgeAnalysis edge)
    {
        int valid = edge.Analysis?.Channels.Count(c => c.Valid) ?? 0;
        int count = edge.Analysis?.Channels.Count ?? 0;
        return valid == 0 ? "INVALID" : valid == count ? $"{valid}/{count} 可计算" : $"{valid}/{count} 部分可计算";
    }
    public static bool HasCenter(BmwTargetAnalysis? target) => target is { Located: true } && double.IsFinite(target.CenterX) && double.IsFinite(target.CenterY);
    public static string OverlayLabel(BmwEdgeAnalysis edge, string channel, BmwSfrOverlaySettings? settings = null, BmwTargetAnalysis? target = null)
    {
        settings ??= new();
        var result = Channel(edge,channel);
        bool response = settings.Metric is BmwSfrDisplayMetric.AtFrequency or BmwSfrDisplayMetric.AtNyquist;
        double frequency = settings.Metric == BmwSfrDisplayMetric.AtNyquist ? .5 : settings.Frequency;
        string name = response ? $"MTF@{frequency:G}" : settings.Metric == BmwSfrDisplayMetric.Mtf10 ? "MTF10" : "MTF50";
        double? value = result is not { Valid: true } ? null : response ? SfrCurveQueries.AtFrequency(result.Frequencies, result.Mtf, frequency)
            : settings.Metric == BmwSfrDisplayMetric.Mtf10 ? result.Mtf10 : result.Mtf50;
        string metric = result is not { Valid: true } ? "INVALID" : value.HasValue ? response ? value.Value.ToString("P1", CultureInfo.CurrentCulture) : Number(value)
            : response ? "无数据" : "未交叉";
        string label = settings.ShowEdgeNames ? EdgeName(edge.Id) : "";
        if (settings.ShowValues) label += (label.Length > 0 ? "  " : "") + (settings.CompactMetricLabels ? metric : $"{channel} {name} {metric}");
        var geometry = new List<string>();
        if (settings.ShowRoiDimensions) geometry.Add($"{edge.Roi.Width}×{edge.Roi.Height} px");
        if (settings.ShowCenterDistance && HasCenter(target))
        {
            double dx = edge.Roi.X + edge.Roi.Width / 2.0 - target!.CenterX, dy = edge.Roi.Y + edge.Roi.Height / 2.0 - target.CenterY;
            geometry.Add($"距中心 {Math.Sqrt(dx * dx + dy * dy):F1} px");
        }
        return label + (geometry.Count > 0 ? (label.Length > 0 ? "\n" : "") + string.Join(" · ", geometry) : "");
    }
    public static string ChannelState(BmwEdgeAnalysis edge, string name)
    {
        var c = Channel(edge,name);
        if (c == null) return edge.Analysis == null ? Reason(edge.Reason) : Reason("channel_missing");
        if (!c.Valid) return "INVALID · " + Reason(c.Reason);
        return c.Warnings.Length == 0 ? "可计算" : string.Join("；", c.Warnings.Select(Reason));
    }
}
