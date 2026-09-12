using ColorVision.Core;
using System;

namespace ColorVision.ImageEditor.Video
{
    internal interface IVideoPlaybackBackend
    {
        int Open(string filePath, out OpenCVMediaHelper.VideoInfo info);
        int ReadFrame(int handle, out HImage image);
        void ReleaseFrame(HImage image);
        int Play(int handle, OpenCVMediaHelper.VideoFrameCallback frameCallback, OpenCVMediaHelper.VideoStatusCallback statusCallback);
        int Pause(int handle);
        int Seek(int handle, int frameIndex);
        int SetPlaybackSpeed(int handle, double speed);
        int SetResizeScale(int handle, double scale);
        int Close(int handle);
    }

    internal sealed class NativeVideoPlaybackBackend : IVideoPlaybackBackend
    {
        public int Open(string filePath, out OpenCVMediaHelper.VideoInfo info) => OpenCVMediaHelper.M_VideoOpen(filePath, out info);
        public int ReadFrame(int handle, out HImage image) => OpenCVMediaHelper.M_VideoReadFrame(handle, out image);
        public void ReleaseFrame(HImage image) => image.Dispose();
        public int Play(int handle, OpenCVMediaHelper.VideoFrameCallback frameCallback, OpenCVMediaHelper.VideoStatusCallback statusCallback)
            => OpenCVMediaHelper.M_VideoPlay(handle, frameCallback, statusCallback, IntPtr.Zero);
        public int Pause(int handle) => OpenCVMediaHelper.M_VideoPause(handle);
        public int Seek(int handle, int frameIndex) => OpenCVMediaHelper.M_VideoSeek(handle, frameIndex);
        public int SetPlaybackSpeed(int handle, double speed) => OpenCVMediaHelper.M_VideoSetPlaybackSpeed(handle, speed);
        public int SetResizeScale(int handle, double scale) => OpenCVMediaHelper.M_VideoSetResizeScale(handle, scale);
        public int Close(int handle) => OpenCVMediaHelper.M_VideoClose(handle);
    }
}
