using ColorVision.FileIO;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    internal static class CvcieSrgbRenderer
    {
        private const int ChunkSize = 65536;

        public static bool Supports(int channels, int bpp) => channels == 3 && (bpp == 32 || bpp == 64);

        public static WriteableBitmap Render(CVCIEFile file, CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            cancellationToken.ThrowIfCancellationRequested();
            if (!Supports(file.Channels, file.Bpp))
                throw new InvalidOperationException("XYZ 真彩显示仅支持三通道的 32 位或 64 位浮点 CVCIE 数据。");
            if (file.Cols <= 0 || file.Rows <= 0)
                throw new InvalidOperationException("CVCIE 图像尺寸无效，无法生成 XYZ 真彩显示。");
            if (brightnessMode != CvcieBrightnessMode.Auto && brightnessMode != CvcieBrightnessMode.ReferenceWhite)
                throw new InvalidOperationException("CVCIE 真彩亮度映射模式无效。");
            if (brightnessMode == CvcieBrightnessMode.ReferenceWhite && (!double.IsFinite(referenceWhiteLuminance) || referenceWhiteLuminance <= 0))
                throw new InvalidOperationException("CVCIE 真彩参考白亮度必须为大于 0 的有限数值。");

            int pixelCount;
            int planeBytes;
            int expectedLength;
            int stride;
            int outputLength;
            int bytesPerValue = file.Bpp / 8;
            try
            {
                pixelCount = checked(file.Cols * file.Rows);
                planeBytes = checked(pixelCount * bytesPerValue);
                expectedLength = checked(planeBytes * 3);
                stride = checked(file.Cols * 3);
                outputLength = checked(stride * file.Rows);
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException("CVCIE 图像尺寸或数据长度过大，无法生成 XYZ 真彩显示。", ex);
            }

            byte[]? data = file.Data;
            if (data == null || data.Length != expectedLength)
                throw new InvalidOperationException($"CVCIE XYZ 数据长度不匹配：需要 {expectedLength} 字节，实际 {data?.Length ?? 0} 字节。请检查文件是否完整。");

            // One divisor for the whole image preserves the relative XYZ/linear RGB values.
            // Measured XYZ already includes calibration: exposure and gain are not applied here.
            byte[] pixels = file.Bpp == 32
                ? RenderPixels<float>(data, pixelCount, outputLength, brightnessMode, referenceWhiteLuminance, cancellationToken)
                : RenderPixels<double>(data, pixelCount, outputLength, brightnessMode, referenceWhiteLuminance, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            WriteableBitmap bitmap = new(file.Cols, file.Rows, 96, 96, PixelFormats.Bgr24, null);
            bitmap.WritePixels(new Int32Rect(0, 0, file.Cols, file.Rows), pixels, stride, 0);
            bitmap.Freeze();
            return bitmap;
        }

        private static byte[] RenderPixels<T>(byte[] data, int pixelCount, int outputLength, CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance, CancellationToken cancellationToken)
            where T : unmanaged, IFloatingPointIeee754<T>
        {
            int chunkCount = (pixelCount + ChunkSize - 1) / ChunkSize;
            double divisor = referenceWhiteLuminance;
            if (brightnessMode == CvcieBrightnessMode.Auto)
            {
                double[] maxima = new double[chunkCount];
                RunChunks(chunkCount, pixelCount, cancellationToken, chunk =>
                    maxima[chunk] = ConvertChunk<T>(data, pixelCount, chunk, null, 1));
                double maximum = 0;
                foreach (double value in maxima) maximum = Math.Max(maximum, value);
                divisor = maximum > 0 ? maximum : 1;
            }

            byte[] pixels = new byte[outputLength];
            RunChunks(chunkCount, pixelCount, cancellationToken, chunk =>
                ConvertChunk<T>(data, pixelCount, chunk, pixels, divisor));
            return pixels;
        }

        private static void RunChunks(int chunkCount, int pixelCount, CancellationToken cancellationToken, Action<int> action)
        {
            if (pixelCount < 1024 * 1024 || Environment.ProcessorCount < 2)
            {
                for (int chunk = 0; chunk < chunkCount; chunk++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    action(chunk);
                }
                return;
            }

            try
            {
                // Bound CPU usage and keep cancellation responsive between small chunks.
                Parallel.For(0, chunkCount, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2),
                }, action);
            }
            catch (AggregateException ex)
            {
                // Preserve the same readable validation error as the sequential path.
                ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw();
                throw;
            }
        }

        private static double ConvertChunk<T>(byte[] data, int pixelCount, int chunk, byte[]? pixels, double divisor)
            where T : unmanaged, IFloatingPointIeee754<T>
        {
            ReadOnlySpan<T> planes = MemoryMarshal.Cast<byte, T>(data.AsSpan());
            ReadOnlySpan<T> xPlane = planes[..pixelCount];
            ReadOnlySpan<T> yPlane = planes.Slice(pixelCount, pixelCount);
            ReadOnlySpan<T> zPlane = planes.Slice(pixelCount * 2, pixelCount);
            int start = chunk * ChunkSize;
            int end = Math.Min(start + ChunkSize, pixelCount);
            double maximum = 0;
            for (int pixelIndex = start; pixelIndex < end; pixelIndex++)
            {
                var (red, green, blue) = ToLinearRgb(double.CreateChecked(xPlane[pixelIndex]), double.CreateChecked(yPlane[pixelIndex]), double.CreateChecked(zPlane[pixelIndex]), pixelIndex);
                if (pixels == null)
                {
                    maximum = Math.Max(maximum, Math.Max(red, Math.Max(green, blue)));
                }
                else
                {
                    int offset = pixelIndex * 3;
                    pixels[offset] = EncodeSrgb(blue / divisor);
                    pixels[offset + 1] = EncodeSrgb(green / divisor);
                    pixels[offset + 2] = EncodeSrgb(red / divisor);
                }
            }
            return maximum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (double Red, double Green, double Blue) ToLinearRgb(double x, double y, double z, int pixelIndex)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
                throw new InvalidOperationException($"CVCIE 第 {pixelIndex + 1} 个像素的 XYZ 包含 NaN 或无穷值，无法生成真彩显示。请检查校正结果和文件数据。");

            // D65 XYZ -> linear sRGB, consistent with CieBackgroundRenderer.
            double red = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
            double green = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
            double blue = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;
            if (!double.IsFinite(red) || !double.IsFinite(green) || !double.IsFinite(blue))
                throw new InvalidOperationException($"CVCIE 第 {pixelIndex + 1} 个像素的 XYZ 数值超出真彩转换范围。请检查校正结果和文件数据。");
            return (red, green, blue);
        }

        private static byte EncodeSrgb(double linear)
        {
            double clamped = Math.Clamp(linear, 0, 1);
            double encoded = clamped <= 0.0031308 ? 12.92 * clamped : 1.055 * Math.Pow(clamped, 1 / 2.4) - 0.055;
            return (byte)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
        }
    }
}
