#pragma warning disable OCVS002
using ColorVision.Core;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
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
            using OpenCvSharp.Mat channelMat = CreateDisplayChannelMat(xMat, yMat, zMat, channel, createColorDifferenceMat, createContrastMat);
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

        private static double GetMaskedPercentile(OpenCvSharp.Mat source, OpenCvSharp.Mat? mask, double percentile)
        {
            if (!double.IsFinite(percentile))
            {
                return double.NaN;
            }

            percentile = Math.Max(0, Math.Min(1, percentile));
            List<float> values = new List<float>();

            for (int row = 0; row < source.Rows; row++)
            {
                for (int col = 0; col < source.Cols; col++)
                {
                    if (mask != null && mask.At<byte>(row, col) == 0)
                    {
                        continue;
                    }

                    float value = source.At<float>(row, col);
                    if (!float.IsFinite(value))
                    {
                        continue;
                    }

                    values.Add(value);
                }
            }

            if (values.Count == 0)
            {
                return double.NaN;
            }

            values.Sort();
            int index = (int)Math.Round((values.Count - 1) * percentile);
            index = Math.Max(0, Math.Min(values.Count - 1, index));
            return values[index];
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
            using OpenCvSharp.Mat channelMat = CreateDisplayChannelMat(xMat, yMat, zMat, channel, createColorDifferenceMat, createContrastMat);
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

        private static WriteableBitmap CreateMaskedHeightMapBitmap(OpenCvSharp.Mat gray8, Point center, double radius)
        {
            WriteableBitmap grayBitmap = gray8.ToWriteableBitmap();
            int width = grayBitmap.PixelWidth;
            int height = grayBitmap.PixelHeight;
            byte[] grayPixels = new byte[width * height];
            grayBitmap.CopyPixels(grayPixels, width, 0);

            byte[] pixels = new byte[width * height * 4];
            double radiusSquared = radius * radius;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int grayIndex = y * width + x;
                    int pixelIndex = grayIndex * 4;
                    byte gray = grayPixels[grayIndex];
                    bool isInsideMask = Math.Pow(x - center.X, 2) + Math.Pow(y - center.Y, 2) <= radiusSquared;

                    pixels[pixelIndex] = gray;
                    pixels[pixelIndex + 1] = gray;
                    pixels[pixelIndex + 2] = gray;
                    pixels[pixelIndex + 3] = isInsideMask ? (byte)255 : (byte)0;
                }
            }

            WriteableBitmap bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            bitmap.Freeze();
            return bitmap;
        }

        private static OpenCvSharp.Mat CreateDisplayChannelMat(
            OpenCvSharp.Mat xMat,
            OpenCvSharp.Mat yMat,
            OpenCvSharp.Mat zMat,
            ExportChannel channel,
            Func<OpenCvSharp.Mat> createColorDifferenceMat,
            Func<OpenCvSharp.Mat> createContrastMat)
        {
            return channel switch
            {
                ExportChannel.ColorDifference => createColorDifferenceMat(),
                ExportChannel.Contrast => createContrastMat(),
                _ => ConoscopeColorimetry.CreateChannelMat(xMat, yMat, zMat, channel)
            };
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
