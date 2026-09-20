namespace CameraTest.Application;

public static class ColorShiftPresentation
{
    public static string Explain(string reason) => string.Join("；", reason.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ExplainPart));

    private static string ExplainPart(string reason)
    {
        int separator = reason.IndexOf(':');
        if (separator > 0) return reason[..separator] + "：" + ExplainPart(reason[(separator + 1)..].Trim());
        return reason switch
        {
            "ok" or "" => "有效",
            "channel_missing" => "缺少颜色通道（需 RGB 图像）",
            "invalid_channel" => "通道测量无效",
            "target_not_found" => "搜索框内未找到完整靶标",
            "multiple_targets_in_search_roi" => "搜索框内有多个靶标",
            "edge_roi_out_of_bounds" => "测量框超出搜索范围，请减小尺寸或距中心距离",
            "edge_fit_unavailable" => "未获得刃边拟合位置",
            "inconsistent_edge_orientation" => "RGB 刃边方向不一致",
            "invalid_esf" => "没有有效的边缘曲线",
            "esf_crossing_not_found" => "未找到 50% 边缘交点",
            "ambiguous_esf_crossing" => "50% 边缘交点不唯一",
            "textured_or_noisy_plateaus" => "平台纹理或噪声过强，检查通道图像和曝光",
            "low_contrast_or_no_edge" => "对比度不足或缺少完整刃边",
            "multiple_or_noisy_edges" => "存在多条刃边或明显纹理噪声",
            "roi_too_small" => "测量框太小",
            "edge_angle_out_of_range" => "刃边倾角不在 1°～15°范围",
            "edge_fit_residual_too_large" => "刃边弯曲、破碎或拟合不稳定",
            "insufficient_edge_support" => "刃边离测量框边界过近",
            "insufficient_subpixel_coverage" => "亚像素采样覆盖不足",
            "unknown_input_encoding" => "输入编码未知，仅供诊断",
            "clipped_pixels" => "像素存在削顶风险",
            _ => reason
        };
    }

    public static IReadOnlyList<ColorShiftRow> Rows(FrameAnalysis result) => result.ColorShifts.SelectMany(edge => edge.Analysis.Pairs.Select(pair =>
        new ColorShiftRow(edge.Target, edge.Edge, pair.Pair, edge.Analysis.Axis, pair.Valid ? pair.NormalShiftPixels : null,
            pair.Valid ? pair.Warnings.Length == 0 ? "有效" : "有效 · " + Explain(string.Join(";", pair.Warnings)) : "不可计算 · " + Explain(pair.Reason), edge.Analysis))).ToArray();

    public static string Summary(IReadOnlyList<ColorShiftRow> rows)
    {
        if (rows.Count == 0) return "分析图像后显示 R−G、R−B、G−B 的刃边位移。";
        int valid = rows.Count(row => row.Shift.HasValue);
        return $"RGB 刃边位移 · 有效 {valid}/{rows.Count} 组 · 单位 px" + (valid == 0 ? "\n当前通道不满足测量条件，请查看下方原因；缺测不代表色差为零。" : "\n正值表示前一通道相对后一通道沿公共法线正方向偏移。");
    }
}
