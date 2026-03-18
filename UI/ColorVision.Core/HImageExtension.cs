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
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void RtlMoveMemory(IntPtr Destination, IntPtr Source, uint Length);

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
            var formatInfoMap = new Dictionary<PixelFormat, (int channels, int depth)>
            {
                { PixelFormats.Gray8, (1, 8) },
                { PixelFormats.Gray16, (1, 16) },
                { PixelFormats.Bgr24, (3, 8) },
                { PixelFormats.Rgb24, (3, 8) },
                { PixelFormats.Bgr32, (3, 8) },
                { PixelFormats.Bgra32, (4, 8) },
                { PixelFormats.Rgb48, (3, 16) }
            };

            if (!formatInfoMap.TryGetValue(writeableBitmap.Format, out var formatInfo) ||
                hImage.channels != formatInfo.channels ||
                hImage.depth != formatInfo.depth)
            {
                return false;
            }

            // Check if dimensions match
            if (writeableBitmap.PixelHeight != hImage.rows || writeableBitmap.PixelWidth != hImage.cols)
                return false;

            // Update the WriteableBitmap
            writeableBitmap.Lock();
            try
            {
                unsafe
                {
                    byte* src = (byte*)hImage.pData;
                    byte* dst = (byte*)writeableBitmap.BackBuffer;

                    for (int y = 0; y < hImage.rows; y++)
                    {
                        RtlMoveMemory(new IntPtr(dst), new IntPtr(src), (uint)(hImage.cols * hImage.channels * (hImage.depth / 8)));
                        src += hImage.stride;
                        dst += writeableBitmap.BackBufferStride;
                    }
                }

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
        { PixelFormats.Bgr32, (3, 8) }, // 32-bit usually padded 3 channels
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
            int bytesPerRow = cols * channels * (depth / 8);
            int srcStride = hImage.stride;
            IntPtr srcData = hImage.pData;

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
                    isValid = true;
                    writeableBitmap.Lock();
                    // 提取指针和步长供后台线程使用
                    backBuffer = writeableBitmap.BackBuffer;
                    backBufferStride = writeableBitmap.BackBufferStride;
                }
            });

            if (!isValid) return false;

            try
            {
                // 2. Heavy Lifting (在后台线程执行内存拷贝，释放 UI 线程)
                await Task.Run(() =>
                {
                    unsafe
                    {
                        bool useParallel = (rows * bytesPerRow) > (1024 * 1024);
                        if (useParallel)
                        {
                            byte* pSrcBase = (byte*)srcData;
                            byte* pDstBase = (byte*)backBuffer;
                            var parallelOptions = new ParallelOptions
                            {
                                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
                            };
                            Parallel.For(0, rows, parallelOptions, y =>
                            {
                                byte* src = pSrcBase + (y * srcStride);
                                byte* dst = pDstBase + (y * backBufferStride);
                                Buffer.MemoryCopy(src, dst, bytesPerRow, bytesPerRow);
                            });
                        }
                        else
                        {
                            // 【修复点】：使用提取好的指针变量，千万不要在这里访问 writeableBitmap
                            byte* src = (byte*)srcData;
                            byte* dst = (byte*)backBuffer;

                            for (int y = 0; y < rows; y++)
                            {
                                RtlMoveMemory(new IntPtr(dst), new IntPtr(src), (uint)bytesPerRow);
                                src += srcStride;
                                dst += backBufferStride; // 使用提取的 backBufferStride
                            }
                        }
                    }
                });
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

            // Create the bitmap on the calling thread (usually UI thread)
            var writeableBitmap = new WriteableBitmap(width, height, DpiX, DpiY, format, null);

            // Calculate parameters needed for the copy
            int strideSrc = hImage.stride;
            int strideDest = writeableBitmap.BackBufferStride;
            long pDataSrc = (long)hImage.pData; // Store pointer as long to safely pass to task
            long pDataDest = (long)writeableBitmap.BackBuffer;
            int bytesPerLine = width * hImage.channels * (hImage.depth / 8);

            // 2. Background Thread: Perform the heavy memory copy
            await Task.Run(() =>
            {

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
                };

                unsafe
                {
                    byte* pSrcBase = (byte*)pDataSrc;
                    byte* pDstBase = (byte*)pDataDest;

                    Parallel.For(0, height, parallelOptions, y =>
                    {
                        // 指针运算：使用 long 避免 32位 溢出（虽然行偏移通常不会溢出，但习惯要好）
                        byte* src = pSrcBase + ((long)y * strideSrc);
                        byte* dst = pDstBase + ((long)y * strideDest);

                        // Copy
                        // 参数3: destinationSizeInBytes。这里传 bytesPerLine 是因为我们只操作这一行，
                        // 只要保证 bytesPerLine <= strideDest 即可，这是安全的。
                        Buffer.MemoryCopy(src, dst, bytesPerLine, bytesPerLine);
                    });
                }
            });

            // 3. UI Thread: Mark as dirty and return
            writeableBitmap.Lock();
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
            writeableBitmap.Unlock();

            return writeableBitmap;
        }

        public static WriteableBitmap ToWriteableBitmap(this HImage hImage,double DpiX = 96, double DpiY =96)
        {
            PixelFormat format = hImage.ToPixelFormat();
            WriteableBitmap writeableBitmap = new WriteableBitmap(hImage.cols, hImage.rows, DpiX, DpiY, format, null);

            writeableBitmap.Lock();

            unsafe
            {
                byte* src = (byte*)hImage.pData;
                byte* dst = (byte*)writeableBitmap.BackBuffer;

                for (int y = 0; y < hImage.rows; y++)
                {
                    RtlMoveMemory(new IntPtr(dst), new IntPtr(src), (uint)(hImage.cols * hImage.channels * (hImage.depth / 8)));
                    src += hImage.stride;
                    dst += writeableBitmap.BackBufferStride;
                }
            }

            //RtlMoveMemory(writeableBitmap.BackBuffer, hImage.pData, (uint)(hImage.cols * hImage.rows * hImage.channels * (hImage.depth / 8)));
            //writeableBitmap.Lock();

            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            writeableBitmap.Unlock();
            return writeableBitmap;
        }


        public static HImage ToHImage(this BitmapImage bitmapImage) => bitmapImage.ToWriteableBitmap().ToHImage();

        public static WriteableBitmap ToWriteableBitmap(this BitmapImage bitmapImage) => new(bitmapImage);


        public static HImage ForHImage(this WriteableBitmap writeableBitmap)
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
                    MessageBox.Show($"{writeableBitmap.Format}暂不支持的格式,请联系开发人员");
                    throw new NotSupportedException("The pixel format is not supported.");
            }

            bytesPerPixel = channels * (depth / 8);
            int stride = writeableBitmap.PixelWidth * bytesPerPixel;

            // Create a new HImageCache instance
            HImage hImage = new()
            {
                rows = writeableBitmap.PixelHeight,
                cols = writeableBitmap.PixelWidth,
                channels = channels,
                depth = depth, // You might need to adjust this based on the actual bits per pixel
                pData = writeableBitmap.BackBuffer,
                stride = writeableBitmap.BackBufferStride
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
                    MessageBox.Show($"{writeableBitmap.Format}暂不支持的格式,请联系开发人员");
                    throw new NotSupportedException("The pixel format is not supported.");
            }

            bytesPerPixel = channels * (depth / 8);
            int stride = writeableBitmap.PixelWidth * bytesPerPixel;

            // Create a new HImageCache instance
            HImage hImage = new()
            {
                rows = writeableBitmap.PixelHeight,
                cols = writeableBitmap.PixelWidth,
                channels = channels,
                depth = depth, // You might need to adjust this based on the actual bits per pixel
                pData = Marshal.AllocCoTaskMem(writeableBitmap.PixelWidth * writeableBitmap.PixelHeight * channels* (depth/8))
            };

            // Copy the pixel data from the WriteableBitmap to the HImageCache
            writeableBitmap.Lock();
            //RtlMoveMemory(hImage.pData, writeableBitmap.BackBuffer, (uint)(hImage.cols * hImage.rows * hImage.channels*(depth / 8)));
            try
            {
                unsafe
                {
                    byte* src = (byte*)writeableBitmap.BackBuffer;
                    byte* dst = (byte*)hImage.pData;

                    for (int y = 0; y < hImage.rows; y++)
                    {
                        RtlMoveMemory(new IntPtr(dst), new IntPtr(src), (uint)stride);
                        src += writeableBitmap.BackBufferStride;
                        dst += stride;
                    }
                }
            }
            finally
            {
                writeableBitmap.Unlock();
            }
            return hImage;
        }
    }
}
