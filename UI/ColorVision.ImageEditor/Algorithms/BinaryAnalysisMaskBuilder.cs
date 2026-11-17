using ColorVision.Algorithms;
using System;
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

    }
}
