using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Core;

public sealed record SfrColorEdge(string Channel, bool Valid, string Reason, string[] Warnings,
    double? AxisCrossingPixels, double[] CommonNormalPositions, double[] Esf);
public sealed record SfrColorShift(string Pair, bool Valid, string Reason, string[] Warnings,
    double? AxisShiftPixels, double? NormalShiftPixels);
public sealed record SfrChromaticAberrationResult(string AlgorithmVersion, string Axis, double NormalX, double NormalY,
    IReadOnlyList<SfrColorEdge> Channels, IReadOnlyList<SfrColorShift> Pairs);

/// <summary>RGB 50% crossing displacement on a common ROI coordinate system. Not Delta E, area CA, or radial lens CA.</summary>
public static class SfrChromaticAberration
{
    public static SfrChromaticAberrationResult Analyze(SfrAnalysisResult? analysis, RoiRect roi)
    {
        var source = analysis?.Channels ?? [];
        var reference = source.FirstOrDefault(c => c.Channel == "G" && c.Valid && c.FitAvailable)
            ?? source.FirstOrDefault(c => c.Channel == "L" && c.Valid && c.FitAvailable)
            ?? source.FirstOrDefault(c => c.Valid && c.FitAvailable);
        bool rotated = reference?.Rotated ?? false;
        double slope = reference?.EdgeSlope ?? 0;
        double cosine = 1 / Math.Sqrt(1 + slope * slope);
        double row = ((rotated ? roi.Width : roi.Height) - 1) / 2.0;
        double origin = reference == null ? 0 : reference.EdgeIntercept + slope * row;
        var edges = new List<SfrColorEdge>();
        foreach (string name in new[] { "R", "G", "B" })
        {
            var channel = source.FirstOrDefault(c => c.Channel == name);
            string reason = channel == null ? "channel_missing" : !channel.Valid ? string.IsNullOrWhiteSpace(channel.Reason) ? "invalid_channel" : channel.Reason : "";
            double? crossing = null;
            if (reason.Length == 0 && channel != null)
            {
                if (roi.Width <= 0 || roi.Height <= 0 || !channel.FitAvailable || !double.IsFinite(channel.EdgeIntercept) || !double.IsFinite(channel.EdgeSlope)) reason = "edge_fit_unavailable";
                else if (channel.Rotated != rotated) reason = "inconsistent_edge_orientation";
                else crossing = FindCrossing(channel.EdgePositions, channel.Esf, out reason);
            }
            if (!crossing.HasValue || channel == null)
            {
                edges.Add(new(name, false, reason, channel?.Warnings ?? [], null, [], []));
                continue;
            }
            // Native V2 ESF positions are relative to each channel's fitted edge, already projected onto its normal.
            // Undo that projection/centering before comparing at the same middle row. All channels share the native 90° orientation.
            double inverseCosine = Math.Sqrt(1 + channel.EdgeSlope * channel.EdgeSlope);
            double center = channel.EdgeIntercept + channel.EdgeSlope * row;
            double axisCrossing = center + crossing.Value * inverseCosine;
            double[] common = channel.EdgePositions.Select(x => (center + x * inverseCosine - origin) * cosine).ToArray();
            edges.Add(new(name, true, "ok", channel.Warnings, axisCrossing, common, (double[])channel.Esf.Clone()));
        }
        var pairs = new List<SfrColorShift>();
        foreach (var (first, second) in new[] { ("R", "G"), ("R", "B"), ("G", "B") })
        {
            var a = edges.Single(c => c.Channel == first);
            var b = edges.Single(c => c.Channel == second);
            var warnings = a.Warnings.Concat(b.Warnings).Distinct().ToArray();
            if (!a.Valid || !b.Valid)
            {
                string reason = string.Join("; ", new[] { a, b }.Where(c => !c.Valid).Select(c => $"{c.Channel}:{c.Reason}"));
                pairs.Add(new($"{first}-{second}", false, reason, warnings, null, null));
            }
            else
            {
                double shift = a.AxisCrossingPixels!.Value - b.AxisCrossingPixels!.Value;
                pairs.Add(new($"{first}-{second}", true, "ok", warnings, shift, shift * cosine));
            }
        }
        // A counterclockwise rotation maps original (x,y) to (y, width-1-x). Positive axis remains original +y.
        return new("1.0", rotated ? "Y" : "X", rotated ? slope * cosine : cosine, rotated ? cosine : -slope * cosine, edges, pairs);
    }

    private static double? FindCrossing(double[] x, double[] y, out string reason)
    {
        reason = "invalid_esf";
        if (!SfrCurveQueries.IsValid(x, y)) return null;
        var crossings = new List<double>();
        for (int i = 0; i < x.Length; i++)
        {
            if (Math.Abs(x[i]) <= 12 && y[i] == 0.5) crossings.Add(x[i]);
            if (i == 0 || x[i] < -12 || x[i - 1] > 12) continue;
            if (y[i - 1] < 0.5 && y[i] > 0.5 || y[i - 1] > 0.5 && y[i] < 0.5)
            {
                double position = x[i - 1] + (0.5 - y[i - 1]) * (x[i] - x[i - 1]) / (y[i] - y[i - 1]);
                if (Math.Abs(position) <= 12) crossings.Add(position);
            }
        }
        reason = crossings.Count == 1 ? "ok" : crossings.Count == 0 ? "esf_crossing_not_found" : "ambiguous_esf_crossing";
        return crossings.Count == 1 ? crossings[0] : null;
    }
}
