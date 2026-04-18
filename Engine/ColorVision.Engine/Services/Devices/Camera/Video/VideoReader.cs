using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
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
            Image.ImageShow.AddVisualCommand(DVRectangleText);
            Image.ImageShow.AddVisualCommand(DVText);

            if (Image.Config.IsPseudo)
            {
                Config.IsUseCacheFile = true;
                Config.IsCalArtculation = true;
            }

            Image.Config.PseudoChanged -= Config_PseudoChanged;
            Image.Config.PseudoChanged += Config_PseudoChanged;
            Task.Run(StartupAsync);

            return 0;
        }

        private void Config_PseudoChanged(object? sender, EventArgs e)
        {
            if (Image.Config.IsPseudo)
                Config.IsUseCacheFile = true;
        }

    private VideoFrameProcessor? _frameProcessor;

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
                    if (TryBuildFrameProcessingRequest(width, height, out VideoFrameProcessingRequest? request))
                    {
                        int bytesPerChannel = Math.Max(1, bpp / 8);
                        int stride = height > 0 && len % height == 0 ? len / height : width * channels * bytesPerChannel;
                        _frameProcessor?.SubmitFrame(buffer, len, width, height, channels, bpp, stride, request);
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

                        if (!Image.Config.IsPseudo)
                        {
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
                        }

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
                            Image.Zoombox1.ZoomUniform();
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

            if (lastFrameData != null)
            {
                ArrayPool<byte>.Shared.Return(lastFrameData);
                lastFrameData = null;
                lastFrameLen = 0;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                Image.ImageShow.RemoveVisualCommand(DVRectangleText);
                Image.ImageShow.RemoveVisualCommand(DVText);
            });

            Image.Config.PseudoChanged -= Config_PseudoChanged;
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
            if (!Config.IsUseCacheFile || Image == null)
            {
                return false;
            }

            bool enablePseudo = Image.Config.IsPseudo;
            bool enableArticulation = Config.IsCalArtculation;
            if (!enablePseudo && !enableArticulation)
            {
                return false;
            }

            Rect rect = DVRectangleText.Rect;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                rect = new Rect(0, 0, width, height);
            }

            request = new VideoFrameProcessingRequest
            {
                EnableArticulation = enableArticulation,
                FocusAlgorithm = Config.EvaFunc,
                Roi = new RoiRect(rect),
                EnablePseudoColor = enablePseudo,
                PseudoMin = enablePseudo ? (uint)Image.PseudoSlider.ValueStart : 0,
                PseudoMax = enablePseudo ? (uint)Image.PseudoSlider.ValueEnd : 0,
                ColormapTypes = Image.Config.ColormapTypes,
                PseudoChannel = 0
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
                    if (Image.Config.IsPseudo)
                    {
                        VideoFrameUiHelper.ApplyPseudoImage(Image, pseudoImage);
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