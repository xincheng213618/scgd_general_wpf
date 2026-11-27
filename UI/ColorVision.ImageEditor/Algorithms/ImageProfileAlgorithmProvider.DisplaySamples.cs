using ColorVision.Algorithms;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms
{
    public sealed partial class ImageProfileAlgorithmProvider
    {
        private const int MinimumDenseProfileBytes = 8 * 1024 * 1024;
        private const int MinimumDenseBytesPerSample = 8 * 1024;
        private const int MaximumDisplaySnapshotSamples = 20_000;

        // Capture the neighbors on the bitmap's dispatcher before scheduling analysis.
        // The worker only needs the packed copy and never touches the displayed bitmap.
        internal static DisplaySampleSnapshot? TryCaptureDisplaySamples(BitmapSource bitmap, PolylineAlgorithmRoi path,
            ImageProfileParameters parameters, ImageSelectionScope scope, Int32Rect? denseBounds)
        {
            PixelFormat sourceFormat = bitmap.Format;
            int sourcePixelBytes = sourceFormat.BitsPerPixel / 8;
            long densePixels = (long)(denseBounds?.Width ?? scope.PixelWidth) * (denseBounds?.Height ?? scope.PixelHeight);
            if (sourceFormat == PixelFormats.Indexed8 || sourcePixelBytes == 0
                || densePixels < (MinimumDenseProfileBytes + sourcePixelBytes - 1) / sourcePixelBytes
                || path.CoordinateSpace != AlgorithmCoordinateSpace.Pixel || path.Points.Count > ImageProfileParameters.MaximumPathPoints
                || !path.Validate().IsValid || !parameters.Validate().IsValid)
                return null;

            Segment[] segments = BuildSegments(path.Points.ToArray(), parameters.ClosePath, scope.DpiX, scope.DpiY, default);
            if (segments.Length == 0) return null;
            double length = segments[^1].PixelStart + segments[^1].PixelLength;
            if (!double.IsFinite(length) || length / parameters.SampleSpacingPixels > MaximumDisplaySnapshotSamples)
                return null;
            int count = CountSamples(length, parameters.SampleSpacingPixels, parameters.ClosePath);
            if (count > MaximumDisplaySnapshotSamples || count > parameters.MaximumSamples
                || densePixels < ((long)count * MinimumDenseBytesPerSample + sourcePixelBytes - 1) / sourcePixelBytes)
                return null;

            AlgorithmImageFormat format = ImageAlgorithmInputFactory.FromPixelFormat(sourceFormat);
            int pixelBytes = format.BytesPerPixel();
            bool bilinear = parameters.Interpolation == ImageProfileInterpolation.Bilinear;
            int side = bilinear ? 2 : 1;
            int stride = side * pixelBytes;
            int sampleBytes = stride * side;
            DisplaySampleSnapshot samples = new(new byte[count * sampleBytes], format, stride, sampleBytes);
            int width = scope.PixelWidth, height = scope.PixelHeight;
            int segmentIndex = 0;
            for (int index = 0; index < count; index++)
            {
                ProfileSample sample = GetSample(index, count, length, parameters, segments, ref segmentIndex);
                if (IsOutside(sample, width, height))
                {
                    if (parameters.BoundaryMode == ImageProfileBoundaryMode.Reject) break;
                    if (parameters.BoundaryMode == ImageProfileBoundaryMode.Skip) continue;
                    sample = ClampSample(sample, width, height);
                }

                Int32Rect tile;
                if (bilinear)
                {
                    var (x0, y0, x1, y1, _, _) = BilinearFootprint(width, height, sample.X, sample.Y);
                    tile = new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                }
                else tile = new Int32Rect(NearestPixel(sample.X, width), NearestPixel(sample.Y, height), 1, 1);
                bitmap.CopyPixels(tile, samples.Data, stride, samples.GetOffset(index, 0));
            }
            ImageAlgorithmInputFactory.NormalizePackedPixels(samples.Data, sourceFormat, side, count * side, stride, pixelBytes);
            return samples;
        }

        internal AlgorithmResult ExecuteDisplaySamples(AlgorithmInvocation invocation, ImageProfileParameters parameters,
            DisplaySampleSnapshot samples, ImageSelectionScope scope, CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress = null)
            => ExecuteInteractive(invocation, parameters, new ProfileRaster(samples, scope), cancellationToken, progress);

        internal sealed record DisplaySampleSnapshot(byte[] Data, AlgorithmImageFormat Format, int Stride, int SampleBytes)
        {
            public int GetOffset(int sample, int row) => sample * SampleBytes + row * Stride;
        }
    }
}
