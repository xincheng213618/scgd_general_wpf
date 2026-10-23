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

            int stride = checked(source.PixelWidth * format.BitsPerPixel / 8);
            byte[] row = ArrayPool<byte>.Shared.Rent(stride);
            try
            {
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                for (int y = 0; y < source.PixelHeight; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    source.CopyPixels(new Int32Rect(0, y, source.PixelWidth, 1), row, stride, 0);
                    if (format == PixelFormats.Bgr24)
                    {
                        for (int offset = 0; offset < stride; offset += 3)
                            (row[offset], row[offset + 2]) = (row[offset + 2], row[offset]);
                    }
                    hash.AppendData(row, 0, stride);
                }
                return Convert.ToHexString(hash.GetHashAndReset());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(row);
            }
        }
    }
}
