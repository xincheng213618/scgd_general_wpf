#pragma warning disable CS8603
using ColorVision.Core;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Presentation;
using ColorVision.Themes.Controls;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ColorVision.Engine.Media
{

    public static class MediaHelper
    {
        private static ILog log = LogManager.GetLogger(typeof(MediaHelper));

        public static MatType GetPixelFormat(this PixelFormat pixelFormat)
        {
            if (pixelFormat == PixelFormats.Gray8)
            {
                return MatType.CV_8UC1;
            }
            if (pixelFormat == PixelFormats.Gray16)
            {
                return MatType.CV_16UC1;
            }
            if (pixelFormat == PixelFormats.Bgr24)
            {
                return MatType.CV_8UC3;
            }
            if (pixelFormat == PixelFormats.Rgb24)
            {
                return MatType.CV_8UC3;
            }
            if (pixelFormat == PixelFormats.Bgr32)
            {
                return MatType.CV_8UC4;
            }
            if (pixelFormat == PixelFormats.Rgb48)
            {
                return MatType.CV_16SC3;
            }
            if (pixelFormat == PixelFormats.Bgra32)
            {
                return MatType.CV_8UC4;
            }
            if (pixelFormat == PixelFormats.Gray32Float)
            {
                return MatType.CV_32FC1;
            }
            if (pixelFormat == PixelFormats.Prgba64)
            {
                return MatType.CV_16UC4;
            }
            throw new Exception("Unsupported file format.");
        }

        private static int GetMatDepth(int bpp)
        {
            return bpp switch
            {
                8 => 0,
                16 => 2,
                32 => 5,
                64 => 6,
                _ => throw new NotSupportedException($"Unsupported bit depth: {bpp}"),
            };
        }

        public static byte[] SwapRedBlueChannels(byte[] sourceData, int rows, int cols, int bpp, int channels)
        {
            ArgumentNullException.ThrowIfNull(sourceData);

            if (sourceData.Length == 0 || (channels != 3 && channels != 4))
            {
                return sourceData;
            }

            try
            {
                ValidatePixelData(sourceData.LongLength, rows, cols, bpp, channels);
                using var src = Mat.FromPixelData(rows, cols, MatType.MakeType(GetMatDepth(bpp), channels), sourceData);
                using var dst = new Mat();
                var conversion = channels == 3 ? ColorConversionCodes.BGR2RGB : ColorConversionCodes.BGRA2RGBA;
                Cv2.CvtColor(src, dst, conversion);

                int length = checked((int)(dst.Total() * dst.ElemSize()));
                byte[] converted = new byte[length];
                Marshal.Copy(dst.Data, converted, 0, length);
                return converted;
            }
            catch (Exception ex)
            {
                log.Warn("SwapRedBlueChannels failed, fallback to original data.", ex);
                return sourceData;
            }
        }


        public static Mat ToMat(this CVCIEFile fileInfo, bool showErrors = true)
        {
            OpenCvSharp.Mat? src = null;
            try
            {
                ArgumentNullException.ThrowIfNull(fileInfo);
                if (fileInfo.FileExtType == CVType.Tif)
                {
                    if (fileInfo.Data == null || fileInfo.Data.Length == 0) throw new InvalidDataException("图像数据为空。");
                    src = OpenCvSharp.Cv2.ImDecode(fileInfo.Data, OpenCvSharp.ImreadModes.Unchanged);
                }
                else if (fileInfo.FileExtType == CVType.Raw || fileInfo.FileExtType == CVType.Src || fileInfo.FileExtType == CVType.CIE)
                {
                    ValidatePixelData(fileInfo.Data?.LongLength ?? 0, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp, fileInfo.Channels);
                    if (fileInfo.FileExtType == CVType.CIE)
                    {
                        if (fileInfo.Channels == 3)
                        {
                            src = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, OpenCvSharp.MatType.MakeType(fileInfo.Depth, 1), fileInfo.Data);
                        }
                        else
                        {
                            src = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                        }
                    }
                    else
                    {
                        src = OpenCvSharp.Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                    }
                    if (fileInfo.Bpp is 32 or 64)
                    {
                        using Mat floatingPixels = src;
                        src = null;
                        src = ConvertFloatToDisplayBytes(floatingPixels, CancellationToken.None);
                    }
                }

                if (src == null)
                {
                    throw new Exception("Unsupported file format.");
                }


                return src;
            }
            catch (Exception ex)
            {
                src?.Dispose();
                log.Error(ex);
                if (showErrors)
                    MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.OpenFileFailed + $":{ex.Message} ", "ColorVision");
                return null;
            }
        }

        internal static Mat ConvertFloatToDisplayBytes(Mat source, CancellationToken cancellationToken)
        {
            const int chunkSamples = 64 * 1024;
            const double doubleMachineEpsilon = 2.2204460492503131e-16;
            using Mat samples = source.Reshape(1, 1);
            Cv2.MinMaxLoc(samples, out double min, out double max);
            cancellationToken.ThrowIfCancellationRequested();

            // Match OpenCV NORM_MINMAX at the input depth before the separate rounding to bytes.
            MatType depth = source.Depth();
            double scale = 255 * (max - min > doubleMachineEpsilon ? 1 / (max - min) : 0);
            if (depth == MatType.CV_32F) scale = (float)scale;
            double shift = depth == MatType.CV_32F ? 0f - (float)(min * scale) : 0d - min * scale;
            Mat bytes = new(source.Rows, source.Cols, MatType.MakeType(MatType.CV_8U, source.Channels()));
            try
            {
                // Work in bounded blocks so the source samples stay unchanged without a full float copy.
                using Mat normalized = new();
                int sampleCount = samples.Cols;
                IntPtr output = bytes.Data;
                for (int start = 0; start < sampleCount;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = Math.Min(chunkSamples, sampleCount - start);
                    using Mat block = samples.ColRange(start, start + count);
                    using Mat destination = Mat.FromPixelData(1, count, MatType.CV_8UC1, IntPtr.Add(output, start));
                    block.ConvertTo(normalized, depth, scale, shift);
                    normalized.ConvertTo(destination, MatType.CV_8U);
                    start += count;
                }
                return bytes;
            }
            catch
            {
                bytes.Dispose();
                throw;
            }
        }

        private static void ValidatePixelData(long dataLength, int rows, int cols, int bpp, int channels)
        {
            if (rows <= 0 || cols <= 0) throw new InvalidDataException("图像尺寸必须为正数。");
            if (channels is not (1 or 3 or 4)) throw new InvalidDataException($"不支持的图像通道数：{channels}。");
            if (bpp is not (8 or 16 or 32 or 64)) throw new InvalidDataException($"不支持的图像位深：{bpp}。");
            long expectedLength = checked((long)rows * cols * (bpp / 8) * channels);
            if (dataLength != expectedLength)
                throw new InvalidDataException($"图像数据长度与尺寸不匹配：需要 {expectedLength} 字节，实际 {dataLength} 字节。");
        }

        internal static WriteableBitmap RenderFloatChannel(CVCIEFile fileInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fileInfo);
            cancellationToken.ThrowIfCancellationRequested();
            if (fileInfo.Channels != 1)
                throw new InvalidDataException("浮点灰度显示要求单通道 32 位或 64 位数据。");
            return RenderFloatChannel(fileInfo.Data, fileInfo.Cols, fileInfo.Rows, fileInfo.Bpp, cancellationToken);
        }

        internal static WriteableBitmap RenderFloatChannel(ReadOnlyMemory<byte> samples, int width, int height, int bpp,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (bpp is not (32 or 64))
                throw new InvalidDataException("浮点灰度显示要求单通道 32 位或 64 位数据。");
            ValidatePixelData(samples.Length, height, width, bpp, 1);
            return RenderFloatChannel(new FloatChannelInput(samples, null), width, height, bpp, cancellationToken);
        }

        internal static WriteableBitmap RenderFloatChannel(CvcieDiskPixels plane, int width, int height, int bpp, CancellationToken token)
        {
            using PreparedFloatChannelWriter writer = PrepareFloatChannelWriter(plane, width, height, bpp, token);
            return CreateFrozenDisplayBitmap(width, height, PixelFormats.Gray8, writer.Write, token);
        }

        private static WriteableBitmap RenderFloatChannel(FloatChannelInput source, int width, int height, int bpp, CancellationToken token)
            => CreateFrozenDisplayBitmap(width, height, PixelFormats.Gray8, PrepareFloatChannelWriter(source, width, height, bpp, token), token);

        internal static PreparedFloatChannelWriter PrepareFloatChannelWriter(CvcieDiskPixels plane, int width, int height, int bpp, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int pixelCount = checked(width * height);
            CvcieDiskPixels.MappedPlane? mapping = pixelCount >= LargeFloatChannelPixelCount
                ? plane.TryMapFixedVolume((long)pixelCount * (bpp / 8)) : null;
            try
            {
                Action<IntPtr, int> write = PrepareFloatChannelWriter(new FloatChannelInput(default, plane, mapping), width, height, bpp, token);
                return new PreparedFloatChannelWriter(write, mapping);
            }
            catch
            {
                mapping?.Dispose();
                throw;
            }
        }

        // Keep the mapped plane open from range discovery until the bitmap writer has returned.
        internal sealed class PreparedFloatChannelWriter(Action<IntPtr, int> write, CvcieDiskPixels.MappedPlane? mapping) : IDisposable
        {
            public void Write(IntPtr buffer, int stride) => write(buffer, stride);
            public void Dispose() => mapping?.Dispose();
        }

        private static Action<IntPtr, int> PrepareFloatChannelWriter(FloatChannelInput source, int width, int height, int bpp, CancellationToken token)
            => bpp == 32 ? PrepareFloatChannelWriter<float>(source, width, height, token) : PrepareFloatChannelWriter<double>(source, width, height, token);

        private const int FloatChannelChunkSize = 65536;
        private const int LargeFloatChannelPixelCount = 1024 * 1024;
        private readonly record struct FloatChannelInput(ReadOnlyMemory<byte> Samples, CvcieDiskPixels? File, CvcieDiskPixels.MappedPlane? Mapping = null);

        private static Action<IntPtr, int> PrepareFloatChannelWriter<T>(FloatChannelInput source, int width, int height,
            CancellationToken cancellationToken)
            where T : unmanaged, System.Numerics.IFloatingPointIeee754<T>
        {
            int pixelCount = checked(width * height);
            int chunkCount = (pixelCount - 1) / FloatChannelChunkSize + 1;
            int workers = Math.Max(1, Environment.ProcessorCount / 2);
            if (source.File.HasValue) workers = Math.Min(workers, 16);
            (double Minimum, double Maximum)[] ranges = new (double, double)[chunkCount];
            RunFloatChannelChunks(chunkCount, pixelCount, workers, cancellationToken,
                chunk => ranges[chunk] = FindFloatChannelRange<T>(source, pixelCount, chunk, cancellationToken));
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            foreach (var local in ranges)
            {
                minimum = Math.Min(minimum, local.Minimum);
                maximum = Math.Max(maximum, local.Maximum);
            }

            double range = maximum - minimum;
            // Scale endpoints only when finite double extremes overflow their difference.
            // Divide by the range rather than forming an infinite factor for subnormal data.
            double magnitude = double.IsFinite(range) ? 1 : Math.Max(Math.Abs(minimum), Math.Abs(maximum));
            if (magnitude != 1)
            {
                minimum /= magnitude;
                range = maximum / magnitude - minimum;
            }
            return (buffer, stride) => RunFloatChannelChunks(chunkCount, pixelCount, workers, cancellationToken,
                chunk => WriteFloatChannelChunk<T>(source, pixelCount, buffer, width, stride, chunk, minimum, range, magnitude, cancellationToken));
        }

        internal static WriteableBitmap CreateFrozenDisplayBitmap(int width, int height, PixelFormat format,
            Action<IntPtr, int> fillBuffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteableBitmap bitmap = new(width, height, 96, 96, format, null);
            bitmap.Lock();
            try
            {
                // The writer must finish all work before the buffer is unlocked.
                fillBuffer(bitmap.BackBuffer, bitmap.BackBufferStride);
                cancellationToken.ThrowIfCancellationRequested();
                bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally { bitmap.Unlock(); }
            bitmap.Freeze();
            return bitmap;
        }

        private static (double Minimum, double Maximum) FindFloatChannelRange<T>(FloatChannelInput source, int pixelCount, int chunk, CancellationToken token)
            where T : unmanaged, System.Numerics.IFloatingPointIeee754<T>
        {
            int start = chunk * FloatChannelChunkSize;
            using var block = new FloatChannelChunk<T>(source, pixelCount, start, token);
            ReadOnlySpan<T> values = block.Samples;
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            for (int index = 0; index < values.Length; index++)
            {
                double value = double.CreateChecked(values[index]);
                if (!double.IsFinite(value)) throw new InvalidDataException($"灰度通道第 {start + index + 1} 个像素包含非有限数值。");
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }
            return (minimum, maximum);
        }

        private static unsafe void WriteFloatChannelChunk<T>(FloatChannelInput source, int pixelCount, IntPtr buffer, int width, int stride,
            int chunk, double minimum, double range, double magnitude, CancellationToken token)
            where T : unmanaged, System.Numerics.IFloatingPointIeee754<T>
        {
            int start = chunk * FloatChannelChunkSize;
            using FloatChannelChunk<T> block = range == 0 ? default : new FloatChannelChunk<T>(source, pixelCount, start, token);
            ReadOnlySpan<T> values = block.Samples;
            int end = start + Math.Min(FloatChannelChunkSize, pixelCount - start);
            for (int index = start; index < end;)
            {
                int row = Math.DivRem(index, width, out int column);
                int count = Math.Min(width - column, end - index);
                Span<byte> pixels = new((byte*)buffer + (long)row * stride + column, count);
                if (range == 0)
                    pixels.Clear();
                else
                {
                    for (int x = 0; x < count; x++)
                    {
                        double value = double.CreateChecked(values[index + x - start]);
                        double normalized = (value / magnitude - minimum) / range;
                        pixels[x] = (byte)Math.Round(Math.Clamp(normalized, 0, 1) * 255);
                    }
                }
                index += count;
            }
        }

        private readonly ref struct FloatChannelChunk<T> where T : unmanaged
        {
            private readonly CvcieDiskPixels.Chunk<T> _rented;
            public ReadOnlySpan<T> Samples { get; }

            public FloatChannelChunk(FloatChannelInput source, int pixelCount, int start, CancellationToken token)
            {
                int count = Math.Min(FloatChannelChunkSize, pixelCount - start);
                if (source.Mapping is { } mapping) Samples = mapping.GetSamples<T>(start, count);
                else if (source.File is { } file)
                {
                    _rented = new CvcieDiskPixels.Chunk<T>(file, pixelCount, 1, start, count, token);
                    Samples = _rented.Samples;
                }
                else Samples = MemoryMarshal.Cast<byte, T>(source.Samples.Span).Slice(start, count);
            }

            public void Dispose() => _rented.Dispose();
        }

        private static void RunFloatChannelChunks(int chunkCount, int pixelCount, int workers, CancellationToken cancellationToken, Action<int> action)
        {
            if (pixelCount < LargeFloatChannelPixelCount || workers == 1)
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
                Parallel.For(0, chunkCount, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = workers
                }, action);
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw();
                throw;
            }
        }

        /// <summary>Copies display pixels straight into WPF's back buffer, including 16-bit BGR/RGB conversion.</summary>
        public static WriteableBitmap CreateDisplayBitmap(this Mat srcMat)
        {
            if (!TryGetDisplayPixelFormat(srcMat.Type(), out PixelFormat format))
                return srcMat.ToWriteableBitmap();

            WriteableBitmap bitmap = new(srcMat.Cols, srcMat.Rows, 96, 96, format, null);
            if (!srcMat.MatUpdateWriteableBitmap(bitmap))
                throw new InvalidOperationException("Failed to copy display pixels into WriteableBitmap.");
            return bitmap;
        }

        internal static bool TryGetDisplayPixelFormat(MatType type, out PixelFormat format)
        {
            if (type == MatType.CV_8UC1)
                format = PixelFormats.Gray8;
            else if (type == MatType.CV_16UC1)
                format = PixelFormats.Gray16;
            else if (type == MatType.CV_32FC1)
                format = PixelFormats.Gray32Float;
            else if (type == MatType.CV_8UC3)
                format = PixelFormats.Bgr24;
            else if (type == MatType.CV_8UC4)
                format = PixelFormats.Bgra32;
            else if (type == MatType.CV_16UC3 || type == MatType.CV_16SC3)
                format = PixelFormats.Rgb48;
            else if (type == MatType.CV_16UC4 || type == MatType.CV_16SC4)
                format = PixelFormats.Rgba64;
            else
            {
                format = default;
                return false;
            }
            return true;
        }

        internal static bool IsCompatibleDisplayBitmap(WriteableBitmap? bitmap, Mat source, PixelFormat format)
            => bitmap is { IsFrozen: false } && bitmap.PixelWidth == source.Cols && bitmap.PixelHeight == source.Rows
                && bitmap.Format == format;

        internal static Task<WriteableBitmap> PrepareDisplayBitmapAsync(this Mat source, PixelFormat format, CancellationToken cancellationToken)
            => EditableBitmapCopy.CreateAsync(source.Cols, source.Rows, 96, 96, format, null,
                (buffer, stride) => CopyDisplayPixels(source, buffer, stride), cancellationToken);

        internal const int RawDisplayStagingBytes = 4 * 1024 * 1024;

        internal delegate void RawDisplayReader(Span<byte> destination, long offset, CancellationToken token);

        internal static Task<WriteableBitmap> PrepareRawDisplayAsync(RawDisplayReader readPixels, int width, int height, PixelFormat format, CancellationToken token)
            => EditableBitmapCopy.CreateAsync(width, height, 96, 96, format, null,
                (buffer, stride) => CopyRawDisplayPixels(readPixels, width, height, format, buffer, stride, token), token);

        internal static unsafe void CopyPackedRawToLockedBitmap(ReadOnlySpan<byte> pixels, WriteableBitmap bitmap)
        {
            int stride = bitmap.BackBufferStride;
            var destination = new Span<byte>(bitmap.BackBuffer.ToPointer(), checked(stride * bitmap.PixelHeight));
            EditableBitmapCopy.CopyPackedRows(pixels, destination, bitmap.PixelWidth, bitmap.PixelHeight,
                bitmap.Format.BitsPerPixel / 8, stride, swapRedBlue16: bitmap.Format == PixelFormats.Rgb48);
        }

        private static unsafe void CopyRawDisplayPixels(RawDisplayReader readPixels, int width, int height, PixelFormat format, IntPtr buffer, int stride, CancellationToken token)
        {
            int sourceStride = checked(width * (format.BitsPerPixel / 8));
            int rowsPerBlock = Math.Max(1, Math.Min(height, RawDisplayStagingBytes / sourceStride));
            byte* target = (byte*)buffer;
            if (format != PixelFormats.Rgb48 && stride == sourceStride)
            {
                // Matching grayscale and BGR24 rows can be read directly into the unpublished bitmap.
                for (int top = 0; top < height; top += rowsPerBlock)
                {
                    token.ThrowIfCancellationRequested();
                    int length = Math.Min(rowsPerBlock, height - top) * sourceStride;
                    long offset = (long)top * sourceStride;
                    readPixels(new Span<byte>(target + offset, length), offset, token);
                }
                return;
            }

            MatType type = format == PixelFormats.Rgb48 ? MatType.CV_16UC3 : format.GetPixelFormat();
            byte[] staging = ArrayPool<byte>.Shared.Rent(checked(rowsPerBlock * sourceStride));
            try
            {
                fixed (byte* source = staging)
                {
                    for (int top = 0; top < height;)
                    {
                        token.ThrowIfCancellationRequested();
                        int rows = Math.Min(rowsPerBlock, height - top);
                        readPixels(staging.AsSpan(0, rows * sourceStride), (long)top * sourceStride, token);
                        token.ThrowIfCancellationRequested();
                        using var block = Mat.FromPixelData(rows, width, type, (IntPtr)source, sourceStride);
                        CopyDisplayPixels(block, (IntPtr)(target + (long)top * stride), stride);
                        top += rows;
                    }
                }
            }
            finally { ArrayPool<byte>.Shared.Return(staging); }
        }

        public static bool MatUpdateWriteableBitmap(this Mat srcMat, WriteableBitmap writeableBitmap)
        {
            if (!TryGetDisplayPixelFormat(srcMat.Type(), out PixelFormat format)
                || !IsCompatibleDisplayBitmap(writeableBitmap, srcMat, format))
                return false;

            writeableBitmap.Lock();
            try
            {
                CopyDisplayPixels(srcMat, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, srcMat.Cols, srcMat.Rows));
            }
            catch (Exception ex)
            {
                log.Error("Failed to update WriteableBitmap from Mat.", ex);
                return false;
            }
            finally
            {
                writeableBitmap.Unlock();
            }
            return true;
        }

        private static void CopyDisplayPixels(Mat source, IntPtr buffer, int stride)
        {
            MatType type = source.Type();
            using var destination = Mat.FromPixelData(source.Rows, source.Cols, type, buffer, stride);
            if (type == MatType.CV_16UC3 || type == MatType.CV_16SC3)
                Cv2.CvtColor(source, destination, ColorConversionCodes.BGR2RGB);
            else if (type == MatType.CV_16UC4 || type == MatType.CV_16SC4)
                Cv2.CvtColor(source, destination, ColorConversionCodes.BGRA2RGBA);
            else
                source.CopyTo(destination);
        }

        public static WriteableBitmap? ToWriteableBitmap(this CVCIEFile fileInfo, bool showErrors = true)
        {
            OpenCvSharp.Mat? src = null;
            try
            {
                src = fileInfo.ToMat(showErrors);
                if (src == null) return null;
                WriteableBitmap writeableBitmap = null;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    writeableBitmap = src.CreateDisplayBitmap();
                });
                return writeableBitmap;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                if (showErrors)
                    MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.OpenFileFailed+$":{ex.Message} ", "ColorVision");
                return null;
            }
            finally
            {
                src?.Dispose();
            }
        }

    }
}
