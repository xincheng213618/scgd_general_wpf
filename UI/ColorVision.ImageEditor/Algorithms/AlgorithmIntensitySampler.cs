using ColorVision.Algorithms;
using System;
using System.Buffers.Binary;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>
    /// Shared canonical-image luminance boundary for analysis providers. Integer and
    /// normalized-float samples are exposed on one nominal 0..255 scale; BGR ordering
    /// is explicit and alpha is not part of luminance.
    /// </summary>
    internal static class AlgorithmIntensitySampler
    {
        public const double NominalPeak = byte.MaxValue;

        public static double ReadLuminanceNominal(AlgorithmImageBuffer image, int x, int y)
        {
            double scale = NominalPeak / NativePeak(image.Format);
            double blueOrGray = ReadChannel(image, x, y, 0);
            if (image.Format.Channels() == 1) return blueOrGray * scale;
            double green = ReadChannel(image, x, y, 1);
            double red = ReadChannel(image, x, y, 2);
            return double.IsFinite(blueOrGray) && double.IsFinite(green) && double.IsFinite(red)
                ? (0.114 * blueOrGray + 0.587 * green + 0.299 * red) * scale
                : double.NaN;
        }

        public static bool TrySampleLuminanceNominal(
            AlgorithmImageBuffer image,
            double x,
            double y,
            bool clampToBounds,
            out double value,
            out bool clamped)
        {
            clamped = false;
            if (!double.IsFinite(x) || !double.IsFinite(y))
            {
                value = double.NaN;
                return false;
            }
            if (x < 0 || x > image.Width - 1 || y < 0 || y > image.Height - 1)
            {
                if (!clampToBounds)
                {
                    value = double.NaN;
                    return false;
                }
                x = Math.Clamp(x, 0, image.Width - 1);
                y = Math.Clamp(y, 0, image.Height - 1);
                clamped = true;
            }

            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(x0 + 1, image.Width - 1);
            int y1 = Math.Min(y0 + 1, image.Height - 1);
            double tx = x - x0;
            double ty = y - y0;
            double topLeft = ReadLuminanceNominal(image, x0, y0);
            double topRight = ReadLuminanceNominal(image, x1, y0);
            double bottomLeft = ReadLuminanceNominal(image, x0, y1);
            double bottomRight = ReadLuminanceNominal(image, x1, y1);
            if (!double.IsFinite(topLeft) || !double.IsFinite(topRight)
                || !double.IsFinite(bottomLeft) || !double.IsFinite(bottomRight))
            {
                value = double.NaN;
                return false;
            }
            value = Lerp(Lerp(topLeft, topRight, tx), Lerp(bottomLeft, bottomRight, tx), ty);
            return true;
        }

        private static double NativePeak(AlgorithmImageFormat format)
            => format.IsFloatingPoint() ? 1 : format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;

        private static double ReadChannel(AlgorithmImageBuffer image, int x, int y, int channel)
        {
            ReadOnlySpan<byte> data = image.Data.Span;
            int bytesPerChannel = image.Format.BitsPerChannel() / 8;
            int offset = checked(y * image.Stride + x * image.Format.BytesPerPixel() + channel * bytesPerChannel);
            return image.Format.BitsPerChannel() switch
            {
                8 => data[offset],
                16 => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2)),
                32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4))),
                _ => throw new ArgumentOutOfRangeException(nameof(image)),
            };
        }

        private static double Lerp(double start, double end, double amount)
            => start + (end - start) * amount;
    }
}
