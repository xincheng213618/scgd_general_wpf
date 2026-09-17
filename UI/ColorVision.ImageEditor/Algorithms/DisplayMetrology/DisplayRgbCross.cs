using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

public sealed partial class DisplayMetrologyProvider
{
    private sealed record CrossTarget(
        bool Valid,
        string Reason,
        Rect Bounds = default,
        double VerticalAxisX = 0,
        double HorizontalAxisY = 0,
        double VerticalLeft = 0,
        double VerticalRight = 0,
        double HorizontalTop = 0,
        double HorizontalBottom = 0);

    private static CrossTarget LocateCross(
        float[] image,
        int width,
        Rect tile,
        RgbCrossRegistrationParameters parameters,
        CancellationToken token)
    {
        var values = new float[tile.Width * tile.Height];
        for (int y = 0; y < tile.Height; y++)
            Array.Copy(image, (tile.Y + y) * width + tile.X, values, y * tile.Width, tile.Width);

        float minimum = values.Min();
        float maximum = values.Max();
        if (maximum - minimum < parameters.MinimumContrast)
            return new(false, "low_contrast");

        var mask = new byte[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] -= minimum;
            if (values[i] >= (maximum - minimum) * parameters.TargetThreshold)
                mask[i] = 255;
        }

        var componentMask = (byte[])mask.Clone();
        Component[] components = Components(componentMask, values, tile.Width, tile.Height, token)
            .Where(component => component.Area >= 5)
            .OrderByDescending(component => component.Sum)
            .ToArray();
        if (components.Length == 0)
            return new(false, "cross_missing");
        if (components.Length > 1 && components[1].Sum > components[0].Sum * 0.05)
            return new(false, "ambiguous_crosses");

        Component component = components[0];
        Rect bounds = component.Bounds;
        if (bounds.X <= 1 || bounds.Y <= 1 || bounds.Right >= tile.Width - 1 || bounds.Bottom >= tile.Height - 1)
            return new(false, "cross_clipped");

        var rowScores = new double[bounds.Height];
        var columnScores = new double[bounds.Width];
        for (int y = bounds.Y; y < bounds.Bottom; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = bounds.X; x < bounds.Right; x++)
            {
                if (mask[y * tile.Width + x] == 0)
                    continue;
                rowScores[y - bounds.Y]++;
                columnScores[x - bounds.X]++;
            }
        }

        double maximumRowSupport = rowScores.Max();
        double maximumColumnSupport = columnScores.Max();
        if (maximumRowSupport < tile.Width * parameters.MinimumArmSpanFraction ||
            maximumColumnSupport < tile.Height * parameters.MinimumArmSpanFraction)
            return new(false, "cross_arm_too_short");

        (int top, int bottom, double horizontalAxis) = FindAxisBand(rowScores, parameters.AxisBandThreshold);
        (int left, int right, double verticalAxis) = FindAxisBand(columnScores, parameters.AxisBandThreshold);
        Rect globalBounds = new(tile.X + bounds.X, tile.Y + bounds.Y, bounds.Width, bounds.Height);
        return new CrossTarget(
            true,
            string.Empty,
            globalBounds,
            tile.X + bounds.X + verticalAxis,
            tile.Y + bounds.Y + horizontalAxis,
            tile.X + bounds.X + left,
            tile.X + bounds.X + right,
            tile.Y + bounds.Y + top,
            tile.Y + bounds.Y + bottom);
    }

    private static (int First, int Last, double Center) FindAxisBand(double[] scores, double thresholdFraction)
    {
        double maximum = scores.Max();
        int peak = Array.IndexOf(scores, maximum);
        double threshold = maximum * thresholdFraction;
        int first = peak;
        int last = peak;
        while (first > 0 && scores[first - 1] >= threshold)
            first--;
        while (last + 1 < scores.Length && scores[last + 1] >= threshold)
            last++;

        double weightedIndex = 0;
        double weight = 0;
        for (int i = first; i <= last; i++)
        {
            weightedIndex += i * scores[i];
            weight += scores[i];
        }
        return (first, last, weight > 0 ? weightedIndex / weight : (first + last) / 2.0);
    }

    private static void MeasureRgbCross(
        AlgorithmExecutionContext context,
        RgbCrossRegistrationParameters parameters,
        List<AlgorithmArtifact> artifacts,
        CancellationToken token)
    {
        AlgorithmImageBuffer input = context.Inputs[0].Image;
        string[] channelNames = ["R", "G", "B"];
        int[] channelIndexes = [2, 1, 0];
        string[] channelColors = ["#FFFF4040", "#FF40D060", "#FF4090FF"];
        float[][] signals = channelIndexes.Select(channel => ReadSignal(input, channel, parameters.DecodeExponent, token)).ToArray();
        for (int channel = 0; channel < signals.Length; channel++)
        {
            artifacts.Add(new AlgorithmImageArtifact(
                $"RGB-channel-{channelNames[channel]}",
                "channel-preview",
                CreateChannelPreview(signals[channel], input.Width, input.Height),
                new Dictionary<string, string>
                {
                    ["channel"] = channelNames[channel],
                    ["source"] = "decoded-relative-device-signal",
                    ["decodeExponent"] = parameters.DecodeExponent.ToString("R", CultureInfo.InvariantCulture),
                    ["quantization"] = "8-bit preview; measurements use float decoded signal",
                }));
        }

        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry>();
        var overlayItems = new List<AlgorithmOverlayItem>();
        var validEdgeSeparations = new List<double>();
        var validAxisSeparations = new List<double>();
        int passedCount = 0;
        int invalidCount = 0;

        foreach ((int index, Rect bounds) in Grid(input.Width, input.Height, parameters))
        {
            token.ThrowIfCancellationRequested();
            CrossTarget[] targets = signals.Select(signal => LocateCross(signal, input.Width, bounds, parameters, token)).ToArray();
            bool valid = targets.All(target => target.Valid);
            string reason = string.Join(";", targets.Select((target, channel) => target.Valid ? null : $"{channelNames[channel]}:{target.Reason}")
                .Where(value => value != null));

            double? verticalAxisXSpread = valid ? Spread(targets.Select(target => target.VerticalAxisX)) : null;
            double? horizontalAxisYSpread = valid ? Spread(targets.Select(target => target.HorizontalAxisY)) : null;
            double? leftEdgeSpread = valid ? Spread(targets.Select(target => target.VerticalLeft)) : null;
            double? rightEdgeSpread = valid ? Spread(targets.Select(target => target.VerticalRight)) : null;
            double? topEdgeSpread = valid ? Spread(targets.Select(target => target.HorizontalTop)) : null;
            double? bottomEdgeSpread = valid ? Spread(targets.Select(target => target.HorizontalBottom)) : null;
            double? maximumAxisSeparation = valid ? Math.Max(verticalAxisXSpread!.Value, horizontalAxisYSpread!.Value) : null;
            double? maximumEdgeSeparation = valid ? new[] { leftEdgeSpread!.Value, rightEdgeSpread!.Value, topEdgeSpread!.Value, bottomEdgeSpread!.Value }.Max() : null;
            bool passed = valid && maximumEdgeSeparation!.Value <= parameters.MaximumEdgeSeparationPixels;

            if (valid)
            {
                validAxisSeparations.Add(maximumAxisSeparation!.Value);
                validEdgeSeparations.Add(maximumEdgeSeparation!.Value);
                if (passed)
                    passedCount++;
                for (int channel = 0; channel < targets.Length; channel++)
                    AddCrossOverlay(index, channelNames[channel], channelColors[channel], targets[channel], shapes, overlayItems);
            }
            else
            {
                invalidCount++;
            }

            string result = !valid ? "INVALID" : passed ? "OK" : "NG";
            string cellId = $"P{index + 1}-{result}";
            shapes.Add(Box(cellId, bounds));
            overlayItems.Add(new AlgorithmOverlayItem(cellId, new AlgorithmOverlayStyle(
                !valid ? "#FFFFA500" : passed ? "#FF36E36E" : "#FFFF3030",
                passed ? "#1036E36E" : "#10FF3030",
                1.5,
                $"P{index + 1} {result}")));

            CrossTarget red = targets[0];
            CrossTarget green = targets[1];
            CrossTarget blue = targets[2];
            rows.Add(Row(
                ("point", $"P{index + 1}"), ("row", index / 3 + 1), ("column", index % 3 + 1),
                ("valid", valid), ("reason", reason), ("result", result),
                ("rVerticalAxisX_px", red.Valid ? red.VerticalAxisX : null), ("rHorizontalAxisY_px", red.Valid ? red.HorizontalAxisY : null),
                ("gVerticalAxisX_px", green.Valid ? green.VerticalAxisX : null), ("gHorizontalAxisY_px", green.Valid ? green.HorizontalAxisY : null),
                ("bVerticalAxisX_px", blue.Valid ? blue.VerticalAxisX : null), ("bHorizontalAxisY_px", blue.Valid ? blue.HorizontalAxisY : null),
                ("rMinusG_dx_px", valid ? red.VerticalAxisX - green.VerticalAxisX : null),
                ("rMinusG_dy_px", valid ? red.HorizontalAxisY - green.HorizontalAxisY : null),
                ("bMinusG_dx_px", valid ? blue.VerticalAxisX - green.VerticalAxisX : null),
                ("bMinusG_dy_px", valid ? blue.HorizontalAxisY - green.HorizontalAxisY : null),
                ("verticalAxisXSpread_px", verticalAxisXSpread), ("horizontalAxisYSpread_px", horizontalAxisYSpread),
                ("leftEdgeSpread_px", leftEdgeSpread), ("rightEdgeSpread_px", rightEdgeSpread),
                ("topEdgeSpread_px", topEdgeSpread), ("bottomEdgeSpread_px", bottomEdgeSpread),
                ("maximumAxisSeparation_px", maximumAxisSeparation), ("maximumEdgeSeparation_px", maximumEdgeSeparation),
                ("limit_px", parameters.MaximumEdgeSeparationPixels)));
        }

        if (validEdgeSeparations.Count == 0)
            throw new MeasurementException("no_valid_crosses", "没有可测十字。每个网格的 R/G/B 通道都必须包含完整、单一的十字。");

        int failedCount = validEdgeSeparations.Count - passedCount;
        bool overallPass = invalidCount == 0 && failedCount == 0 && validEdgeSeparations.Count == parameters.Columns * parameters.Rows;
        Table(artifacts, "RGB-cross-separation",
        [
            "point", "row", "column", "valid", "reason", "result",
            "rVerticalAxisX_px", "rHorizontalAxisY_px", "gVerticalAxisX_px", "gHorizontalAxisY_px", "bVerticalAxisX_px", "bHorizontalAxisY_px",
            "rMinusG_dx_px", "rMinusG_dy_px", "bMinusG_dx_px", "bMinusG_dy_px",
            "verticalAxisXSpread_px", "horizontalAxisYSpread_px", "leftEdgeSpread_px", "rightEdgeSpread_px", "topEdgeSpread_px", "bottomEdgeSpread_px",
            "maximumAxisSeparation_px", "maximumEdgeSeparation_px", "limit_px",
        ], rows);
        Metrics(artifacts,
            ("valid_crosses", validEdgeSeparations.Count, "count"),
            ("invalid_crosses", invalidCount, "count"),
            ("passed_crosses", passedCount, "count"),
            ("failed_crosses", failedCount, "count"),
            ("maximum_cross_axis_separation", validAxisSeparations.Max(), "px"),
            ("maximum_cross_edge_separation", validEdgeSeparations.Max(), "px"),
            ("rms_cross_edge_separation", Math.Sqrt(validEdgeSeparations.Average(value => value * value)), "px"),
            ("configured_edge_separation_limit", parameters.MaximumEdgeSeparationPixels, "px"),
            ("overall_threshold_result", overallPass ? 1 : 0, "1=OK;0=NG"));
        artifacts.Add(new AlgorithmGeometryArtifact("rgb-cross-regions", AlgorithmCoordinateSpace.Pixel, shapes));
        artifacts.Add(new AlgorithmOverlayArtifact("rgb-cross-measurements", AlgorithmOverlayLifetime.Transient, overlayItems));
    }

    private static void AddCrossOverlay(
        int index,
        string channel,
        string color,
        CrossTarget target,
        List<AlgorithmGeometry> shapes,
        List<AlgorithmOverlayItem> overlayItems)
    {
        string horizontalId = $"P{index + 1}-{channel}-horizontal";
        string verticalId = $"P{index + 1}-{channel}-vertical";
        shapes.Add(new AlgorithmGeometry(horizontalId, AlgorithmGeometryKind.Rectangle,
        [
            new(target.Bounds.X, target.HorizontalTop),
            new(target.Bounds.Right, target.HorizontalBottom + 1),
        ]));
        shapes.Add(new AlgorithmGeometry(verticalId, AlgorithmGeometryKind.Rectangle,
        [
            new(target.VerticalLeft, target.Bounds.Y),
            new(target.VerticalRight + 1, target.Bounds.Bottom),
        ]));
        overlayItems.Add(new AlgorithmOverlayItem(horizontalId, new AlgorithmOverlayStyle(color, null, 1.25, $"P{index + 1} {channel}")));
        overlayItems.Add(new AlgorithmOverlayItem(verticalId, new AlgorithmOverlayStyle(color, null, 1.25)));
    }

    private static double Spread(IEnumerable<double> values)
    {
        double[] array = values.ToArray();
        return array.Max() - array.Min();
    }

    private static AlgorithmImageBuffer CreateChannelPreview(float[] signal, int width, int height)
    {
        var pixels = new byte[checked(width * height)];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = (byte)Math.Round(Math.Clamp(signal[i], 0, 1) * 255);
        return new AlgorithmImageBuffer(width, height, width, AlgorithmImageFormat.Gray8, pixels);
    }
}
