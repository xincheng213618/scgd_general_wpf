using ColorVision.FileIO;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    internal static class CvcieSrgbRenderer
    {
        private const int ChunkSize = 65536;
        private const int MaxFileWorkers = 16;
        private const int SrgbBucketCount = 8192;
        private const double SrgbLinearCutoff = 0.0031308;
        private static readonly short[] SrgbBuckets = BuildSrgbBuckets();

        public static bool Supports(int channels, int bpp) => channels == 3 && (bpp == 32 || bpp == 64);

        public static WriteableBitmap Render(CVCIEFile file, CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            cancellationToken.ThrowIfCancellationRequested();
            int pixelCount = ValidateRender(file, brightnessMode, referenceWhiteLuminance);
            long expectedLength = GetXyzByteLength(pixelCount, file.Bpp);
            byte[]? data = file.Data;
            if (data == null || data.LongLength != expectedLength)
                throw new InvalidOperationException($"CVCIE XYZ 数据长度不匹配：需要 {expectedLength} 字节，实际 {data?.LongLength ?? 0} 字节。请检查文件是否完整。");

            return RenderCore(new XyzInput(data, null), file, pixelCount, brightnessMode, referenceWhiteLuminance, cancellationToken);
        }

        public static WriteableBitmap RenderFile(string filePath, CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            int offset = CVFileUtil.ReadCIEFileHeader(stream, out CVCIEFile header, maximumChannels: 3);
            using (header)
            {
                if (offset <= 0 || !Supports(header.Channels, header.Bpp))
                    throw new InvalidDataException($"读取 CVCIE 内嵌 XYZ 失败：{filePath}");
                int pixelCount = ValidateRender(header, brightnessMode, referenceWhiteLuminance);
                CvcieDiskPixels pixels = CvcieDiskPixels.Locate(stream, header, offset, GetXyzByteLength(pixelCount, header.Bpp), requireExactLength: true);
                return RenderCore(new XyzInput(null, pixels), header, pixelCount,
                    brightnessMode, referenceWhiteLuminance, cancellationToken);
            }
        }

        private static int ValidateRender(CVCIEFile file, CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance)
        {
            if (!Supports(file.Channels, file.Bpp))
                throw new InvalidOperationException("XYZ 真彩显示仅支持三通道的 32 位或 64 位浮点 CVCIE 数据。");
            if (file.Cols <= 0 || file.Rows <= 0)
                throw new InvalidOperationException("CVCIE 图像尺寸无效，无法生成 XYZ 真彩显示。");
            if (brightnessMode != CvcieBrightnessMode.Auto && brightnessMode != CvcieBrightnessMode.ReferenceWhite)
                throw new InvalidOperationException("CVCIE 真彩亮度映射模式无效。");
            if (brightnessMode == CvcieBrightnessMode.ReferenceWhite && (!double.IsFinite(referenceWhiteLuminance) || referenceWhiteLuminance <= 0))
                throw new InvalidOperationException("CVCIE 真彩参考白亮度必须为大于 0 的有限数值。");

            try
            {
                int pixelCount = checked(file.Cols * file.Rows);
                _ = checked(pixelCount * 3); // Bgr24 output must fit an int-sized image buffer.
                return pixelCount;
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException("CVCIE 图像尺寸或数据长度过大，无法生成 XYZ 真彩显示。", ex);
            }
        }

        private static long GetXyzByteLength(int pixelCount, int bpp) => (long)pixelCount * (bpp / 8) * 3;

        private static WriteableBitmap RenderCore(XyzInput input, CVCIEFile file, int pixelCount,
            CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance, CancellationToken cancellationToken)
        {
            // One divisor for the whole image preserves the relative XYZ/linear RGB values.
            // Measured XYZ already includes calibration: exposure and gain are not applied here.
            return file.Bpp == 32
                ? RenderBitmap<float>(input, file.Cols, file.Rows, pixelCount, brightnessMode, referenceWhiteLuminance, cancellationToken)
                : RenderBitmap<double>(input, file.Cols, file.Rows, pixelCount, brightnessMode, referenceWhiteLuminance, cancellationToken);
        }

        private static WriteableBitmap RenderBitmap<T>(XyzInput input, int width, int height, int pixelCount,
            CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance, CancellationToken cancellationToken)
            where T : unmanaged, IFloatingPointIeee754<T>
        {
            int chunkCount = (pixelCount + ChunkSize - 1) / ChunkSize;
            int workers = Math.Max(1, Environment.ProcessorCount / 2);
            if (input.File != null) workers = Math.Min(workers, MaxFileWorkers);
            double divisor = referenceWhiteLuminance;
            if (brightnessMode == CvcieBrightnessMode.Auto)
            {
                double[] maxima = new double[chunkCount];
                RunChunks(chunkCount, pixelCount, workers, cancellationToken, chunk =>
                    maxima[chunk] = FindChunkMaximum<T>(input, pixelCount, chunk, cancellationToken));
                double maximum = 0;
                foreach (double value in maxima) maximum = Math.Max(maximum, value);
                divisor = maximum > 0 ? maximum : 1;
            }

            return MediaHelper.CreateFrozenDisplayBitmap(width, height, PixelFormats.Bgr24, (buffer, stride) =>
                RunChunks(chunkCount, pixelCount, workers, cancellationToken,
                    chunk => WriteChunk<T>(input, pixelCount, chunk, buffer, width, stride, divisor, cancellationToken)), cancellationToken);
        }

        private static void RunChunks(int chunkCount, int pixelCount, int workers, CancellationToken cancellationToken, Action<int> action)
        {
            if (pixelCount < 1024 * 1024 || workers == 1)
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
                    MaxDegreeOfParallelism = workers,
                }, action);
            }
            catch (AggregateException ex)
            {
                // Preserve the same readable validation error as the sequential path.
                ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw();
                throw;
            }
        }

        private static double FindChunkMaximum<T>(XyzInput input, int pixelCount, int chunk, CancellationToken token)
            where T : unmanaged, IFloatingPointIeee754<T>
        {
            int start = chunk * ChunkSize;
            using var planes = new XyzChunk<T>(input, pixelCount, start, token);
            double maximum = 0;
            for (int index = 0; index < planes.X.Length; index++)
            {
                var (red, green, blue) = ToLinearRgb(planes.X[index], planes.Y[index], planes.Z[index], start + index);
                maximum = Math.Max(maximum, Math.Max(red, Math.Max(green, blue)));
            }
            return maximum;
        }

        private static unsafe void WriteChunk<T>(XyzInput input, int pixelCount, int chunk, IntPtr buffer, int width, int stride, double divisor, CancellationToken token)
            where T : unmanaged, IFloatingPointIeee754<T>
        {
            int start = chunk * ChunkSize;
            using var planes = new XyzChunk<T>(input, pixelCount, start, token);
            int end = start + planes.X.Length;
            for (int index = start; index < end;)
            {
                int row = Math.DivRem(index, width, out int column);
                int count = Math.Min(width - column, end - index);
                Span<byte> pixels = new((byte*)buffer + (long)row * stride + column * 3, count * 3);
                for (int x = 0; x < count; x++)
                {
                    int local = index + x - start;
                    var (red, green, blue) = ToLinearRgb(planes.X[local], planes.Y[local], planes.Z[local], index + x);
                    int offset = x * 3;
                    pixels[offset] = EncodeSrgb(blue / divisor);
                    pixels[offset + 1] = EncodeSrgb(green / divisor);
                    pixels[offset + 2] = EncodeSrgb(red / divisor);
                }
                index += count;
            }
        }

        private readonly record struct XyzInput(byte[]? Data, CvcieDiskPixels? File);

        private readonly ref struct XyzChunk<T> where T : unmanaged
        {
            private readonly CvcieDiskPixels.Chunk<T> _rented;
            public ReadOnlySpan<T> X { get; }
            public ReadOnlySpan<T> Y { get; }
            public ReadOnlySpan<T> Z { get; }

            public XyzChunk(XyzInput input, int pixelCount, int start, CancellationToken token)
            {
                int count = Math.Min(ChunkSize, pixelCount - start);
                if (input.Data is byte[] data)
                {
                    ReadOnlySpan<T> planes = MemoryMarshal.Cast<byte, T>(data);
                    X = planes.Slice(start, count);
                    Y = planes.Slice(pixelCount + start, count);
                    Z = planes.Slice(2 * pixelCount + start, count);
                    return;
                }

                _rented = new CvcieDiskPixels.Chunk<T>(input.File!.Value, pixelCount, 3, start, count, token);
                ReadOnlySpan<T> block = _rented.Samples;
                X = block[..count];
                Y = block.Slice(count, count);
                Z = block.Slice(2 * count, count);
            }

            public void Dispose() => _rented.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (double Red, double Green, double Blue) ToLinearRgb<T>(T sampleX, T sampleY, T sampleZ, int pixelIndex)
            where T : unmanaged, IFloatingPointIeee754<T>
        {
            double x = double.CreateChecked(sampleX);
            double y = double.CreateChecked(sampleY);
            double z = double.CreateChecked(sampleZ);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte EncodeSrgb(double linear)
        {
            double clamped = Math.Clamp(linear, 0, 1);
            short code = SrgbBuckets[(int)(clamped * SrgbBucketCount)];
            return code >= 0 ? (byte)code : QuantizeSrgb(clamped);
        }

        private static short[] BuildSrgbBuckets()
        {
            var buckets = new short[SrgbBucketCount + 1];
            int cutoffBucket = (int)(SrgbLinearCutoff * SrgbBucketCount);
            for (int bucket = 0; bucket < SrgbBucketCount; bucket++)
            {
                // Include a neighboring interval on each side to stay clear of rounding boundaries.
                // The bucket crossing the piecewise cutoff always uses the original transfer function.
                byte lower = QuantizeSrgb((double)Math.Max(0, bucket - 1) / SrgbBucketCount);
                byte upper = QuantizeSrgb((double)Math.Min(SrgbBucketCount, bucket + 2) / SrgbBucketCount);
                buckets[bucket] = bucket != cutoffBucket && lower == upper ? lower : (short)-1;
            }
            buckets[SrgbBucketCount] = byte.MaxValue;
            return buckets;
        }

        private static byte QuantizeSrgb(double clamped)
        {
            double encoded = clamped <= SrgbLinearCutoff ? 12.92 * clamped : 1.055 * Math.Pow(clamped, 1 / 2.4) - 0.055;
            return (byte)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
        }
    }
}
