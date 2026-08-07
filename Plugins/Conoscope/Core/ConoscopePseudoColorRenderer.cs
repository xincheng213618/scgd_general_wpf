#pragma warning disable OCVS002
using ColorVision.Core;
using OpenCvSharp.WpfExtensions;
using System;
using System.Buffers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Conoscope.Core
{
    public sealed class ConoscopePseudoColorRenderResult
    {
        public ConoscopePseudoColorRenderResult(WriteableBitmap bitmap, ExportChannel channel, double minValue, double maxValue)
        {
            Bitmap = bitmap;
            Channel = channel;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public WriteableBitmap Bitmap { get; }
        public ExportChannel Channel { get; }
        public double MinValue { get; }
        public double MaxValue { get; }
    }

    public static class ConoscopePseudoColorRenderer
    {
        private const double ContrastDisplayUpperPercentile = 0.995;
        private const int MaximumPercentileSamples = 1_000_000;

        public static ConoscopePseudoColorRenderResult Render(
            OpenCvSharp.Mat xMat,
            OpenCvSharp.Mat yMat,
            OpenCvSharp.Mat zMat,
            ExportChannel channel,
            ColormapTypes colormap,
            Func<OpenCvSharp.Mat> createColorDifferenceMat,
            Func<OpenCvSharp.Mat> createContrastMat,
            bool usePseudoColor,
            OpenCvSharp.Mat? rangeMask = null,
            OpenCvSharp.Mat? rangeOutsideMask = null)
        {
            OpenCvSharp.Mat channelMat = GetDisplayChannelMat(xMat, yMat, zMat, channel, createColorDifferenceMat, createContrastMat, out bool ownsChannelMat);
            try
            {
                using OpenCvSharp.Mat gray8 = new OpenCvSharp.Mat();
                OpenCvSharp.Mat? effectiveRangeMask = IsUsableMask(rangeMask, channelMat) ? rangeMask : null;
                OpenCvSharp.Mat? effectiveOutsideMask = IsUsableMask(rangeOutsideMask, channelMat) ? rangeOutsideMask : null;

                GetDisplayRange(channelMat, channel, effectiveRangeMask, out double minValue, out double maxValue);
                ConvertToGray8(channelMat, gray8, minValue, maxValue, effectiveOutsideMask);

                WriteableBitmap bitmap;
                if (usePseudoColor)
                {
                    using OpenCvSharp.Mat pseudoColor = new OpenCvSharp.Mat();
                    OpenCvSharp.Cv2.ApplyColorMap(gray8, pseudoColor, ResolveOpenCvColormap(colormap));
                    if (effectiveOutsideMask != null)
                    {
                        pseudoColor.SetTo(OpenCvSharp.Scalar.All(0), effectiveOutsideMask);
                    }

                    bitmap = pseudoColor.ToWriteableBitmap();
                }
                else
                {
                    bitmap = gray8.ToWriteableBitmap();
                }

                bitmap.Freeze();

                return new ConoscopePseudoColorRenderResult(bitmap, channel, minValue, maxValue);
            }
            finally
            {
                if (ownsChannelMat)
                {
                    channelMat.Dispose();
                }
            }
        }

        private static void GetDisplayRange(OpenCvSharp.Mat channelMat, ExportChannel channel, OpenCvSharp.Mat? rangeMask, out double minValue, out double maxValue)
        {
            GetRawDisplayRange(channelMat, rangeMask, out minValue, out maxValue);

            if (channel != ExportChannel.Contrast)
            {
                return;
            }

            double robustUpper = GetMaskedPercentile(channelMat, rangeMask, ContrastDisplayUpperPercentile);
            if (double.IsFinite(robustUpper) && robustUpper > minValue && robustUpper < maxValue)
            {
                maxValue = robustUpper;
            }
        }

        private static void GetRawDisplayRange(OpenCvSharp.Mat channelMat, OpenCvSharp.Mat? rangeMask, out double minValue, out double maxValue)
        {
            if (rangeMask == null)
            {
                OpenCvSharp.Cv2.MinMaxLoc(channelMat, out minValue, out maxValue);
                return;
            }

            OpenCvSharp.Cv2.MinMaxLoc(channelMat, out minValue, out maxValue, out _, out _, rangeMask);
        }

        internal static unsafe double GetMaskedPercentile(OpenCvSharp.Mat source, OpenCvSharp.Mat? mask, double percentile)
        {
            if (!double.IsFinite(percentile))
            {
                return double.NaN;
            }

            percentile = Math.Max(0, Math.Min(1, percentile));
            int rows = source.Rows;
            int columns = source.Cols;
            long finiteCount = CountFinitePixels(source, mask, rows, columns);
            if (finiteCount == 0)
            {
                return double.NaN;
            }

            int desiredSampleCount = (int)Math.Min(finiteCount, MaximumPercentileSamples);
            float[] values = ArrayPool<float>.Shared.Rent(desiredSampleCount);
            int sampleCount = 0;

            try
            {
                long finiteIndex = 0;
                long nextSampleIndex = GetUniformSampleIndex(0, finiteCount, desiredSampleCount);
                for (int row = 0; row < rows && sampleCount < desiredSampleCount; row++)
                {
                    float* sourceRow = (float*)source.Ptr(row);
                    byte* maskRow = mask == null ? null : (byte*)mask.Ptr(row);
                    for (int column = 0; column < columns && sampleCount < desiredSampleCount; column++)
                    {
                        if (maskRow != null && maskRow[column] == 0)
                        {
                            continue;
                        }

                        float value = sourceRow[column];
                        if (!float.IsFinite(value))
                        {
                            continue;
                        }

                        if (finiteIndex == nextSampleIndex)
                        {
                            values[sampleCount++] = value;
                            if (sampleCount < desiredSampleCount)
                            {
                                nextSampleIndex = GetUniformSampleIndex(sampleCount, finiteCount, desiredSampleCount);
                            }
                        }

                        finiteIndex++;
                    }
                }

                Array.Sort(values, 0, sampleCount);
                int quantileIndex = (int)Math.Round((sampleCount - 1) * percentile);
                return values[Math.Clamp(quantileIndex, 0, sampleCount - 1)];
            }
            finally
            {
                ArrayPool<float>.Shared.Return(values);
            }
        }

        private static unsafe long CountFinitePixels(OpenCvSharp.Mat source, OpenCvSharp.Mat? mask, int rows, int columns)
        {
            long count = 0;
            for (int row = 0; row < rows; row++)
            {
                float* sourceRow = (float*)source.Ptr(row);
                byte* maskRow = mask == null ? null : (byte*)mask.Ptr(row);
                for (int column = 0; column < columns; column++)
                {
                    if ((maskRow == null || maskRow[column] != 0) && float.IsFinite(sourceRow[column]))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static long GetUniformSampleIndex(int sampleIndex, long valueCount, int sampleCount)
        {
            return ((2L * sampleIndex + 1) * valueCount) / (2L * sampleCount);
        }

        private static void ConvertToGray8(OpenCvSharp.Mat channelMat, OpenCvSharp.Mat gray8, double minValue, double maxValue, OpenCvSharp.Mat? rangeOutsideMask)
        {
            double range = maxValue - minValue;
            if (!double.IsFinite(range) || range <= double.Epsilon)
            {
                gray8.Create(channelMat.Rows, channelMat.Cols, OpenCvSharp.MatType.CV_8UC1);
                gray8.SetTo(OpenCvSharp.Scalar.All(0));
            }
            else
            {
                double scale = 255.0 / range;
                channelMat.ConvertTo(gray8, OpenCvSharp.MatType.CV_8UC1, scale, -minValue * scale);
            }

            if (rangeOutsideMask != null)
            {
                gray8.SetTo(OpenCvSharp.Scalar.All(0), rangeOutsideMask);
            }
        }

        public static WriteableBitmap CreateHeightMapBitmap(
            OpenCvSharp.Mat xMat,
            OpenCvSharp.Mat yMat,
            OpenCvSharp.Mat zMat,
            ExportChannel channel,
            Func<OpenCvSharp.Mat> createColorDifferenceMat,
            Func<OpenCvSharp.Mat> createContrastMat,
            Point? maskCenter = null,
            double? maskRadius = null)
        {
            OpenCvSharp.Mat channelMat = GetDisplayChannelMat(xMat, yMat, zMat, channel, createColorDifferenceMat, createContrastMat, out bool ownsChannelMat);
            try
            {
                using OpenCvSharp.Mat normalized = new OpenCvSharp.Mat();
                using OpenCvSharp.Mat gray8 = new OpenCvSharp.Mat();

                OpenCvSharp.Cv2.Normalize(channelMat, normalized, 0, 255, OpenCvSharp.NormTypes.MinMax);
                normalized.ConvertTo(gray8, OpenCvSharp.MatType.CV_8UC1);

                if (maskCenter.HasValue && maskRadius.HasValue && double.IsFinite(maskRadius.Value) && maskRadius.Value > 0)
                {
                    return CreateMaskedHeightMapBitmap(gray8, maskCenter.Value, maskRadius.Value);
                }

                WriteableBitmap bitmap = gray8.ToWriteableBitmap();
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                if (ownsChannelMat)
                {
                    channelMat.Dispose();
                }
            }
        }

        private static unsafe WriteableBitmap CreateMaskedHeightMapBitmap(OpenCvSharp.Mat gray8, Point center, double radius)
        {
            int width = gray8.Cols;
            int height = gray8.Rows;
            double radiusSquared = radius * radius;
            using OpenCvSharp.Mat bgra = new OpenCvSharp.Mat(height, width, OpenCvSharp.MatType.CV_8UC4);
            Parallel.For(0, height, row =>
            {
                byte* grayRow = (byte*)gray8.Ptr(row);
                byte* bgraRow = (byte*)bgra.Ptr(row);
                double deltaY = row - center.Y;
                double deltaYSquared = deltaY * deltaY;
                for (int column = 0; column < width; column++)
                {
                    byte gray = grayRow[column];
                    int pixelOffset = column * 4;
                    bgraRow[pixelOffset] = gray;
                    bgraRow[pixelOffset + 1] = gray;
                    bgraRow[pixelOffset + 2] = gray;
                    double deltaX = column - center.X;
                    bgraRow[pixelOffset + 3] = deltaX * deltaX + deltaYSquared <= radiusSquared ? (byte)255 : (byte)0;
                }
            });

            WriteableBitmap bitmap = bgra.ToWriteableBitmap();
            bitmap.Freeze();
            return bitmap;
        }

        private static OpenCvSharp.Mat GetDisplayChannelMat(
            OpenCvSharp.Mat xMat,
            OpenCvSharp.Mat yMat,
            OpenCvSharp.Mat zMat,
            ExportChannel channel,
            Func<OpenCvSharp.Mat> createColorDifferenceMat,
            Func<OpenCvSharp.Mat> createContrastMat,
            out bool ownsResult)
        {
            OpenCvSharp.Mat result = channel switch
            {
                ExportChannel.X => xMat,
                ExportChannel.Y => yMat,
                ExportChannel.Z => zMat,
                ExportChannel.ColorDifference => createColorDifferenceMat(),
                ExportChannel.Contrast => createContrastMat(),
                _ => ConoscopeColorimetry.CreateChannelMat(xMat, yMat, zMat, channel)
            };
            ownsResult = !ReferenceEquals(result, xMat)
                && !ReferenceEquals(result, yMat)
                && !ReferenceEquals(result, zMat);
            return result;
        }

        private static OpenCvSharp.ColormapTypes ResolveOpenCvColormap(ColormapTypes colormapType)
        {
            int value = (int)colormapType;
            return Enum.IsDefined(typeof(OpenCvSharp.ColormapTypes), value)
                ? (OpenCvSharp.ColormapTypes)value
                : OpenCvSharp.ColormapTypes.Jet;
        }

        private static bool IsUsableMask(OpenCvSharp.Mat? mask, OpenCvSharp.Mat source)
        {
            return mask != null
                && !mask.Empty()
                && mask.Width == source.Width
                && mask.Height == source.Height
                && mask.Type() == OpenCvSharp.MatType.CV_8UC1;
        }
    }
}
