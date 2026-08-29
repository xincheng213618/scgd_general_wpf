#pragma warning disable CA1863
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Core
{
    public static class HImageExtension
    {
        private static bool TryGetRowCopyLayout(HImage hImage, out int bytesPerRow, out int sourceStride)
        {
            bytesPerRow = 0;
            sourceStride = 0;

            if (hImage.pData == IntPtr.Zero ||
                hImage.rows <= 0 ||
                hImage.cols <= 0 ||
                hImage.channels <= 0 ||
                hImage.depth <= 0 ||
                hImage.depth % 8 != 0)
            {
                return false;
            }

            long rowBytes = (long)hImage.cols * hImage.channels * (hImage.depth / 8);
            if (rowBytes <= 0 || rowBytes > int.MaxValue) {
                return false;
            }

            bytesPerRow = (int)rowBytes;
            sourceStride = hImage.stride > 0 ? hImage.stride : bytesPerRow;
            return sourceStride >= bytesPerRow;
        }

        private static unsafe void CopyImageBuffer(
            IntPtr source,
            int sourceStride,
            IntPtr destination,
            int destinationStride,
            int rows,
            int bytesPerRow)
        {
            byte* src = (byte*)source;
            byte* dst = (byte*)destination;

            if (sourceStride == bytesPerRow && destinationStride == bytesPerRow)
            {
                long length = checked((long)rows * bytesPerRow);
                Buffer.MemoryCopy(src, dst, length, length);
                return;
            }

            for (int y = 0; y < rows; y++)
            {
                Buffer.MemoryCopy(src, dst, bytesPerRow, bytesPerRow);
                src += sourceStride;
                dst += destinationStride;
            }
        }

        public static WriteableBitmap ToWriteableBitmapAndDispose(this HImage hImage, double DpiX = 96, double DpiY = 96)
        {
            try
            {
                return hImage.ToWriteableBitmap(DpiX, DpiY);
            }
            finally
            {
                hImage.Dispose();
            }
        }

        public static PixelFormat ToPixelFormat(this HImage hImage)
        {
            PixelFormat format = hImage.channels switch
            {
                1 => hImage.depth switch
                {
                    8 => PixelFormats.Gray8,
                    16 => PixelFormats.Gray16,
                    _ => PixelFormats.Gray8,
                },
                3 => hImage.depth switch
                {
                    8 => PixelFormats.Bgr24,
                    16 => PixelFormats.Rgb48,
                    _ => PixelFormats.Bgr24,
                },
                4 => PixelFormats.Bgr32,
                _ => PixelFormats.Default,
            };
            return format;
        }

        public static bool UpdateWriteableBitmap(ImageSource imageSource, HImage hImage)
        {
            if (imageSource is not WriteableBitmap writeableBitmap) return false;

            // Validate format, channel, and depth consistency
            if (!FormatInfoMap.TryGetValue(writeableBitmap.Format, out var formatInfo) ||
                hImage.channels != formatInfo.channels ||
                hImage.depth != formatInfo.depth)
            {
                return false;
            }

            // Check if dimensions match
            if (writeableBitmap.PixelHeight != hImage.rows || writeableBitmap.PixelWidth != hImage.cols)
                return false;

            if (!TryGetRowCopyLayout(hImage, out int bytesPerRow, out int sourceStride) ||
                writeableBitmap.BackBufferStride < bytesPerRow)
            {
                return false;
            }

            // Update the WriteableBitmap
            writeableBitmap.Lock();
            try
            {
                CopyImageBuffer(
                    hImage.pData,
                    sourceStride,
                    writeableBitmap.BackBuffer,
                    writeableBitmap.BackBufferStride,
                    hImage.rows,
                    bytesPerRow);

                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, hImage.cols, hImage.rows));
            }
            finally
            {
                writeableBitmap.Unlock();
            }
            hImage.Dispose();
            return true;
        }

        private static readonly Dictionary<PixelFormat, (int channels, int depth)> FormatInfoMap = new()
        {
            { PixelFormats.Gray8, (1, 8) },
            { PixelFormats.Gray16, (1, 16) },
            { PixelFormats.Bgr24, (3, 8) }, // Halcon usually 3 channels
            { PixelFormats.Rgb24, (3, 8) },
            { PixelFormats.Bgr32, (4, 8) },
            { PixelFormats.Bgra32, (4, 8) },
            { PixelFormats.Rgb48, (3, 16) }
        };

        /// <summary>
        /// Async update to keep UI responsive during copy
        /// </summary>
        public static async Task<bool> UpdateWriteableBitmapAsync(ImageSource imageSource, HImage hImage)
        {
            if (imageSource is not WriteableBitmap writeableBitmap) return false;

            bool isValid = false;
            IntPtr backBuffer = IntPtr.Zero;
            int backBufferStride = 0;

            // 提前提取 HImage 的数据，避免后续跨线程问题
            int rows = hImage.rows;
            int cols = hImage.cols;
            int channels = hImage.channels;
            int depth = hImage.depth;
            IntPtr srcData = hImage.pData;
            if (!TryGetRowCopyLayout(hImage, out int bytesPerRow, out int srcStride))
            {
                return false;
            }

            // 1. Validation & Lock (必须在 UI 线程)
            // 无论此方法从哪个线程被调用，都能保证安全跑到主线程验证和上锁
            writeableBitmap.Dispatcher.Invoke(() =>
            {
                if (FormatInfoMap.TryGetValue(writeableBitmap.Format, out var formatInfo) &&
                    channels == formatInfo.channels &&
                    depth == formatInfo.depth &&
                    writeableBitmap.PixelHeight == rows &&
                    writeableBitmap.PixelWidth == cols)
                {
                    writeableBitmap.Lock();
                    // 提取指针和步长供后台线程使用
                    backBuffer = writeableBitmap.BackBuffer;
                    backBufferStride = writeableBitmap.BackBufferStride;
                    if (backBufferStride >= bytesPerRow)
                    {
                        isValid = true;
                    }
                    else
                    {
                        writeableBitmap.Unlock();
                    }
                }
            });

            if (!isValid) return false;

            try
            {
                // 2. Heavy Lifting (在后台线程执行内存拷贝，释放 UI 线程)
                await Task.Run(() => CopyImageBuffer(
                    srcData,
                    srcStride,
                    backBuffer,
                    backBufferStride,
                    rows,
                    bytesPerRow));
            }
            finally
            {
                // 3. Mark Dirty and Unlock (必须回到 UI 线程执行)
                writeableBitmap.Dispatcher.Invoke(() =>
                {
                    writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, cols, rows));
                    writeableBitmap.Unlock();
                });

                // 释放 HImage 资源
                hImage.Dispose();
            }

            return true;
        }

        public static async Task<WriteableBitmap> ToWriteableBitmapAsync(this HImage hImage, double DpiX = 96, double DpiY = 96)
        {
            // 1. UI Thread: Create the container
            PixelFormat format = hImage.ToPixelFormat();
            int width = hImage.cols;
            int height = hImage.rows;
            if (!TryGetRowCopyLayout(hImage, out int bytesPerLine, out int strideSrc))
            {
                throw new ArgumentException("Invalid HImage layout.", nameof(hImage));
            }

            // Create the bitmap on the calling thread (usually UI thread)
            var writeableBitmap = new WriteableBitmap(width, height, DpiX, DpiY, format, null);

            // Calculate parameters needed for the copy
            int strideDest = writeableBitmap.BackBufferStride;
            if (strideDest < bytesPerLine)
            {
                throw new ArgumentException("Invalid destination bitmap stride.", nameof(hImage));
            }

            // 2. Background Thread: Perform the heavy memory copy
            writeableBitmap.Lock();
            try
            {
                IntPtr destination = writeableBitmap.BackBuffer;
                await Task.Run(() => CopyImageBuffer(
                    hImage.pData,
                    strideSrc,
                    destination,
                    strideDest,
                    height,
                    bytesPerLine));

                // 3. UI Thread: Mark as dirty and return
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                writeableBitmap.Unlock();
            }

            return writeableBitmap;
        }

        public static WriteableBitmap ToWriteableBitmap(this HImage hImage,double DpiX = 96, double DpiY =96)
        {
            PixelFormat format = hImage.ToPixelFormat();
            if (!TryGetRowCopyLayout(hImage, out int bytesPerRow, out int sourceStride))
            {
                throw new ArgumentException("Invalid HImage layout.", nameof(hImage));
            }

            WriteableBitmap writeableBitmap = new WriteableBitmap(hImage.cols, hImage.rows, DpiX, DpiY, format, null);
            if (writeableBitmap.BackBufferStride < bytesPerRow)
            {
                throw new ArgumentException("Invalid destination bitmap stride.", nameof(hImage));
            }

            writeableBitmap.Lock();
            try
            {
                CopyImageBuffer(
                    hImage.pData,
                    sourceStride,
                    writeableBitmap.BackBuffer,
                    writeableBitmap.BackBufferStride,
                    hImage.rows,
                    bytesPerRow);
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            }
            finally
            {
                writeableBitmap.Unlock();
            }

            return writeableBitmap;
        }


        public static HImage ToHImage(this BitmapImage bitmapImage) => bitmapImage.ToWriteableBitmap().ToHImage();

        public static WriteableBitmap ToWriteableBitmap(this BitmapImage bitmapImage) => new(bitmapImage);


        public static HImage ForHImage(this WriteableBitmap writeableBitmap)
        {
            // Determine the number of channels and Depth based on the pixel format
            int channels, depth;

            switch (writeableBitmap.Format.ToString())
            {
                case "Bgr32":
                case "Bgra32":
                case "Pbgra32":
                    channels = 4; // BGRA format has 4 channels
                    depth = 8; // 8 bits per channel
                    break;
                case "Bgr24":
                case "Rgb24":
                    channels = 3; // RGB format has 3 channels
                    depth = 8; // 8 bits per channel
                    break;
                case "Indexed8":
                    depth = 8; // 8 bits per channel
                    channels = 1;
                    break;
                case "Rgb48":
                    channels = 3; // RGB format has 3 channels
                    depth = 16; // 8 bits per channel
                    break;
                case "Gray8":
                    channels = 1; // Gray scale has 1 channel
                    depth = 8; // 8 bits per channel
                    break;
                case "Gray16":
                    channels = 1; // Gray scale has 1 channel
                    depth = 16; // 16 bits per channel
                    break;
                case "Gray32Float":
                    channels = 1; // Gray scale has 1 channel
                    depth = 32; // 16 bits per channel
                    break;
                default:
                    MessageBox.Show(string.Format(Properties.Resources.Core_UnsupportedFormat, writeableBitmap.Format));
                    throw new NotSupportedException("The pixel format is not supported.");
            }

            // Create a borrowed HImage descriptor for the locked bitmap buffer.
            HImage hImage = new()
            {
                rows = writeableBitmap.PixelHeight,
                cols = writeableBitmap.PixelWidth,
                channels = channels,
                depth = depth, // You might need to adjust this based on the actual bits per pixel
                pData = writeableBitmap.BackBuffer,
                stride = writeableBitmap.BackBufferStride,
                isDispose = true
            };
            return hImage;
        }


        public static HImage ToHImage(this WriteableBitmap writeableBitmap)
        {
            // Determine the number of channels and Depth based on the pixel format
            int channels, depth;
            int bytesPerPixel;

            switch (writeableBitmap.Format.ToString())
            {
                case "Bgr32":
                case "Bgra32":
                case "Pbgra32":
                    channels = 4; // BGRA format has 4 channels
                    depth = 8; // 8 bits per channel
                    break;
                case "Bgr24":
                case "Rgb24":
                    channels = 3; // RGB format has 3 channels
                    depth = 8; // 8 bits per channel
                    break;
                case "Indexed8":
                    depth = 8; // 8 bits per channel
                    channels = 1;
                    break;
                case "Rgb48":
                    channels = 3; // RGB format has 3 channels
                    depth = 16; // 8 bits per channel
                    break;
                case "Gray8":
                    channels = 1; // Gray scale has 1 channel
                    depth = 8; // 8 bits per channel
                    break;
                case "Gray16":
                    channels = 1; // Gray scale has 1 channel
                    depth = 16; // 16 bits per channel
                    break;
                case "Gray32Float":
                    channels = 1; // Gray scale has 1 channel
                    depth = 32; // 16 bits per channel
                    break;
                default:
                    MessageBox.Show(string.Format(Properties.Resources.Core_UnsupportedFormat, writeableBitmap.Format));
                    throw new NotSupportedException("The pixel format is not supported.");
            }

            bytesPerPixel = channels * (depth / 8);
            int stride = checked(writeableBitmap.PixelWidth * bytesPerPixel);
            int bufferSize = checked(writeableBitmap.PixelHeight * stride);

            // Create an owned HImage buffer.
            HImage hImage = new()
            {
                rows = writeableBitmap.PixelHeight,
                cols = writeableBitmap.PixelWidth,
                channels = channels,
                depth = depth, // You might need to adjust this based on the actual bits per pixel
                stride = stride,
                pData = Marshal.AllocCoTaskMem(bufferSize)
            };

            try
            {
                if (writeableBitmap.IsFrozen)
                {
                    writeableBitmap.CopyPixels(Int32Rect.Empty, hImage.pData, bufferSize, stride);
                    return hImage;
                }

                // Copy the pixel data from the WriteableBitmap to the owned HImage buffer.
                writeableBitmap.Lock();
                try
                {
                    CopyImageBuffer(
                        writeableBitmap.BackBuffer,
                        writeableBitmap.BackBufferStride,
                        hImage.pData,
                        stride,
                        hImage.rows,
                        stride);
                }
                finally
                {
                    writeableBitmap.Unlock();
                }
            }
            catch
            {
                Marshal.FreeCoTaskMem(hImage.pData);
                throw;
            }

            return hImage;
        }
    }
}
