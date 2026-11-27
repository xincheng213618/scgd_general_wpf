using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

public sealed partial class DisplayMetrologyProvider
{
    private sealed record Target(bool Valid, string Reason, double X = 0, double Y = 0);

    private struct TargetScratch
    {
        public float[]? Values;
        public byte[]? Mask;
        public int[]? Queue;
    }

    private static Target LocateTarget(float[] image, int width, Rect tile, double contrast, double threshold, CancellationToken token, ref TargetScratch scratch)
    {
        float[] values = GetTargetValues(tile, ref scratch);
        for (int y = 0; y < tile.Height; y++) Array.Copy(image, (tile.Y + y) * width + tile.X, values, y * tile.Width, tile.Width);
        return AnalyzeTarget(values, tile, contrast, threshold, token, ref scratch);
    }

    private static Target LocateIntegerRgbTarget(AlgorithmImageBuffer image, int channel, float[] decoder,
        Rect tile, double contrast, double threshold, CancellationToken token, ref TargetScratch scratch)
    {
        float[] values = GetTargetValues(tile, ref scratch);
        int bytes = image.Format.BitsPerChannel() / 8;
        int pixelBytes = 3 * bytes;
        int tileOffset = tile.X * pixelBytes + channel * bytes;
        ReadOnlySpan<byte> source = image.Data.Span;
        for (int y = 0; y < tile.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            int offset = (tile.Y + y) * image.Stride + tileOffset;
            Span<float> destination = values.AsSpan(y * tile.Width, tile.Width);
            if (bytes == 1)
            {
                for (int x = 0; x < destination.Length; x++, offset += pixelBytes)
                    destination[x] = decoder[source[offset]];
            }
            else
            {
                for (int x = 0; x < destination.Length; x++, offset += pixelBytes)
                    destination[x] = decoder[BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, sizeof(ushort)))];
            }
        }
        return AnalyzeTarget(values, tile, contrast, threshold, token, ref scratch);
    }

    private static float[] GetTargetValues(Rect tile, ref TargetScratch scratch)
    {
        int tilePixels = tile.Width * tile.Height;
        if (scratch.Values == null || scratch.Values.Length < tilePixels)
            scratch.Values = new float[tilePixels];
        return scratch.Values;
    }

    private static (float Minimum, float Maximum) FindTargetExtrema(ReadOnlySpan<float> values)
    {
        float minimum = values[0], maximum = values[0];
        int i = 1;
        // The decoder validates that every sample is finite.
        if (Vector256.IsHardwareAccelerated && values.Length >= Vector256<float>.Count)
        {
            ref float first = ref MemoryMarshal.GetReference(values);
            Vector256<float> minima = Vector256.LoadUnsafe(ref first);
            Vector256<float> maxima = minima;
            for (i = Vector256<float>.Count; i <= values.Length - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                Vector256<float> sample = Vector256.LoadUnsafe(ref first, (nuint)i);
                minima = Vector256.MinNative(minima, sample);
                maxima = Vector256.MaxNative(maxima, sample);
            }
            for (int lane = 0; lane < Vector256<float>.Count; lane++)
            {
                minimum = Math.Min(minimum, minima.GetElement(lane));
                maximum = Math.Max(maximum, maxima.GetElement(lane));
            }
        }
        for (; i < values.Length; i++)
        {
            if (values[i] < minimum) minimum = values[i];
            if (values[i] > maximum) maximum = values[i];
        }
        return (minimum, maximum);
    }

    private static Target AnalyzeTarget(float[] values, Rect tile, double contrast, double threshold, CancellationToken token, ref TargetScratch scratch)
    {
        int tilePixels = tile.Width * tile.Height;
        var (minimum, maximum) = FindTargetExtrema(values.AsSpan(0, tilePixels));
        float range = maximum - minimum;
        if (range < contrast) return new(false, "low_contrast");
        if (scratch.Mask == null || scratch.Mask.Length < tilePixels)
            scratch.Mask = new byte[tilePixels];
        byte[] mask = scratch.Mask;
        double cutoff = range * threshold;
        int foreground = 0;
        for (int i = 0; i < tilePixels; i++)
        {
            values[i] -= minimum;
            if (values[i] >= cutoff)
            {
                mask[i] = 255;
                foreground++;
            }
        }
        if (scratch.Queue == null || scratch.Queue.Length < foreground)
        {
            int capacity = Math.Max(foreground, Math.Min(tilePixels, (scratch.Queue?.Length ?? 0) * 2));
            scratch.Queue = new int[capacity];
        }

        // Components clears the mask before it is used by another tile.
        Component? target = null;
        double nextSum = 0;
        foreach (var blob in Components(mask, values, tile.Width, tile.Height, token, scratch.Queue))
        {
            if (blob.Area < 3) continue;
            if (target == null || blob.Sum > target.Sum)
            {
                nextSum = target?.Sum ?? 0;
                target = blob;
            }
            else if (blob.Sum > nextSum)
                nextSum = blob.Sum;
        }
        if (target == null) return new(false, "target_missing");
        if (nextSum > target.Sum * 0.05) return new(false, "ambiguous_targets");
        if (target.Bounds.X <= 1 || target.Bounds.Y <= 1 || target.Bounds.Right >= tile.Width - 1 || target.Bounds.Bottom >= tile.Height - 1)
            return new(false, "target_clipped");
        return new(true, "", target.X + tile.X, target.Y + tile.Y);
    }

    private static void MeasureRgb(AlgorithmExecutionContext context, RgbRegistrationParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        var input = context.Inputs[0].Image;
        // Integer BGR needs no full-image signal validation before locating targets.
        float[]? decoder = CreateIntegerSignalDecoder(input.Format, p.DecodeExponent, input.Width * input.Height);
        float[]? green = decoder == null ? ReadSignal(input, 1, p.DecodeExponent, token) : null;
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry>();
        var errors = new List<double>();
        var referenceTargets = new Target?[p.Rows * p.Columns];
        TargetScratch scratch = default;
        float[]? moving = null;
        foreach (int channel in new[] { 2, 0 })
        {
            if (decoder == null) moving = ReadSignal(input, channel, p.DecodeExponent, token, moving);
            foreach (var (index, bounds) in Grid(input.Width, input.Height, p))
            {
                token.ThrowIfCancellationRequested();
                Target reference = referenceTargets[index] ??= decoder == null
                    ? LocateTarget(green!, input.Width, bounds, p.MinimumContrast, p.TargetThreshold, token, ref scratch)
                    : LocateIntegerRgbTarget(input, 1, decoder, bounds, p.MinimumContrast, p.TargetThreshold, token, ref scratch);
                Target target = decoder == null
                    ? LocateTarget(moving!, input.Width, bounds, p.MinimumContrast, p.TargetThreshold, token, ref scratch)
                    : LocateIntegerRgbTarget(input, channel, decoder, bounds, p.MinimumContrast, p.TargetThreshold, token, ref scratch);
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

    private static (double Blue, double Green, double Red) IntegerColorTileMeans(AlgorithmImageBuffer image,
        Rect tile, float[] decoder, CancellationToken token)
    {
        double blue = 0, green = 0, red = 0;
        // Each channel follows the original decoded float summation order.
        bool byteSamples = decoder.Length == 256;
        int pixelBytes = byteSamples ? 3 : 6;
        ReadOnlySpan<byte> data = image.Data.Span;
        int rowBytes = tile.Width * pixelBytes;
        for (int y = tile.Y; y < tile.Bottom; y++)
        {
            token.ThrowIfCancellationRequested();
            var row = data.Slice(y * image.Stride + tile.X * pixelBytes, rowBytes);
            if (byteSamples)
            {
                for (int x = 0; x < row.Length; x += 3)
                {
                    blue += decoder[row[x]];
                    green += decoder[row[x + 1]];
                    red += decoder[row[x + 2]];
                }
            }
            else
            {
                for (int x = 0; x < row.Length; x += 6)
                {
                    blue += decoder[BinaryPrimitives.ReadUInt16LittleEndian(row[x..])];
                    green += decoder[BinaryPrimitives.ReadUInt16LittleEndian(row[(x + 2)..])];
                    red += decoder[BinaryPrimitives.ReadUInt16LittleEndian(row[(x + 4)..])];
                }
            }
        }
        long area = (long)tile.Width * tile.Height;
        return (blue / area, green / area, red / area);
    }

    private static (double Left, double Right)[,]? BinocularIntegerColorMeans(AlgorithmImageBuffer left,
        AlgorithmImageBuffer right, BinocularQualityParameters p, CancellationToken token)
    {
        float[]? decoder = CreateIntegerSignalDecoder(left.Format, p.DecodeExponent, left.Width * left.Height);
        if (decoder == null) return null;

        var means = new (double Left, double Right)[p.Rows * p.Columns, 3];
        foreach (var (index, bounds) in Grid(left.Width, left.Height, p))
        {
            var l = IntegerColorTileMeans(left, bounds, decoder, token);
            var r = IntegerColorTileMeans(right, bounds, decoder, token);
            means[index, 0] = (l.Blue, r.Blue);
            means[index, 1] = (l.Green, r.Green);
            means[index, 2] = (l.Red, r.Red);
        }
        return means;
    }

    private static void MeasureBinocular(AlgorithmExecutionContext context, BinocularQualityParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        if (context.Inputs[0].Name != "left" || context.Inputs[1].Name != "right")
            throw new MeasurementException("invalid_input_names", "按 left、right 顺序提供左右眼图像。");
        var leftInput = context.Inputs[0].Image;
        var rightInput = context.Inputs[1].Image;
        bool color = leftInput.Format.Channels() >= 3;
        var channelMeans = color && !p.MeasureAlignment ? BinocularIntegerColorMeans(leftInput, rightInput, p, token) : null;
        float[]? left = channelMeans == null ? ReadSignal(leftInput, p.Channel, p.DecodeExponent, token) : null;
        float[]? right = channelMeans == null ? ReadSignal(rightInput, p.Channel, p.DecodeExponent, token) : null;
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var pairs = new List<(Target Left, Target Right)>();
        var shapes = new List<AlgorithmGeometry>();
        var ratios = new List<double>();
        TargetScratch scratch = default;
        foreach (var (index, bounds) in Grid(leftInput.Width, leftInput.Height, p))
        {
            token.ThrowIfCancellationRequested();
            var a = p.MeasureAlignment ? LocateTarget(left!, leftInput.Width, bounds, p.MinimumContrast, 0.5, token, ref scratch) : new Target(false, "alignment_disabled");
            var b = p.MeasureAlignment ? LocateTarget(right!, leftInput.Width, bounds, p.MinimumContrast, 0.5, token, ref scratch) : new Target(false, "alignment_disabled");
            bool valid = a.Valid && b.Valid;
            (double meanLeft, double meanRight) = channelMeans != null
                ? channelMeans[index, p.Channel]
                : (TileMean(left!, leftInput.Width, bounds), TileMean(right!, leftInput.Width, bounds));
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
        if (color)
        {
            if (p.MeasureAlignment) channelMeans = BinocularIntegerColorMeans(leftInput, rightInput, p, token);
            var channelRows = new List<IReadOnlyDictionary<string, JsonElement>>();
            float[]? extraLeft = null, extraRight = null;
            for (int channel = 0; channel < 3; channel++)
            {
                float[]? l = left, r = right;
                if (channelMeans == null && channel != p.Channel)
                {
                    extraLeft = ReadSignal(leftInput, channel, p.DecodeExponent, token, extraLeft);
                    extraRight = ReadSignal(rightInput, channel, p.DecodeExponent, token, extraRight);
                    l = extraLeft;
                    r = extraRight;
                }
                foreach (var (index, bounds) in Grid(leftInput.Width, leftInput.Height, p))
                {
                    token.ThrowIfCancellationRequested();
                    (double a, double b) = channelMeans != null
                        ? channelMeans[index, channel]
                        : (TileMean(l!, leftInput.Width, bounds), TileMean(r!, leftInput.Width, bounds));
                    channelRows.Add(Row(("cell", index), ("channel", channel == 0 ? "B" : channel == 1 ? "G" : "R"),
                        ("leftSignal", a), ("rightSignal", b), ("rightOverLeft", a > 1e-8 ? b / a : null)));
                }
            }
            Table(artifacts, "binocular-channel-consistency", ["cell", "channel", "leftSignal", "rightSignal", "rightOverLeft"], channelRows);
        }
        Overlay(artifacts, shapes);
    }
}
