using System;
using System.Windows;
using System.Windows.Media.Imaging;
using CVImageChannelLib;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{

    public delegate void CameraVideoFrameHandler(WriteableBitmap bitmap);

    public class CameraVideoControl: IDisposable
    {
        private VideoReader reader;

        public event CameraVideoFrameHandler CameraVideoFrameReceived;

        public int Open(string Host, int Port)
        {
            int ret = 1;
            reader = new VideoReader();
            reader.OnFrameRecv += Reader_OnFrameRecv;
            ret = reader.Open(Host, Port);
            return ret;
        }

        private void Reader_OnFrameRecv(WriteableBitmap bmp)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                CameraVideoFrameReceived?.Invoke(bmp);
            });
        }
        public void Start(bool isLocal, string mapName, uint width, uint height)
        {
            if (reader != null) reader.Startup(mapName, isLocal);
        }

        public void Close()
        {
            reader.OnFrameRecv -= Reader_OnFrameRecv;
            reader?.Close();
            reader?.Dispose();  
        }

        public void Dispose()
        {
            reader?.Close();
            reader?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
