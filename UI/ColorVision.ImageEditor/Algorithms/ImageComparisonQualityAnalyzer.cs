using ColorVision.Algorithms;
using System;
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
                for (int index = 0; index < channels.Length; index++)
                {
                    int channel = channels[index];
                    progress?.Report(new AlgorithmProgress(0.80 + 0.10 * index / Math.Max(1, channels.Length), "comparison.ssim", ChannelName(reference.Format, channel)));
                    SsimAccumulator result = ComputeSsim(reference, candidate, region, channel, parameters, cancellationToken);
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
            CancellationToken cancellationToken)
        {
            int width = reference.Width;
            int height = reference.Height;
            int radius = parameters.SsimWindowSize / 2;
            double[] sumX = new double[width];
            double[] sumY = new double[width];
            double[] sumX2 = new double[width];
            double[] sumY2 = new double[width];
            double[] sumXY = new double[width];
            int[] counts = new int[width];
            int[] possibleCounts = new int[width];
            for (int row = 0; row <= Math.Min(radius, height - 1); row++)
                AddRow(reference, candidate, region, channel, row, 1, sumX, sumY, sumX2, sumY2, sumXY, counts, possibleCounts);

            double peak = reference.Format.IsFloatingPoint() ? parameters.FloatPeakValue
                : reference.Format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            double c1 = Math.Pow(parameters.SsimK1 * peak, 2);
            double c2 = Math.Pow(parameters.SsimK2 * peak, 2);
            KahanSum total = new();
            long valid = 0;
            long invalid = 0;
            for (int y = 0; y < height; y++)
            {
                if ((y & 15) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (y > 0)
                {
                    int remove = y - radius - 1;
                    int add = y + radius;
                    if (remove >= 0) AddRow(reference, candidate, region, channel, remove, -1, sumX, sumY, sumX2, sumY2, sumXY, counts, possibleCounts);
                    if (add < height) AddRow(reference, candidate, region, channel, add, 1, sumX, sumY, sumX2, sumY2, sumXY, counts, possibleCounts);
                }

                double windowX = 0;
                double windowY = 0;
                double windowX2 = 0;
                double windowY2 = 0;
                double windowXY = 0;
                int windowCount = 0;
                int possibleWindowCount = 0;
                for (int column = 0; column <= Math.Min(radius, width - 1); column++)
                    AddColumn(column, 1, sumX, sumY, sumX2, sumY2, sumXY, counts, possibleCounts,
                        ref windowX, ref windowY, ref windowX2, ref windowY2, ref windowXY, ref windowCount, ref possibleWindowCount);

                for (int x = 0; x < width; x++)
                {
                    if (x > 0)
                    {
                        int remove = x - radius - 1;
                        int add = x + radius;
                        if (remove >= 0) AddColumn(remove, -1, sumX, sumY, sumX2, sumY2, sumXY, counts, possibleCounts,
                            ref windowX, ref windowY, ref windowX2, ref windowY2, ref windowXY, ref windowCount, ref possibleWindowCount);
                        if (add < width) AddColumn(add, 1, sumX, sumY, sumX2, sumY2, sumXY, counts, possibleCounts,
                            ref windowX, ref windowY, ref windowX2, ref windowY2, ref windowXY, ref windowCount, ref possibleWindowCount);
                    }
                    if (!region.Contains(x, y)) continue;
                    int required = Math.Max(1, (int)Math.Ceiling(possibleWindowCount * parameters.SsimMinimumValidFraction));
                    if (windowCount < required)
                    {
                        invalid++;
                        continue;
                    }

                    double meanX = windowX / windowCount;
                    double meanY = windowY / windowCount;
                    double varianceX = Math.Max(0, windowX2 / windowCount - meanX * meanX);
                    double varianceY = Math.Max(0, windowY2 / windowCount - meanY * meanY);
                    double covariance = windowXY / windowCount - meanX * meanY;
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
            double[] sumX,
            double[] sumY,
            double[] sumX2,
            double[] sumY2,
            double[] sumXY,
            int[] counts,
            int[] possibleCounts)
        {
            for (int x = 0; x < reference.Width; x++)
            {
                if (!region.Contains(x, row)) continue;
                possibleCounts[x] += direction;
                double left = Read(reference, x, row, channel);
                double right = Read(candidate, x, row, channel);
                if (!double.IsFinite(left) || !double.IsFinite(right)) continue;
                sumX[x] += direction * left;
                sumY[x] += direction * right;
                sumX2[x] += direction * left * left;
                sumY2[x] += direction * right * right;
                sumXY[x] += direction * left * right;
                counts[x] += direction;
            }
        }

        private static void AddColumn(
            int column,
            int direction,
            double[] sumX,
            double[] sumY,
            double[] sumX2,
            double[] sumY2,
            double[] sumXY,
            int[] counts,
            int[] possibleCounts,
            ref double windowX,
            ref double windowY,
            ref double windowX2,
            ref double windowY2,
            ref double windowXY,
            ref int windowCount,
            ref int possibleWindowCount)
        {
            windowX += direction * sumX[column];
            windowY += direction * sumY[column];
            windowX2 += direction * sumX2[column];
            windowY2 += direction * sumY2[column];
            windowXY += direction * sumXY[column];
            windowCount += direction * counts[column];
            possibleWindowCount += direction * possibleCounts[column];
        }

        private static ImageComparisonAlignmentPrecheck ComputeAlignment(
            AlgorithmImageBuffer reference,
            AlgorithmImageBuffer candidate,
            AlgorithmPixelRoi region,
            ImageComparisonParameters parameters,
            CancellationToken cancellationToken)
        {
            long boundingArea = checked((long)(region.MaximumXExclusive - region.MinimumX) * (region.MaximumYExclusive - region.MinimumY));
            int step = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(boundingArea / (double)parameters.AlignmentMaximumSamples)));
            long potential = 0;
            for (int y = region.MinimumY; y < region.MaximumYExclusive; y += step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = region.MinimumX; x < region.MaximumXExclusive; x += step)
                    if (region.Contains(x, y) && double.IsFinite(Luminance(reference, x, y))) potential++;
            }
            if (potential < 4)
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
                    CorrelationCandidate value = Correlate(reference, candidate, region, dx, dy, step, potential, cancellationToken);
                    if (dx == 0 && dy == 0) zero = value;
                    if (value.OverlapFraction < parameters.AlignmentMinimumOverlapFraction || !double.IsFinite(value.Correlation)) continue;
                    if (best == null || value.Correlation > best.Correlation)
                    {
                        second = best;
                        best = value;
                    }
                    else if (second == null || value.Correlation > second.Correlation)
                    {
                        second = value;
                    }
                }
            }
            if (best == null)
            {
                string status = zero == null || zero.SampleCount < 4 ? "insufficient_overlap" : "low_texture";
                return new ImageComparisonAlignmentPrecheck(status, 0, 0, 0, double.NaN, zero?.Correlation ?? double.NaN, double.NaN, 0,
                    zero?.OverlapFraction ?? 0, zero?.SampleCount ?? 0, step);
            }

            double margin = second == null ? 0 : best.Correlation - second.Correlation;
            double confidence = Math.Clamp((best.Correlation + 1) / 2, 0, 1) * Math.Clamp(margin / 0.05, 0, 1);
            double magnitude = Math.Sqrt(best.Dx * best.Dx + best.Dy * best.Dy);
            return new ImageComparisonAlignmentPrecheck(
                "ok", best.Dx, best.Dy, magnitude, best.Correlation, zero?.Correlation ?? double.NaN,
                margin, confidence, best.OverlapFraction, best.SampleCount, step);
        }

        private static CorrelationCandidate Correlate(
            AlgorithmImageBuffer reference,
            AlgorithmImageBuffer candidate,
            AlgorithmPixelRoi region,
            int dx,
            int dy,
            int step,
            long potential,
            CancellationToken cancellationToken)
        {
            double sumX = 0;
            double sumY = 0;
            double sumX2 = 0;
            double sumY2 = 0;
            double sumXY = 0;
            long count = 0;
            int cancellationCounter = 0;
            for (int y = region.MinimumY; y < region.MaximumYExclusive; y += step)
            {
                int candidateY = y + dy;
                if (candidateY < 0 || candidateY >= candidate.Height) continue;
                for (int x = region.MinimumX; x < region.MaximumXExclusive; x += step)
                {
                    if ((cancellationCounter++ & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (!region.Contains(x, y)) continue;
                    int candidateX = x + dx;
                    if (candidateX < 0 || candidateX >= candidate.Width) continue;
                    double left = Luminance(reference, x, y);
                    double right = Luminance(candidate, candidateX, candidateY);
                    if (!double.IsFinite(left) || !double.IsFinite(right)) continue;
                    sumX += left;
                    sumY += right;
                    sumX2 += left * left;
                    sumY2 += right * right;
                    sumXY += left * right;
                    count++;
                }
            }
            if (count < 4) return new CorrelationCandidate(dx, dy, double.NaN, count, potential == 0 ? 0 : count / (double)potential);
            double covariance = sumXY - sumX * sumY / count;
            double varianceX = sumX2 - sumX * sumX / count;
            double varianceY = sumY2 - sumY * sumY / count;
            double denominator = Math.Sqrt(Math.Max(0, varianceX) * Math.Max(0, varianceY));
            double correlation = denominator <= 1e-20 ? double.NaN : Math.Clamp(covariance / denominator, -1, 1);
            return new CorrelationCandidate(dx, dy, correlation, count, potential == 0 ? 0 : count / (double)potential);
        }

        private static double Luminance(AlgorithmImageBuffer image, int x, int y)
        {
            if (image.Format.Channels() == 1) return Read(image, x, y, 0);
            return 0.114 * Read(image, x, y, 0) + 0.587 * Read(image, x, y, 1) + 0.299 * Read(image, x, y, 2);
        }

        private static double Read(AlgorithmImageBuffer image, int x, int y, int channel)
        {
            int bytesPerChannel = image.Format.BitsPerChannel() / 8;
            int offset = checked(y * image.Stride + (x * image.Format.Channels() + channel) * bytesPerChannel);
            ReadOnlySpan<byte> data = image.Data.Span;
            return image.Format.BitsPerChannel() switch
            {
                8 => data[offset],
                16 => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2)),
                32 when image.Format.IsFloatingPoint() => BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, 4)),
                _ => throw new ArgumentOutOfRangeException(nameof(image)),
            };
        }

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
        private sealed record CorrelationCandidate(int Dx, int Dy, double Correlation, long SampleCount, double OverlapFraction);

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
