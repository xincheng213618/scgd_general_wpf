using log4net;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    public class CVImagePacket
    {
        public int width { get; set; }
        public int height { get; set; }
        public int bpp { get; set; }
        public int channels { get; set; }
        public int len { get; set; }
        public byte[] data { get; set; }
        public void Deserialize(BinaryReader reader)
        {
            width = reader.ReadInt32();
            height = reader.ReadInt32();
            bpp = reader.ReadInt32();
            channels = reader.ReadInt32();
            len = reader.ReadInt32();
            data = reader.ReadBytes(len);
        }
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(width);
            writer.Write(height);
            writer.Write(bpp);
            writer.Write(channels);
            writer.Write(len);
            writer.Write(data);
            writer.Flush();
        }
    }

    public class VideoReader : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VideoReader));
        private bool OpenVideo;
        MemoryMappedFile memoryMappedFile;
        protected MemoryMappedViewStream memoryMappedViewStream;
        BinaryReader binaryReader;
        Image? Image { get; set; }

        public VideoReader()
        {
            OpenVideo = false;
        }
        byte[]? lastFrameData = null; // 保存上一帧数据

        public int Startup(string mapNamePrefix, Image image)
        {
            Image = image;
            try
            {
                memoryMappedFile = MemoryMappedFile.OpenExisting(mapNamePrefix);
            }
            catch (Exception ex)
            {
                log.Error("Startup error: " + ex);
                return -1;
            }
            if (memoryMappedFile != null)
            {
                memoryMappedViewStream = memoryMappedFile.CreateViewStream();
                binaryReader = new BinaryReader(memoryMappedViewStream);
            }
            OpenVideo = true;
            Task.Run(async () => await StartupAsync());
            return 0;
        }

        public void Close()
        {
            lastFrameData = null;
            OpenVideo = false;
            Image = null;
            memoryMappedFile?.Dispose();
            memoryMappedViewStream?.Dispose();
            binaryReader?.Dispose();
        }

        private int frameCount = 0;
        private Stopwatch fpsTimer = new Stopwatch();
        private double lastFps = 0;

        private WriteableBitmap? writeableBitmap = null;

        private async Task StartupAsync()
        {
            fpsTimer.Start();
            while (OpenVideo)
            {
                try
                {
                    memoryMappedViewStream.Position = 0L;
                    CVImagePacket cVImagePacket = new CVImagePacket();
                    cVImagePacket.Deserialize(binaryReader);
                    if (cVImagePacket != null && cVImagePacket.len > 0)
                    {
                        bool isFrameChanged = lastFrameData == null || !cVImagePacket.data.SequenceEqual(lastFrameData);
                        if (!isFrameChanged)
                        {
                            await Task.Delay(1);
                            continue;
                        }
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            if (Image == null) return;

                            // 判断是否需要重建WriteableBitmap
                            bool needNewBitmap = writeableBitmap == null ||
                                writeableBitmap.PixelWidth != cVImagePacket.width ||
                                writeableBitmap.PixelHeight != cVImagePacket.height ||
                                GetPixelFormat(cVImagePacket) != writeableBitmap.Format;

                            if (needNewBitmap)
                            {
                                writeableBitmap = new WriteableBitmap(
                                    cVImagePacket.width,
                                    cVImagePacket.height,
                                    96, 96,
                                    GetPixelFormat(cVImagePacket),
                                    null);
                                Image.Source = writeableBitmap;
                            }

                            // 写入数据到 WriteableBitmap
                            writeableBitmap!.Lock();
                            writeableBitmap.WritePixels(
                                new Int32Rect(0, 0, cVImagePacket.width, cVImagePacket.height),
                                cVImagePacket.data,
                                cVImagePacket.width * cVImagePacket.channels * (cVImagePacket.bpp / 8),
                                0);
                            writeableBitmap.Unlock();

                            Interlocked.Increment(ref frameCount);

                            // 每秒统计一次帧率
                            if (fpsTimer.ElapsedMilliseconds >= 1000)
                            {
                                lastFps = (double)frameCount*1000/ fpsTimer.ElapsedMilliseconds;
                                log.Info($"Current FPS: {lastFps}");
                                Interlocked.Exchange(ref frameCount, 0);
                                fpsTimer.Restart();
                            }
                        });

                        lastFrameData = (byte[])cVImagePacket.data.Clone();
                        await Task.Delay(20);
                    }
                    else
                    {
                        await Task.Delay(10);
                    }
                }
                catch (Exception ex)
                {
                    log.Error("StartupAsync error: " + ex);
                }
            }
            fpsTimer.Stop();
        }

        private System.Windows.Media.PixelFormat GetPixelFormat(CVImagePacket pkt)
        {
            if (pkt.channels == 3)
            {
                return pkt.bpp == 16
                    ? System.Windows.Media.PixelFormats.Rgb48
                    : System.Windows.Media.PixelFormats.Bgr24;
            }
            else
            {
                return pkt.bpp == 16
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