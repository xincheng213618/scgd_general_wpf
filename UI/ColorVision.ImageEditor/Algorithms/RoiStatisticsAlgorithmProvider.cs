using ColorVision.Algorithms;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic CPU implementation of rectangle, circle and polygon ROI statistics.</summary>
    public sealed class RoiStatisticsAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.analysis.roi-statistics/v1";
        private const int MaximumStackPercentiles = 32;
        private const int ParallelBadPixelThreshold = 512 * 1024;
        private const int MaximumBadPixelWorkers = 4;
        private const int ParallelCandidateCapacity = 16 * 1024;
        internal const long MaximumExactFloatingValueBytes = 16L * 1024 * 1024;
        private static readonly IReadOnlySet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();
        private (AlgorithmPixelRoi Roi, ImageSelectionScope Source)? DisplayRegion { get; init; }

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

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.RoiStatistics, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.RoiStatistics
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        internal static AlgorithmPixelRoi? GetDisplayRegion(AlgorithmRoi roi, ImageSelectionScope source)
        {
            if (source.PixelWidth <= 0 || source.PixelHeight <= 0 || !roi.Validate().IsValid
                || roi is not (RectangleAlgorithmRoi or CircleAlgorithmRoi or PolygonAlgorithmRoi))
                return null;

            AlgorithmPixelRoi region = AlgorithmPixelRoi.Create(roi, source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY);
            if (region.IsEmpty || (region.MinimumX == 0 && region.MinimumY == 0
                && region.MaximumXExclusive == source.PixelWidth && region.MaximumYExclusive == source.PixelHeight))
                return null;
            return region;
        }

        internal static AlgorithmRunner CreateDisplayRegionRunner(AlgorithmRuntime runtime, AlgorithmPixelRoi region, ImageSelectionScope source)
            => new(runtime.Catalog, [new RoiStatisticsAlgorithmProvider { DisplayRegion = (region, source) }], runtime.Scheduler);

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
            AlgorithmPixelRoi roi = DisplayRegion?.Roi ?? AlgorithmPixelRoi.Create(context.Invocation.Roi, image);
            if (roi.IsEmpty)
                return ValueTask.FromResult(Failure(context, "roi_empty_after_clip", "The ROI does not contain any image pixel centers after clipping.", "roi"));

            int originX = DisplayRegion?.Roi.MinimumX ?? 0;
            int originY = DisplayRegion?.Roi.MinimumY ?? 0;
            int sourceWidth = DisplayRegion?.Source.PixelWidth ?? image.Width;
            int sourceHeight = DisplayRegion?.Source.PixelHeight ?? image.Height;

            // A rectangular ROI includes the entire clipped raster when both corner pixels are inside.
            bool boundsCovered = roi.Geometry.Kind == AlgorithmGeometryKind.Rectangle
                && roi.Contains(roi.MinimumX, roi.MinimumY)
                && roi.Contains(roi.MaximumXExclusive - 1, roi.MaximumYExclusive - 1);
            roi = roi.WithCircleScanlines(cancellationToken).WithPolygonScanlines(cancellationToken);
            var circleRows = roi.CircleScanlines;
            int channelCount = image.Format.Channels();
            long? preflightPixelCount = null;
            if (image.Format.IsFloatingPoint())
            {
                long maximumPixels = MaximumExactFloatingValueBytes / checked(channelCount * sizeof(float));
                context.Progress?.Report(new AlgorithmProgress(0.02, "roi.float-budget", "Checking the exact floating-point statistics budget"));
                cancellationToken.ThrowIfCancellationRequested();
                preflightPixelCount = boundsCovered
                    ? (long)(roi.MaximumXExclusive - roi.MinimumX) * (roi.MaximumYExclusive - roi.MinimumY)
                    : roi.PreparedPolygon?.PixelCount ?? CountIncludedPixels(roi, circleRows, maximumPixels, cancellationToken);
                if (ExceedsExactFloatingStatisticsBudget(preflightPixelCount.Value, channelCount, out _))
                {
                    return ValueTask.FromResult(Failure(
                        context,
                        "roi_exact_float_statistics_budget_exceeded",
                        $"Exact floating-point percentiles and histograms retain at most {MaximumExactFloatingValueBytes} bytes of ROI samples; reduce the ROI or channel count.",
                        "roi"));
                }
            }

            context.Progress?.Report(new AlgorithmProgress(0.05, "roi.scan", "Scanning ROI pixels"));
            int floatingCapacity = preflightPixelCount.HasValue ? checked((int)preflightPixelCount.Value) : 0;
            ChannelAccumulator[] channels = Enumerable.Range(0, channelCount)
                .Select(index => new ChannelAccumulator(index, ChannelName(image.Format, index), image.Format, floatingCapacity))
                .ToArray();
            long includedPixels;
            if (boundsCovered || circleRows != null || roi.PreparedPolygon != null)
            {
                includedPixels = image.Format.BitsPerChannel() switch
                {
                    8 => ScanIntegerRows<byte>(image, roi, circleRows, originX, originY, channels, cancellationToken, context.Progress),
                    16 when BitConverter.IsLittleEndian => ScanIntegerRows<ushort>(image, roi, circleRows, originX, originY, channels, cancellationToken, context.Progress),
                    _ => roi.PreparedPolygon == null
                        ? Scan<FullBounds>(image, roi, circleRows, originX, originY, channels, cancellationToken, context.Progress)
                        : Scan<PolygonBounds>(image, roi, null, originX, originY, channels, cancellationToken, context.Progress),
                };
            }
            else includedPixels = Scan<ShapeBoundary>(image, roi, null, originX, originY, channels, cancellationToken, context.Progress);
            if (includedPixels == 0)
                return ValueTask.FromResult(Failure(context, "roi_empty_after_clip", "The ROI does not contain any image pixel centers after clipping.", "roi"));

            context.Progress?.Report(new AlgorithmProgress(0.5, "roi.percentiles", "Ordering finite samples for exact percentiles"));
            cancellationToken.ThrowIfCancellationRequested();
            foreach (ChannelAccumulator channel in channels) channel.PreparePercentiles(parameters.Percentiles, cancellationToken);
            BadPixelDetectionResult badPixels = BadPixelDetectionResult.Empty;
            if (parameters.DetectBadPixelCandidates && parameters.MaximumBadPixelCandidates > 0)
            {
                context.Progress?.Report(new AlgorithmProgress(0.55, "roi.bad-pixels", "Detecting local outlier candidates"));
                badPixels = boundsCovered
                    ? DetectBadPixels<FullBounds>(image, roi, originX, originY, channels, parameters, cancellationToken, context.Progress)
                    : circleRows != null
                        ? DetectBadPixels<CircleBounds>(image, roi, originX, originY, channels, parameters, cancellationToken, context.Progress)
                        : roi.PreparedPolygon != null
                            ? DetectBadPixels<PolygonBounds>(image, roi, originX, originY, channels, parameters, cancellationToken, context.Progress)
                            : DetectBadPixels<ShapeBoundary>(image, roi, originX, originY, channels, parameters, cancellationToken, context.Progress);
            }

            context.Progress?.Report(new AlgorithmProgress(0.82, "roi.artifacts", "Building structured result artifacts"));
            IReadOnlyList<AlgorithmArtifact> artifacts = BuildArtifacts(
                context,
                image,
                sourceWidth,
                sourceHeight,
                roi,
                channels,
                includedPixels,
                badPixels.Candidates,
                badPixels.UniquePixelCount,
                parameters);
            List<AlgorithmDiagnosticMessage> messages = new();
            if (roi.WasClipped)
                messages.Add(new AlgorithmDiagnosticMessage("roi_clipped", "The requested ROI was intersected with the image bounds."));
            long totalCandidates = channels.Sum(channel => channel.BadPixelCandidateCount);
            if (totalCandidates > badPixels.Candidates.Count)
            {
                messages.Add(new AlgorithmDiagnosticMessage(
                    "bad_pixel_candidates_truncated",
                    $"Detected {totalCandidates} candidates; returned {badPixels.Candidates.Count} strongest candidates by parameter limit.",
                    Data: new Dictionary<string, string>
                    {
                        ["detected"] = totalCandidates.ToString(CultureInfo.InvariantCulture),
                        ["returned"] = badPixels.Candidates.Count.ToString(CultureInfo.InvariantCulture),
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

        private static long CountIncludedPixels(
            AlgorithmPixelRoi roi,
            (int Start, int End)[]? circleRows,
            long maximumPixels,
            CancellationToken cancellationToken)
        {
            long included = 0;
            for (int y = roi.MinimumY; y < roi.MaximumYExclusive; y++)
            {
                if (((y - roi.MinimumY) & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (circleRows != null)
                {
                    var (start, end) = circleRows[y - roi.MinimumY];
                    included += end - start;
                    if (included > maximumPixels) return maximumPixels + 1;
                    continue;
                }
                for (int x = roi.MinimumX; x < roi.MaximumXExclusive; x++)
                {
                    if (((x - roi.MinimumX) & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (!roi.Contains(x, y)) continue;
                    included++;
                    if (included > maximumPixels) return included;
                }
            }
            return included;
        }

        internal static bool ExceedsExactFloatingStatisticsBudget(long pixelCount, int channelCount, out long requiredBytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(pixelCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channelCount);
            try
            {
                requiredBytes = checked(pixelCount * channelCount * sizeof(float));
            }
            catch (OverflowException)
            {
                requiredBytes = long.MaxValue;
                return true;
            }
            return requiredBytes > MaximumExactFloatingValueBytes;
        }

        private static long ScanIntegerRows<T>(
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            (int Start, int End)[]? circleRows,
            int originX,
            int originY,
            ChannelAccumulator[] channels,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
            where T : unmanaged, IBinaryInteger<T>
        {
            ReadOnlySpan<byte> data = image.Data.Span;
            int pixelBytes = image.Format.BytesPerPixel();
            long included = 0;
            int height = roi.MaximumYExclusive - roi.MinimumY;
            var polygon = roi.PreparedPolygon;
            Span<(int Start, int End)> singleRun = stackalloc (int, int)[1];
            for (int row = 0; row < height; row++)
            {
                if ((row & 31) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.05 + 0.42 * row / height, "roi.scan"));
                }
                int y = roi.MinimumY + row;
                scoped ReadOnlySpan<(int Start, int End)> runs;
                if (polygon != null) runs = polygon.GetRuns(y);
                else
                {
                    singleRun[0] = circleRows == null ? (roi.MinimumX, roi.MaximumXExclusive) : circleRows[row];
                    runs = singleRun;
                }
                int rowOffset = (y - originY) * image.Stride;
                foreach (var (start, end) in runs)
                {
                    if (start == end) continue;
                    int rowBytes = (end - start) * pixelBytes;
                    included += end - start;
                    int offset = rowOffset + (start - originX) * pixelBytes;
                    ReadOnlySpan<T> samples = MemoryMarshal.Cast<byte, T>(data.Slice(offset, rowBytes));
                    foreach (ChannelAccumulator channel in channels)
                        channel.AddIntegerRow(samples, channels.Length);
                }
            }
            foreach (ChannelAccumulator channel in channels) channel.UpdateIntegerSaturationCounts();
            return included;
        }

        private static long Scan<TMembership>(
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            (int Start, int End)[]? circleRows,
            int originX,
            int originY,
            ChannelAccumulator[] channels,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
            where TMembership : struct, IRoiMembership
        {
            long included = 0;
            ReadOnlySpan<byte> data = image.Data.Span;
            int stride = image.Stride;
            int pixelBytes = image.Format.BytesPerPixel();
            int channelBytes = pixelBytes / channels.Length;
            int height = Math.Max(1, roi.MaximumYExclusive - roi.MinimumY);
            for (int y = roi.MinimumY; y < roi.MaximumYExclusive; y++)
            {
                if (((y - roi.MinimumY) & 31) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.05 + 0.42 * (y - roi.MinimumY) / height, "roi.scan"));
                }
                var (start, end) = circleRows == null ? (roi.MinimumX, roi.MaximumXExclusive) : circleRows[y - roi.MinimumY];
                for (int x = start; x < end; x++)
                {
                    if (!TMembership.Contains(roi, x, y)) continue;
                    included++;
                    int pixelOffset = (y - originY) * stride + (x - originX) * pixelBytes;
                    for (int channel = 0; channel < channels.Length; channel++)
                        channels[channel].Add(ReadValue(data, pixelOffset + channel * channelBytes, channelBytes));
                }
            }
            return included;
        }

        private static BadPixelDetectionResult DetectBadPixels<TMembership>(
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            int originX,
            int originY,
            ChannelAccumulator[] channels,
            RoiStatisticsParameters parameters,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
            where TMembership : struct, IRoiMembership
        {
            int height = roi.MaximumYExclusive - roi.MinimumY;
            int width = roi.MaximumXExclusive - roi.MinimumX;
            int capacity = parameters.MaximumBadPixelCandidates;
            int radius = parameters.BadPixelNeighborhoodRadius;
            int maximumNeighbors = checked((radius * 2 + 1) * (radius * 2 + 1) - 1);
            int workers = height >= 32 && (long)width * height >= ParallelBadPixelThreshold
                ? Math.Min(Math.Min(MaximumBadPixelWorkers, Environment.ProcessorCount), Math.Max(1, ParallelCandidateCapacity / capacity))
                : 1;
            var scans = new BadPixelScan[workers];
            for (int worker = 0; worker < workers; worker++) scans[worker] = new(capacity, maximumNeighbors, channels.Length);

            if (workers == 1)
            {
                ScanBadPixelRows<TMembership>(image, roi, originX, originY, channels, parameters, scans[0],
                    roi.MinimumY, roi.MaximumYExclusive, cancellationToken, progress);
            }
            else
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = workers,
                    TaskScheduler = TaskScheduler.Default,
                    CancellationToken = cancellationToken,
                };
                int rowsPerBand = Math.Clamp(131_072 / (width * channels.Length), 8, 64);
                // Report between bounded batches on the caller thread. Each band owns its
                // scratch and retains only its strongest candidates across those batches.
                for (int top = roi.MinimumY; top < roi.MaximumYExclusive;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.55 + 0.22 * (top - roi.MinimumY) / height, "roi.bad-pixels"));
                    int end = Math.Min(roi.MaximumYExclusive, top + rowsPerBand * workers);
                    Parallel.For(0, workers, options, worker =>
                    {
                        int firstRow = top + worker * rowsPerBand;
                        ScanBadPixelRows<TMembership>(image, roi, originX, originY, channels, parameters, scans[worker],
                            firstRow, Math.Min(end, firstRow + rowsPerBand), cancellationToken, null);
                    });
                    top = end;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            BadPixelScan combined = scans[0];
            for (int worker = 1; worker < scans.Length; worker++)
            {
                BadPixelScan scan = scans[worker];
                combined.UniquePixelCount += scan.UniquePixelCount;
                for (int channel = 0; channel < channels.Length; channel++)
                    combined.ChannelCounts[channel] += scan.ChannelCounts[channel];
                foreach (var item in scan.Strongest.UnorderedItems) combined.Keep(item.Element);
            }
            for (int channel = 0; channel < channels.Length; channel++)
                channels[channel].BadPixelCandidateCount = combined.ChannelCounts[channel];

            BadPixelCandidate[] candidates = combined.Strongest.UnorderedItems.Select(item => item.Element).ToArray();
            Array.Sort(candidates, (left, right) => BadPixelCandidateComparer.Instance.Compare(right, left));
            return new BadPixelDetectionResult(candidates, combined.UniquePixelCount);
        }

        private static void ScanBadPixelRows<TMembership>(AlgorithmImageBuffer image, AlgorithmPixelRoi roi,
            int originX, int originY, ChannelAccumulator[] channels, RoiStatisticsParameters parameters, BadPixelScan scan,
            int firstRow, int endRow, CancellationToken cancellationToken, IProgress<AlgorithmProgress>? progress)
            where TMembership : struct, IRoiMembership
        {
            int radius = parameters.BadPixelNeighborhoodRadius;
            double[] neighbors = scan.Neighbors;
            ReadOnlySpan<byte> data = image.Data.Span;
            int stride = image.Stride;
            int pixelBytes = image.Format.BytesPerPixel();
            int channelBytes = pixelBytes / channels.Length;
            double minimumDeviation = parameters.BadPixelMinimumDeviationFraction * channels[0].NominalMaximum;
            bool useInteriorNeighborhood = typeof(TMembership) == typeof(FullBounds) && radius == 1 && channelBytes is 1 or 2;
            int height = Math.Max(1, roi.MaximumYExclusive - roi.MinimumY);
            for (int y = firstRow; y < endRow; y++)
            {
                if (((y - roi.MinimumY) & 7) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.55 + 0.22 * (y - roi.MinimumY) / height, "roi.bad-pixels"));
                }
                bool interiorRow = useInteriorNeighborhood && y > roi.MinimumY && y < roi.MaximumYExclusive - 1;
                for (int x = roi.MinimumX; x < roi.MaximumXExclusive; x++)
                {
                    if (((x - roi.MinimumX) & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (!TMembership.Contains(roi, x, y)) continue;
                    bool pixelIsCandidate = false;
                    int pixelOffset = (y - originY) * stride + (x - originX) * pixelBytes;
                    bool interior = interiorRow && x > roi.MinimumX && x < roi.MaximumXExclusive - 1;
                    for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++)
                    {
                        int channelOffset = channelIndex * channelBytes;
                        int center = pixelOffset + channelOffset;
                        double value = ReadValue(data, center, channelBytes);
                        if (!double.IsFinite(value)) continue;
                        int count;
                        if (interior)
                        {
                            if (!TryCollectInteriorNeighborhood(data, center, stride, pixelBytes, channelBytes,
                                value, minimumDeviation, neighbors)) continue;
                            count = neighbors.Length;
                        }
                        else
                        {
                            count = 0;
                            int distantNeighbors = 0;
                            for (int offsetY = -radius; offsetY <= radius; offsetY++)
                            {
                                int neighborY = y + offsetY;
                                if (neighborY < roi.MinimumY || neighborY >= roi.MaximumYExclusive) continue;
                                int neighborRowOffset = (neighborY - originY) * stride + channelOffset;
                                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                                {
                                    if (offsetX == 0 && offsetY == 0) continue;
                                    int neighborX = x + offsetX;
                                    if (neighborX < roi.MinimumX || neighborX >= roi.MaximumXExclusive
                                        || !TMembership.Contains(roi, neighborX, neighborY)) continue;
                                    double neighbor = ReadValue(data, neighborRowOffset + (neighborX - originX) * pixelBytes, channelBytes);
                                    if (!double.IsFinite(neighbor)) continue;
                                    neighbors[count++] = neighbor;
                                    distantNeighbors += Math.Abs(value - neighbor) > minimumDeviation ? 1 : 0;
                                }
                            }
                            // Fewer than half the neighbors outside the limit keeps both middle ranks within it.
                            if (count < 3 || distantNeighbors * 2 < count) continue;
                        }

                        Array.Sort(neighbors, 0, count);
                        double median = Median(neighbors, count);
                        double deviation = Math.Abs(value - median);
                        if (deviation <= minimumDeviation) continue;
                        for (int index = 0; index < count; index++) neighbors[index] = Math.Abs(neighbors[index] - median);
                        Array.Sort(neighbors, 0, count);
                        double madSigma = 1.4826 * Median(neighbors, count);
                        double threshold = Math.Max(minimumDeviation, parameters.BadPixelSigmaThreshold * madSigma);
                        if (deviation <= threshold) continue;

                        scan.ChannelCounts[channelIndex]++;
                        pixelIsCandidate = true;
                        double confidence = threshold <= 0 ? 1 : Math.Clamp(1 - threshold / deviation, 0, 1);
                        scan.Keep(new BadPixelCandidate(
                            x,
                            y,
                            channelIndex,
                            channels[channelIndex].Name,
                            value,
                            median,
                            deviation,
                            threshold,
                            confidence));
                    }
                    if (pixelIsCandidate) scan.UniquePixelCount++;
                }
            }
        }

        private static bool TryCollectInteriorNeighborhood(ReadOnlySpan<byte> data, int center, int stride, int pixelBytes,
            int channelBytes, double value, double minimumDeviation, double[] neighbors)
        {
            int above = center - stride;
            double topLeft = ReadValue(data, above - pixelBytes, channelBytes);
            double top = ReadValue(data, above, channelBytes);
            double topRight = ReadValue(data, above + pixelBytes, channelBytes);
            double left = ReadValue(data, center - pixelBytes, channelBytes);
            double right = ReadValue(data, center + pixelBytes, channelBytes);
            int distant = (Math.Abs(value - topLeft) > minimumDeviation ? 1 : 0)
                + (Math.Abs(value - top) > minimumDeviation ? 1 : 0)
                + (Math.Abs(value - topRight) > minimumDeviation ? 1 : 0)
                + (Math.Abs(value - left) > minimumDeviation ? 1 : 0)
                + (Math.Abs(value - right) > minimumDeviation ? 1 : 0);
            // Five close neighbors leave both middle ranks within the limit, regardless of the last three.
            if (distant == 0) return false;

            int below = center + stride;
            double bottomLeft = ReadValue(data, below - pixelBytes, channelBytes);
            double bottom = ReadValue(data, below, channelBytes);
            double bottomRight = ReadValue(data, below + pixelBytes, channelBytes);
            distant += (Math.Abs(value - bottomLeft) > minimumDeviation ? 1 : 0)
                + (Math.Abs(value - bottom) > minimumDeviation ? 1 : 0)
                + (Math.Abs(value - bottomRight) > minimumDeviation ? 1 : 0);
            if (distant < 4) return false;

            neighbors[0] = topLeft;
            neighbors[1] = top;
            neighbors[2] = topRight;
            neighbors[3] = left;
            neighbors[4] = right;
            neighbors[5] = bottomLeft;
            neighbors[6] = bottom;
            neighbors[7] = bottomRight;
            return true;
        }

        // Specialize the scan loops once per ROI so shape checks need no per-pixel mode branch.
        private interface IRoiMembership
        {
            static abstract bool Contains(AlgorithmPixelRoi roi, int x, int y);
        }

        private readonly struct FullBounds : IRoiMembership
        {
            public static bool Contains(AlgorithmPixelRoi roi, int x, int y) => true;
        }

        private readonly struct CircleBounds : IRoiMembership
        {
            public static bool Contains(AlgorithmPixelRoi roi, int x, int y)
            {
                var (start, end) = roi.CircleScanlines![y - roi.MinimumY];
                return x >= start && x < end;
            }
        }

        private readonly struct PolygonBounds : IRoiMembership
        {
            public static bool Contains(AlgorithmPixelRoi roi, int x, int y) => roi.PreparedPolygon!.Contains(x, y);
        }

        private readonly struct ShapeBoundary : IRoiMembership
        {
            public static bool Contains(AlgorithmPixelRoi roi, int x, int y) => roi.Contains(x, y);
        }

        private static IReadOnlyList<AlgorithmArtifact> BuildArtifacts(
            AlgorithmExecutionContext context,
            AlgorithmImageBuffer image,
            int sourceWidth,
            int sourceHeight,
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

            Span<double> percentileValues = percentileNames.Length <= MaximumStackPercentiles
                ? stackalloc double[percentileNames.Length]
                : new double[percentileNames.Length];
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
                    channel.FillPercentiles(parameters.Percentiles, percentileValues);
                    measurements.Add(new AlgorithmMeasurement("channel.minimum", channel.Minimum, ChannelUnit(image.Format), channel.Index));
                    measurements.Add(new AlgorithmMeasurement("channel.maximum", channel.Maximum, ChannelUnit(image.Format), channel.Index));
                    measurements.Add(new AlgorithmMeasurement("channel.mean", channel.Mean, ChannelUnit(image.Format), channel.Index));
                    measurements.Add(new AlgorithmMeasurement("channel.stddev.population", channel.StandardDeviation, ChannelUnit(image.Format), channel.Index));
                    for (int index = 0; index < parameters.Percentiles.Length; index++)
                        measurements.Add(new AlgorithmMeasurement("channel.percentile", percentileValues[index], ChannelUnit(image.Format), channel.Index,
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
                    row[percentileNames[index]] = Value(channel.ValidCount > 0 ? percentileValues[index] : null);
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
                input = new { Width = sourceWidth, Height = sourceHeight, format = image.Format.ToString(), image.DpiX, image.DpiY },
                roi = context.Invocation.Roi,
                roiPixelBounds = new { roi.MinimumX, roi.MinimumY, roi.MaximumXExclusive, roi.MaximumYExclusive, roi.WasClipped },
                includedPixels,
                parameters,
                standardDeviation = "population",
                percentileInterpolation = "linear-rank-n-minus-one",
                integerHistogramRange = "bit-depth-nominal",
                floatHistogramRange = "finite-roi-min-max; constant distributions use one closed degenerate bin [value,value]",
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

        private static double ReadValue(ReadOnlySpan<byte> data, int offset, int channelBytes)
            => channelBytes switch
            {
                1 => data[offset],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]),
                4 => BinaryPrimitives.ReadSingleLittleEndian(data[offset..]),
                _ => throw new ArgumentOutOfRangeException(nameof(channelBytes)),
            };

        private static string ChannelName(AlgorithmImageFormat format, int channel)
        {
            if (format.Channels() == 1) return "Gray";
            return channel switch { 0 => "B", 1 => "G", 2 => "R", 3 => "A", _ => channel.ToString(CultureInfo.InvariantCulture) };
        }

        private static string ChannelUnit(AlgorithmImageFormat format) => format.IsFloatingPoint() ? "value" : "DN";

        private readonly record struct HistogramBin(int Index, double Lower, double Upper, bool IsUpperInclusive, long Count);

        private sealed class BadPixelScan(int capacity, int maximumNeighbors, int channelCount)
        {
            public readonly double[] Neighbors = new double[maximumNeighbors];
            public readonly long[] ChannelCounts = new long[channelCount];
            public readonly PriorityQueue<BadPixelCandidate, BadPixelCandidate> Strongest = new(capacity, BadPixelCandidateComparer.Instance);
            public long UniquePixelCount;

            public void Keep(BadPixelCandidate candidate)
            {
                if (Strongest.Count < capacity) Strongest.Enqueue(candidate, candidate);
                else Strongest.EnqueueDequeue(candidate, candidate);
            }
        }

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

        private readonly record struct BadPixelDetectionResult(
            IReadOnlyList<BadPixelCandidate> Candidates,
            long UniquePixelCount)
        {
            public static BadPixelDetectionResult Empty { get; } = new(Array.Empty<BadPixelCandidate>(), 0);
        }

        private sealed class BadPixelCandidateComparer : IComparer<BadPixelCandidate>
        {
            public static BadPixelCandidateComparer Instance { get; } = new();

            public int Compare(BadPixelCandidate left, BadPixelCandidate right)
            {
                int comparison = left.Confidence.CompareTo(right.Confidence);
                if (comparison != 0) return comparison;
                comparison = StrengthRatio(left).CompareTo(StrengthRatio(right));
                if (comparison != 0) return comparison;
                comparison = left.Deviation.CompareTo(right.Deviation);
                if (comparison != 0) return comparison;
                comparison = right.Y.CompareTo(left.Y);
                if (comparison != 0) return comparison;
                comparison = right.X.CompareTo(left.X);
                if (comparison != 0) return comparison;
                return right.Channel.CompareTo(left.Channel);
            }

            private static double StrengthRatio(BadPixelCandidate candidate)
                => candidate.Threshold <= 0 ? double.PositiveInfinity : candidate.Deviation / candidate.Threshold;
        }

        private sealed class ChannelAccumulator
        {
            private readonly long[]? _integerCounts;
            private readonly int _floatingCapacity;
            private List<float>? _floatValues;
            private double _m2;

            public ChannelAccumulator(int index, string name, AlgorithmImageFormat format, int floatingCapacity)
            {
                Index = index;
                Name = name;
                NominalMaximum = format.BitsPerChannel() switch { 8 => byte.MaxValue, 16 => ushort.MaxValue, _ => 1 };
                if (format.IsFloatingPoint()) _floatingCapacity = floatingCapacity;
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

            public void AddIntegerRow<T>(ReadOnlySpan<T> samples, int channelCount)
                where T : unmanaged, IBinaryInteger<T>
            {
                long[] counts = _integerCounts!;
                long count = ValidCount;
                int minimum = count == 0 ? counts.Length - 1 : (int)Minimum;
                int maximum = count == 0 ? 0 : (int)Maximum;
                double mean = Mean;
                double m2 = _m2;
                // Keep Welford's update order for each channel, even when channels share one row.
                for (int index = Index; index < samples.Length; index += channelCount)
                {
                    int value = int.CreateTruncating(samples[index]);
                    counts[value]++;
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                    count++;
                    double delta = value - mean;
                    mean += delta / count;
                    m2 += delta * (value - mean);
                }
                ValidCount = count;
                Minimum = minimum;
                Maximum = maximum;
                Mean = mean;
                _m2 = m2;
            }

            public void UpdateIntegerSaturationCounts()
            {
                long[] counts = _integerCounts!;
                LowSaturatedCount = counts[0];
                HighSaturatedCount = counts[^1];
            }

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
                else if (_floatValues != null) _floatValues.Add((float)value);
                else if (Minimum != Maximum)
                {
                    // The earlier finite samples all have the other bound's value.
                    List<float> values = new(_floatingCapacity);
                    int prefixLength = checked((int)ValidCount - 1);
                    CollectionsMarshal.SetCount(values, prefixLength);
                    CollectionsMarshal.AsSpan(values).Fill((float)(value == Minimum ? Maximum : Minimum));
                    values.Add((float)value);
                    _floatValues = values;
                }
            }

            public void PreparePercentiles(ReadOnlySpan<double> percentiles, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_floatValues != null)
                {
                    Span<float> samples = CollectionsMarshal.AsSpan(_floatValues);
                    bool descending = samples[0] > samples[^1];
                    if (samples[0] != samples[^1] && IsOrdered(samples, descending))
                    {
                        if (descending) samples.Reverse();
                    }
                    else if (samples.Length < 16_384 || percentiles.Length > MaximumStackPercentiles
                        || (Minimum == 0 && ContainsNegativeZero(percentiles))) _floatValues.Sort();
                    else
                    {
                        // Histograms do not require sorted samples. Place only the ranks used for interpolation.
                        Span<int> ranks = stackalloc int[2 * percentiles.Length];
                        for (int index = 0; index < percentiles.Length; index++)
                        {
                            double rank = percentiles[index] / 100 * (ValidCount - 1);
                            ranks[2 * index] = (int)Math.Floor(rank);
                            ranks[2 * index + 1] = (int)Math.Ceiling(rank);
                        }
                        ranks.Sort();
                        // Fall back to sorting the unfinished partition if pivots repeatedly split poorly.
                        int depthLimit = 2 * BitOperations.Log2((uint)samples.Length);
                        PositionRanks(samples, ranks, 0, depthLimit, cancellationToken);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }

            private static bool ContainsNegativeZero(ReadOnlySpan<double> percentiles)
            {
                // Interpolation at -0% can expose which signed zero occupies the first rank.
                foreach (double percentile in percentiles)
                    if (BitConverter.DoubleToInt64Bits(percentile) == long.MinValue) return true;
                return false;
            }

            private static void PositionRanks(Span<float> samples, ReadOnlySpan<int> ranks, int offset,
                int depthLimit, CancellationToken cancellationToken)
            {
                if (ranks.IsEmpty || samples.Length <= 1) return;
                cancellationToken.ThrowIfCancellationRequested();
                if (depthLimit == 0 || samples.Length <= 32)
                {
                    samples.Sort();
                    return;
                }

                float first = samples[0], last = samples[^1];
                if (first > last) (first, last) = (last, first);
                float pivot = Math.Clamp(samples[samples.Length / 2], first, last);
                int left = 0, right = samples.Length - 1;
                do
                {
                    while (samples[left] < pivot) left++;
                    while (samples[right] > pivot) right--;
                    if (left > right) break;
                    (samples[left], samples[right]) = (samples[right], samples[left]);
                    left++;
                    right--;
                } while (left <= right);

                // The two partitions bound the pivot; any index between them already has its final value.
                int lowerCount = 0;
                while (lowerCount < ranks.Length && ranks[lowerCount] < offset + right + 1) lowerCount++;
                int upperStart = lowerCount;
                while (upperStart < ranks.Length && ranks[upperStart] < offset + left) upperStart++;
                PositionRanks(samples[..(right + 1)], ranks[..lowerCount], offset, depthLimit - 1, cancellationToken);
                PositionRanks(samples[left..], ranks[upperStart..], offset + left, depthLimit - 1, cancellationToken);
            }

            private static bool IsOrdered(ReadOnlySpan<float> samples, bool descending)
            {
                float previous = samples[0];
                for (int index = 1; index < samples.Length; index++)
                {
                    float current = samples[index];
                    if (descending ? current > previous : current < previous) return false;
                    previous = current;
                }
                return true;
            }

            public void FillPercentiles(ReadOnlySpan<double> percentiles, Span<double> values)
            {
                if (_integerCounts is not { Length: > 256 } counts || percentiles.Length > MaximumStackPercentiles)
                {
                    for (int index = 0; index < percentiles.Length; index++)
                        values[index] = Percentile(percentiles[index]);
                    return;
                }

                Span<double> ranks = stackalloc double[percentiles.Length];
                Span<int> slots = stackalloc int[ranks.Length];
                for (int index = 0; index < percentiles.Length; index++)
                {
                    ranks[index] = percentiles[index] / 100 * (ValidCount - 1);
                    slots[index] = index;
                }
                ranks.Sort(slots);

                int lower = (int)Minimum;
                long cumulative = counts[lower];
                for (int index = 0; index < ranks.Length; index++)
                {
                    double rank = ranks[index];
                    long lowerRank = (long)Math.Floor(rank);
                    while (cumulative <= lowerRank)
                        cumulative += counts[++lower];

                    // Look ahead without moving the cursor. The next lower rank may still use this bin.
                    int upper = lower;
                    if (rank > lowerRank && cumulative == lowerRank + 1)
                        while (counts[++upper] == 0) { }
                    values[slots[index]] = lower + (upper - lower) * (rank - lowerRank);
                }
            }

            private double Percentile(double percentile)
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
                if (rangeFloat == 0)
                {
                    yield return new HistogramBin(0, Minimum, Maximum, true, ValidCount);
                    yield break;
                }
                foreach (float value in _floatValues!)
                {
                    int index = (int)Math.Min(bins - 1, (value - Minimum) / rangeFloat * bins);
                    counts[index]++;
                }
                for (int index = 0; index < bins; index++)
                {
                    double lower = Minimum + rangeFloat * index / bins;
                    double upper = Minimum + rangeFloat * (index + 1) / bins;
                    yield return new HistogramBin(index, lower, upper, index == bins - 1, counts[index]);
                }
            }

            private double ValueAtRank(long rank)
            {
                if (_floatValues != null) return _floatValues[checked((int)rank)];
                if (_integerCounts == null) return Minimum;
                long cumulative = 0;
                for (int value = 0; value < _integerCounts.Length; value++)
                {
                    cumulative += _integerCounts[value];
                    if (cumulative > rank) return value;
                }
                return Maximum;
            }
        }

    }
}
