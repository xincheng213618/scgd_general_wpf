using ColorVision.Algorithms;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms
{
    internal readonly record struct BinaryAnalysisMaskSummary(long RoiPixels, long ForegroundPixels, long InvalidPixels);

    /// <summary>Single normalization boundary shared by binary industrial-analysis providers.</summary>
    internal static class BinaryAnalysisMaskBuilder
    {
        public static BinaryAnalysisMaskSummary Build(
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            double nominalThreshold,
            bool brightForeground,
            byte[] mask,
            CancellationToken cancellationToken,
            Action<double>? reportProgress = null)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(roi);
            ArgumentNullException.ThrowIfNull(mask);
            if (mask.Length != checked(image.Width * image.Height))
                throw new ArgumentException("The mask must contain exactly one byte per source pixel.", nameof(mask));

            long roiPixels = 0;
            long foregroundPixels = 0;
            long invalidPixels = 0;
            int channels = image.Format.Channels();
            int bytesPerChannel = image.Format.BitsPerChannel() / 8;
            int pixelBytes = channels * bytesPerChannel;
            double scale = AlgorithmIntensitySampler.NominalPeak / AlgorithmIntensitySampler.NativePeak(image.Format);
            int width = roi.MaximumXExclusive - roi.MinimumX;
            int height = Math.Max(1, roi.MaximumYExclusive - roi.MinimumY);
            bool rectangular = !roi.IsEmpty && roi.Geometry.Kind == AlgorithmGeometryKind.Rectangle
                && roi.Contains(roi.MinimumX, roi.MinimumY)
                && roi.Contains(roi.MaximumXExclusive - 1, roi.MaximumYExclusive - 1);
            bool gray8Rectangle = rectangular && image.Format == AlgorithmImageFormat.Gray8
                && nominalThreshold is >= 0 and <= byte.MaxValue;
            // Integer-valued Gray8 samples preserve the inclusive comparison at fractional thresholds.
            byte gray8Threshold = gray8Rectangle
                ? (byte)(brightForeground ? Math.Ceiling(nominalThreshold) : Math.Floor(nominalThreshold))
                : (byte)0;
            bool gray16Rectangle = rectangular && image.Format == AlgorithmImageFormat.Gray16
                && BitConverter.IsLittleEndian && width >= Vector256<byte>.Count
                && nominalThreshold >= 0 && nominalThreshold <= ushort.MaxValue * scale;
            ushort gray16Threshold = gray16Rectangle ? FindGray16Threshold(nominalThreshold, brightForeground, scale) : (ushort)0;
            for (int y = roi.MinimumY; y < roi.MaximumYExclusive; y++)
            {
                if (((y - roi.MinimumY) & 15) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    reportProgress?.Invoke((y - roi.MinimumY) / (double)height);
                }
                if (width <= 0) continue;
                ReadOnlySpan<byte> row = image.Data.Span[(y * image.Stride)..];
                Span<byte> destination = mask.AsSpan(y * image.Width, image.Width);
                if (gray8Rectangle)
                {
                    roiPixels += width;
                    foregroundPixels += ThresholdGray8Row(row.Slice(roi.MinimumX, width), destination.Slice(roi.MinimumX, width),
                        gray8Threshold, brightForeground);
                    continue;
                }
                if (gray16Rectangle)
                {
                    roiPixels += width;
                    ReadOnlySpan<ushort> samples = MemoryMarshal.Cast<byte, ushort>(row.Slice(roi.MinimumX * sizeof(ushort), width * sizeof(ushort)));
                    foregroundPixels += ThresholdGray16Row(samples, destination.Slice(roi.MinimumX, width),
                        gray16Threshold, brightForeground);
                    continue;
                }

                int offset = roi.MinimumX * pixelBytes;
                for (int x = roi.MinimumX; x < roi.MaximumXExclusive; x++, offset += pixelBytes)
                {
                    if (!rectangular && !roi.Contains(x, y)) continue;
                    roiPixels++;
                    double intensity = AlgorithmIntensitySampler.ReadLuminanceNominal(row, offset, channels, bytesPerChannel, scale);
                    if (!double.IsFinite(intensity))
                    {
                        invalidPixels++;
                        continue;
                    }
                    bool foreground = brightForeground ? intensity >= nominalThreshold : intensity <= nominalThreshold;
                    if (!foreground) continue;
                    destination[x] = byte.MaxValue;
                    foregroundPixels++;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            reportProgress?.Invoke(1);
            return new BinaryAnalysisMaskSummary(roiPixels, foregroundPixels, invalidPixels);
        }

        private static ushort FindGray16Threshold(double nominalThreshold, bool brightForeground, double scale)
        {
            // Search using the same multiplication as the scalar sampler, including its rounding at the cut.
            int low = 0;
            int high = ushort.MaxValue + 1;
            while (low < high)
            {
                int middle = (low + high) / 2;
                double intensity = middle * scale;
                if (brightForeground ? intensity < nominalThreshold : intensity <= nominalThreshold)
                    low = middle + 1;
                else
                    high = middle;
            }
            return (ushort)(brightForeground ? low : low - 1);
        }

        private static int ThresholdGray16Row(ReadOnlySpan<ushort> source, Span<byte> destination, ushort threshold, bool brightForeground)
        {
            int foreground = 0;
            int x = 0;
            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<ushort> limits = Vector256.Create(threshold);
                ref ushort input = ref MemoryMarshal.GetReference(source);
                ref byte output = ref MemoryMarshal.GetReference(destination);
                for (; x <= source.Length - Vector256<byte>.Count; x += Vector256<byte>.Count)
                {
                    Vector256<ushort> low = Vector256.LoadUnsafe(ref input, (nuint)x);
                    Vector256<ushort> high = Vector256.LoadUnsafe(ref input, (nuint)(x + Vector256<ushort>.Count));
                    Vector256<byte> matches = brightForeground
                        ? Vector256.Narrow(Vector256.GreaterThanOrEqual(low, limits), Vector256.GreaterThanOrEqual(high, limits))
                        : Vector256.Narrow(Vector256.LessThanOrEqual(low, limits), Vector256.LessThanOrEqual(high, limits));
                    (Vector256.LoadUnsafe(ref output, (nuint)x) | matches).StoreUnsafe(ref output, (nuint)x);
                    foreground += BitOperations.PopCount(matches.ExtractMostSignificantBits());
                }
            }
            for (; x < source.Length; x++)
            {
                ushort pixel = source[x];
                if (!(brightForeground ? pixel >= threshold : pixel <= threshold)) continue;
                destination[x] = byte.MaxValue;
                foreground++;
            }
            return foreground;
        }

        private static int ThresholdGray8Row(ReadOnlySpan<byte> source, Span<byte> destination, byte threshold, bool brightForeground)
        {
            int foreground = 0;
            int x = 0;
            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<byte> limits = Vector256.Create(threshold);
                ref byte input = ref MemoryMarshal.GetReference(source);
                ref byte output = ref MemoryMarshal.GetReference(destination);
                for (; x <= source.Length - Vector256<byte>.Count; x += Vector256<byte>.Count)
                {
                    Vector256<byte> pixels = Vector256.LoadUnsafe(ref input, (nuint)x);
                    Vector256<byte> matches = brightForeground ? Vector256.GreaterThanOrEqual(pixels, limits) : Vector256.LessThanOrEqual(pixels, limits);
                    // As on the scalar path, leave the destination value alone when the pixel does not match.
                    (Vector256.LoadUnsafe(ref output, (nuint)x) | matches).StoreUnsafe(ref output, (nuint)x);
                    foreground += BitOperations.PopCount(matches.ExtractMostSignificantBits());
                }
            }
            for (; x < source.Length; x++)
            {
                byte pixel = source[x];
                if (!(brightForeground ? pixel >= threshold : pixel <= threshold)) continue;
                destination[x] = byte.MaxValue;
                foreground++;
            }
            return foreground;
        }
    }
}
