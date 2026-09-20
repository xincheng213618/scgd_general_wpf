using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    internal readonly record struct HeightMapSample(byte[] Gray, byte[]? Alpha, int Width, int Height);

    internal static class HeightMapPixelSampler
    {
        private const int CopyBufferBytes = 4 * 1024 * 1024;

        // Heights are display luminance bytes, not the source's high-bit-depth measurements.
        // Keep straight-color luma rounding before interpolation, including at transparent pixels;
        // the independently sampled alpha tells the renderer which points are invalid.
        public static HeightMapSample Sample(BitmapSource source, int maxWidth, int maxHeight,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();

            int sourceWidth = source.PixelWidth;
            int sourceHeight = source.PixelHeight;
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                return new HeightMapSample(Array.Empty<byte>(), null, 0, 0);
            }

            (int targetWidth, int targetHeight) = CalculateFitSize(sourceWidth, sourceHeight, maxWidth, maxHeight);
            bool isGray = source.Format == PixelFormats.Gray8;
            bool isOpaque = isGray || source.Format == PixelFormats.Bgr24 || source.Format == PixelFormats.Bgr32;
            bool canCopyDirectly = isOpaque || source.Format == PixelFormats.Bgra32;
            BitmapSource pixels = canCopyDirectly ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int bytesPerPixel = pixels.Format.BitsPerPixel / 8;
            int sourceStride = checked(sourceWidth * bytesPerPixel);

            double scaleY = (double)(sourceHeight - 1) / Math.Max(targetHeight - 1, 1);
            // Dense sampling benefits from a bounded strip. Sparse sampling copies only each
            // required row pair, avoiding conversion of all the skipped source rows.
            int bufferRows = scaleY <= 2
                ? Math.Min(sourceHeight, Math.Max(2, CopyBufferBytes / sourceStride))
                : Math.Min(sourceHeight, 2);
            byte[] rows = new byte[checked(sourceStride * bufferRows)];
            int copiedTop = -1;
            int copiedBottom = -1;
            byte[] gray = new byte[checked(targetWidth * targetHeight)];
            byte[]? alpha = isOpaque ? null : new byte[gray.Length];
            bool hasTransparency = false;

            var x0 = new int[targetWidth];
            var x1 = new int[targetWidth];
            var xFraction = new double[targetWidth];
            double scaleX = (double)(sourceWidth - 1) / Math.Max(targetWidth - 1, 1);
            for (int x = 0; x < targetWidth; x++)
            {
                double sourceX = x * scaleX;
                int left = (int)sourceX;
                x0[x] = left * bytesPerPixel;
                x1[x] = Math.Min(left + 1, sourceWidth - 1) * bytesPerPixel;
                xFraction[x] = sourceX - left;
            }

            for (int y = 0; y < targetHeight; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double sourceY = y * scaleY;
                int top = (int)sourceY;
                int bottom = Math.Min(top + 1, sourceHeight - 1);
                double yFraction = sourceY - top;

                if (top < copiedTop || bottom > copiedBottom)
                {
                    int rowCount = Math.Min(bufferRows, sourceHeight - top);
                    pixels.CopyPixels(new Int32Rect(0, top, sourceWidth, rowCount), rows, sourceStride, 0);
                    copiedTop = top;
                    copiedBottom = top + rowCount - 1;
                }

                int topOffset = (top - copiedTop) * sourceStride;
                int bottomOffset = (bottom - copiedTop) * sourceStride;
                int targetOffset = y * targetWidth;
                for (int x = 0; x < targetWidth; x++)
                {
                    int leftOffset = x0[x];
                    int rightOffset = x1[x];
                    double xWeight = xFraction[x];

                    byte topLeftGray = ReadGray(rows, topOffset + leftOffset, isGray);
                    byte topRightGray = ReadGray(rows, topOffset + rightOffset, isGray);
                    byte bottomLeftGray = ReadGray(rows, bottomOffset + leftOffset, isGray);
                    byte bottomRightGray = ReadGray(rows, bottomOffset + rightOffset, isGray);

                    int index = targetOffset + x;
                    gray[index] = Interpolate(topLeftGray, topRightGray, bottomLeftGray, bottomRightGray, xWeight, yFraction);
                    if (alpha != null)
                    {
                        alpha[index] = Interpolate(rows[topOffset + leftOffset + 3], rows[topOffset + rightOffset + 3],
                            rows[bottomOffset + leftOffset + 3], rows[bottomOffset + rightOffset + 3], xWeight, yFraction);
                        hasTransparency |= alpha[index] < byte.MaxValue;
                    }
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

        private static byte ReadGray(byte[] pixels, int offset, bool isGray)
        {
            if (isGray) return pixels[offset];
            return (byte)Math.Clamp(Math.Round(pixels[offset] * 0.114 + pixels[offset + 1] * 0.587 + pixels[offset + 2] * 0.299), 0, 255);
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
