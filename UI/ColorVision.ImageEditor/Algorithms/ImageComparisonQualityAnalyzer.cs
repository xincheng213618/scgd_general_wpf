using ColorVision.Algorithms;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms
{
    internal sealed record ImageComparisonChannelSsim(int Channel, string ChannelName, double Value, long ValidWindowCount, long InvalidWindowCount);

    internal sealed record ImageComparisonAlignmentPrecheck(
        string Status,
        int EstimatedShiftX,
        int EstimatedShiftY,
        double ShiftMagnitudePixels,
        double BestCorrelation,
        double ZeroShiftCorrelation,
        double PeakMargin,
        double Confidence,
        double OverlapFraction,
        long SampleCount,
        int SampleStep);

    internal sealed record ImageComparisonQualityResult(
        double Ssim,
        long ValidSsimWindowCount,
        long InvalidSsimWindowCount,
        IReadOnlyList<ImageComparisonChannelSsim> Channels,
        ImageComparisonAlignmentPrecheck Alignment);

    /// <summary>Linear-memory SSIM and bounded-sample translation diagnostics; it never transforms either input.</summary>
    internal static class ImageComparisonQualityAnalyzer
    {
        public static ImageComparisonQualityResult Analyze(
            AlgorithmImageBuffer reference,
            AlgorithmImageBuffer candidate,
            AlgorithmPixelRoi region,
            ImageComparisonParameters parameters,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            int[] channels = ComparedChannelIndexes(reference.Format, parameters.IncludeAlphaInMetrics).ToArray();
            List<ImageComparisonChannelSsim> channelResults = new();
            KahanSum aggregate = new();
            long validWindows = 0;
            long invalidWindows = 0;
            if (parameters.EnableSsim)
            {
                var bounds = region.GetSamplingBounds(reference);
                SsimSums[]? columns = null;
                for (int index = 0; index < channels.Length; index++)
                {
                    int channel = channels[index];
                    progress?.Report(new AlgorithmProgress(0.80 + 0.10 * index / Math.Max(1, channels.Length), "comparison.ssim", ChannelName(reference.Format, channel)));
                    cancellationToken.ThrowIfCancellationRequested();
                    if (columns == null)
                        columns = new SsimSums[bounds.MaximumXExclusive - bounds.MinimumX];
                    else
                        Array.Clear(columns);
                    SsimAccumulator result = ComputeSsim(reference, candidate, region, channel, parameters, bounds, columns, cancellationToken);
                    channelResults.Add(new ImageComparisonChannelSsim(channel, ChannelName(reference.Format, channel), result.Mean, result.ValidCount, result.InvalidCount));
                    if (result.ValidCount > 0) aggregate.Add(result.Mean * result.ValidCount);
                    validWindows += result.ValidCount;
                    invalidWindows += result.InvalidCount;
                }
            }

            progress?.Report(new AlgorithmProgress(0.91, "comparison.alignment-precheck"));
            ImageComparisonAlignmentPrecheck alignment = parameters.EnableAlignmentPrecheck
                ? ComputeAlignment(reference, candidate, region, parameters, cancellationToken)
                : DisabledAlignment();
            return new ImageComparisonQualityResult(
                validWindows == 0 ? double.NaN : aggregate.Value / validWindows,
                validWindows,
                invalidWindows,
                channelResults,
                alignment);
        }

        private static SsimAccumulator ComputeSsim(
            AlgorithmImageBuffer reference,
            AlgorithmImageBuffer candidate,
            AlgorithmPixelRoi region,
            int channel,
            ImageComparisonParameters parameters,
            (int MinimumX, int MinimumY, int MaximumXExclusive, int MaximumYExclusive) bounds,
            SsimSums[] columns,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (minimumX, minimumY, _, maximumYExclusive) = bounds;
            int radius = parameters.SsimWindowSize / 2;
            // Samples outside the ROI never enter a window. Skip their zero-valued
            // rows/columns while preserving the original order of nonzero additions.
            for (int row = minimumY; row <= Math.Min(minimumY + radius, maximumYExclusive - 1); row++)
                AddRow(reference, candidate, region, channel, row, 1, minimumX, columns);

            double peak = reference.Format.IsFloatingPoint() ? parameters.FloatPeakValue
                : reference.Format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            double c1 = Math.Pow(parameters.SsimK1 * peak, 2);
            double c2 = Math.Pow(parameters.SsimK2 * peak, 2);
            KahanSum total = new();
            long valid = 0;
            long invalid = 0;
            for (int y = minimumY; y < maximumYExclusive; y++)
            {
                if ((y & 15) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (y > minimumY)
                {
                    int remove = y - radius - 1;
                    int add = y + radius;
                    if (remove >= minimumY) AddRow(reference, candidate, region, channel, remove, -1, minimumX, columns);
                    if (add < maximumYExclusive) AddRow(reference, candidate, region, channel, add, 1, minimumX, columns);
                }

                SsimSums window = default;
                for (int column = 0; column <= Math.Min(radius, columns.Length - 1); column++)
                    window.AddColumn(in columns[column], 1);

                for (int column = 0; column < columns.Length; column++)
                {
                    if (column > 0)
                    {
                        int remove = column - radius - 1;
                        int add = column + radius;
                        if (remove >= 0) window.AddColumn(in columns[remove], -1);
                        if (add < columns.Length) window.AddColumn(in columns[add], 1);
                    }
                    if (!region.Contains(minimumX + column, y)) continue;
                    int required = Math.Max(1, (int)Math.Ceiling(window.PossibleCount * parameters.SsimMinimumValidFraction));
                    if (window.Count < required)
                    {
                        invalid++;
                        continue;
                    }

                    double meanX = window.X / window.Count;
                    double meanY = window.Y / window.Count;
                    double varianceX = Math.Max(0, window.X2 / window.Count - meanX * meanX);
                    double varianceY = Math.Max(0, window.Y2 / window.Count - meanY * meanY);
                    double covariance = window.XY / window.Count - meanX * meanY;
                    double denominator = (meanX * meanX + meanY * meanY + c1) * (varianceX + varianceY + c2);
                    double ssim = denominator <= 0 ? double.NaN
                        : ((2 * meanX * meanY + c1) * (2 * covariance + c2)) / denominator;
                    if (!double.IsFinite(ssim))
                    {
                        invalid++;
                        continue;
                    }
                    total.Add(Math.Clamp(ssim, -1, 1));
                    valid++;
                }
            }
            return new SsimAccumulator(valid == 0 ? double.NaN : total.Value / valid, valid, invalid);
        }

        private static void AddRow(
            AlgorithmImageBuffer reference,
            AlgorithmImageBuffer candidate,
            AlgorithmPixelRoi region,
            int channel,
            int row,
            int direction,
            int minimumX,
            SsimSums[] columns)
        {
            int bytesPerChannel = reference.Format.BitsPerChannel() / 8;
            int bytesPerPixel = reference.Format.Channels() * bytesPerChannel;
            int firstSample = checked(minimumX * bytesPerPixel + channel * bytesPerChannel);
            ReadOnlySpan<byte> referenceRow = reference.Data.Span[checked(row * reference.Stride + firstSample)..];
            ReadOnlySpan<byte> candidateRow = candidate.Data.Span[checked(row * candidate.Stride + firstSample)..];
            for (int column = 0, offset = 0; column < columns.Length; column++, offset += bytesPerPixel)
            {
                int x = minimumX + column;
                if (!region.Contains(x, row)) continue;
                ref SsimSums sums = ref columns[column];
                sums.PossibleCount += direction;
                double left = ReadSample(referenceRow, offset, bytesPerChannel);
                double right = ReadSample(candidateRow, offset, bytesPerChannel);
                if (!double.IsFinite(left) || !double.IsFinite(right)) continue;
                sums.X += direction * left;
                sums.Y += direction * right;
                sums.X2 += direction * left * left;
                sums.Y2 += direction * right * right;
                sums.XY += direction * left * right;
                sums.Count += direction;
            }
        }

        private struct SsimSums
        {
            public double X;
            public double Y;
            public double X2;
            public double Y2;
            public double XY;
            public int Count;
            public int PossibleCount;

            public void AddColumn(in SsimSums column, int direction)
            {
                X += direction * column.X;
                Y += direction * column.Y;
                X2 += direction * column.X2;
                Y2 += direction * column.Y2;
                XY += direction * column.XY;
                Count += direction * column.Count;
                PossibleCount += direction * column.PossibleCount;
            }
        }

        private static ImageComparisonAlignmentPrecheck ComputeAlignment(
            AlgorithmImageBuffer reference,
            AlgorithmImageBuffer candidate,
            AlgorithmPixelRoi region,
            ImageComparisonParameters parameters,
            CancellationToken cancellationToken)
        {
            int width = region.MaximumXExclusive - region.MinimumX;
            int height = region.MaximumYExclusive - region.MinimumY;
            long boundingArea = checked((long)width * height);
            int step = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(boundingArea / (double)parameters.AlignmentMaximumSamples)));
            int maximumStep = Math.Max(width, height);
            // Bound the rounded grid dimensions while retaining uniform spacing over the entire ROI.
            while (step < maximumStep)
            {
                int candidateStep = step + (maximumStep - step) / 2;
                long candidateSampleCount = ((width + (long)candidateStep - 1) / candidateStep) * ((height + (long)candidateStep - 1) / candidateStep);
                if (candidateSampleCount <= parameters.AlignmentMaximumSamples) maximumStep = candidateStep;
                else step = candidateStep + 1;
            }
            int gridWidth = 1 + (width - 1) / step;
            int gridHeight = 1 + (height - 1) / step;
            AlignmentSample[] samples = ArrayPool<AlignmentSample>.Shared.Rent(checked(gridWidth * gridHeight));
            try
            {
                // Every trial shift uses the same finite reference samples in scan order.
                int channels = reference.Format.Channels();
                int bytesPerChannel = reference.Format.BitsPerChannel() / 8;
                int bytesPerPixel = channels * bytesPerChannel;
                int count = 0;
                ReadOnlySpan<byte> data = reference.Data.Span;
                for (int y = region.MinimumY; y < region.MaximumYExclusive; y += step)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReadOnlySpan<byte> row = data[(y * reference.Stride)..];
                    for (int x = region.MinimumX; x < region.MaximumXExclusive; x += step)
                    {
                        if (!region.Contains(x, y)) continue;
                        double luminance = Luminance(row, x * bytesPerPixel, channels, bytesPerChannel);
                        if (double.IsFinite(luminance)) samples[count++] = new(x, y, luminance);
                    }
                }
                return FindAlignment(candidate, samples.AsSpan(0, count), parameters, step, cancellationToken);
            }
            finally
            {
                ArrayPool<AlignmentSample>.Shared.Return(samples);
            }
        }

        private static ImageComparisonAlignmentPrecheck FindAlignment(
            AlgorithmImageBuffer candidate,
            ReadOnlySpan<AlignmentSample> samples,
            ImageComparisonParameters parameters,
            int step,
            CancellationToken cancellationToken)
        {
            if (samples.Length < 4)
                return new ImageComparisonAlignmentPrecheck("insufficient_samples", 0, 0, 0, double.NaN, double.NaN, double.NaN, 0, 0, 0, step);

            CorrelationCandidate? best = null;
            CorrelationCandidate? second = null;
            CorrelationCandidate? zero = null;
            int radius = parameters.AlignmentSearchRadius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int dx = -radius; dx <= radius; dx++)
                {
                    CorrelationCandidate value = Correlate(candidate, samples, dx, dy, cancellationToken);
                    if (dx == 0 && dy == 0) zero = value;
                    if (value.OverlapFraction < parameters.AlignmentMinimumOverlapFraction || !double.IsFinite(value.Correlation)) continue;
                    if (best is not CorrelationCandidate bestValue || value.Correlation > bestValue.Correlation)
                    {
                        second = best;
                        best = value;
                    }
                    else if (second is not CorrelationCandidate secondValue || value.Correlation > secondValue.Correlation)
                    {
                        second = value;
                    }
                }
            }
            if (best is not CorrelationCandidate peak)
            {
                string status = (zero?.SampleCount ?? 0) < 4 ? "insufficient_overlap" : "low_texture";
                return new ImageComparisonAlignmentPrecheck(status, 0, 0, 0, double.NaN, zero?.Correlation ?? double.NaN, double.NaN, 0,
                    zero?.OverlapFraction ?? 0, zero?.SampleCount ?? 0, step);
            }

            double margin = second is CorrelationCandidate runnerUp ? peak.Correlation - runnerUp.Correlation : 0;
            double confidence = Math.Clamp((peak.Correlation + 1) / 2, 0, 1) * Math.Clamp(margin / 0.05, 0, 1);
            double magnitude = Math.Sqrt(peak.Dx * peak.Dx + peak.Dy * peak.Dy);
            return new ImageComparisonAlignmentPrecheck(
                "ok", peak.Dx, peak.Dy, magnitude, peak.Correlation, zero?.Correlation ?? double.NaN,
                margin, confidence, peak.OverlapFraction, peak.SampleCount, step);
        }

        private static CorrelationCandidate Correlate(
            AlgorithmImageBuffer candidate,
            ReadOnlySpan<AlignmentSample> samples,
            int dx,
            int dy,
            CancellationToken cancellationToken)
        {
            double sumX = 0;
            double sumY = 0;
            double sumX2 = 0;
            double sumY2 = 0;
            double sumXY = 0;
            long count = 0;
            int channels = candidate.Format.Channels();
            int bytesPerChannel = candidate.Format.BitsPerChannel() / 8;
            int bytesPerPixel = channels * bytesPerChannel;
            ReadOnlySpan<byte> data = candidate.Data.Span;
            for (int i = 0; i < samples.Length; i++)
            {
                if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                ref readonly AlignmentSample sample = ref samples[i];
                int x = sample.X + dx;
                int y = sample.Y + dy;
                if ((uint)x >= (uint)candidate.Width || (uint)y >= (uint)candidate.Height) continue;
                double right = Luminance(data, y * candidate.Stride + x * bytesPerPixel, channels, bytesPerChannel);
                if (!double.IsFinite(right)) continue;
                double left = sample.Luminance;
                sumX += left;
                sumY += right;
                sumX2 += left * left;
                sumY2 += right * right;
                sumXY += left * right;
                count++;
            }
            double overlap = count / (double)samples.Length;
            if (count < 4) return new CorrelationCandidate(dx, dy, double.NaN, count, overlap);
            double covariance = sumXY - sumX * sumY / count;
            double varianceX = sumX2 - sumX * sumX / count;
            double varianceY = sumY2 - sumY * sumY / count;
            double denominator = Math.Sqrt(Math.Max(0, varianceX) * Math.Max(0, varianceY));
            double correlation = denominator <= 1e-20 ? double.NaN : Math.Clamp(covariance / denominator, -1, 1);
            return new CorrelationCandidate(dx, dy, correlation, count, overlap);
        }

        private static double Luminance(ReadOnlySpan<byte> data, int offset, int channels, int bytesPerChannel)
        {
            double first = ReadSample(data, offset, bytesPerChannel);
            if (channels == 1) return first;
            return 0.114 * first + 0.587 * ReadSample(data, offset + bytesPerChannel, bytesPerChannel)
                + 0.299 * ReadSample(data, offset + 2 * bytesPerChannel, bytesPerChannel);
        }

        private static double ReadSample(ReadOnlySpan<byte> data, int offset, int bytesPerChannel) => bytesPerChannel switch
        {
            1 => data[offset],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2)),
            4 => BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, 4)),
            _ => throw new ArgumentOutOfRangeException(nameof(bytesPerChannel)),
        };

        private static IEnumerable<int> ComparedChannelIndexes(AlgorithmImageFormat format, bool includeAlpha)
            => Enumerable.Range(0, includeAlpha || format.Channels() < 4 ? format.Channels() : 3);

        private static string ChannelName(AlgorithmImageFormat format, int channel) => format.Channels() switch
        {
            1 => "Gray",
            3 => channel switch { 0 => "B", 1 => "G", _ => "R" },
            4 => channel switch { 0 => "B", 1 => "G", 2 => "R", _ => "A" },
            _ => channel.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        private static ImageComparisonAlignmentPrecheck DisabledAlignment()
            => new("disabled", 0, 0, 0, double.NaN, double.NaN, double.NaN, 0, 0, 0, 0);

        private readonly record struct SsimAccumulator(double Mean, long ValidCount, long InvalidCount);
        private readonly record struct AlignmentSample(int X, int Y, double Luminance);
        private readonly record struct CorrelationCandidate(int Dx, int Dy, double Correlation, long SampleCount, double OverlapFraction);

        private sealed class KahanSum
        {
            private double _compensation;
            public double Value { get; private set; }

            public void Add(double value)
            {
                double adjusted = value - _compensation;
                double total = Value + adjusted;
                _compensation = (total - Value) - adjusted;
                Value = total;
            }
        }
    }
}
