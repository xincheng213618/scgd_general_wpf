using ColorVision.Algorithms;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic CPU implementation of rectangle, circle and polygon ROI statistics.</summary>
    public sealed class RoiStatisticsAlgorithmProvider : IImageAlgorithmProvider
    {
        private const string ResultSchema = "colorvision.analysis.roi-statistics/v1";
        private static readonly IReadOnlySet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.roi-statistics.cpu",
            "ColorVision ROI Statistics CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            110,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Roi,
            Formats,
            "1.0.0");

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.RoiStatistics
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Invocation.Roi is not RectangleAlgorithmRoi
                and not CircleAlgorithmRoi
                and not PolygonAlgorithmRoi)
            {
                return ValueTask.FromResult(Failure(context, "roi_required", "ROI statistics requires a rectangle, circle or polygon ROI.", "roi"));
            }

            AlgorithmImageBuffer image = context.Inputs[0].Image;
            RoiStatisticsParameters parameters = (RoiStatisticsParameters)context.Parameters;
            AlgorithmPixelRoi roi = AlgorithmPixelRoi.Create(context.Invocation.Roi, image);
            if (roi.IsEmpty)
                return ValueTask.FromResult(Failure(context, "roi_empty_after_clip", "The ROI does not contain any image pixel centers after clipping.", "roi"));

            context.Progress?.Report(new AlgorithmProgress(0.05, "roi.scan", "Scanning ROI pixels"));
            ChannelAccumulator[] channels = Enumerable.Range(0, image.Format.Channels())
                .Select(index => new ChannelAccumulator(index, ChannelName(image.Format, index), image.Format))
                .ToArray();
            long includedPixels = Scan(image, roi, channels, cancellationToken, context.Progress);
            if (includedPixels == 0)
                return ValueTask.FromResult(Failure(context, "roi_empty_after_clip", "The ROI does not contain any image pixel centers after clipping.", "roi"));

            foreach (ChannelAccumulator channel in channels) channel.PreparePercentiles();
            context.Progress?.Report(new AlgorithmProgress(0.55, "roi.bad-pixels", "Detecting local outlier candidates"));
            List<BadPixelCandidate> returnedCandidates = new();
            HashSet<long> badPixelLocations = new();
            if (parameters.DetectBadPixelCandidates)
                DetectBadPixels(image, roi, channels, parameters, returnedCandidates, badPixelLocations, cancellationToken, context.Progress);

            context.Progress?.Report(new AlgorithmProgress(0.82, "roi.artifacts", "Building structured result artifacts"));
            IReadOnlyList<AlgorithmArtifact> artifacts = BuildArtifacts(
                context,
                image,
                roi,
                channels,
                includedPixels,
                returnedCandidates,
                badPixelLocations.Count,
                parameters);
            List<AlgorithmDiagnosticMessage> messages = new();
            if (roi.WasClipped)
                messages.Add(new AlgorithmDiagnosticMessage("roi_clipped", "The requested ROI was intersected with the image bounds."));
            long totalCandidates = channels.Sum(channel => channel.BadPixelCandidateCount);
            if (totalCandidates > returnedCandidates.Count)
            {
                messages.Add(new AlgorithmDiagnosticMessage(
                    "bad_pixel_candidates_truncated",
                    $"Detected {totalCandidates} candidates; returned {returnedCandidates.Count} by parameter limit.",
                    Data: new Dictionary<string, string>
                    {
                        ["detected"] = totalCandidates.ToString(CultureInfo.InvariantCulture),
                        ["returned"] = returnedCandidates.Count.ToString(CultureInfo.InvariantCulture),
                    }));
            }

            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = artifacts,
                Diagnostics = new AlgorithmExecutionDiagnostics { Messages = messages },
            });
        }

        private static long Scan(
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            ChannelAccumulator[] channels,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            long included = 0;
            int height = Math.Max(1, roi.MaximumYExclusive - roi.MinimumY);
            for (int y = roi.MinimumY; y < roi.MaximumYExclusive; y++)
            {
                if (((y - roi.MinimumY) & 31) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.05 + 0.42 * (y - roi.MinimumY) / height, "roi.scan"));
                }
                for (int x = roi.MinimumX; x < roi.MaximumXExclusive; x++)
                {
                    if (!roi.Contains(x, y)) continue;
                    included++;
                    for (int channel = 0; channel < channels.Length; channel++)
                        channels[channel].Add(ReadValue(image, x, y, channel));
                }
            }
            return included;
        }

        private static void DetectBadPixels(
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            ChannelAccumulator[] channels,
            RoiStatisticsParameters parameters,
            List<BadPixelCandidate> returned,
            HashSet<long> badPixelLocations,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            int radius = parameters.BadPixelNeighborhoodRadius;
            int maximumNeighbors = checked((radius * 2 + 1) * (radius * 2 + 1) - 1);
            double[] neighbors = new double[maximumNeighbors];
            int height = Math.Max(1, roi.MaximumYExclusive - roi.MinimumY);
            for (int y = roi.MinimumY; y < roi.MaximumYExclusive; y++)
            {
                if (((y - roi.MinimumY) & 7) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.55 + 0.22 * (y - roi.MinimumY) / height, "roi.bad-pixels"));
                }
                for (int x = roi.MinimumX; x < roi.MaximumXExclusive; x++)
                {
                    if (!roi.Contains(x, y)) continue;
                    for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++)
                    {
                        double value = ReadValue(image, x, y, channelIndex);
                        if (!double.IsFinite(value)) continue;
                        int count = 0;
                        for (int offsetY = -radius; offsetY <= radius; offsetY++)
                        {
                            int neighborY = y + offsetY;
                            if (neighborY < roi.MinimumY || neighborY >= roi.MaximumYExclusive) continue;
                            for (int offsetX = -radius; offsetX <= radius; offsetX++)
                            {
                                if (offsetX == 0 && offsetY == 0) continue;
                                int neighborX = x + offsetX;
                                if (neighborX < roi.MinimumX || neighborX >= roi.MaximumXExclusive || !roi.Contains(neighborX, neighborY)) continue;
                                double neighbor = ReadValue(image, neighborX, neighborY, channelIndex);
                                if (double.IsFinite(neighbor)) neighbors[count++] = neighbor;
                            }
                        }
                        if (count < 3) continue;

                        Array.Sort(neighbors, 0, count);
                        double median = Median(neighbors, count);
                        for (int index = 0; index < count; index++) neighbors[index] = Math.Abs(neighbors[index] - median);
                        Array.Sort(neighbors, 0, count);
                        double madSigma = 1.4826 * Median(neighbors, count);
                        double minimumDeviation = parameters.BadPixelMinimumDeviationFraction * channels[channelIndex].NominalMaximum;
                        double threshold = Math.Max(minimumDeviation, parameters.BadPixelSigmaThreshold * madSigma);
                        double deviation = Math.Abs(value - median);
                        if (deviation <= threshold || deviation <= 0) continue;

                        ChannelAccumulator channel = channels[channelIndex];
                        channel.BadPixelCandidateCount++;
                        badPixelLocations.Add(((long)y << 32) | (uint)x);
                        if (returned.Count < parameters.MaximumBadPixelCandidates)
                        {
                            double confidence = threshold <= 0 ? 1 : Math.Clamp(1 - threshold / deviation, 0, 1);
                            returned.Add(new BadPixelCandidate(x, y, channelIndex, channel.Name, value, median, deviation, threshold, confidence));
                        }
                    }
                }
            }
        }

        private static IReadOnlyList<AlgorithmArtifact> BuildArtifacts(
            AlgorithmExecutionContext context,
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            ChannelAccumulator[] channels,
            long includedPixels,
            IReadOnlyList<BadPixelCandidate> candidates,
            long badPixelLocationCount,
            RoiStatisticsParameters parameters)
        {
            List<AlgorithmMeasurement> measurements =
            [
                new("roi.pixel_count", includedPixels, "px"),
                new("roi.bad_pixel_candidate_count", badPixelLocationCount, "px"),
                new("roi.bad_pixel_channel_candidate_count", channels.Sum(channel => channel.BadPixelCandidateCount), "candidate-channel"),
            ];
            List<AlgorithmTableColumn> summaryColumns =
            [
                new("Channel", "integer"), new("ChannelName", "string"), new("IncludedCount", "integer", "px"),
                new("ValidCount", "integer", "px"), new("InvalidCount", "integer", "px"), new("NaNCount", "integer", "px"),
                new("PositiveInfinityCount", "integer", "px"), new("NegativeInfinityCount", "integer", "px"),
                new("Minimum", "number"), new("Maximum", "number"), new("Mean", "number"), new("StdDevPopulation", "number"),
                new("LowSaturatedCount", "integer", "px"), new("HighSaturatedCount", "integer", "px"),
                new("SaturatedCount", "integer", "px"), new("BadPixelCandidateCount", "integer", "px"),
            ];
            string[] percentileNames = parameters.Percentiles
                .Select(percentile => $"P{percentile.ToString("0.###", CultureInfo.InvariantCulture)}")
                .ToArray();
            summaryColumns.AddRange(percentileNames.Select(name => new AlgorithmTableColumn(name, "number")));

            List<IReadOnlyDictionary<string, JsonElement>> summaryRows = new();
            foreach (ChannelAccumulator channel in channels)
            {
                measurements.Add(new AlgorithmMeasurement("channel.valid_count", channel.ValidCount, "px", channel.Index));
                measurements.Add(new AlgorithmMeasurement("channel.invalid_count", channel.InvalidCount, "px", channel.Index));
                measurements.Add(new AlgorithmMeasurement("channel.nan_count", channel.NaNCount, "px", channel.Index));
                measurements.Add(new AlgorithmMeasurement("channel.positive_infinity_count", channel.PositiveInfinityCount, "px", channel.Index));
                measurements.Add(new AlgorithmMeasurement("channel.negative_infinity_count", channel.NegativeInfinityCount, "px", channel.Index));
                measurements.Add(new AlgorithmMeasurement("channel.low_saturated_count", channel.LowSaturatedCount, "px", channel.Index));
                measurements.Add(new AlgorithmMeasurement("channel.high_saturated_count", channel.HighSaturatedCount, "px", channel.Index));
                measurements.Add(new AlgorithmMeasurement("channel.saturated_count", channel.LowSaturatedCount + channel.HighSaturatedCount, "px", channel.Index));
                measurements.Add(new AlgorithmMeasurement("channel.bad_pixel_candidate_count", channel.BadPixelCandidateCount, "px", channel.Index));
                if (channel.ValidCount > 0)
                {
                    measurements.Add(new AlgorithmMeasurement("channel.minimum", channel.Minimum, ChannelUnit(image.Format), channel.Index));
                    measurements.Add(new AlgorithmMeasurement("channel.maximum", channel.Maximum, ChannelUnit(image.Format), channel.Index));
                    measurements.Add(new AlgorithmMeasurement("channel.mean", channel.Mean, ChannelUnit(image.Format), channel.Index));
                    measurements.Add(new AlgorithmMeasurement("channel.stddev.population", channel.StandardDeviation, ChannelUnit(image.Format), channel.Index));
                    for (int index = 0; index < parameters.Percentiles.Length; index++)
                        measurements.Add(new AlgorithmMeasurement("channel.percentile", channel.Percentile(parameters.Percentiles[index]), ChannelUnit(image.Format), channel.Index,
                            Qualifiers: new Dictionary<string, string> { ["percentile"] = parameters.Percentiles[index].ToString("0.###", CultureInfo.InvariantCulture) }));
                }

                Dictionary<string, JsonElement> row = Row(
                    ("Channel", channel.Index), ("ChannelName", channel.Name), ("IncludedCount", includedPixels),
                    ("ValidCount", channel.ValidCount), ("InvalidCount", channel.InvalidCount), ("NaNCount", channel.NaNCount),
                    ("PositiveInfinityCount", channel.PositiveInfinityCount), ("NegativeInfinityCount", channel.NegativeInfinityCount),
                    ("Minimum", channel.ValidCount > 0 ? channel.Minimum : null), ("Maximum", channel.ValidCount > 0 ? channel.Maximum : null),
                    ("Mean", channel.ValidCount > 0 ? channel.Mean : null), ("StdDevPopulation", channel.ValidCount > 0 ? channel.StandardDeviation : null),
                    ("LowSaturatedCount", channel.LowSaturatedCount), ("HighSaturatedCount", channel.HighSaturatedCount),
                    ("SaturatedCount", channel.LowSaturatedCount + channel.HighSaturatedCount),
                    ("BadPixelCandidateCount", channel.BadPixelCandidateCount));
                for (int index = 0; index < percentileNames.Length; index++)
                    row[percentileNames[index]] = Value(channel.ValidCount > 0 ? channel.Percentile(parameters.Percentiles[index]) : null);
                summaryRows.Add(row);
            }

            List<IReadOnlyDictionary<string, JsonElement>> histogramRows = new();
            foreach (ChannelAccumulator channel in channels)
            {
                foreach (HistogramBin bin in channel.Histogram(parameters.HistogramBins))
                {
                    histogramRows.Add(Row(
                        ("Channel", channel.Index), ("ChannelName", channel.Name), ("BinIndex", bin.Index),
                        ("LowerInclusive", bin.Lower), ("Upper", bin.Upper), ("UpperInclusive", bin.IsUpperInclusive),
                        ("Count", bin.Count)));
                }
            }

            List<IReadOnlyDictionary<string, JsonElement>> candidateRows = candidates.Select(candidate =>
                (IReadOnlyDictionary<string, JsonElement>)Row(
                    ("X", candidate.X), ("Y", candidate.Y), ("Channel", candidate.Channel), ("ChannelName", candidate.ChannelName),
                    ("Value", candidate.Value), ("LocalMedian", candidate.LocalMedian), ("Deviation", candidate.Deviation),
                    ("Threshold", candidate.Threshold), ("Confidence", candidate.Confidence), ("Reason", "local_median_outlier")))
                .ToList();

            List<AlgorithmGeometry> geometries = [roi.Geometry];
            geometries.AddRange(candidates.Select((candidate, index) => new AlgorithmGeometry(
                $"bad-pixel-{index}",
                AlgorithmGeometryKind.Point,
                [new AlgorithmPoint(candidate.X, candidate.Y)],
                Residual: candidate.Deviation,
                Confidence: candidate.Confidence,
                FilterReason: "local_median_outlier",
                Measurements: new Dictionary<string, double>
                {
                    ["channel"] = candidate.Channel,
                    ["value"] = candidate.Value,
                    ["localMedian"] = candidate.LocalMedian,
                    ["threshold"] = candidate.Threshold,
                })));
            List<AlgorithmOverlayItem> overlayItems = [new("roi", new AlgorithmOverlayStyle("#FFFFA500", "#20FFA500", 1.5, "ROI"))];
            overlayItems.AddRange(candidates.Select((candidate, index) =>
                new AlgorithmOverlayItem($"bad-pixel-{index}", new AlgorithmOverlayStyle("#FFFF3030", null, 1.5, $"{candidate.ChannelName}"))));

            JsonElement provenance = AlgorithmJson.ToElement(new
            {
                schema = ResultSchema,
                input = new { image.Width, image.Height, format = image.Format.ToString(), image.DpiX, image.DpiY },
                roi = context.Invocation.Roi,
                roiPixelBounds = new { roi.MinimumX, roi.MinimumY, roi.MaximumXExclusive, roi.MaximumYExclusive, roi.WasClipped },
                includedPixels,
                parameters,
                standardDeviation = "population",
                percentileInterpolation = "linear-rank-n-minus-one",
                integerHistogramRange = "bit-depth-nominal",
                floatHistogramRange = "finite-roi-min-max",
            });

            return new AlgorithmArtifact[]
            {
                new AlgorithmMeasurementArtifact("roi-statistics", measurements),
                new AlgorithmTableArtifact("roi-statistics-summary", summaryColumns, summaryRows),
                new AlgorithmTableArtifact("roi-histogram",
                [
                    new("Channel", "integer"), new("ChannelName", "string"), new("BinIndex", "integer"),
                    new("LowerInclusive", "number"), new("Upper", "number"), new("UpperInclusive", "boolean"), new("Count", "integer", "px"),
                ], histogramRows),
                new AlgorithmTableArtifact("bad-pixel-candidates",
                [
                    new("X", "integer", "px"), new("Y", "integer", "px"), new("Channel", "integer"), new("ChannelName", "string"),
                    new("Value", "number"), new("LocalMedian", "number"), new("Deviation", "number"), new("Threshold", "number"),
                    new("Confidence", "number"), new("Reason", "string"),
                ], candidateRows),
                new AlgorithmGeometryArtifact("roi-statistics-geometry", AlgorithmCoordinateSpace.Pixel, geometries),
                new AlgorithmOverlayArtifact("roi-statistics-overlay", AlgorithmOverlayLifetime.Transient, overlayItems),
                new AlgorithmStructuredDataArtifact("roi-statistics-provenance", ResultSchema, provenance),
            };
        }

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string path)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path)],
            };

        private static Dictionary<string, JsonElement> Row(params (string Name, object? Value)[] values)
            => values.ToDictionary(value => value.Name, value => Value(value.Value), StringComparer.Ordinal);

        private static JsonElement Value(object? value) => AlgorithmJson.ToElement(value);

        private static double Median(double[] sorted, int count)
            => count % 2 == 1 ? sorted[count / 2] : (sorted[count / 2 - 1] + sorted[count / 2]) / 2;

        private static double ReadValue(AlgorithmImageBuffer image, int x, int y, int channel)
        {
            ReadOnlySpan<byte> data = image.Data.Span;
            int bytesPerChannel = image.Format.BitsPerChannel() / 8;
            int offset = checked(y * image.Stride + x * image.Format.BytesPerPixel() + channel * bytesPerChannel);
            return image.Format.BitsPerChannel() switch
            {
                8 => data[offset],
                16 => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2)),
                32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4))),
                _ => throw new ArgumentOutOfRangeException(nameof(image)),
            };
        }

        private static string ChannelName(AlgorithmImageFormat format, int channel)
        {
            if (format.Channels() == 1) return "Gray";
            return channel switch { 0 => "B", 1 => "G", 2 => "R", 3 => "A", _ => channel.ToString(CultureInfo.InvariantCulture) };
        }

        private static string ChannelUnit(AlgorithmImageFormat format) => format.IsFloatingPoint() ? "value" : "DN";

        private readonly record struct HistogramBin(int Index, double Lower, double Upper, bool IsUpperInclusive, long Count);

        private readonly record struct BadPixelCandidate(
            int X,
            int Y,
            int Channel,
            string ChannelName,
            double Value,
            double LocalMedian,
            double Deviation,
            double Threshold,
            double Confidence);

        private sealed class ChannelAccumulator
        {
            private readonly long[]? _integerCounts;
            private readonly List<float>? _floatValues;
            private double _m2;

            public ChannelAccumulator(int index, string name, AlgorithmImageFormat format)
            {
                Index = index;
                Name = name;
                NominalMaximum = format.BitsPerChannel() switch { 8 => byte.MaxValue, 16 => ushort.MaxValue, _ => 1 };
                if (format.IsFloatingPoint()) _floatValues = new List<float>();
                else _integerCounts = new long[(int)NominalMaximum + 1];
            }

            public int Index { get; }
            public string Name { get; }
            public double NominalMaximum { get; }
            public long ValidCount { get; private set; }
            public long InvalidCount { get; private set; }
            public long NaNCount { get; private set; }
            public long PositiveInfinityCount { get; private set; }
            public long NegativeInfinityCount { get; private set; }
            public long LowSaturatedCount { get; private set; }
            public long HighSaturatedCount { get; private set; }
            public long BadPixelCandidateCount { get; set; }
            public double Minimum { get; private set; } = double.PositiveInfinity;
            public double Maximum { get; private set; } = double.NegativeInfinity;
            public double Mean { get; private set; }
            public double StandardDeviation => ValidCount == 0 ? double.NaN : Math.Sqrt(_m2 / ValidCount);

            public void Add(double value)
            {
                if (!double.IsFinite(value))
                {
                    InvalidCount++;
                    if (double.IsNaN(value)) NaNCount++;
                    else if (double.IsPositiveInfinity(value)) PositiveInfinityCount++;
                    else NegativeInfinityCount++;
                    return;
                }

                ValidCount++;
                Minimum = Math.Min(Minimum, value);
                Maximum = Math.Max(Maximum, value);
                double delta = value - Mean;
                Mean += delta / ValidCount;
                _m2 += delta * (value - Mean);
                if (value <= 0) LowSaturatedCount++;
                if (value >= NominalMaximum) HighSaturatedCount++;
                if (_integerCounts != null) _integerCounts[(int)value]++;
                else _floatValues!.Add((float)value);
            }

            public void PreparePercentiles()
            {
                _floatValues?.Sort();
            }

            public double Percentile(double percentile)
            {
                if (ValidCount == 0) return double.NaN;
                double rank = percentile / 100 * (ValidCount - 1);
                long lowerRank = (long)Math.Floor(rank);
                long upperRank = (long)Math.Ceiling(rank);
                double lower = ValueAtRank(lowerRank);
                double upper = ValueAtRank(upperRank);
                return lower + (upper - lower) * (rank - lowerRank);
            }

            public IEnumerable<HistogramBin> Histogram(int bins)
            {
                long[] counts = new long[bins];
                if (_integerCounts != null)
                {
                    long range = (long)NominalMaximum + 1;
                    for (int value = 0; value < _integerCounts.Length; value++)
                    {
                        int index = (int)Math.Min(bins - 1, value * (long)bins / range);
                        counts[index] += _integerCounts[value];
                    }
                    for (int index = 0; index < bins; index++)
                        yield return new HistogramBin(index, index * range / (double)bins, (index + 1) * range / (double)bins, index == bins - 1, counts[index]);
                    yield break;
                }

                if (ValidCount == 0) yield break;
                double rangeFloat = Maximum - Minimum;
                foreach (float value in _floatValues!)
                {
                    int index = rangeFloat <= 0 ? 0 : (int)Math.Min(bins - 1, (value - Minimum) / rangeFloat * bins);
                    counts[index]++;
                }
                for (int index = 0; index < bins; index++)
                {
                    double lower = rangeFloat <= 0 ? Minimum : Minimum + rangeFloat * index / bins;
                    double upper = rangeFloat <= 0 ? Maximum : Minimum + rangeFloat * (index + 1) / bins;
                    yield return new HistogramBin(index, lower, upper, index == bins - 1, counts[index]);
                }
            }

            private double ValueAtRank(long rank)
            {
                if (_floatValues != null) return _floatValues[checked((int)rank)];
                long cumulative = 0;
                for (int value = 0; value < _integerCounts!.Length; value++)
                {
                    cumulative += _integerCounts[value];
                    if (cumulative > rank) return value;
                }
                return Maximum;
            }
        }

    }
}
