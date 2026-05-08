using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Realtime;
using ColorVision.UI;
using iText.Kernel.Crypto.Securityhandler;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

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
        }

        public MemoryMappedFile VideoMemoryMappedFile { get; set; }

        public void CreateMemoryMappedFile(string mapNamePrefix, long capacity)
        {
            VideoMemoryMappedFile = MemoryMappedFile.CreateOrOpen(mapNamePrefix, 1024L * 1024 * 1000 *2, MemoryMappedFileAccess.ReadWrite);
        }


        public int Startup(string mapNamePrefix, ImageView image)
        {
            first = true;
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
            Image.Realtime.Configure(new RealtimeFrameOptions
            {
                MaxDisplayFps = Config.MaxDisplayFps,
                AutoZoomOnFirstFrame = true,
                UpdateImageMetadata = true
            });
            Image.Realtime.AddOverlayVisual(DVRectangleText);
            Image.Realtime.AddOverlayVisual(DVText);

            if (IsPseudoEnabled())
            {
                Config.IsUseCacheFile = true;
                Config.IsCalArtculation = true;
            }
            Task.Run(StartupAsync);

            return 0;
        }

    private VideoFrameProcessor? _frameProcessor;

        private bool IsPseudoEnabled()
        {
            return Image?.PseudoColorService?.IsEnabled == true;
        }

        public void Close()
        {
            openVideo = false;
            VideoMemoryMappedFile?.Dispose();

        }

        private int frameCount;
        private readonly Stopwatch fpsTimer = new Stopwatch();
        private double lastFps;
        private bool first = true;

        

        private async Task StartupAsync()
        {
            fpsTimer.Restart();
            Interlocked.Exchange(ref frameCount, 0);
            lastFps = 0;
            _frameProcessor ??= new VideoFrameProcessor(HandleProcessedFrame);
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

                    if (TryBuildFrameProcessingRequest(width, height, out VideoFrameProcessingRequest? request))
                    {
                        _frameProcessor?.SubmitFrame(buffer, len, width, height, channels, bpp, stride, request);
                    }

                    if (Image == null)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        await Task.Delay(10);
                        continue;
                    }

                    if (!IsPseudoEnabled())
                    {
                        Image.Realtime.SubmitFrame(buffer, width, height, GetPixelFormat(channels, bpp), stride, len);
                    }

                    Interlocked.Increment(ref frameCount);
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
                    }

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
            fpsTimer.Stop();

            if (lastFrameData != null)
            {
                ArrayPool<byte>.Shared.Return(lastFrameData);
                lastFrameData = null;
                lastFrameLen = 0;
            }
            ImageView? image = Image;
            if (image != null)
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    image.Realtime.RemoveOverlayVisual(DVRectangleText);
                    image.Realtime.RemoveOverlayVisual(DVText);
                }));
            }

            Image = null;
            binaryReader?.Dispose();
            memoryMappedViewStream?.Dispose();
            memoryMappedFile?.Dispose();

            _frameProcessor?.Dispose();
            _frameProcessor = null;
        }

        private bool TryBuildFrameProcessingRequest(int width, int height, out VideoFrameProcessingRequest? request)
        {
            request = null;
            if (Image == null)
            {
                return false;
            }

            var pseudoColorService = Image.PseudoColorService;
            bool enablePseudo = pseudoColorService.IsEnabled;
            if (enablePseudo)
            {
                Config.IsUseCacheFile = true;
            }

            bool enableArticulation = Config.IsCalArtculation;
            if (!Config.IsUseCacheFile || (!enablePseudo && !enableArticulation))
            {
                return false;
            }

            Rect rect = DVRectangleText.Rect;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                rect = new Rect(0, 0, width, height);
            }

            PseudoColorFrameRequest? pseudoColorRequest = null;
            if (enablePseudo && pseudoColorService.TryCreateRequest(out var capturedRequest, 0))
            {
                pseudoColorRequest = capturedRequest;
            }

            request = new VideoFrameProcessingRequest
            {
                EnableArticulation = enableArticulation,
                FocusAlgorithm = Config.EvaFunc,
                Roi = new RoiRect(rect),
                PseudoColor = pseudoColorRequest
            };
            return true;
        }

        private void HandleProcessedFrame(VideoFrameProcessingResult result)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!openVideo || Image == null)
                {
                    if (result.PseudoImage is HImage staleImage)
                    {
                        staleImage.Dispose();
                    }
                    return;
                }

                if (result.Articulation is double articulation)
                {
                    DVText.Attribute.Text = $"Articulation: {articulation:F5}";
                    log.Info($"Image Articulation: {articulation}");
                }

                if (result.PseudoImage is HImage pseudoImage)
                {
                    if (Image.PseudoColorService.IsEnabled)
                    {
                        VideoFrameUiHelper.ApplyPseudoImage(Image.PseudoColorService, pseudoImage);
                    }
                    else
                    {
                        pseudoImage.Dispose();
                    }
                }
            }));
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
