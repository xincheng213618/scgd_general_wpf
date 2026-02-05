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
        private FocusAlgorithm  _EvaFunc = FocusAlgorithm .Laplacian;

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

        HImage? _calculationHImage;

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
                    if (Config.IsUseCacheFile)
                    {
                        // 直接从帧数据构造 HImage，无需内存拷贝
                        if (_calculationHImage == null || _calculationHImage.Value.cols != width || _calculationHImage.Value.rows != height || _calculationHImage.Value.channels != channels || _calculationHImage.Value.depth != bpp / 8)
                        {
                            _calculationHImage?.Dispose(); // 释放旧的
                            _calculationHImage = new HImage
                            {
                                rows = height,
                                cols = width,
                                channels = channels,
                                depth = bpp / 8,
                                pData = Marshal.AllocHGlobal(buffer.Length) // 分配新的非托管内存
                            };
                            log.Info("Allocated new HImage for calculation.");
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                DVRectangleText.Rect = new Rect(0, 0, width, height);
                            });
                        }
                        if (_calculationHImage != null)
                        {
                            Marshal.Copy(buffer, 0, _calculationHImage.Value.pData, buffer.Length);
                            if (_calculationHImage is HImage hImage)
                            {
                                Rect rect = DVRectangleText.Rect;
                                if (Config.IsCalArtculation)
                                {
                                    Thread task = new Thread(() =>
                                    {
                                        // 在后台线程执行计算
                                        double articulation = OpenCVMediaHelper.M_CalArtculation(hImage, FocusAlgorithm.Laplacian, new RoiRect(rect));
                                        Application.Current?.Dispatcher.Invoke(() =>
                                        {
                                            DVText.Attribute.Text = $"Articulation: {articulation:F5}";
                                        });
                                        log.Info($"Image Articulation: {articulation}");
                                    });
                                    task.Start();
                                }
                                if (Image == null)
                                {
                                    return;
                                }
                                if (Image.Config.IsPseudo)
                                {
                                    Application.Current?.Dispatcher.Invoke(() =>
                                    {

                                        uint min = (uint)Image.PseudoSlider.ValueStart;
                                        uint max = (uint)Image.PseudoSlider.ValueEnd;

                                        log.Info($"ImagePath，正在执行PseudoColor,min:{min},max:{max}");

                                        Thread task1 = new Thread(() =>
                                        {
                                            int ret = OpenCVMediaHelper.M_PseudoColor(hImage, out HImage hImageProcessed, min, max, Image.Config.ColormapTypes, 0);
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                if (ret == 0)
                                                {
                                                    if (!HImageExtension.UpdateWriteableBitmap(Image.FunctionImage, hImageProcessed))
                                                    {
                                                        var image = hImageProcessed.ToWriteableBitmap();
                                                        hImageProcessed.Dispose();

                                                        Image.FunctionImage = image;
                                                    }
                                                    if (Image.Config.IsPseudo == true)
                                                    {
                                                        Image.ImageShow.Source = Image.FunctionImage;
                                                    }
                                                }
                                            });
                                        });
                                        task1.Start();
                                    });

                                }
 
                            }
                        }

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

            _calculationHImage?.Dispose();
            _calculationHImage = null;
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