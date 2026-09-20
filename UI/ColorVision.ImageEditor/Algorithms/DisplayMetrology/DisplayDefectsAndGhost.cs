using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

public sealed partial class DisplayMetrologyProvider
{
    private static float[] Smooth(float[] signal, int width, int height, double sigma)
    {
        using Mat source = Mat.FromPixelData(height, width, MatType.CV_32FC1, signal);
        using Mat result = new();
        Cv2.GaussianBlur(source, result, new Size(0, 0), sigma, sigma, BorderTypes.Reflect101);
        result.GetArray(out float[] values);
        return values;
    }

    private static void MeasureDefects(float[] signal, int width, int height, DisplayDefectParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        int margin = p.BackgroundRadius;
        if (width <= margin * 2 + 8 || height <= margin * 2 + 8)
            throw new MeasurementException("background_region_too_small", "图像须大于背景排除边界两倍加 8 像素；请减小背景平滑尺度。");
        float[] background = Smooth(signal, width, height, p.BackgroundRadius);
        token.ThrowIfCancellationRequested();
        float[] smoothed = Smooth(signal, width, height, 2);
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry>();
        var classification = new byte[signal.Length];
        var residual = new float[signal.Length];
        int brightPoints = 0, darkPoints = 0, lines = 0, mura = 0;
        // Two scales: narrow residual for points/lines, smoothed residual for low-frequency mura.
        foreach (bool lowFrequency in new[] { false, true })
        {
            for (int i = 0; i < signal.Length; i++) residual[i] = lowFrequency ? smoothed[i] - background[i] : signal[i] - smoothed[i];
            foreach (int sign in new[] { 1, -1 })
            {
                var mask = new byte[signal.Length];
                for (int y = margin; y < height - margin; y++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int x = margin; x < width - margin; x++)
                    {
                        int i = y * width + x;
                        double threshold = Math.Max(p.AbsoluteThreshold, p.RelativeThreshold * Math.Max(background[i], p.AbsoluteThreshold));
                        if (sign * residual[i] > threshold) mask[i] = 255;
                    }
                }
                foreach (var region in Components(mask, residual, width, height, token))
                {
                    double aspect = (double)Math.Max(region.Bounds.Width, region.Bounds.Height) / Math.Min(region.Bounds.Width, region.Bounds.Height);
                    string kind;
                    if (lowFrequency)
                    {
                        if (region.Area < p.MinimumMuraArea || aspect >= p.MinimumLineAspect) continue;
                        kind = sign > 0 ? "bright_mura_candidate" : "dark_mura_candidate"; mura++;
                    }
                    else if (aspect >= p.MinimumLineAspect && Math.Max(region.Bounds.Width, region.Bounds.Height) >= p.MinimumLineLength)
                    { kind = sign > 0 ? "bright_line_candidate" : "dark_line_candidate"; lines++; }
                    else if (region.Area <= p.MaximumPointArea)
                    {
                        kind = sign > 0 ? "bright_point_candidate" : "dark_point_candidate";
                        if (sign > 0) brightPoints++; else darkPoints++;
                    }
                    else continue;
                    if (rows.Count == MaximumComponents) throw new MeasurementException("defect_budget_exceeded", "累计候选超过 2048，结果未截断为成功。");
                    bool clipped = region.Bounds.X == margin || region.Bounds.Y == margin
                        || region.Bounds.Right == width - margin || region.Bounds.Bottom == height - margin;
                    rows.Add(Row(("kind", kind), ("x_px", region.Bounds.X), ("y_px", region.Bounds.Y), ("width_px", region.Bounds.Width),
                        ("height_px", region.Bounds.Height), ("area_px2", region.Area), ("peakResidual", region.Peak),
                        ("meanResidual", region.Sum / region.Area), ("touchesAnalysisBoundary", clipped)));
                    shapes.Add(Box($"{kind}-{rows.Count}", region.Bounds));
                    // A region map of reported bounding boxes, explicitly not a segmentation mask.
                    for (int y = region.Bounds.Y; y < region.Bounds.Bottom; y++)
                        for (int x = region.Bounds.X; x < region.Bounds.Right; x++) classification[y * width + x] = lowFrequency ? (byte)127 : (byte)255;
                }
            }
        }
        Table(artifacts, "defect-candidates", ["kind", "x_px", "y_px", "width_px", "height_px", "area_px2", "peakResidual", "meanResidual", "touchesAnalysisBoundary"], rows);
        Metrics(artifacts, ("bright_point_candidates", brightPoints, "count"), ("dark_point_candidates", darkPoints, "count"),
            ("line_candidates", lines, "count"), ("mura_candidates", mura, "count"), ("excluded_border", margin, "px"),
            ("analyzed_pixels", (double)(width - 2 * margin) * (height - 2 * margin), "px²"));
        artifacts.Add(new AlgorithmImageArtifact("candidate-bounds", "visualization", new AlgorithmImageBuffer(width, height, width, AlgorithmImageFormat.Gray8, classification),
            new Dictionary<string, string> { ["semantics"] = "bounding-box-map; 255=point-or-line; 127=mura; not-pixel-segmentation" }));
        Overlay(artifacts, shapes);
    }

    private static void MeasureGhost(float[] signal, int width, int height, GhostMeasurementParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        var primary = new Rect((int)Math.Floor(p.PrimaryX * width), (int)Math.Floor(p.PrimaryY * height),
            (int)Math.Floor(p.PrimaryWidth * width), (int)Math.Floor(p.PrimaryHeight * height));
        if (primary.Width < 3 || primary.Height < 3) throw new MeasurementException("primary_too_small", "主像区域至少需要 3×3 像素。");
        if ((long)primary.Width * primary.Height >= signal.Length) throw new MeasurementException("outside_region_missing", "主像区域必须留出外部杂散光测量区。");
        var residual = new float[signal.Length];
        double primarySum = 0, primaryPeak = 0, outsideSum = 0;
        for (int y = 0; y < height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                residual[i] = (float)Math.Max(0, signal[i] - p.Background);
                if (primary.Contains(x, y))
                {
                    if (signal[i] >= 1) throw new MeasurementException("saturated_primary", "主像包含满量程像素，强度比不可可靠测量。");
                    primarySum += residual[i]; primaryPeak = Math.Max(primaryPeak, residual[i]);
                }
                else outsideSum += residual[i];
            }
        }
        if (primaryPeak <= 1e-6) throw new MeasurementException("primary_missing", "主像区域没有高于背景的有效信号。");
        var mask = new byte[signal.Length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (!primary.Contains(x, y) && residual[y * width + x] >= primaryPeak * p.RelativeThreshold) mask[y * width + x] = 255;
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry> { Box("primary", primary) };
        foreach (var candidate in Components(mask, residual, width, height, token).Where(v => v.Area >= p.MinimumArea))
        {
            bool touchesPrimary = candidate.Bounds.IntersectsWith(new Rect(Math.Max(0, primary.X - 1), Math.Max(0, primary.Y - 1), primary.Width + 2, primary.Height + 2));
            double aspect = (double)Math.Max(candidate.Bounds.Width, candidate.Bounds.Height) / Math.Min(candidate.Bounds.Width, candidate.Bounds.Height);
            string kind = touchesPrimary ? "connected_flare_candidate" : aspect >= 5 ? "streak_candidate" : "ghost_or_stray_light_candidate";
            rows.Add(Row(("kind", kind), ("x_px", candidate.X), ("y_px", candidate.Y), ("area_px2", candidate.Area),
                ("width_px", candidate.Bounds.Width), ("height_px", candidate.Bounds.Height),
                ("peakOverPrimaryPeak", candidate.Peak / primaryPeak), ("energyOverPrimaryRegion", candidate.Sum / primarySum)));
            shapes.Add(Box($"{kind}-{rows.Count}", candidate.Bounds));
        }
        Table(artifacts, "stray-light-candidates", ["kind", "x_px", "y_px", "area_px2", "width_px", "height_px", "peakOverPrimaryPeak", "energyOverPrimaryRegion"], rows);
        Metrics(artifacts, ("candidate_count", rows.Count, "count"), ("primary_peak_above_background", primaryPeak, "relative-signal"),
            ("outside_integral_over_primary_region", outsideSum / primarySum, "ratio"),
            ("outside_mean_over_primary_peak", outsideSum / (signal.Length - primary.Width * primary.Height) / primaryPeak, "ratio"));
        Overlay(artifacts, shapes);
    }
}
