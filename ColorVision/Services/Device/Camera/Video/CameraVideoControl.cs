using System;
using System.Windows;
using CVImageChannelLib;
using log4net;
using OpenCvSharp.Extensions;

namespace ColorVision.Device.Camera.Video
{

    public delegate void CameraVideoFrameHandler(System.Drawing.Bitmap bitmap);

    public class CameraVideoControl : System.IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CameraVideoControl));
        private VideoReader reader;
        private int width;
        private int height;
        public event CameraVideoFrameHandler CameraVideoFrameReceived;
        public int Open(string Host, int Port)
        {
            int ret = 1;
            reader = new VideoReader();
            reader.OnFrameRecv += Reader_OnFrameRecv;
            ret = reader.Open(Host, Port);
            return ret;
        }

        private void Reader_OnFrameRecv(System.Drawing.Bitmap bmp)
        {
            if (bmp != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Drawing.Bitmap bmpNew = ReSize(bmp);
                    CameraVideoFrameReceived?.Invoke(bmpNew);
                    bmpNew.Dispose();
                });
                bmp.Dispose();
            }
        }
        private System.Drawing.Bitmap ReSize(System.Drawing.Bitmap bmp)
        {
            var data = bmp.LockBits(new System.Drawing.Rectangle(new System.Drawing.Point(0, 0), bmp.Size), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
            OpenCvSharp.Mat src = new OpenCvSharp.Mat(bmp.Height, bmp.Width, OpenCvSharp.MatType.CV_8UC3, data.Scan0);
            bmp.UnlockBits(data);
            OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Resize(src, dst, new OpenCvSharp.Size(width, height));
            return dst.ToBitmap();
        }
        public void Start(bool isLocal, string mapName, uint width, uint height)
        {
            this.width = (int)width;
            this.height = (int)height;
            //this.width = 5544;
            //this.height = 3684;
            if (reader != null) reader.Startup(mapName, isLocal);
        }

        public void Close()
        {
            reader?.Close();
            reader?.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
