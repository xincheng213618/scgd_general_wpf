using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.Core;

public enum BmwEdgeId { Left, Top, Right, Bottom }
public sealed record BmwSearchRegion(string Id, RoiRect Roi);
public sealed record BmwEdgeAnalysis(BmwEdgeId Id, RoiRect Roi, bool Valid, string Reason, SfrAnalysisResult? Analysis)
{
    public RoiRect SupportRoi { get; init; }
}
public sealed record BmwTargetAnalysis(string Id, RoiRect SearchRoi, bool Located, string Reason,
    RoiRect TargetRoi, double CenterX, double CenterY, IReadOnlyList<BmwEdgeAnalysis> Edges)
{
    public SfrChartType? DetectedChartType { get; init; }
    public string ChartTypeText => DetectedChartType switch { SfrChartType.Checkerboard => "棋盘格", SfrChartType.Bmw => "BMW", _ => Located ? "BMW" : "未识别" };
}

/// <summary>One explicit search region per target. Coordinates and frequencies refer to original pixels.
/// Caller retains image ownership for the entire synchronous call. No resizing or automatic encoding inference.</summary>
public static class BmwSfrAnalyzer
{
    // Retain the existing public signature for already compiled plugins.
    public static IReadOnlyList<BmwTargetAnalysis> Analyze(HImage image, IReadOnlyList<BmwSearchRegion> regions, SfrAnalysisOptions options)
        => Analyze(image, regions, options, new());

    public static IReadOnlyList<BmwTargetAnalysis> Analyze(HImage image, IReadOnlyList<BmwSearchRegion> regions, SfrAnalysisOptions options, BmwSfrRoiSettings? roiSettings)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(options);
        options = options with { };
        options.Validate();
        roiSettings = roiSettings is null ? new() : roiSettings with { };
        roiSettings.Validate();
        var requests = regions.ToArray();
        if (requests.Length > 256 || requests.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id)) || requests.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != requests.Length)
            throw new ArgumentException("搜索框 ID 必须非空且唯一，最多 256 个。");
        var results = new List<BmwTargetAnalysis>();
        foreach (var request in requests)
        {
            var roi = request.Roi;
            if (roi.X < 0 || roi.Y < 0 || roi.Width <= 0 || roi.Height <= 0 || roi.Width > image.cols || roi.Height > image.rows ||
                roi.X > image.cols - roi.Width || roi.Y > image.rows - roi.Height || roi.Width > 8192 || roi.Height > 8192 || (long)roi.Width * roi.Height > 16_000_000)
            { results.Add(Failed(request, "invalid_search_roi")); continue; }
            IntPtr pointer = IntPtr.Zero;
            try
            {
                int code = roiSettings.ChartType == SfrChartType.Bmw
                    ? OpenCVMediaHelper.M_LocateBmwTargetV1(image, roi, out pointer)
                    : OpenCVMediaHelper.M_LocateSfrTargetV1(image, roi, (int)roiSettings.ChartType, out pointer);
                if (code <= 0 || pointer == IntPtr.Zero) { results.Add(Failed(request, $"localization_error_{code}")); continue; }
                using var json = JsonDocument.Parse(Marshal.PtrToStringUTF8(pointer, code - 1)!);
                var root = json.RootElement;
                if (!root.GetProperty("located").GetBoolean()) { results.Add(Failed(request, root.GetProperty("reason").GetString()!)); continue; }
                var edges = new List<BmwEdgeAnalysis>();
                double centerX = root.GetProperty("centerX").GetDouble(), centerY = root.GetProperty("centerY").GetDouble();
                foreach (var entry in root.GetProperty("edges").EnumerateArray())
                {
                    var id = (BmwEdgeId)entry.GetProperty("id").GetInt32();
                    var edgeRoi = roiSettings.Resolve(id, ReadRect(entry.GetProperty("roi")), centerX, centerY);
                    var support = entry.TryGetProperty("supportRoi", out var supportJson) ? ReadRect(supportJson) : default;
                    string localizationReason = entry.TryGetProperty("reason", out var edgeReason) ? edgeReason.GetString() ?? "" : "";
                    if (localizationReason.Length > 0)
                    {
                        edges.Add(new(id, edgeRoi, false, localizationReason, null) { SupportRoi = support });
                        continue;
                    }
                    SfrAnalysisResult? analysis = null;
                    string reason = "edge_roi_out_of_bounds";
                    if (BmwSfrRoiSettings.IsInside(edgeRoi, roi) && (support.Width == 0 || BmwSfrRoiSettings.IsInside(edgeRoi, support)))
                    {
                        try
                        {
                            analysis = SfrAnalyzer.Analyze(image, edgeRoi, options);
                            reason = string.Join(";", analysis.Channels.Where(c => !c.Valid).Select(c => $"{c.Channel}:{c.Reason}"));
                        }
                        catch (InvalidOperationException ex) { reason = ex.Message; }
                    }
                    else if (support.Width > 0) reason = "checkerboard_roi_crosses_junction";
                    edges.Add(new(id, edgeRoi, analysis != null && analysis.Channels.All(c => c.Valid), reason, analysis) { SupportRoi = support });
                }
                results.Add(new(request.Id, roi, true, "", ReadRect(root.GetProperty("targetRoi")), root.GetProperty("centerX").GetDouble(), root.GetProperty("centerY").GetDouble(), edges)
                { DetectedChartType = root.TryGetProperty("chartType", out var type) && type.GetString() == "checkerboard" ? SfrChartType.Checkerboard : SfrChartType.Bmw });
            }
            catch (EntryPointNotFoundException ex) { throw new InvalidOperationException("原生 DLL 缺少所选图卡定位接口，请使用配套版本。", ex); }
            finally { if (pointer != IntPtr.Zero) _ = OpenCVMediaHelper.FreeResult(pointer); }
        }
        return results;
    }
    private static RoiRect ReadRect(JsonElement value) => new(value.GetProperty("x").GetInt32(), value.GetProperty("y").GetInt32(), value.GetProperty("width").GetInt32(), value.GetProperty("height").GetInt32());
    private static BmwTargetAnalysis Failed(BmwSearchRegion request, string reason) => new(request.Id, request.Roi, false, reason, default, 0, 0,
        Enum.GetValues<BmwEdgeId>().Select(id => new BmwEdgeAnalysis(id, default, false, reason, null)).ToArray());
}
