using System;
using System.Threading.Tasks;
using System.Windows;
using CVImageChannelLib;
using log4net;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{

    public delegate void CameraVideoFrameHandler(System.Drawing.Bitmap bitmap);

    public class CameraVideoControl : System.IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CameraVideoControl));
        private VideoReader reader;
        private int width;
        private int height;
        public bool IsEnableResize { get; set; }
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
                    if(IsResize(bmp.Width, bmp.Height)) {
                        System.Drawing.Bitmap bmpNew = ReSize(bmp);
                        CameraVideoFrameReceived?.Invoke(bmpNew);
                        bmpNew.Dispose();
                        bmp.Dispose();
                    }
                    else
                    {
                        CameraVideoFrameReceived?.Invoke(bmp);
                        bmp.Dispose();
                    }
                });
            }
        }
        private bool IsResize(int width,int height)
        {
            if(IsEnableResize) return (this.width != width || this.height != height);
            else return false;
        }
        private System.Drawing.Bitmap ReSize(System.Drawing.Bitmap bmp)
        {
            var data = bmp.LockBits(new System.Drawing.Rectangle(new System.Drawing.Point(0, 0), bmp.Size), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
            OpenCvSharp.Mat src = Mat.FromPixelData(bmp.Height, bmp.Width, MatType.CV_8UC3, data.Scan0);
            bmp.UnlockBits(data);
            OpenCvSharp.Mat dst = new();
            Cv2.Resize(src, dst, new OpenCvSharp.Size(width, height));
            return dst.ToBitmap();
        }
        public void Start(bool isLocal, string mapName, uint width, uint height)
        {
            this.width = (int)width;
            this.height = (int)height;
            IsEnableResize = true;
            //this.rows = 5544;
            //this.cols = 3684;
            if (reader != null) reader.Startup(mapName, isLocal);
        }

        public void Close()
        {
            reader?.Close();
            Task.Run(() =>
            {
                System.Threading.Thread.Sleep(500);
                reader?.Dispose();
            });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
