#pragma warning disable CS0649,CS8625
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Settings;
using ColorVision.UI;
using log4net;
using System;
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    /// <summary>
    /// 写标志位的方案有问题，会少刷新
    /// </summary>
    public class VideoReader : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VideoReader));
        private bool openVideo;
        private MemoryMappedFile memoryMappedFile;
        private MemoryMappedViewStream memoryMappedViewStream;
        private BinaryReader binaryReader;

        private ImageView Image { get; set; }

        private byte[]? lastFrameData; // 上一帧池化数据
        private int lastFrameLen;      // 上一帧有效长度

        public DefaultRealtimeCameraConfig Config { get; set; }
        private readonly CameraRealtimeFramePipeline _realtimePipeline;

        public RelayCommand EditConfigCommand { get; set; }
        public void EditConfig()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public VideoReader()
        {
            Config = DefaultRealtimeCameraConfig.Current;
            EditConfigCommand = new RelayCommand(a => EditConfig());
            _realtimePipeline = new CameraRealtimeFramePipeline();
        }

        public MemoryMappedFile VideoMemoryMappedFile { get; set; }

        public void CreateMemoryMappedFile(string mapNamePrefix, long capacity)
        {
            VideoMemoryMappedFile = MemoryMappedFile.CreateOrOpen(mapNamePrefix, 1024L * 1024 * 1000 *2, MemoryMappedFileAccess.ReadWrite);
        }


        public int Startup(string mapNamePrefix, ImageView image)
        {
            Image = image;
            try
            {
                MemoryMappedFile memoryMappedFile =  MemoryMappedFile.CreateOrOpen(mapNamePrefix, 1024 * 1024 * 1000, MemoryMappedFileAccess.ReadWrite);
                memoryMappedFile?.Dispose();

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
            _realtimePipeline.Start(Image);
            Task.Run(StartupAsync);

            return 0;
        }

        public void Close()
        {
            openVideo = false;
            VideoMemoryMappedFile?.Dispose();

        }

        private async Task StartupAsync()
        {
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
                    int bytesPerChannel = Math.Max(1, bpp / 8);
                    int stride = height > 0 && len % height == 0 ? len / height : width * channels * bytesPerChannel;

                    if (Image == null)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        await Task.Delay(10);
                        continue;
                    }

                    _realtimePipeline.SubmitFrame(buffer, len, width, height, channels, bpp, stride);

                    if (lastFrameData != null)
                    {
                        ArrayPool<byte>.Shared.Return(lastFrameData);
                    }
                    lastFrameData = buffer;
                    lastFrameLen = len;


                    await Task.Delay(20);
                }
                catch (Exception ex)
                {
                    log.Error("StartupAsync error: " + ex);
                }
            }
            _realtimePipeline.Stop(resetRealtime: false);

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

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Close();
        }
    }
}
