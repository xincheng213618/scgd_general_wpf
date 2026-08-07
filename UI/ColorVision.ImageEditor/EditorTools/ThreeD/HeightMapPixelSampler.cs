using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    internal readonly record struct HeightMapSample(byte[] Gray, byte[]? Alpha, int Width, int Height);

    internal static class HeightMapPixelSampler
    {
        public static HeightMapSample Sample(BitmapSource source, int maxWidth, int maxHeight)
        {
            ArgumentNullException.ThrowIfNull(source);

            int sourceWidth = source.PixelWidth;
            int sourceHeight = source.PixelHeight;
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                return new HeightMapSample(Array.Empty<byte>(), null, 0, 0);
            }

            (int targetWidth, int targetHeight) = CalculateFitSize(sourceWidth, sourceHeight, maxWidth, maxHeight);
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int sourceStride = checked(sourceWidth * 4);
            byte[] firstRow = new byte[sourceStride];
            byte[] secondRow = new byte[sourceStride];
            byte[] gray = new byte[checked(targetWidth * targetHeight)];
            byte[] alpha = new byte[gray.Length];
            bool hasTransparency = false;

            var x0 = new int[targetWidth];
            var x1 = new int[targetWidth];
            var xFraction = new double[targetWidth];
            double scaleX = (double)(sourceWidth - 1) / Math.Max(targetWidth - 1, 1);
            for (int x = 0; x < targetWidth; x++)
            {
                double sourceX = x * scaleX;
                int left = (int)sourceX;
                x0[x] = left;
                x1[x] = Math.Min(left + 1, sourceWidth - 1);
                xFraction[x] = sourceX - left;
            }

            double scaleY = (double)(sourceHeight - 1) / Math.Max(targetHeight - 1, 1);
            for (int y = 0; y < targetHeight; y++)
            {
                double sourceY = y * scaleY;
                int top = (int)sourceY;
                int bottom = Math.Min(top + 1, sourceHeight - 1);
                double yFraction = sourceY - top;

                converted.CopyPixels(new Int32Rect(0, top, sourceWidth, 1), firstRow, sourceStride, 0);
                byte[] bottomRow = firstRow;
                if (bottom != top)
                {
                    converted.CopyPixels(new Int32Rect(0, bottom, sourceWidth, 1), secondRow, sourceStride, 0);
                    bottomRow = secondRow;
                }

                int targetOffset = y * targetWidth;
                for (int x = 0; x < targetWidth; x++)
                {
                    int leftOffset = x0[x] * 4;
                    int rightOffset = x1[x] * 4;
                    double xWeight = xFraction[x];

                    byte topLeftGray = Luma(firstRow[leftOffset], firstRow[leftOffset + 1], firstRow[leftOffset + 2]);
                    byte topRightGray = Luma(firstRow[rightOffset], firstRow[rightOffset + 1], firstRow[rightOffset + 2]);
                    byte bottomLeftGray = Luma(bottomRow[leftOffset], bottomRow[leftOffset + 1], bottomRow[leftOffset + 2]);
                    byte bottomRightGray = Luma(bottomRow[rightOffset], bottomRow[rightOffset + 1], bottomRow[rightOffset + 2]);

                    int index = targetOffset + x;
                    gray[index] = Interpolate(topLeftGray, topRightGray, bottomLeftGray, bottomRightGray, xWeight, yFraction);
                    alpha[index] = Interpolate(firstRow[leftOffset + 3], firstRow[rightOffset + 3],
                        bottomRow[leftOffset + 3], bottomRow[rightOffset + 3], xWeight, yFraction);
                    hasTransparency |= alpha[index] < byte.MaxValue;
                }
            }

            return new HeightMapSample(gray, hasTransparency ? alpha : null, targetWidth, targetHeight);
        }

        internal static (int Width, int Height) CalculateFitSize(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

            maxWidth = Math.Max(maxWidth, 2);
            maxHeight = Math.Max(maxHeight, 2);
            double scale = Math.Min(1.0, Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight));
            int width = Math.Clamp((int)Math.Round(sourceWidth * scale), 2, maxWidth);
            int height = Math.Clamp((int)Math.Round(sourceHeight * scale), 2, maxHeight);
            return (width, height);
        }

        private static byte Luma(byte blue, byte green, byte red)
        {
            return (byte)Math.Clamp(Math.Round(blue * 0.114 + green * 0.587 + red * 0.299), 0, 255);
        }

        private static byte Interpolate(byte topLeft, byte topRight, byte bottomLeft, byte bottomRight,
            double xFraction, double yFraction)
        {
            double value = topLeft * (1 - xFraction) * (1 - yFraction)
                         + topRight * xFraction * (1 - yFraction)
                         + bottomLeft * (1 - xFraction) * yFraction
                         + bottomRight * xFraction * yFraction;
            return (byte)Math.Clamp(Math.Round(value), 0, 255);
        }
    }
}
