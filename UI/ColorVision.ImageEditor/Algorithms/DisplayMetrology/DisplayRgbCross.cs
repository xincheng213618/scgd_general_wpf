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
        double HorizontalBottom = 0,
        double HorizontalCoverage = 0,
        double VerticalCoverage = 0,
        int RejectedProfiles = 0,
        int SaturatedSamples = 0);

    private sealed record CrossSlot(Rect Bounds, Rect Evidence, string Reason = "");

    // Pool every source pixel, preserving thin colored arms. This union is for location only;
    // all edge measurements below read independent channels at source resolution.
    private static (float[][] Channels, int Width, int Height, int Step) CrossLocatorImage(
        AlgorithmImageBuffer input, Rect search, double exponent, CancellationToken token)
    {
        int step = Math.Max(1, (Math.Max(search.Width, search.Height) + 1599) / 1600);
        int width = (search.Width + step - 1) / step, height = (search.Height + step - 1) / step;
        float[][] channels = [new float[width * height], new float[width * height], new float[width * height]];
        int count = input.Format.Channels(), bytes = input.Format.BitsPerChannel() / 8;
        ReadOnlySpan<byte> data = input.Data.Span;
        for (int y = 0; y < search.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < search.Width; x++)
            {
                int offset = (search.Y + y) * input.Stride + (search.X + x) * count * bytes;
                for (int c = 0; c < count; c++)
                {
                    double value = CrossSample(data, offset + c * bytes, bytes);
                    if (!double.IsFinite(value) || value < 0 || value > 1)
                        throw new MeasurementException("invalid_signal", $"像素 ({search.X + x},{search.Y + y}) 通道 {c} 不在有限 [0,1] 范围内。");
                    if (c == 3 && value != 1) throw new MeasurementException("transparent_input", "显示计量要求不透明图像。");
                    if (c < 3)
                    {
                        int i = y / step * width + x / step;
                        channels[2 - c][i] = Math.Max(channels[2 - c][i], (float)(exponent == 1 ? value : Math.Pow(value, exponent)));
                    }
                }
            }
        }
        return (channels, width, height, step);
    }

    private static double CrossSample(ReadOnlySpan<byte> data, int offset, int bytes) => bytes == 1 ? data[offset] / 255.0
        : bytes == 2 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2)) / 65535.0
        : BitConverter.Int32BitsToSingle(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4)));

    private static CrossSlot[] LocateCrossArray(float[][] channels, int width, int height, int step,
        Rect search, RgbCrossRegistrationParameters p, CancellationToken token, out int candidateCount)
    {
        int sourceWidth = search.Width, sourceHeight = search.Height;
        var signal = new float[width * height];
        for (int i = 0; i < signal.Length; i++) signal[i] = Math.Max(channels[0][i], Math.Max(channels[1][i], channels[2][i]));
        double minimum = signal.Min(), contrast = signal.Max() - minimum;
        candidateCount = 0;
        CrossSlot[] Reject(string reason) => Enumerable.Range(0, 9).Select(_ => new CrossSlot(default, default, reason)).ToArray();
        if (contrast < p.MinimumContrast) return Reject("low_contrast");
        var mask = new byte[signal.Length];
        for (int i = 0; i < signal.Length; i++) if (signal[i] >= minimum + contrast * p.TargetThreshold) mask[i] = 255;
        Rect[] candidates = Components(mask, signal, width, height, token).Where(c => c.Area >= 5 && c.Bounds.Width >= 3 && c.Bounds.Height >= 3)
            .Select(c => new Rect(c.Bounds.X * step, c.Bounds.Y * step,
                Math.Min(c.Bounds.Width * step, sourceWidth - c.Bounds.X * step),
                Math.Min(c.Bounds.Height * step, sourceHeight - c.Bounds.Y * step))).ToArray();
        candidateCount = candidates.Length;
        if (candidates.Length == 0) return Reject("cross_missing");
        // An axis-aligned array must have three disjoint row envelopes and three disjoint
        // column envelopes. Overlapping/diagonal layouts do not get arbitrary sorted labels.
        List<List<Rect>> Group(bool horizontal)
        {
            var groups = new List<List<Rect>>();
            double end = -1;
            foreach (Rect r in candidates.OrderBy(r => horizontal ? r.X : r.Y))
            {
                int start = horizontal ? r.X : r.Y;
                if (start >= end) groups.Add(new List<Rect>());
                groups[^1].Add(r);
                end = Math.Max(end, horizontal ? r.Right : r.Bottom);
            }
            return groups;
        }
        var rows = Group(false); var columns = Group(true);
        if (rows.Count != 3 || columns.Count != 3)
            return Reject(candidates.Length > 9 ? "duplicate_or_extra_candidates" : candidates.Length < 9 ? "missing_array_structure" : "array_order_ambiguous");
        var slots = new CrossSlot[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                Rect[] matches = rows[r].Intersect(columns[c]).ToArray();
                if (matches.Length != 1)
                {
                    slots[r * 3 + c] = new(default, default, matches.Length == 0 ? "cross_missing" : "duplicate_crosses");
                    continue;
                }
                Rect evidence = matches[0];
                int padX = Math.Max(4 * step, evidence.Width / 2), padY = Math.Max(4 * step, evidence.Height / 2);
                int left = Math.Max(0, evidence.X - padX), top = Math.Max(0, evidence.Y - padY);
                int right = Math.Min(sourceWidth, evidence.Right + padX), bottom = Math.Min(sourceHeight, evidence.Bottom + padY);
                Rect roi = new(left, top, right - left, bottom - top);
                string reason = candidates.Any(other => other != evidence && roi.IntersectsWith(other)) ? "target_roi_overlap" : "";
                if ((long)roi.Width * roi.Height > MaximumFramePixels) reason = "target_roi_budget_exceeded";
                if (evidence.X == 0 || evidence.Y == 0 || evidence.Right >= sourceWidth || evidence.Bottom >= sourceHeight) reason = "cross_clipped";
                roi.X += search.X; roi.Y += search.Y;
                evidence.X += search.X; evidence.Y += search.Y;
                slots[r * 3 + c] = new(roi, evidence, reason);
            }
        return slots;
    }

    private static CrossTarget LocateCross(AlgorithmImageBuffer input, int channel, CrossSlot slot,
        RgbCrossRegistrationParameters p, CancellationToken token, int pointIndex, string channelName, List<IReadOnlyDictionary<string, JsonElement>> profiles)
    {
        if (slot.Reason.Length != 0) return new(false, slot.Reason);
        Rect roi = slot.Bounds, evidence = slot.Evidence;
        if (evidence.X == 0 || evidence.Y == 0 || evidence.Right >= input.Width || evidence.Bottom >= input.Height)
            return new(false, "cross_clipped");
        int channels = input.Format.Channels(), bytes = input.Format.BitsPerChannel() / 8;
        var values = new float[roi.Width * roi.Height];
        ReadOnlySpan<byte> data = input.Data.Span;
        for (int y = 0; y < roi.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < roi.Width; x++)
            {
                double value = CrossSample(data, (roi.Y + y) * input.Stride + (roi.X + x) * channels * bytes + channel * bytes, bytes);
                values[y * roi.Width + x] = (float)(p.DecodeExponent == 1 ? value : Math.Pow(value, p.DecodeExponent));
            }
        }
        int saturatedSamples = values.Count(value => value >= 1);
        double minimum = values.Min();
        if (values.Max() - minimum < p.MinimumContrast) return new(false, "low_contrast");
        var rows = new double[roi.Height]; var columns = new double[roi.Width];
        for (int y = 0; y < roi.Height; y++)
            for (int x = 0; x < roi.Width; x++)
            {
                double value = values[y * roi.Width + x] - minimum;
                rows[y] += value; columns[x] += value;
            }
        var horizontal = FindAxisBand(rows, p.AxisBandThreshold);
        var vertical = FindAxisBand(columns, p.AxisBandThreshold);
        if (!horizontal.Valid || !vertical.Valid) return new(false, "ambiguous_axis_bands");
        if (horizontal.Last - horizontal.First >= evidence.Height / 2 || vertical.Last - vertical.First >= evidence.Width / 2)
            return new(false, "not_a_cross");
        if (evidence.Width < roi.Width * p.MinimumArmSpanFraction || evidence.Height < roi.Height * p.MinimumArmSpanFraction)
            return new(false, "cross_arm_too_short");

        // Identical sampling positions across channels; discard the middle half where the
        // perpendicular arm crosses. Per-profile thresholds retain dim arms alongside bright ones.
        (bool Valid, string Reason, double First, double Last, double Coverage, int Rejected) Edges(bool alongHorizontal)
        {
            var firstEdges = new List<double>(); var lastEdges = new List<double>();
            int rejected = 0;
            double coverage = 1;
            int start = alongHorizontal ? evidence.X - roi.X : evidence.Y - roi.Y;
            int length = alongHorizontal ? evidence.Width : evidence.Height;
            int transverseLength = alongHorizontal ? roi.Height : roi.Width;
            for (int side = 0; side < 2; side++)
            {
                int count = 0, attempted = 0, ambiguous = 0;
                // Inner part of each outer quarter avoids endpoint falloff and the junction.
                int from = start + (int)(length * (side == 0 ? 0.10 : 0.75));
                int to = start + (int)(length * (side == 0 ? 0.25 : 0.90));
                for (int position = from; position < to; position++)
                {
                    token.ThrowIfCancellationRequested();
                    attempted++;
                    var profile = new double[transverseLength];
                    for (int t = 0; t < transverseLength; t++) profile[t] = alongHorizontal ? values[t * roi.Width + position] : values[position * roi.Width + t];
                    void Diagnostic(string status, int runs, double? first = null, double? last = null) => profiles.Add(Row(
                        ("point", $"P{pointIndex + 1}"), ("channel", channelName), ("arm", alongHorizontal ? "horizontal" : "vertical"),
                        ("side", side == 0 ? "negative" : "positive"), ("samplePosition_px", position + (alongHorizontal ? roi.X : roi.Y)),
                        ("status", status), ("thresholdRunCount", runs), ("firstEdge_px", first), ("lastEdge_px", last)));
                    double low = profile.Min(), high = profile.Max();
                    if (high - low < p.MinimumContrast) { Diagnostic("low_contrast", 0); continue; }
                    var band = FindAxisBand(profile.Select(v => v - low).ToArray(), p.TargetThreshold);
                    if (!band.Valid) { Diagnostic("ambiguous_arm_edges", band.RunCount); ambiguous++; continue; }
                    if (band.First == 0 || band.Last == transverseLength - 1) return (false, "cross_clipped", 0, 0, 0, rejected);
                    if (band.Last - band.First >= transverseLength / 2) return (false, "not_a_cross", 0, 0, 0, rejected);
                    double level = low + (high - low) * p.TargetThreshold;
                    double first = band.First - 1 + (level - profile[band.First - 1]) / (profile[band.First] - profile[band.First - 1]);
                    double last = band.Last + (profile[band.Last] - level) / (profile[band.Last] - profile[band.Last + 1]);
                    Diagnostic("valid", 1, first + (alongHorizontal ? roi.Y : roi.X), last + (alongHorizontal ? roi.Y : roi.X));
                    firstEdges.Add(first); lastEdges.Add(last); count++;
                }
                rejected += attempted - count;
                coverage = Math.Min(coverage, attempted == 0 ? 0 : (double)count / attempted);
                if (attempted == 0 || count < Math.Max(1, attempted * p.MinimumArmCoverage))
                    return (false, ambiguous > 0 ? "ambiguous_arm_edges" : "arm_missing_or_low_contrast", 0, 0, coverage, rejected);
            }
            return (true, "", Median(firstEdges), Median(lastEdges), coverage, rejected);
        }
        var h = Edges(true); var v = Edges(false);
        if (!h.Valid || !v.Valid) return new(false, string.Join(";", new[] { h.Valid ? null : $"horizontal:{h.Reason}", v.Valid ? null : $"vertical:{v.Reason}" }.Where(reason => reason != null)),
            HorizontalCoverage: h.Coverage, VerticalCoverage: v.Coverage, RejectedProfiles: h.Rejected + v.Rejected, SaturatedSamples: saturatedSamples);
        return new(true, "", evidence, roi.X + (v.First + v.Last) / 2, roi.Y + (h.First + h.Last) / 2,
            roi.X + v.First, roi.X + v.Last, roi.Y + h.First, roi.Y + h.Last, h.Coverage, v.Coverage, h.Rejected + v.Rejected, saturatedSamples);
    }

    private static double Median(List<double> values)
    {
        values.Sort(); int middle = values.Count / 2;
        return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
    }

    private static (bool Valid, int First, int Last, int RunCount) FindAxisBand(double[] scores, double thresholdFraction)
    {
        double maximum = scores.Max(), threshold = maximum * thresholdFraction;
        int peak = Array.IndexOf(scores, maximum), first = peak, last = peak;
        while (first > 0 && scores[first - 1] >= threshold) first--;
        while (last + 1 < scores.Length && scores[last + 1] >= threshold) last++;
        int runs = 0;
        for (int i = 0; i < scores.Length; i++)
            if (scores[i] >= threshold && (i == 0 || scores[i - 1] < threshold)) runs++;
        return (maximum > 0 && runs == 1, first, last, runs);
    }

    internal static Rect ResolveCrossSearchRegion(AlgorithmRoi? roi, AlgorithmImageBuffer input)
    {
        if (roi == null) return new Rect(0, 0, input.Width, input.Height);
        if (roi is not RectangleAlgorithmRoi rectangle || !rectangle.Validate().IsValid)
            throw new MeasurementException("rectangle_roi_required", "九点十字仅支持有效的矩形搜索区域。");
        AlgorithmPoint start = AlgorithmCoordinates.ToPixel(new(rectangle.X, rectangle.Y), rectangle.CoordinateSpace, input.DpiX, input.DpiY);
        AlgorithmPoint end = AlgorithmCoordinates.ToPixel(new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), rectangle.CoordinateSpace, input.DpiX, input.DpiY);
        if (start.X < 0 || start.Y < 0 || end.X > input.Width || end.Y > input.Height || !double.IsFinite(end.X) || !double.IsFinite(end.Y))
            throw new MeasurementException("roi_out_of_bounds", "搜索区域必须完全位于原图内，不自动裁切。");
        int left = (int)Math.Floor(start.X), top = (int)Math.Floor(start.Y);
        int right = (int)Math.Ceiling(end.X), bottom = (int)Math.Ceiling(end.Y);
        if (right - left < 32 || bottom - top < 32)
            throw new MeasurementException("roi_too_small", "九点十字搜索区域至少为 32×32 像素。");
        return new Rect(left, top, right - left, bottom - top);
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
        Rect search = ResolveCrossSearchRegion(context.Invocation.Roi, input);
        var locator = CrossLocatorImage(input, search, parameters.DecodeExponent, token);
        float[][] signals = locator.Channels;
        CrossSlot[] slots = LocateCrossArray(signals, locator.Width, locator.Height, locator.Step, search, parameters, token, out int candidateCount);
        for (int channel = 0; channel < signals.Length; channel++)
        {
            artifacts.Add(new AlgorithmImageArtifact(
                $"RGB-channel-{channelNames[channel]}",
                "channel-preview",
                CreateChannelPreview(signals[channel], locator.Width, locator.Height),
                new Dictionary<string, string>
                {
                    ["channel"] = channelNames[channel],
                    ["source"] = "decoded-relative-device-signal",
                    ["decodeExponent"] = parameters.DecodeExponent.ToString("R", CultureInfo.InvariantCulture),
                    ["quantization"] = "8-bit max-pool locator preview; measurements use source-resolution decoded signal",
                    ["sourceOriginX"] = search.X.ToString(CultureInfo.InvariantCulture),
                    ["sourceOriginY"] = search.Y.ToString(CultureInfo.InvariantCulture),
                    ["sourcePixelsPerPreviewPixel"] = locator.Step.ToString(CultureInfo.InvariantCulture),
                }));
        }

        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var profiles = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry>();
        var overlayItems = new List<AlgorithmOverlayItem>();
        var validEdgeSeparations = new List<double>();
        var validAxisSeparations = new List<double>();
        int passedCount = 0;
        int invalidCount = 0;

        for (int index = 0; index < slots.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            Rect bounds = slots[index].Bounds;
            CrossTarget[] targets = channelIndexes.Select((channel, color) => LocateCross(input, channel, slots[index], parameters, token, index, channelNames[color], profiles)).ToArray();
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
            bool passed = valid && parameters.MaximumEdgeSeparationPixels.HasValue && maximumEdgeSeparation!.Value <= parameters.MaximumEdgeSeparationPixels.Value;

            for (int channel = 0; channel < targets.Length; channel++)
                if (targets[channel].Valid) AddCrossOverlay(index, channelNames[channel], channelColors[channel], targets[channel], shapes, overlayItems);
            if (valid)
            {
                validAxisSeparations.Add(maximumAxisSeparation!.Value);
                validEdgeSeparations.Add(maximumEdgeSeparation!.Value);
                if (passed)
                    passedCount++;
            }
            else
            {
                invalidCount++;
            }

            string result = !valid ? "INVALID" : !parameters.MaximumEdgeSeparationPixels.HasValue ? "MEASURED" : passed ? "OK" : "NG";
            string cellId = $"P{index + 1}-{result}";
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                shapes.Add(Box(cellId, bounds));
                string color = !valid ? "#FFFFA500" : !parameters.MaximumEdgeSeparationPixels.HasValue ? "#FF00BFFF" : passed ? "#FF36E36E" : "#FFFF3030";
                overlayItems.Add(new AlgorithmOverlayItem(cellId, new AlgorithmOverlayStyle(color, null, 1.5, $"P{index + 1} {result}")));
            }

            CrossTarget red = targets[0];
            CrossTarget green = targets[1];
            CrossTarget blue = targets[2];
            rows.Add(Row(
                ("point", $"P{index + 1}"), ("row", index / 3 + 1), ("column", index % 3 + 1),
                ("valid", valid), ("reason", reason), ("result", result),
                ("warning", targets.Any(t => t.SaturatedSamples > 0) ? "saturated_samples_threshold_edges_may_be_biased" : ""),
                ("rHorizontalCoverage", red.HorizontalCoverage), ("rVerticalCoverage", red.VerticalCoverage),
                ("gHorizontalCoverage", green.HorizontalCoverage), ("gVerticalCoverage", green.VerticalCoverage),
                ("bHorizontalCoverage", blue.HorizontalCoverage), ("bVerticalCoverage", blue.VerticalCoverage),
                ("rRejectedProfiles", red.RejectedProfiles), ("gRejectedProfiles", green.RejectedProfiles), ("bRejectedProfiles", blue.RejectedProfiles),
                ("rSaturatedSamples", red.SaturatedSamples), ("gSaturatedSamples", green.SaturatedSamples), ("bSaturatedSamples", blue.SaturatedSamples),
                ("roiX_px", bounds.X), ("roiY_px", bounds.Y), ("roiWidth_px", bounds.Width), ("roiHeight_px", bounds.Height),
                ("rLeft_px", red.Valid ? red.VerticalLeft : null), ("rRight_px", red.Valid ? red.VerticalRight : null),
                ("rTop_px", red.Valid ? red.HorizontalTop : null), ("rBottom_px", red.Valid ? red.HorizontalBottom : null),
                ("gLeft_px", green.Valid ? green.VerticalLeft : null), ("gRight_px", green.Valid ? green.VerticalRight : null),
                ("gTop_px", green.Valid ? green.HorizontalTop : null), ("gBottom_px", green.Valid ? green.HorizontalBottom : null),
                ("bLeft_px", blue.Valid ? blue.VerticalLeft : null), ("bRight_px", blue.Valid ? blue.VerticalRight : null),
                ("bTop_px", blue.Valid ? blue.HorizontalTop : null), ("bBottom_px", blue.Valid ? blue.HorizontalBottom : null),
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

        Table(artifacts, "RGB-cross-profile-quality", ["point", "channel", "arm", "side", "samplePosition_px", "status", "thresholdRunCount", "firstEdge_px", "lastEdge_px"], profiles);
        int failedCount = parameters.MaximumEdgeSeparationPixels.HasValue ? validEdgeSeparations.Count - passedCount : 0;
        bool overallPass = invalidCount == 0 && failedCount == 0 && validEdgeSeparations.Count == parameters.Columns * parameters.Rows;
        Table(artifacts, "RGB-cross-separation",
        [
            "point", "row", "column", "valid", "reason", "result", "warning",
            "rHorizontalCoverage", "rVerticalCoverage", "gHorizontalCoverage", "gVerticalCoverage", "bHorizontalCoverage", "bVerticalCoverage",
            "rRejectedProfiles", "gRejectedProfiles", "bRejectedProfiles", "rSaturatedSamples", "gSaturatedSamples", "bSaturatedSamples",
            "roiX_px", "roiY_px", "roiWidth_px", "roiHeight_px",
            "rLeft_px", "rRight_px", "rTop_px", "rBottom_px", "gLeft_px", "gRight_px", "gTop_px", "gBottom_px", "bLeft_px", "bRight_px", "bTop_px", "bBottom_px",
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
            ("candidate_count", candidateCount, "count"),
            ("searched_pixels", (long)search.Width * search.Height, "pixels"),
            ("threshold_configured", parameters.MaximumEdgeSeparationPixels.HasValue ? 1 : 0, "boolean"));
        if (validEdgeSeparations.Count > 0)
            Metrics(artifacts,
                ("maximum_cross_axis_separation", validAxisSeparations.Max(), "px"),
                ("maximum_cross_edge_separation", validEdgeSeparations.Max(), "px"),
                ("rms_cross_edge_separation", Math.Sqrt(validEdgeSeparations.Average(value => value * value)), "px"));
        if (parameters.MaximumEdgeSeparationPixels is double limit)
            Metrics(artifacts, ("configured_edge_separation_limit", limit, "px"), ("overall_threshold_result", overallPass ? 1 : 0, "1=OK;0=NG"));
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
            new(target.Bounds.Right, target.HorizontalBottom),
        ]));
        shapes.Add(new AlgorithmGeometry(verticalId, AlgorithmGeometryKind.Rectangle,
        [
            new(target.VerticalLeft, target.Bounds.Y),
            new(target.VerticalRight, target.Bounds.Bottom),
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
