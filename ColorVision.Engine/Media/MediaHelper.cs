using ColorVision.Common.Utilities;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using OpenCvSharp.WpfExtensions;
using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public static class MediaHelper
    {
        public static WriteableBitmap? ToWriteableBitmap(this CVCIEFile fileInfo)
        {
            WriteableBitmap writeableBitmap = null;
            try
            {
                if (fileInfo.FileExtType == FileExtType.Tif)
                {
                    var src = OpenCvSharp.Cv2.ImDecode(fileInfo.data, OpenCvSharp.ImreadModes.Unchanged);
                    writeableBitmap = src.ToWriteableBitmap();
                    src.Dispose();
                }
                else if (fileInfo.FileExtType == FileExtType.Raw || fileInfo.FileExtType == FileExtType.Src)
                {
                    OpenCvSharp.Mat src = OpenCvSharp.Mat.FromPixelData(fileInfo.cols, fileInfo.rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.channels), fileInfo.data);
                    OpenCvSharp.Mat dst = null;
                    if (fileInfo.bpp == 32)
                    {
                        OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                        dst = new OpenCvSharp.Mat();
                        src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                    }
                    else
                    {
                        dst = src;
                    }
                    writeableBitmap = dst.ToWriteableBitmap();
                    dst.Dispose();
                    src.Dispose();
                }
                else if (fileInfo.FileExtType == FileExtType.CIE)
                {

                    OpenCvSharp.Mat src = OpenCvSharp.Mat.FromPixelData(fileInfo.cols, fileInfo.rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.channels), fileInfo.data);
                    OpenCvSharp.Mat dst = null;
                    if (fileInfo.bpp == 32)
                    {
                        OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                        dst = new OpenCvSharp.Mat();
                        src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                    }
                    else
                    {
                        dst = src;
                    }
                   
                    writeableBitmap = dst.ToWriteableBitmap();
                    dst.Dispose();
                    src.Dispose();
                }
                return writeableBitmap;
            }
            catch (Exception ex)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), $"打开文件失败:{ex.Message} ", "ColorVision");
                return writeableBitmap;
            }
        }

    }
}
