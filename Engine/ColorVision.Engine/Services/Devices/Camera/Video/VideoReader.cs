using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using iText.Kernel.Crypto.Securityhandler;
using log4net;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    public class VideoReaderConfig:ViewModelBase,IConfig
    {
        [DisplayName("启用视频计算")]
        public bool IsUseCacheFile { get => _IsUseCacheFile; set { _IsUseCacheFile = value; OnPropertyChanged(); } }
        private bool _IsUseCacheFile;

        [DisplayName("计算清晰度")]
        public bool IsCalArtculation { get => _IsCalArtculation; set { _IsCalArtculation = value; OnPropertyChanged(); } }
        private bool _IsCalArtculation = true;

        public FocusAlgorithm  EvaFunc { get => _EvaFunc; set { _EvaFunc = value; OnPropertyChanged(); } }
        private FocusAlgorithm  _EvaFunc = FocusAlgorithm .VarianceOfLaplacian;

        [DisplayName("显示帧率上限")]
        public int MaxDisplayFps { get => _MaxDisplayFps; set { _MaxDisplayFps = value < 0 ? 0 : value; OnPropertyChanged(); } }
        private int _MaxDisplayFps = 60;

        [JsonIgnore]
        public TextProperties TextProperties { get => _TextProperties; set { _TextProperties = value; OnPropertyChanged(); } }
        private TextProperties _TextProperties = new TextProperties() { FontSize = 200 };

        [Browsable(false),JsonIgnore]
        public RectangleTextProperties RectangleTextProperties { get; set; } = new RectangleTextProperties();


    }

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

        public VideoReaderConfig Config { get; set; }
        private readonly CameraRealtimeFramePipeline _realtimePipeline;

        public RelayCommand EditConfigCommand { get; set; }
        public void EditConfig()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        DVRectangleText DVRectangleText { get; set; }
        DVText DVText { get; set; }

        public VideoReader()
        {
            Config = ConfigService.Instance.GetRequiredService<VideoReaderConfig>();
            EditConfigCommand = new RelayCommand(a => EditConfig());
            DVRectangleText = new DVRectangleText(Config.RectangleTextProperties);
            DVText = new DVText(Config.TextProperties);
            _realtimePipeline = new CameraRealtimeFramePipeline(
                Config,
                DVRectangleText,
                DVText,
                isActive: () => openVideo,
                statusTextFormatter: state => $"Articulation: {state.Articulation:F5}",
                fpsUpdated: fps => log.Info($"Current FPS: {fps:F2}"),
                articulationUpdated: articulation => log.Info($"Image Articulation: {articulation}"));
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
