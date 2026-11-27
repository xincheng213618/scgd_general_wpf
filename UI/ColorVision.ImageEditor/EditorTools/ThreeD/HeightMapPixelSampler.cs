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
            double scaleY = (double)(sourceHeight - 1) / Math.Max(targetHeight - 1, 1);
            bool copyStrips = scaleY <= 2;
            PixelFormat sourceFormat = source.Format;
            bool isGray16 = sourceFormat == PixelFormats.Gray16;
            bool isGray = sourceFormat == PixelFormats.Gray8 || isGray16;
            bool isRgb48 = sourceFormat == PixelFormats.Rgb48;
            bool isOpaqueColor = sourceFormat == PixelFormats.Bgr24 || sourceFormat == PixelFormats.Bgr32 || isRgb48;
            bool isOpaque = isGray || isOpaqueColor;
            // Gray16-to-Bgra32 conversion is faster for the small, disjoint row copies in sparse sampling.
            PixelFormat copyFormat = isGray && (!isGray16 || copyStrips) ? PixelFormats.Gray8
                : isOpaqueColor ? sourceFormat : PixelFormats.Bgra32;
            BitmapSource pixels = sourceFormat == copyFormat ? source : new FormatConvertedBitmap(source, copyFormat, null, 0);
            if (targetWidth == sourceWidth && targetHeight == sourceHeight)
                return SampleAtSourceSize(pixels, copyFormat == PixelFormats.Gray8, isOpaque, isRgb48, cancellationToken);

            int bytesPerPixel = pixels.Format.BitsPerPixel / 8;
            int sourceStride = checked(sourceWidth * bytesPerPixel);

            // Dense sampling benefits from a bounded strip. Sparse sampling copies only each
            // required row pair, avoiding conversion of all the skipped source rows.
            int bufferRows = copyStrips
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
                double topWeight = 1 - yFraction;

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
                    double leftWeight = 1 - xWeight;

                    byte topLeftGray, topRightGray, bottomLeftGray, bottomRightGray;
                    if (isGray)
                    {
                        topLeftGray = rows[topOffset + leftOffset];
                        topRightGray = rows[topOffset + rightOffset];
                        bottomLeftGray = rows[bottomOffset + leftOffset];
                        bottomRightGray = rows[bottomOffset + rightOffset];
                    }
                    else if (isRgb48)
                    {
                        topLeftGray = ReadRgb48Gray(rows, topOffset + leftOffset);
                        topRightGray = ReadRgb48Gray(rows, topOffset + rightOffset);
                        bottomLeftGray = ReadRgb48Gray(rows, bottomOffset + leftOffset);
                        bottomRightGray = ReadRgb48Gray(rows, bottomOffset + rightOffset);
                    }
                    else
                    {
                        topLeftGray = ReadColorGray(rows, topOffset + leftOffset);
                        topRightGray = ReadColorGray(rows, topOffset + rightOffset);
                        bottomLeftGray = ReadColorGray(rows, bottomOffset + leftOffset);
                        bottomRightGray = ReadColorGray(rows, bottomOffset + rightOffset);
                    }

                    int index = targetOffset + x;
                    gray[index] = Interpolate(topLeftGray, topRightGray, bottomLeftGray, bottomRightGray,
                        leftWeight, xWeight, topWeight, yFraction);
                    if (alpha != null)
                    {
                        alpha[index] = Interpolate(rows[topOffset + leftOffset + 3], rows[topOffset + rightOffset + 3],
                            rows[bottomOffset + leftOffset + 3], rows[bottomOffset + rightOffset + 3],
                            leftWeight, xWeight, topWeight, yFraction);
                        hasTransparency |= alpha[index] < byte.MaxValue;
                    }
                }
            }

            return new HeightMapSample(gray, hasTransparency ? alpha : null, targetWidth, targetHeight);
        }

        private static HeightMapSample SampleAtSourceSize(BitmapSource pixels, bool isGray, bool isOpaque, bool isRgb48,
            CancellationToken cancellationToken)
        {
            int width = pixels.PixelWidth;
            int height = pixels.PixelHeight;
            int bytesPerPixel = pixels.Format.BitsPerPixel / 8;
            int stride = checked(width * bytesPerPixel);
            int rowsPerStrip = Math.Min(height, Math.Max(1, CopyBufferBytes / stride));
            byte[] gray = new byte[checked(width * height)];
            byte[]? alpha = isOpaque ? null : new byte[gray.Length];
            byte[] rows = isGray ? gray : new byte[checked(stride * rowsPerStrip)];
            bool hasTransparency = false;

            // At integer coordinates each output needs only its corresponding source pixel.
            for (int top = 0; top < height;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int rowCount = Math.Min(rowsPerStrip, height - top);
                pixels.CopyPixels(new Int32Rect(0, top, width, rowCount), rows, stride, isGray ? top * stride : 0);
                if (!isGray)
                {
                    for (int row = 0; row < rowCount; row++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int sourceOffset = row * stride;
                        int targetOffset = (top + row) * width;
                        if (isRgb48)
                        {
                            for (int x = 0; x < width; x++, sourceOffset += bytesPerPixel)
                                gray[targetOffset + x] = ReadRgb48Gray(rows, sourceOffset);
                            continue;
                        }
                        for (int x = 0; x < width; x++, sourceOffset += bytesPerPixel)
                        {
                            int index = targetOffset + x;
                            gray[index] = (byte)Math.Round(rows[sourceOffset] * 0.114
                                + rows[sourceOffset + 1] * 0.587 + rows[sourceOffset + 2] * 0.299);
                            if (alpha != null)
                            {
                                alpha[index] = rows[sourceOffset + 3];
                                hasTransparency |= alpha[index] < byte.MaxValue;
                            }
                        }
                    }
                }
                top += rowCount;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new HeightMapSample(gray, hasTransparency ? alpha : null, width, height);
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

        private static byte ReadColorGray(byte[] pixels, int offset)
            => RoundColorGray(pixels[offset], pixels[offset + 1], pixels[offset + 2]);

        // RGB48's high bytes match WPF's 8-bit display conversion.
        private static byte ReadRgb48Gray(byte[] pixels, int offset)
            => RoundColorGray(pixels[offset + 5], pixels[offset + 3], pixels[offset + 1]);

        private static byte RoundColorGray(int blue, int green, int red)
        {
            int weighted = blue * 114 + green * 587 + red * 299 + 500;
            int gray = weighted / 1000;
            // Only an exact midpoint can be rounded differently by the original floating-point expression.
            return weighted != gray * 1000 ? (byte)gray : (byte)Math.Round(blue * 0.114 + green * 0.587 + red * 0.299);
        }

        private static byte Interpolate(byte topLeft, byte topRight, byte bottomLeft, byte bottomRight,
            double leftWeight, double rightWeight, double topWeight, double bottomWeight)
        {
            // Keep the original multiplication order for values close to a rounding midpoint.
            double value = topLeft * leftWeight * topWeight
                         + topRight * rightWeight * topWeight
                         + bottomLeft * leftWeight * bottomWeight
                         + bottomRight * rightWeight * bottomWeight;
            return (byte)Math.Round(value);
        }
    }
}
