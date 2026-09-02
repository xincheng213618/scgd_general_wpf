#pragma warning disable CA1859,CS8604
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Layers;
using log4net;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    internal sealed class CvRawLayerController : IImageLayerController, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CvRawLayerController));
        private readonly ImageView _imageView;
        private readonly string _filePath;
        private readonly CVCIEFile? _liveXyz;
        private readonly WriteableBitmap? _liveSource;
        private ImageLayerDescriptor _lastSuccessfulLayer;
        private CancellationTokenSource? _selection;
        private readonly SemaphoreSlim _loadGate = new(1, 1);
        private bool _disposed;
        private DisplayCache? _srgbCache;
        private DisplayCache? _channelCache;
        private readonly record struct FileStamp(long Length, DateTime LastWriteTime);
        private sealed record DisplayCache(string LayerId, CvcieBrightnessMode Mode, double White, FileStamp Stamp, WriteableBitmap Bitmap);

        private CvRawLayerController(ImageView imageView, string filePath, IReadOnlyList<ImageLayerDescriptor> layers, string displayedLayerId,
            CVCIEFile? liveXyz = null, WriteableBitmap? liveSource = null)
        {
            _imageView = imageView;
            _filePath = filePath;
            Layers = layers;
            DefaultLayer = layers.FirstOrDefault(layer => layer.Id == displayedLayerId) ?? layers[0];
            _lastSuccessfulLayer = DefaultLayer;
            _liveXyz = liveXyz;
            if (liveSource != null)
            {
                _liveSource = liveSource.Clone();
                _liveSource.Freeze();
            }
        }

        public IReadOnlyList<ImageLayerDescriptor> Layers { get; }

        public ImageLayerDescriptor? DefaultLayer { get; private set; }

        public static CvRawLayerController Create(ImageView imageView, string filePath, bool isCie, int channelCount, int bpp, bool hasRgbLayers, string displayedLayerId)
        {
            return new CvRawLayerController(imageView, filePath, BuildLayers(isCie, channelCount, bpp, hasRgbLayers), displayedLayerId);
        }

        public static IImageLayerController CreateLive(ImageView imageView, CVCIEFile xyz, WriteableBitmap source, string displayedLayerId)
        {
            return new CvRawLayerController(imageView, string.Empty, BuildLayers(true, xyz.Channels, xyz.Bpp, false), displayedLayerId, xyz, source);
        }

        public static WriteableBitmap LoadSrgb(string filePath, CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool read = CVFileUtil.Read(filePath, out CVCIEFile xyz);
            using (xyz)
            {
                if (!read) throw new InvalidDataException($"读取 CVCIE 内嵌 XYZ 失败：{filePath}");
                return CvcieSrgbRenderer.Render(xyz, brightnessMode, referenceWhiteLuminance, cancellationToken);
            }
        }

        public static CVCIEFile LoadSourceFile(string filePath, out bool usesLuminance, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            usesLuminance = false;
            if (!string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase))
                return CVFileUtil.OpenLocalCVFile(filePath, CVType.Raw);

            int headerOffset = CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile header);
            using (header)
            {
                string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                List<string> candidates = new();
                if (!string.IsNullOrWhiteSpace(header.SrcFileName))
                {
                    candidates.Add(header.SrcFileName);
                    try
                    {
                        candidates.Add(Path.Combine(directory, header.SrcFileName));
                    }
                    catch (ArgumentException ex)
                    {
                        Log.Warn($"CVCIE 关联原图路径无效：{filePath}", ex);
                    }
                }
                candidates.Add(Path.ChangeExtension(filePath, ".cvraw"));
                foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!File.Exists(candidate)) continue;
                    CVCIEFile? source = null;
                    try
                    {
                        if (string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase)
                            || string.Equals(Path.GetExtension(candidate), ".cvcie", StringComparison.OrdinalIgnoreCase)) continue;
                        if (CVFileUtil.IsCIEFile(candidate))
                        {
                            if (!CVFileUtil.Read(candidate, out source)) throw new InvalidDataException("无法读取关联原图。");
                            source.FileExtType = CVType.Raw;
                            long expectedLength = checked((long)source.Rows * source.Cols * source.Channels * (source.Bpp / 8));
                            if (source.Rows <= 0 || source.Cols <= 0 || source.Channels is not (1 or 3 or 4)
                                || source.Bpp is not (8 or 16 or 32 or 64) || source.Data?.LongLength != expectedLength)
                                throw new InvalidDataException("关联原图尺寸或数据长度无效。");
                        }
                        else
                        {
                            source = new CVCIEFile { FileExtType = CVType.Tif, Data = File.ReadAllBytes(candidate) };
                            using OpenCvSharp.Mat decoded = source.ToMat(showErrors: false);
                            if (decoded == null || decoded.Empty()) throw new InvalidDataException("关联原图无法解码。");
                            source.Rows = decoded.Rows;
                            source.Cols = decoded.Cols;
                            source.Channels = decoded.Channels();
                            source.Bpp = checked((int)decoded.ElemSize1() * 8);
                        }
                        source.SrcFileName = candidate;
                        source.FilePath = filePath;
                        return source;
                    }
                    catch (Exception ex)
                    {
                        source?.Dispose();
                        Log.Warn($"关联原图不可用：{candidate}", ex);
                    }
                }

                if (headerOffset <= 0) throw new InvalidDataException($"CVCIE 文件头无效且没有可用 CVRAW：{filePath}");
                int yIndex = header.Channels == 1 ? 0 : 1;
                bool read = CVFileUtil.ReadCIEFileChannel(filePath, yIndex, out CVCIEFile luminance, cancellationToken);
                if (!read)
                {
                    luminance.Dispose();
                    throw new InvalidDataException($"没有可用 CVRAW，且无法读取 CVCIE Y 通道：{filePath}");
                }
                luminance.Channels = 1;
                luminance.FileExtType = CVType.Raw;
                usesLuminance = true;
                return luminance;
            }
        }

        public async void SelectLayer(ImageLayerDescriptor layer)
        {
            if (_disposed) return;
            _selection?.Cancel();
            using CancellationTokenSource selection = new();
            _selection = selection;
            CvcieDisplayConfig config = CvcieDisplayConfig.Current;
            CvcieBrightnessMode mode = config.BrightnessMode;
            double white = config.ReferenceWhiteLuminance;
            try
            {
                FileStamp stamp = GetFileStamp();
                DisplayCache? cache = layer.Id == "cie-srgb" ? _srgbCache : _channelCache;
                WriteableBitmap bitmap;
                if (cache != null && cache.LayerId == layer.Id && cache.Stamp == stamp
                    && (layer.Id != "cie-srgb" || (cache.Mode == mode && cache.White == white)))
                    bitmap = cache.Bitmap;
                else
                    bitmap = await RunLoadAsync(() => LoadLayer(layer, mode, white, selection.Token), selection.Token);

                if (!IsCurrent(selection)) return;
                StoreCache(layer.Id, bitmap, mode, white, stamp);
                ShowLayer(bitmap);
                if (IsCurrent(selection))
                {
                    if (layer.SourceChannelIndex is int channel) _imageView.ExtractChannel(channel);
                    _lastSuccessfulLayer = layer;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsCurrent(selection)) return;
                Log.Warn($"CVCIE 图层 {layer.Id} 显示失败，回退原图：{_filePath}", ex);
                try
                {
                    var fallback = await RunLoadAsync(() =>
                    {
                        WriteableBitmap bitmap = LoadSource(out bool usesLuminance, selection.Token);
                        return (Bitmap: bitmap, UsesLuminance: usesLuminance);
                    }, selection.Token);
                    if (!IsCurrent(selection)) return;
                    DefaultLayer = Layers.FirstOrDefault(item => item.Id == (fallback.UsesLuminance ? "cie-y" : "composite")) ?? Layers[0];
                    _imageView.SetLayerController(this);
                    ShowLayer(fallback.Bitmap);
                    if (IsCurrent(selection)) _lastSuccessfulLayer = DefaultLayer;
                }
                catch (OperationCanceledException) { }
                catch (Exception fallbackError)
                {
                    if (!IsCurrent(selection)) return;
                    Log.Warn($"CVRAW 和 Y 灰度回退均不可用，保留当前图像：{_filePath}", fallbackError);
                    DefaultLayer = _lastSuccessfulLayer;
                    _imageView.SetLayerController(this);
                }
            }
            finally
            {
                if (ReferenceEquals(_selection, selection)) _selection = null;
            }
        }

        private bool IsCurrent(CancellationTokenSource selection) => !_disposed && !selection.IsCancellationRequested
            && ReferenceEquals(_selection, selection) && ReferenceEquals(_imageView.ComboBoxLayers.ItemsSource, Layers);

        private async Task<T> RunLoadAsync<T>(Func<T> load, CancellationToken token)
        {
            // A superseded large read must finish before another payload is allocated in this view.
            await _loadGate.WaitAsync(token);
            try { return await Task.Run(load, token); }
            finally { _loadGate.Release(); }
        }

        private WriteableBitmap LoadLayer(ImageLayerDescriptor layer, CvcieBrightnessMode mode, double white, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return layer.Id switch
            {
                "cie-srgb" => _liveXyz == null ? LoadSrgb(_filePath, mode, white, token) : CvcieSrgbRenderer.Render(_liveXyz, mode, white, token),
                "cie-x" => LoadCieChannel(0, token),
                "cie-y" => LoadCieChannel(1, token),
                "cie-z" => LoadCieChannel(2, token),
                _ => LoadSource(out _, token),
            };
        }

        public void CacheSrgb(WriteableBitmap bitmap, CvcieBrightnessMode mode, double white)
        {
            StoreCache("cie-srgb", bitmap, mode, white, GetFileStamp());
        }

        private FileStamp GetFileStamp()
        {
            if (_liveXyz != null) return default;
            FileInfo file = new(_filePath);
            return new FileStamp(file.Length, file.LastWriteTimeUtc);
        }

        private void StoreCache(string layerId, WriteableBitmap bitmap, CvcieBrightnessMode mode, double white, FileStamp stamp)
        {
            // Retain display pixels only, never the multi-gigabyte XYZ input. Two entries at most.
            long bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * bitmap.Format.BitsPerPixel / 8;
            if (bytes > 512L * 1024 * 1024) return;
            if (layerId == "cie-srgb") _srgbCache = new(layerId, mode, white, stamp, bitmap);
            else if (layerId is "cie-x" or "cie-y" or "cie-z") _channelCache = new(layerId, mode, white, stamp, bitmap);
        }

        public void Dispose()
        {
            _disposed = true;
            _selection?.Cancel();
            _srgbCache = null;
            _channelCache = null;
        }

        private WriteableBitmap LoadSource(out bool usesLuminance, CancellationToken token)
        {
            usesLuminance = false;
            if (_liveSource != null) return _liveSource;
            using CVCIEFile source = LoadSourceFile(_filePath, out usesLuminance, token);
            return ConvertForDisplay(source, token);
        }

        private WriteableBitmap LoadCieChannel(int index, CancellationToken token)
        {
            if (_liveXyz == null)
            {
                if (CVFileUtil.ReadCIEFileHeader(_filePath, out CVCIEFile header) <= 0)
                {
                    header.Dispose();
                    throw new InvalidDataException("CVCIE 文件头无效。");
                }
                using (header) { if (header.Channels == 1) index = 0; }
                bool read = CVFileUtil.ReadCIEFileChannel(_filePath, index, out CVCIEFile file, token);
                using (file)
                {
                    if (!read) throw new InvalidDataException($"无法读取 CVCIE 通道 {index}。");
                    file.Channels = 1;
                    file.FileExtType = CVType.Raw;
                    return ConvertForDisplay(file, token);
                }
            }

            if (_liveXyz.Channels == 1) index = 0;
            int length = checked(_liveXyz.Cols * _liveXyz.Rows * (_liveXyz.Bpp / 8));
            byte[] data = new byte[length];
            Buffer.BlockCopy(_liveXyz.Data, checked(index * length), data, 0, length);
            using CVCIEFile plane = new()
            {
                Cols = _liveXyz.Cols, Rows = _liveXyz.Rows, Bpp = _liveXyz.Bpp,
                Channels = 1, FileExtType = CVType.Raw, Data = data,
            };
            return ConvertForDisplay(plane, token);
        }

        private static WriteableBitmap ConvertForDisplay(CVCIEFile file, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (file.Channels == 1 && file.Bpp is 32 or 64) return MediaHelper.RenderFloatChannel(file, token);
            using OpenCvSharp.Mat mat = file.ToMat(showErrors: false);
            if (mat == null || mat.Empty()) throw new InvalidDataException("原图或通道数据无法显示。");
            WriteableBitmap bitmap = mat.ToWriteableBitmap();
            bitmap.Freeze();
            return bitmap;
        }

        private void ShowLayer(WriteableBitmap writeableBitmap)
        {
            _imageView.SetImageSource(writeableBitmap.IsFrozen ? writeableBitmap.Clone() : writeableBitmap, _imageView.EnableEditorImageServices, configureDefaultLayerController: false);
        }

        private static IReadOnlyList<ImageLayerDescriptor> BuildLayers(bool isCie, int channelCount, int bpp, bool hasRgbLayers)
        {
            List<ImageLayerDescriptor> layers = new()
            {
                new ImageLayerDescriptor
                {
                    Id = "composite",
                    DisplayName = "Composite",
                    Kind = ImageLayerKind.Composite,
                }
            };

            if (!isCie)
            {
                if (channelCount >= 3)
                {
                    layers.AddRange(CreateRgbLayers());
                }

                return layers;
            }

            if (channelCount >= 3 && hasRgbLayers)
            {
                layers.AddRange(CreateRgbLayers());
            }

            if (CvcieSrgbRenderer.Supports(channelCount, bpp))
            {
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-srgb",
                    DisplayName = "真彩 sRGB（XYZ）",
                    Kind = ImageLayerKind.Derived,
                });
            }

            if (channelCount >= 3)
            {
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-x",
                    DisplayName = "CIE X",
                    Kind = ImageLayerKind.Derived,
                });
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-y",
                    DisplayName = "CIE Y",
                    Kind = ImageLayerKind.Derived,
                });
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-z",
                    DisplayName = "CIE Z",
                    Kind = ImageLayerKind.Derived,
                });
            }
            else if (channelCount == 1)
            {
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-y",
                    DisplayName = "Luminance",
                    Kind = ImageLayerKind.Derived,
                });
            }

            return layers;
        }

        private static IEnumerable<ImageLayerDescriptor> CreateRgbLayers()
        {
            yield return new ImageLayerDescriptor
            {
                Id = "red",
                DisplayName = "Red",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = 0,
            };
            yield return new ImageLayerDescriptor
            {
                Id = "green",
                DisplayName = "Green",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = 1,
            };
            yield return new ImageLayerDescriptor
            {
                Id = "blue",
                DisplayName = "Blue",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = 2,
            };
        }
    }
}
