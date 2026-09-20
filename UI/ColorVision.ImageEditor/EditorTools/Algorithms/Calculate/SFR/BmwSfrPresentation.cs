using ColorVision.Core;
using System;
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
        "target_not_found" => "搜索框内未找到完整 BMW 靶标",
        "multiple_targets_in_search_roi" => "搜索框内有多个靶标，请分开绘制矩形",
        "invalid_search_roi" => "搜索矩形为空或超出原图",
        "edge_roi_out_of_bounds" => "边缘测量区域超出原图",
        "channel_missing" => "输入不包含该通道",
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
    public static string OverlayLabel(BmwEdgeAnalysis edge)
    {
        var luminance = Channel(edge,"L");
        string metric = luminance is { Valid: true } ? luminance.Mtf50.HasValue ? Number(luminance.Mtf50) : "未交叉" : "INVALID";
        return $"{EdgeName(edge.Id)}  L MTF50 {metric}";
    }
    public static string ChannelState(BmwEdgeAnalysis edge, string name)
    {
        var c = Channel(edge,name);
        if (c == null) return edge.Analysis == null ? Reason(edge.Reason) : Reason("channel_missing");
        if (!c.Valid) return "INVALID · " + Reason(c.Reason);
        return c.Warnings.Length == 0 ? "可计算" : string.Join("；", c.Warnings.Select(Reason));
    }
}
