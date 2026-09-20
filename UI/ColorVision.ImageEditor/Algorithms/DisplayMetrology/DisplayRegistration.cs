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
    private sealed record Target(bool Valid, string Reason, double X = 0, double Y = 0);

    private static Target LocateTarget(float[] image, int width, Rect tile, double contrast, double threshold, CancellationToken token)
    {
        var values = new float[tile.Width * tile.Height];
        for (int y = 0; y < tile.Height; y++) Array.Copy(image, (tile.Y + y) * width + tile.X, values, y * tile.Width, tile.Width);
        float minimum = values.Min(), maximum = values.Max();
        if (maximum - minimum < contrast) return new(false, "low_contrast");
        var mask = new byte[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] -= minimum;
            if (values[i] >= (maximum - minimum) * threshold) mask[i] = 255;
        }
        var blobs = Components(mask, values, tile.Width, tile.Height, token).Where(b => b.Area >= 3).OrderByDescending(b => b.Sum).ToArray();
        if (blobs.Length == 0) return new(false, "target_missing");
        if (blobs.Length > 1 && blobs[1].Sum > blobs[0].Sum * 0.05) return new(false, "ambiguous_targets");
        var target = blobs[0];
        if (target.Bounds.X <= 1 || target.Bounds.Y <= 1 || target.Bounds.Right >= tile.Width - 1 || target.Bounds.Bottom >= tile.Height - 1)
            return new(false, "target_clipped");
        return new(true, "", target.X + tile.X, target.Y + tile.Y);
    }

    private static void MeasureRgb(AlgorithmExecutionContext context, RgbRegistrationParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        var input = context.Inputs[0].Image;
        float[] green = ReadSignal(input, 1, p.DecodeExponent, token);
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry>();
        var errors = new List<double>();
        foreach (int channel in new[] { 2, 0 })
        {
            float[] moving = ReadSignal(input, channel, p.DecodeExponent, token);
            foreach (var (index, bounds) in Grid(input.Width, input.Height, p))
            {
                token.ThrowIfCancellationRequested();
                Target reference = LocateTarget(green, input.Width, bounds, p.MinimumContrast, p.TargetThreshold, token);
                Target target = LocateTarget(moving, input.Width, bounds, p.MinimumContrast, p.TargetThreshold, token);
                bool valid = reference.Valid && target.Valid;
                double dx = target.X - reference.X, dy = target.Y - reference.Y;
                string pair = channel == 2 ? "R-G" : "B-G";
                rows.Add(Row(("cell", index), ("pair", pair), ("valid", valid), ("reason", !reference.Valid ? "G:" + reference.Reason : target.Reason),
                    ("referenceX_px", reference.Valid ? reference.X : null), ("referenceY_px", reference.Valid ? reference.Y : null),
                    ("dx_px", valid ? dx : null), ("dy_px", valid ? dy : null), ("distance_px", valid ? Math.Sqrt(dx * dx + dy * dy) : null)));
                if (!valid) continue;
                errors.Add(Math.Sqrt(dx * dx + dy * dy));
                shapes.Add(new AlgorithmGeometry($"{pair}-{index}", AlgorithmGeometryKind.Line,
                    [new AlgorithmPoint(reference.X, reference.Y), new AlgorithmPoint(target.X, target.Y)]));
            }
        }
        if (errors.Count == 0) throw new MeasurementException("no_valid_targets", "没有可测目标。每个网格中必须有一个完整、独立的亮目标。");
        Table(artifacts, "RGB-displacement", ["cell", "pair", "valid", "reason", "referenceX_px", "referenceY_px", "dx_px", "dy_px", "distance_px"], rows);
        Metrics(artifacts, ("valid_pairs", errors.Count, "count"), ("invalid_pairs", rows.Count - errors.Count, "count"),
            ("maximum_color_displacement", errors.Max(), "px"), ("rms_color_displacement", Math.Sqrt(errors.Average(v => v * v)), "px"));
        Overlay(artifacts, shapes);
    }

    private static double TileMean(float[] image, int width, Rect tile)
    {
        double sum = 0;
        for (int y = tile.Y; y < tile.Bottom; y++)
            for (int x = tile.X; x < tile.Right; x++) sum += image[y * width + x];
        return sum / ((long)tile.Width * tile.Height);
    }

    private static void MeasureBinocular(AlgorithmExecutionContext context, BinocularQualityParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        if (context.Inputs[0].Name != "left" || context.Inputs[1].Name != "right")
            throw new MeasurementException("invalid_input_names", "按 left、right 顺序提供左右眼图像。");
        var leftInput = context.Inputs[0].Image;
        float[] left = ReadSignal(leftInput, p.Channel, p.DecodeExponent, token);
        float[] right = ReadSignal(context.Inputs[1].Image, p.Channel, p.DecodeExponent, token);
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var pairs = new List<(Target Left, Target Right)>();
        var shapes = new List<AlgorithmGeometry>();
        var ratios = new List<double>();
        foreach (var (index, bounds) in Grid(leftInput.Width, leftInput.Height, p))
        {
            token.ThrowIfCancellationRequested();
            var a = p.MeasureAlignment ? LocateTarget(left, leftInput.Width, bounds, p.MinimumContrast, 0.5, token) : new Target(false, "alignment_disabled");
            var b = p.MeasureAlignment ? LocateTarget(right, leftInput.Width, bounds, p.MinimumContrast, 0.5, token) : new Target(false, "alignment_disabled");
            bool valid = a.Valid && b.Valid;
            double meanLeft = TileMean(left, leftInput.Width, bounds), meanRight = TileMean(right, leftInput.Width, bounds);
            double? ratio = meanLeft > 1e-8 ? meanRight / meanLeft : null;
            if (ratio.HasValue) ratios.Add(ratio.Value);
            rows.Add(Row(("cell", index), ("alignmentValid", valid), ("reason", !a.Valid ? a.Reason : b.Reason),
                ("dx_px", valid ? b.X - a.X : null), ("dy_px", valid ? b.Y - a.Y : null),
                ("leftSignal", meanLeft), ("rightSignal", meanRight), ("rightOverLeft", ratio)));
            if (valid)
            {
                pairs.Add((a, b));
                shapes.Add(new AlgorithmGeometry($"L-R-{index}", AlgorithmGeometryKind.Line, [new(a.X, a.Y), new(b.X, b.Y)]));
            }
        }
        if (p.MeasureAlignment)
        {
            if (pairs.Count < 3) throw new MeasurementException("insufficient_binocular_targets", "左右眼至少需要三个有效、非共线的对应目标。");
            double lx = pairs.Average(v => v.Left.X), ly = pairs.Average(v => v.Left.Y);
            double rx = pairs.Average(v => v.Right.X), ry = pairs.Average(v => v.Right.Y);
            double xx = 0, yy = 0, xy = 0, dot = 0, cross = 0;
            foreach (var v in pairs)
            {
                double x = v.Left.X - lx, y = v.Left.Y - ly, u = v.Right.X - rx, w = v.Right.Y - ry;
                xx += x * x; yy += y * y; xy += x * y; dot += x * u + y * w; cross += x * w - y * u;
            }
            if (xx * yy - xy * xy <= 1e-8 * (xx + yy) * (xx + yy))
                throw new MeasurementException("degenerate_binocular_geometry", "对应目标共线，无法可靠评价视场倍率和旋转。");
            double a = dot / (xx + yy), b = cross / (xx + yy), tx = rx - a * lx + b * ly, ty = ry - b * lx - a * ly;
            double residual = Math.Sqrt(pairs.Average(v => Math.Pow(a * v.Left.X - b * v.Left.Y + tx - v.Right.X, 2)
                + Math.Pow(b * v.Left.X + a * v.Left.Y + ty - v.Right.Y, 2)));
            Metrics(artifacts, ("valid_alignment_cells", pairs.Count, "count"), ("invalid_alignment_cells", rows.Count - pairs.Count, "count"),
                ("mean_horizontal_disparity", rx - lx, "px"), ("mean_vertical_disparity", ry - ly, "px"),
                ("right_over_left_scale", Math.Sqrt(a * a + b * b), "ratio"), ("right_rotation_clockwise", Math.Atan2(b, a) * 180 / Math.PI, "degree"),
                ("similarity_residual_rms", residual, "px"));
            artifacts.Add(new AlgorithmStructuredDataArtifact("left-to-right-fit", "colorvision.display.binocular-fit/v1", AlgorithmJson.ToElement(new
            { matrix = new[] { a, -b, tx, b, a, ty, 0, 0, 1 }, semantics = "left-pixel-center-to-right-pixel-center", imagesWarped = false })));
        }
        if (ratios.Count == 0) throw new MeasurementException("zero_reference_signal", "左眼所有分区信号为零，不能计算比值。");
        Metrics(artifacts, ("mean_right_over_left_signal", ratios.Average(), "ratio"), ("valid_signal_cells", ratios.Count, "count"));
        Table(artifacts, "binocular-field", ["cell", "alignmentValid", "reason", "dx_px", "dy_px", "leftSignal", "rightSignal", "rightOverLeft"], rows);
        if (leftInput.Format.Channels() >= 3)
        {
            var channelRows = new List<IReadOnlyDictionary<string, JsonElement>>();
            for (int channel = 0; channel < 3; channel++)
            {
                float[] l = channel == p.Channel ? left : ReadSignal(leftInput, channel, p.DecodeExponent, token);
                float[] r = channel == p.Channel ? right : ReadSignal(context.Inputs[1].Image, channel, p.DecodeExponent, token);
                foreach (var (index, bounds) in Grid(leftInput.Width, leftInput.Height, p))
                {
                    token.ThrowIfCancellationRequested();
                    double a = TileMean(l, leftInput.Width, bounds), b = TileMean(r, leftInput.Width, bounds);
                    channelRows.Add(Row(("cell", index), ("channel", channel == 0 ? "B" : channel == 1 ? "G" : "R"),
                        ("leftSignal", a), ("rightSignal", b), ("rightOverLeft", a > 1e-8 ? b / a : null)));
                }
            }
            Table(artifacts, "binocular-channel-consistency", ["cell", "channel", "leftSignal", "rightSignal", "rightOverLeft"], channelRows);
        }
        Overlay(artifacts, shapes);
    }
}
