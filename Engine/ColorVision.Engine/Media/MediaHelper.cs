#pragma warning disable CS8603
using ColorVision.Core;
using ColorVision.FileIO;
using ColorVision.Themes.Controls;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
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
                ValidatePixelData(sourceData, rows, cols, bpp, channels);
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
                    ValidatePixelData(fileInfo.Data, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp, fileInfo.Channels);
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
                        OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                        src.ConvertTo(src, OpenCvSharp.MatType.CV_8U);
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

        private static void ValidatePixelData(byte[]? data, int rows, int cols, int bpp, int channels)
        {
            if (rows <= 0 || cols <= 0) throw new InvalidDataException("图像尺寸必须为正数。");
            if (channels is not (1 or 3 or 4)) throw new InvalidDataException($"不支持的图像通道数：{channels}。");
            if (bpp is not (8 or 16 or 32 or 64)) throw new InvalidDataException($"不支持的图像位深：{bpp}。");
            long expectedLength = checked((long)rows * cols * (bpp / 8) * channels);
            if (data == null || data.LongLength != expectedLength)
                throw new InvalidDataException($"图像数据长度与尺寸不匹配：需要 {expectedLength} 字节，实际 {data?.LongLength ?? 0} 字节。");
        }

        internal static WriteableBitmap RenderFloatChannel(CVCIEFile fileInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fileInfo);
            cancellationToken.ThrowIfCancellationRequested();
            if (fileInfo.Channels != 1 || fileInfo.Bpp is not (32 or 64))
                throw new InvalidDataException("浮点灰度显示要求单通道 32 位或 64 位数据。");
            ValidatePixelData(fileInfo.Data, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp, fileInfo.Channels);

            int pixelCount = checked(fileInfo.Rows * fileInfo.Cols);
            byte[] pixels = new byte[pixelCount];
            if (fileInfo.Bpp == 32)
                NormalizeFloatChannel<float>(fileInfo.Data, pixels, cancellationToken);
            else
                NormalizeFloatChannel<double>(fileInfo.Data, pixels, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            WriteableBitmap bitmap = new(fileInfo.Cols, fileInfo.Rows, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, fileInfo.Cols, fileInfo.Rows), pixels, fileInfo.Cols, 0);
            bitmap.Freeze();
            return bitmap;
        }

        private const int FloatChannelChunkSize = 65536;

        private static void NormalizeFloatChannel<T>(byte[] data, byte[] pixels, CancellationToken cancellationToken)
            where T : unmanaged, System.Numerics.IFloatingPointIeee754<T>
        {
            int chunkCount = (pixels.Length + FloatChannelChunkSize - 1) / FloatChannelChunkSize;
            (double Minimum, double Maximum)[] ranges = new (double, double)[chunkCount];
            RunFloatChannelChunks(chunkCount, pixels.Length, cancellationToken, chunk => ranges[chunk] = FindFloatChannelRange<T>(data, chunk));
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            foreach (var local in ranges)
            {
                minimum = Math.Min(minimum, local.Minimum);
                maximum = Math.Max(maximum, local.Maximum);
            }

            if (minimum == maximum) return;
            double range = maximum - minimum;
            // Scale endpoints only when finite double extremes overflow their difference.
            // Divide by the range rather than forming an infinite factor for subnormal data.
            double magnitude = double.IsFinite(range) ? 1 : Math.Max(Math.Abs(minimum), Math.Abs(maximum));
            if (magnitude != 1)
            {
                minimum /= magnitude;
                range = maximum / magnitude - minimum;
            }
            RunFloatChannelChunks(chunkCount, pixels.Length, cancellationToken,
                chunk => WriteFloatChannelChunk<T>(data, pixels, chunk, minimum, range, magnitude));
        }

        private static (double Minimum, double Maximum) FindFloatChannelRange<T>(byte[] data, int chunk)
            where T : unmanaged, System.Numerics.IFloatingPointIeee754<T>
        {
            ReadOnlySpan<T> values = MemoryMarshal.Cast<byte, T>(data);
            int start = chunk * FloatChannelChunkSize;
            int end = Math.Min(start + FloatChannelChunkSize, values.Length);
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            for (int index = start; index < end; index++)
            {
                double value = double.CreateChecked(values[index]);
                if (!double.IsFinite(value)) throw new InvalidDataException($"灰度通道第 {index + 1} 个像素包含非有限数值。");
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }
            return (minimum, maximum);
        }

        private static void WriteFloatChannelChunk<T>(byte[] data, byte[] pixels, int chunk, double minimum, double range, double magnitude)
            where T : unmanaged, System.Numerics.IFloatingPointIeee754<T>
        {
            ReadOnlySpan<T> values = MemoryMarshal.Cast<byte, T>(data);
            int start = chunk * FloatChannelChunkSize;
            int end = Math.Min(start + FloatChannelChunkSize, values.Length);
            for (int index = start; index < end; index++)
            {
                double value = double.CreateChecked(values[index]);
                double normalized = (value / magnitude - minimum) / range;
                pixels[index] = (byte)Math.Round(Math.Clamp(normalized, 0, 1) * 255);
            }
        }

        private static void RunFloatChannelChunks(int chunkCount, int pixelCount, CancellationToken cancellationToken, Action<int> action)
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
                Parallel.For(0, chunkCount, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2)
                }, action);
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.Flatten().InnerExceptions[0]).Throw();
                throw;
            }
        }

        public static bool MatUpdateWriteableBitmap(this Mat srcMat, WriteableBitmap writeableBitmap)
        {
            if (writeableBitmap.PixelWidth != srcMat.Cols || writeableBitmap.PixelHeight != srcMat.Rows)
                return false;

            // 相同的每像素字节数不代表格式兼容，例如 CV_32FC1 和 BGRA32 都是 4 字节。
            // 这里只复用 OpenCvSharp 转换器会为该 MatType 创建的精确 WPF 格式。
            MatType type = srcMat.Type();
            PixelFormat expectedFormat;
            if (type == MatType.CV_8UC1)
                expectedFormat = PixelFormats.Gray8;
            else if (type == MatType.CV_16UC1)
                expectedFormat = PixelFormats.Gray16;
            else if (type == MatType.CV_32FC1)
                expectedFormat = PixelFormats.Gray32Float;
            else if (type == MatType.CV_8UC3)
                expectedFormat = PixelFormats.Bgr24;
            else if (type == MatType.CV_8UC4)
                expectedFormat = PixelFormats.Bgra32;
            else if (type == MatType.CV_16UC3 || type == MatType.CV_16SC3)
                expectedFormat = PixelFormats.Rgb48;
            else if (type == MatType.CV_16UC4 || type == MatType.CV_16SC4)
                expectedFormat = PixelFormats.Rgba64;
            else
                return false;

            if (writeableBitmap.Format != expectedFormat)
                return false;

            writeableBitmap.Lock();
            try
            {
                // 使用 srcMat.Type() 确保 dstMat 的元数据与源完全一致
                using var dstMat = Mat.FromPixelData(srcMat.Rows, srcMat.Cols, srcMat.Type(), writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);
                if (type == MatType.CV_16UC3 || type == MatType.CV_16SC3) // PixelFormats.Rgb48
                {
                    Cv2.CvtColor(srcMat, dstMat, ColorConversionCodes.BGR2RGB);
                }
                else if (type == MatType.CV_16UC4 || type == MatType.CV_16SC4) // PixelFormats.Rgba64
                {
                    Cv2.CvtColor(srcMat, dstMat, ColorConversionCodes.BGRA2RGBA);
                }
                else
                {
                    srcMat.CopyTo(dstMat);
                }
                // 标记脏区域
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, srcMat.Cols, srcMat.Rows));

            }
            catch (Exception ex)
            {
                log.Error("Failed to update WriteableBitmap from Mat.",ex);
                // 可以在这里记录日志
                return false;
            }
            finally
            {
                // 无论是否发生异常，必须解锁，否则 UI 暴毙
                writeableBitmap.Unlock();
            }
            return true;
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
                    writeableBitmap = src.ToWriteableBitmap();
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
