using ColorVision.ImageEditor.Algorithms;
using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    /// <summary>Hashes tight rows of lossless samples, with RGB channel order and little-endian samples.</summary>
    public static class ColorVisionPixelBinding
    {
        public static string ComputeSha256(BitmapSource source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            PixelFormat format = source.Format;
            if (format != PixelFormats.Gray8 && format != PixelFormats.Gray16 && format != PixelFormats.Gray32Float
                && format != PixelFormats.Bgr24 && format != PixelFormats.Rgb24 && format != PixelFormats.Rgb48)
                throw new NotSupportedException($"Pixel binding does not support {format}.");

            int width = source.PixelWidth, height = source.PixelHeight;
            int stride = checked(width * format.BitsPerPixel / 8);
            // Keep WIC copies bounded while hashing several tightly packed rows at a time.
            const int blockBytes = 512 * 1024;
            int rowsPerBlock = Math.Min(height, Math.Max(1, blockBytes / Math.Max(1, stride)));
            byte[] buffer = ArrayPool<byte>.Shared.Rent(checked(stride * rowsPerBlock));
            try
            {
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                for (int y = 0; y < height;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int rows = Math.Min(rowsPerBlock, height - y);
                    source.CopyPixels(new Int32Rect(0, y, width, rows), buffer, stride, 0);
                    Span<byte> pixels = buffer.AsSpan(0, rows * stride);
                    if (format == PixelFormats.Bgr24)
                        ImageAlgorithmInputFactory.SwapRedBlue24(pixels);
                    hash.AppendData(pixels);
                    y += rows;
                }
                return Convert.ToHexString(hash.GetHashAndReset());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
