using ColorVision.Core;
using ColorVision.FileIO;
using ColorVision.Themes.Controls;
using ColorVision.UI.Desktop;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
                var type = srcMat.Type();
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
