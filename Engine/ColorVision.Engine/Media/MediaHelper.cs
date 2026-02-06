using ColorVision.Core;
using ColorVision.FileIO;
using ColorVision.Themes.Controls;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp.WpfExtensions;
namespace ColorVision.Engine.Media
{

    public static class MediaHelper
    {
        private static ILog log = LogManager.GetLogger(typeof(MediaHelper));
        public static bool UpdateWriteableBitmap(this CVCIEFile fileInfo, WriteableBitmap writeableBitmap)
        {   
            OpenCvSharp.Mat? src = null;
            OpenCvSharp.Mat? dst = null;
            try
            {
                if (fileInfo.FileExtType == CVType.Tif)
                {
                    src = OpenCvSharp.Cv2.ImDecode(fileInfo.Data, OpenCvSharp.ImreadModes.Unchanged);
                }
                else if (fileInfo.FileExtType == CVType.Raw || fileInfo.FileExtType == CVType.Src || fileInfo.FileExtType == CVType.CIE)
                {
                    src = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                    if (fileInfo.Bpp == 32)
                    {
                        OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                        dst = new OpenCvSharp.Mat();
                        src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                    }
                    else
                    {
                        dst = src;
                    }
                }

                if (src == null)
                {
                    return false;
                    throw new Exception("Unsupported file format.");
                }

                if (dst == null)
                {
                    dst = src;
                }

                if (writeableBitmap.PixelWidth != dst.Width ||
                    writeableBitmap.PixelHeight != dst.Height ||
                    writeableBitmap.Format != GetPixelFormat(dst))
                {
                    return false;
                    throw new InvalidOperationException("The existing WriteableBitmap does not match the dimensions or format of the new image.");
                }

                writeableBitmap.Lock();
                unsafe
                {
                    byte* srcPtr = (byte*)dst.Data;
                    byte* dstPtr = (byte*)writeableBitmap.BackBuffer;

                    for (int y = 0; y < dst.Rows; y++)
                    {
                        Buffer.MemoryCopy(srcPtr, dstPtr, writeableBitmap.BackBufferStride, dst.Cols * dst.ElemSize());
                        srcPtr += dst.Step();
                        dstPtr += writeableBitmap.BackBufferStride;
                    }
                }
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, dst.Cols, dst.Rows));
                writeableBitmap.Unlock();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                src?.Dispose();
                dst?.Dispose();
            }
        }


        private static PixelFormat GetPixelFormat(OpenCvSharp.Mat mat)
        {
            switch (mat.Channels())
            {
                case 1:
                    return PixelFormats.Gray8;
                case 3:
                    return PixelFormats.Bgr24;
                case 4:
                    return PixelFormats.Bgra32;
                default:
                    throw new NotSupportedException("Unsupported image format.");
            }
        }

        public static Mat ToMat(this CVCIEFile fileInfo)
        {
            OpenCvSharp.Mat? src = null;
            try
            {
                if (fileInfo.FileExtType == CVType.Tif)
                {
                    src = OpenCvSharp.Cv2.ImDecode(fileInfo.Data, OpenCvSharp.ImreadModes.Unchanged);
                }
                else if (fileInfo.FileExtType == CVType.Raw || fileInfo.FileExtType == CVType.Src || fileInfo.FileExtType == CVType.CIE)
                {
                    if (fileInfo.FileExtType == CVType.CIE)
                    {
                        int len = (int)(fileInfo.Rows * fileInfo.Cols * (fileInfo.Bpp / 8));
                        if (fileInfo.Channels == 3)
                        {
                            byte[] data = new byte[len];
                            Buffer.BlockCopy(fileInfo.Data, len, data, 0, data.Length);
                            src = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, 1), fileInfo.Data);
                        }
                        else
                        {
                            src = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                        }
                    }
                    else
                    {
                        src = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                    }
                    if (fileInfo.Bpp == 32)
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
                log.Error(ex);
                MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.OpenFileFailed + $":{ex.Message} ", "ColorVision");
                return null;
            }
            finally
            {
            }
        
        }

        public static bool MatUpdateWriteableBitmap(this Mat srcMat, WriteableBitmap writeableBitmap)
        {
            // 1. 基础尺寸校验
            if (writeableBitmap.PixelWidth != srcMat.Cols || writeableBitmap.PixelHeight != srcMat.Rows)
                return false;

            // 2. 严格的格式校验 (同时检查通道数和位深)
            // ElemSize() 返回一个像素的总字节数 (例如: 8位3通道=3, 16位1通道=2)
            int srcPixelBytes = (int)srcMat.ElemSize();
            int dstPixelBytes = writeableBitmap.Format.BitsPerPixel / 8;

            if (srcPixelBytes != dstPixelBytes)
                return false; // 字节对齐不一致，直接拷贝会导致错位

            // 可选：如果你想保留严格的格式映射，可以保留你的 switch，
            // 但建议加上对 Depth 的判断，或者直接信赖上面的字节数判断（通用性更强）。
            // 例如：即便是 BGR 转 RGB，字节数一样，拷贝过去只是颜色反了，不会崩；但字节数不对必定崩。

            // 3. 安全的内存操作
            writeableBitmap.Lock();
            try
            {
                // 使用 srcMat.Type() 确保 dstMat 的元数据与源完全一致
                using var dstMat = Mat.FromPixelData(srcMat.Rows, srcMat.Cols, srcMat.Type(), writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride);

                srcMat.CopyTo(dstMat);
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



        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void RtlMoveMemory(IntPtr dest, IntPtr src, uint count);
        public static bool UpdateWriteableBitmap(this Mat mat,ImageSource imageSource)
        {
            if (imageSource is not WriteableBitmap writeableBitmap) return false;

            // 1. 验证尺寸
            if (writeableBitmap.PixelWidth != mat.Cols || writeableBitmap.PixelHeight != mat.Rows)
                return false;

            // 2. 验证格式兼容性
            // 获取 Mat 的通道数和位深
            int matChannels = mat.Channels();
            int matDepth = (int)mat.ElemSize1() * 8; // ElemSize1 返回每个通道的字节数 (1=8bit, 2=16bit)

            // 定义格式映射用于校验 (根据你的现有逻辑扩展)
            var formatInfoMap = new Dictionary<PixelFormat, (int channels, int depth)>
    {
        { PixelFormats.Gray8,   (1, 8) },
        { PixelFormats.Gray16,  (1, 16) },
        { PixelFormats.Bgr24,   (3, 8) },
        { PixelFormats.Rgb24,   (3, 8) },
        { PixelFormats.Bgr32,   (4, 8) }, // 注意：Bgr32 通常是 32bpp (4字节)，需确保 Mat 也是 4 通道 (BGRA) 才能直接拷贝
        { PixelFormats.Bgra32,  (4, 8) },
        { PixelFormats.Rgb48,   (3, 16) }
    };

            if (!formatInfoMap.TryGetValue(writeableBitmap.Format, out var formatInfo))
                return false;

            // 检查通道数和深度是否匹配
            // 特殊情况：如果 WriteableBitmap 是 Bgr32/Bgra32 (4字节)，Mat 必须是 4 通道才能直接内存拷贝
            // 如果 Mat 是 3 通道但 WB 是 4 通道，直接拷贝会导致错位，需要先 Cv2.CvtColor 转换
            if (matChannels != formatInfo.channels || matDepth != formatInfo.depth)
            {
                // 简单的不兼容返回 false，实际项目中你可能需要在这里做一个临时的 cvtColor
                return false;
            }

            // 3. 更新 WriteableBitmap
            writeableBitmap.Lock();
            try
            {
                unsafe
                {
                    byte* src = (byte*)mat.Data;
                    byte* dst = (byte*)writeableBitmap.BackBuffer;

                    // 获取步长 (Stride)
                    long srcStep = mat.Step();
                    int dstStep = writeableBitmap.BackBufferStride;

                    // 计算每行需要拷贝的字节数 (使用宽度 * 像素字节大小)
                    uint rowBytes = (uint)(mat.Cols * matChannels * (matDepth / 8));

                    // 行对行拷贝，处理 Stride 不一致的情况
                    for (int y = 0; y < mat.Rows; y++)
                    {
                        RtlMoveMemory(new IntPtr(dst), new IntPtr(src), rowBytes);
                        src += srcStep;
                        dst += dstStep;
                    }
                }

                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, mat.Cols, mat.Rows));
            }
            finally
            {
                writeableBitmap.Unlock();
            }

            return true;
        }

        public static WriteableBitmap? ToWriteableBitmap(this CVCIEFile fileInfo)
        {
            OpenCvSharp.Mat? src = null;
            try
            {
                if (fileInfo.FileExtType == CVType.Tif)
                {
                    src = OpenCvSharp.Cv2.ImDecode(fileInfo.Data, OpenCvSharp.ImreadModes.Unchanged);
                }
                else if (fileInfo.FileExtType == CVType.Raw || fileInfo.FileExtType == CVType.Src || fileInfo.FileExtType == CVType.CIE)
                {
                    if (fileInfo.FileExtType == CVType.CIE)
                    {
                        int len = (int)(fileInfo.Rows * fileInfo.Cols * (fileInfo.Bpp / 8));
                        if (fileInfo.Channels == 3)
                        {
                            byte[] data = new byte[len];
                            Buffer.BlockCopy(fileInfo.Data, len, data, 0, data.Length);
                            src = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, 1), fileInfo.Data);
                        }
                        else
                        {
                            src = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                        }
                    }
                    else
                    {
                        src = OpenCvSharp.Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                    }
                    if (fileInfo.Bpp == 32)
                    {
                        OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                        src.ConvertTo(src, OpenCvSharp.MatType.CV_8U);
                    }
                }

                if (src == null)
                {
                    throw new Exception("Unsupported file format.");
                }
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
