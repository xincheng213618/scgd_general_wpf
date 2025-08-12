using ColorVision.ImageEditor;
using log4net;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    public class VideoReader : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VideoReader));
        private bool openVideo;
        private MemoryMappedFile memoryMappedFile;
        private MemoryMappedViewStream memoryMappedViewStream;
        private BinaryReader binaryReader;
        private ImageView? Image { get; set; }

        private byte[]? lastFrameData; // 上一帧池化数据
        private int lastFrameLen;      // 上一帧有效长度

        public int Startup(string mapNamePrefix, ImageView image)
        {
            first = true;
            Image = image;
            try
            {
                memoryMappedFile = MemoryMappedFile.OpenExisting(mapNamePrefix);
                memoryMappedViewStream = memoryMappedFile.CreateViewStream();
                binaryReader = new BinaryReader(memoryMappedViewStream);
            }
            catch (Exception ex)
            {
                log.Error("Startup error: " + ex);
                return -1;
            }
            openVideo = true;
            Task.Run(StartupAsync);
            return 0;
        }

        public void Close()
        {
            openVideo = false;

            if (lastFrameData != null)
            {
                ArrayPool<byte>.Shared.Return(lastFrameData);
                lastFrameData = null;
                lastFrameLen = 0;
            }
            Image = null;
            binaryReader?.Dispose();
            memoryMappedViewStream?.Dispose();
            memoryMappedFile?.Dispose();
        }

        private int frameCount;
        private readonly Stopwatch fpsTimer = new Stopwatch();
        private double lastFps;
        private bool first = true;

        private async Task StartupAsync()
        {
            fpsTimer.Start();
            while (openVideo)
            {
                byte[] buffer = null;
                int width = 0, height = 0, bpp = 0, channels = 0, len = 0;
                try
                {
                    memoryMappedViewStream.Position = 0L;
                    width = binaryReader.ReadInt32();
                    height = binaryReader.ReadInt32();
                    bpp = binaryReader.ReadInt32();
                    channels = binaryReader.ReadInt32();
                    len = binaryReader.ReadInt32();

                    if (len <= 0)
                    {
                        await Task.Delay(10);
                        continue;
                    }

                    buffer = ArrayPool<byte>.Shared.Rent(len);
                    int read = binaryReader.Read(buffer, 0, len);
                    if (read < len)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        await Task.Delay(10);
                        continue;
                    }

                    bool isFrameChanged = lastFrameData == null ||
                        lastFrameLen != len ||
                        !buffer.AsSpan(0, len).SequenceEqual(lastFrameData.AsSpan(0, lastFrameLen));

                    if (!isFrameChanged)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        await Task.Delay(1);
                        continue;
                    }

                    // 渲染逻辑调度到UI线程
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (Image == null)
                        { 
                            // 用完上一帧归还
                            if (lastFrameData != null)
                                ArrayPool<byte>.Shared.Return(lastFrameData);
                            return;
                        }
                        WriteableBitmap writeableBitmap = Image.ImageShow.Source as WriteableBitmap;
                        bool needNewBitmap = writeableBitmap == null
                            || writeableBitmap.PixelWidth != width
                            || writeableBitmap.PixelHeight != height
                            || GetPixelFormat(channels, bpp) != writeableBitmap.Format;

                        if (needNewBitmap)
                        {
                            writeableBitmap = new WriteableBitmap(
                                width,
                                height,
                                96, 96,
                                GetPixelFormat(channels, bpp),
                                null);
                            Image.ImageShow.Source = writeableBitmap;
                        }

                        writeableBitmap!.Lock();
                        writeableBitmap.WritePixels(
                            new Int32Rect(0, 0, width, height),
                            buffer,
                            width * channels * (bpp / 8),
                            0);
                        writeableBitmap.Unlock();

                        Interlocked.Increment(ref frameCount);

                        // 帧率统计
                        if (fpsTimer.ElapsedMilliseconds >= 1000)
                        {
                            lastFps = (double)frameCount * 1000 / fpsTimer.ElapsedMilliseconds;
                            log.Info($"Current FPS: {lastFps:F2}");
                            Interlocked.Exchange(ref frameCount, 0);
                            fpsTimer.Restart();
                        }

                        if (first)
                        {
                            first = false;
                            Image.ImageViewModel.ZoomboxSub.ZoomUniform();
                        }
                        // 用完上一帧归还
                        if (lastFrameData != null)
                            ArrayPool<byte>.Shared.Return(lastFrameData);
                        lastFrameData = buffer;
                    }));
                    lastFrameLen = len;
                    await Task.Delay(20);
                }
                catch (Exception ex)
                {
                    log.Error("StartupAsync error: " + ex);
                }
            }
            fpsTimer.Stop();
        }

        private static System.Windows.Media.PixelFormat GetPixelFormat(int channels, int bpp)
        {
            if (channels == 3)
            {
                return bpp == 16
                    ? System.Windows.Media.PixelFormats.Rgb48
                    : System.Windows.Media.PixelFormats.Bgr24;
            }
            else
            {
                return bpp == 16
                    ? System.Windows.Media.PixelFormats.Gray16
                    : System.Windows.Media.PixelFormats.Gray8;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Close();
        }
    }
}