using LocalizedText = global::ColorVision.Engine.EngineLocalization;
#pragma warning disable CA1859,CS8604
using ColorVision.FileIO;
using ColorVision.Engine.Services.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Layers;
using ColorVision.ImageEditor.Presentation;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    internal sealed record CvcieAssociatedSource(string Path, CvcieFileRevision Revision, int Rows, int Cols, bool SupportsRawProfile)
    {
        public FileStream OpenRead()
        {
            try { return Revision.OpenRead(Path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new CvcieAssociatedSourceChangedException(Path, error);
            }
        }
    }

    internal sealed class CvcieAssociatedSourceChangedException(string path, Exception error)
        : IOException($"CVCIE 关联原图已改变或不可读取，请重新打开图像：{path}", error);

    internal sealed class CvcieDisplaySource(CVCIEFile file, OpenCvSharp.Mat? decoded = null) : IDisposable
    {
        public CVCIEFile File { get; } = file;
        public OpenCvSharp.Mat? Decoded { get; } = decoded;

        public void Dispose()
        {
            try { Decoded?.Dispose(); }
            finally { File.Dispose(); }
        }
    }

    internal sealed class CvRawLayerController : IImageLayerController, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CvRawLayerController));
        private readonly ImageView _imageView;
        private readonly string _filePath;
        private readonly bool _isCie;
        private readonly CvcieFileRevision? _fileRevision;
        private readonly CvcieAssociatedSource? _associatedSource;
        private readonly CVCIEFile? _liveXyz;
        private readonly RawColorMeasurementSource? _rawColor;
        private BitmapSource? _liveSource;
        private ImageLayerDescriptor _lastSuccessfulLayer;
        private CancellationTokenSource? _selection;
        private readonly SemaphoreSlim _loadGate = new(1, 1);
        private bool _disposed;
        private DisplayCache? _srgbCache;
        private DisplayCache? _channelCache;
        internal const long DisplayCacheBudgetBytes = 128L * 1024 * 1024;
        private sealed record DisplayCache(string LayerId, CvcieBrightnessMode Mode, double White, WriteableBitmap Bitmap, long Bytes);

        private CvRawLayerController(ImageView imageView, string filePath, bool isCie, IReadOnlyList<ImageLayerDescriptor> layers, string displayedLayerId,
            CVCIEFile? liveXyz = null, BitmapSource? liveSource = null, RawColorMeasurementSource? rawColor = null,
            CvcieFileRevision? fileRevision = null, CvcieAssociatedSource? associatedSource = null)
        {
            _imageView = imageView;
            _filePath = filePath;
            _isCie = isCie;
            _fileRevision = fileRevision;
            _associatedSource = associatedSource;
            Layers = layers;
            DefaultLayer = layers.FirstOrDefault(layer => layer.Id == displayedLayerId) ?? layers[0];
            _lastSuccessfulLayer = DefaultLayer;
            _liveXyz = liveXyz;
            _rawColor = rawColor;
            if (liveSource != null)
            {
                _liveSource = liveSource.IsFrozen ? liveSource : liveSource.Clone();
                _liveSource.Freeze();
            }
        }

        public IReadOnlyList<ImageLayerDescriptor> Layers { get; }

        public ImageLayerDescriptor? DefaultLayer { get; private set; }

        public static CvRawLayerController Create(ImageView imageView, string filePath, bool isCie, int channelCount, int bpp,
            bool hasRgbLayers, string displayedLayerId, CvcieFileRevision? fileRevision = null, CvcieAssociatedSource? associatedSource = null)
        {
            return new CvRawLayerController(imageView, filePath, isCie, BuildLayers(isCie, channelCount, bpp, hasRgbLayers), displayedLayerId,
                fileRevision: fileRevision, associatedSource: associatedSource);
        }

        public static IImageLayerController CreateLive(ImageView imageView, CVCIEFile xyz, WriteableBitmap source, string displayedLayerId)
        {
            return new CvRawLayerController(imageView, string.Empty, true, BuildLayers(true, xyz.Channels, xyz.Bpp, false), displayedLayerId, xyz, source);
        }

        internal static CvRawLayerController CreateCalibratedRaw(ImageView imageView, string path, RawColorMeasurementSource source, string displayedLayerId, BitmapSource? sourcePixels)
        {
            var layers = BuildLayers(true, source.Channels, 32, source.Channels >= 3).Where(layer => layer.Id != "cie-srgb").ToArray();
            if (sourcePixels?.Format == PixelFormats.Bgr24)
                layers = layers.Select(layer => layer.SourceChannelIndex is int channel ? layer with { SourceChannelIndex = 2 - channel } : layer).ToArray();
            return new CvRawLayerController(imageView, path, true, layers, displayedLayerId, liveSource: sourcePixels, rawColor: source);
        }

        public static WriteableBitmap LoadSrgb(string filePath, CvcieBrightnessMode brightnessMode, double referenceWhiteLuminance, CancellationToken cancellationToken = default)
            => CvcieSrgbRenderer.RenderFile(filePath, brightnessMode, referenceWhiteLuminance, cancellationToken);

        internal static CvcieDisplaySource LoadSourceForDisplay(string filePath, out bool usesLuminance, out WriteableBitmap? bitmap,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            usesLuminance = false;
            bitmap = null;
            if (!string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase))
                return new(CVFileUtil.OpenLocalCVFile(filePath, CVType.Raw));

            int headerOffset = CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile header);
            using (header)
            {
                PrepareAssociatedSource(filePath, header, loadPixels: true, out CvcieDisplaySource? source, cancellationToken,
                    matchCieGeometry: headerOffset > 0);
                if (source != null) return source;
                if (headerOffset <= 0) throw new InvalidDataException($"CVCIE 文件头无效且没有可用 CVRAW：{filePath}");
                usesLuminance = true;
                return new(LoadFileChannel(filePath, 1, out bitmap, cancellationToken));
            }
        }

        internal static CvcieAssociatedSource? PrepareAssociatedSource(string filePath, CVCIEFile? cieHeader, bool loadPixels,
            out CvcieDisplaySource? source, CancellationToken token, bool matchCieGeometry = true)
        {
            source = null;
            string ciePath = Path.GetFullPath(filePath);
            List<string> candidates = new();
            if (!string.IsNullOrWhiteSpace(cieHeader?.SrcFileName))
            {
                try { candidates.Add(Path.Combine(Path.GetDirectoryName(ciePath) ?? string.Empty, cieHeader.SrcFileName)); }
                catch (ArgumentException ex) { Log.Warn($"CVCIE 关联原图路径无效：{filePath}", ex); }
                candidates.Add(cieHeader.SrcFileName);
            }
            candidates.Add(Path.ChangeExtension(ciePath, ".cvraw"));
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if (!File.Exists(candidate)) continue;
                try
                {
                    string path = Path.GetFullPath(candidate);
                    if (string.Equals(path, ciePath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Path.GetExtension(path), ".cvcie", StringComparison.OrdinalIgnoreCase)) continue;
                    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var revision = new CvcieFileRevision(stream.Length, File.GetLastWriteTimeUtc(stream.SafeFileHandle),
                        File.GetCreationTimeUtc(stream.SafeFileHandle));
                    CvcieDisplaySource prepared = ReadAssociatedImage(stream, filePath, matchCieGeometry ? cieHeader?.Rows : null,
                        matchCieGeometry ? cieHeader?.Cols : null, loadPixels, token);
                    try
                    {
                        CVCIEFile image = prepared.File;
                        bool supportsProfile = image.FileExtType == CVType.Raw && image.Channels is 1 or 3 && image.Bpp is 8 or 16
                            && string.Equals(Path.GetExtension(path), ".cvraw", StringComparison.OrdinalIgnoreCase);
                        var snapshot = new CvcieAssociatedSource(path, revision, image.Rows, image.Cols, supportsProfile);
                        if (loadPixels) source = prepared;
                        return snapshot;
                    }
                    finally { if (source == null) prepared.Dispose(); }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { Log.Warn($"关联原图不可用：{candidate}", ex); }
            }
            return null;
        }

        private static CvcieDisplaySource ReadAssociatedImage(FileStream stream, string filePath, int? expectedRows, int? expectedCols,
            bool loadPixels, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Span<byte> magic = stackalloc byte[5];
            if (stream.Length >= magic.Length) stream.ReadExactly(magic);
            bool isCvImage = stream.Length >= magic.Length && magic.SequenceEqual("CVCIE"u8);
            stream.Position = 0;
            CVCIEFile? image = null;
            OpenCvSharp.Mat? decoded = null;
            try
            {
                if (isCvImage)
                {
                    int offset = CVFileUtil.ReadCIEFileHeader(stream, out image, maximumChannels: 4);
                    if (offset <= 0 || image.Rows <= 0 || image.Cols <= 0 || image.Channels is not (1 or 3 or 4)
                        || image.Bpp is not (8 or 16 or 32 or 64)) throw new InvalidDataException("关联原图文件头无效。");
                    ValidateGeometry(image.Rows, image.Cols);
                    if (loadPixels)
                    {
                        if (!CVFileUtil.ReadCIEFilePixels(stream, image, offset, requireExactPayload: true, token))
                            throw new InvalidDataException("关联原图像素长度无效。");
                    }
                    else
                    {
                        long required = checked((long)image.Rows * image.Cols * image.Channels * (image.Bpp / 8));
                        CvcieDiskPixels.Locate(stream, image, offset, required, requireExactLength: true);
                    }
                    image.FileExtType = CVType.Raw;
                }
                else if (loadPixels)
                {
                    if (stream.Length > Array.MaxLength) throw new InvalidDataException("关联原图文件过大。");
                    image = new CVCIEFile { FileExtType = CVType.Tif, Data = GC.AllocateUninitializedArray<byte>((int)stream.Length) };
                    stream.ReadExactly(image.Data);
                    token.ThrowIfCancellationRequested();
                    decoded = image.ToMat(showErrors: false);
                    if (decoded == null || decoded.Empty()) throw new InvalidDataException("关联原图无法解码。");
                    image.Rows = decoded.Rows;
                    image.Cols = decoded.Cols;
                    image.Channels = decoded.Channels();
                    image.Bpp = checked((int)decoded.ElemSize1() * 8);
                    ValidateGeometry(image.Rows, image.Cols);
                }
                else
                {
                    // True color already has pixels. Inspect optional source geometry without decoding it.
                    int rows, cols;
                    try
                    {
                        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        BitmapFrame frame = decoder.Frames[0];
                        rows = frame.PixelHeight;
                        cols = frame.PixelWidth;
                    }
                    catch (Exception error) when (error is NotSupportedException or FileFormatException)
                    {
                        // Preserve sources that only the legacy OpenCV decoder recognizes.
                        stream.Position = 0;
                        return ReadAssociatedImage(stream, filePath, expectedRows, expectedCols, loadPixels: true, token);
                    }
                    ValidateGeometry(rows, cols);
                    image = new CVCIEFile { FileExtType = CVType.Tif, Rows = rows, Cols = cols };
                }

                token.ThrowIfCancellationRequested();
                image.SrcFileName = stream.Name;
                image.FilePath = filePath;
                return new(image, decoded);
            }
            catch
            {
                try { decoded?.Dispose(); }
                finally { image?.Dispose(); }
                throw;
            }

            void ValidateGeometry(int rows, int cols)
            {
                if (rows <= 0 || cols <= 0 || (expectedRows.HasValue && (rows != expectedRows || cols != expectedCols)))
                    throw new InvalidDataException($"关联原图尺寸 {cols}×{rows} 与 CVCIE {expectedCols}×{expectedRows} 不一致。");
            }
        }

        internal static CVCIEFile LoadFileChannel(string filePath, int index, out WriteableBitmap? bitmap, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            int offset = CVFileUtil.ReadCIEFileHeader(stream, out CVCIEFile plane);
            try
            {
                if (plane.Channels == 1) index = 0;
                if (offset <= 0 || plane.Cols <= 0 || plane.Rows <= 0 || (uint)index >= (uint)plane.Channels)
                    throw new InvalidDataException($"CVCIE 通道或尺寸无效：{filePath}");
                bitmap = null;
                if (plane.Bpp is 32 or 64)
                {
                    long pixelCount = (long)plane.Cols * plane.Rows;
                    if (pixelCount > int.MaxValue) throw new InvalidDataException($"CVCIE 通道尺寸过大：{filePath}");
                    long planeBytes = pixelCount * (plane.Bpp / 8);
                    if (planeBytes > stream.Length / plane.Channels) throw new InvalidDataException($"CVCIE 内嵌数据长度不足：{filePath}");
                    CvcieDiskPixels pixels = CvcieDiskPixels.Locate(stream, plane, offset, planeBytes * plane.Channels, requireExactLength: false);
                    pixels = pixels with { Offset = pixels.Offset + index * planeBytes };
                    bitmap = MediaHelper.RenderFloatChannel(pixels, plane.Cols, plane.Rows, plane.Bpp, token);
                }
                else
                {
                    plane.Dispose();
                    if (!CVFileUtil.ReadCIEFileChannel(filePath, index, out plane, token))
                        throw new InvalidDataException($"无法读取 CVCIE 通道 {index}：{filePath}");
                }
                plane.FilePath = filePath;
                plane.Channels = 1;
                plane.FileExtType = CVType.Raw;
                return plane;
            }
            catch
            {
                plane.Dispose();
                throw;
            }
        }

        public async void SelectLayer(ImageLayerDescriptor layer) => await SelectLayerAsync(layer);

        internal async Task SelectLayerAsync(ImageLayerDescriptor layer)
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
                // A CVRAW RGB selection is a display choice, not a new document source.
                // Keep the already loaded composite in place so channel changes neither
                // reread the file nor publish a colored intermediate frame.
                if ((!_isCie || (_rawColor != null && _lastSuccessfulLayer.Kind != ImageLayerKind.Derived))
                    && (layer.Id == "composite" || layer.SourceChannelIndex.HasValue))
                {
                    _imageView.ExtractChannel(layer.SourceChannelIndex ?? -1);
                    _lastSuccessfulLayer = layer;
                    return;
                }

                DisplayCache? cache = layer.Id == "cie-srgb" ? _srgbCache : _channelCache;
                (WriteableBitmap Bitmap, ImageLayerDescriptor Layer)? cached = cache != null && cache.LayerId == layer.Id
                    && (layer.Id != "cie-srgb" || (cache.Mode == mode && cache.White == white)) ? (cache.Bitmap, layer) : null;
                var (source, display, displayedLayer) = await PrepareLayerAsync(
                    () => LoadLayer(layer, mode, white, selection.Token), selection.Token, cached);

                if (!IsCurrent(selection)) return;
                StoreCache(displayedLayer.Id, source, mode, white);
                if (!ReferenceEquals(layer, displayedLayer))
                {
                    DefaultLayer = displayedLayer;
                    _imageView.SetLayerController(this);
                    if (!IsCurrent(selection)) return;
                }
                _imageView.SetDisplayLayerSource(display);
                if (IsCurrent(selection))
                {
                    if (displayedLayer.SourceChannelIndex is int channel) _imageView.ExtractChannel(channel);
                    _lastSuccessfulLayer = displayedLayer;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is CvcieFileChangedException or CvcieAssociatedSourceChangedException)
            {
                if (!IsCurrent(selection)) return;
                Log.Warn(ex.Message);
                RestoreLayerSelection();
            }
            catch (Exception ex)
            {
                if (!IsCurrent(selection)) return;
                Log.Warn($"CVCIE 图层 {layer.Id} 显示失败，回退原图：{_filePath}", ex);
                try
                {
                    var fallback = await PrepareLayerAsync(() => LoadSourceLayer(Layers[0], selection.Token), selection.Token);
                    if (!IsCurrent(selection)) return;
                    DefaultLayer = fallback.Layer;
                    _imageView.SetLayerController(this);
                    if (!IsCurrent(selection)) return;
                    _imageView.SetDisplayLayerSource(fallback.Display);
                    if (IsCurrent(selection)) _lastSuccessfulLayer = DefaultLayer;
                }
                catch (OperationCanceledException) { }
                catch (Exception fallbackError)
                {
                    if (!IsCurrent(selection)) return;
                    Log.Warn($"CVRAW 和 Y 灰度回退均不可用，保留当前图像：{_filePath}", fallbackError);
                    RestoreLayerSelection();
                }
            }
            finally
            {
                if (ReferenceEquals(_selection, selection)) _selection = null;
            }
        }

        private void RestoreLayerSelection()
        {
            DefaultLayer = _lastSuccessfulLayer;
            _imageView.SetLayerController(this);
        }

        private bool IsCurrent(CancellationTokenSource selection) => !_disposed && !selection.IsCancellationRequested
            && ReferenceEquals(_selection, selection) && ReferenceEquals(_imageView.ComboBoxLayers.ItemsSource, Layers);

        private async Task<(WriteableBitmap Source, WriteableBitmap Display, ImageLayerDescriptor Layer)> PrepareLayerAsync(
            Func<(WriteableBitmap Bitmap, ImageLayerDescriptor Layer)> load, CancellationToken token,
            (WriteableBitmap Bitmap, ImageLayerDescriptor Layer)? cached = null)
        {
            // Hold the slot through large copies and decoding, while keeping a small cache hit immediate.
            bool useGate = cached is not { } hit || (hit.Bitmap.IsFrozen && EditableBitmapCopy.ShouldCopyInBackground(hit.Bitmap));
            if (useGate) await _loadGate.WaitAsync(token);
            try
            {
                var (bitmap, layer) = cached ?? await Task.Run(() =>
                {
                    // Display loaders may open the path again. Hold this revision until they finish.
                    using FileStream? verified = _fileRevision?.OpenRead(_filePath);
                    return load();
                }, token);
                token.ThrowIfCancellationRequested();
                WriteableBitmap display = bitmap;
                if (bitmap.IsFrozen)
                {
                    display = EditableBitmapCopy.ShouldCopyInBackground(bitmap)
                        ? await EditableBitmapCopy.CreateAsync(bitmap, token)
                        : bitmap.Clone();
                }
                return (bitmap, display, layer);
            }
            finally { if (useGate) _loadGate.Release(); }
        }

        private (WriteableBitmap Bitmap, ImageLayerDescriptor Layer) LoadLayer(ImageLayerDescriptor layer, CvcieBrightnessMode mode, double white, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (layer.Kind != ImageLayerKind.Derived) return LoadSourceLayer(layer, token);
            WriteableBitmap bitmap = layer.Id switch
            {
                "cie-srgb" => _liveXyz == null ? LoadSrgb(_filePath, mode, white, token) : CvcieSrgbRenderer.Render(_liveXyz, mode, white, token),
                "cie-x" => LoadCieChannel(0, token),
                "cie-y" => LoadCieChannel(1, token),
                "cie-z" => LoadCieChannel(2, token),
                _ => throw new InvalidOperationException($"未知的 CVCIE 图层：{layer.Id}"),
            };
            return (bitmap, layer);
        }

        public void CacheSrgb(WriteableBitmap bitmap, CvcieBrightnessMode mode, double white)
        {
            StoreCache("cie-srgb", bitmap, mode, white);
        }

        private void StoreCache(string layerId, WriteableBitmap bitmap, CvcieBrightnessMode mode, double white)
        {
            if (layerId != "cie-srgb" && layerId is not ("cie-x" or "cie-y" or "cie-z")) return;
            // This is extra storage alongside the mutable display clone. Bound the sum, not each slot.
            long stride = (((long)bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 31) / 32) * 4;
            long bytes = stride * bitmap.PixelHeight;
            bool srgb = layerId == "cie-srgb";
            if (srgb) _srgbCache = null;
            else _channelCache = null;
            if (bytes > DisplayCacheBudgetBytes)
            {
                _srgbCache = null;
                _channelCache = null;
                return;
            }

            DisplayCache? other = srgb ? _channelCache : _srgbCache;
            if (bytes + (other?.Bytes ?? 0) > DisplayCacheBudgetBytes)
            {
                if (srgb) _channelCache = null;
                else _srgbCache = null;
            }
            var entry = new DisplayCache(layerId, mode, white, bitmap, bytes);
            if (srgb) _srgbCache = entry;
            else _channelCache = entry;
        }

        public void Dispose()
        {
            _disposed = true;
            _selection?.Cancel();
            _srgbCache = null;
            _channelCache = null;
            _liveSource = null;
        }

        private (WriteableBitmap Bitmap, ImageLayerDescriptor Layer) LoadSourceLayer(ImageLayerDescriptor layer, CancellationToken token)
        {
            BitmapSource? liveSource = _liveSource;
            if (liveSource != null)
            {
                if (liveSource is WriteableBitmap writable) return (writable, layer);
                // Decoded samples may be RGB while native calibration uses planar RGB or BGR.
                WriteableBitmap bitmap = new(liveSource);
                bitmap.Freeze();
                return (bitmap, layer);
            }
            if (_rawColor != null)
            {
                return (_rawColor.BorrowRaw(raw => ConvertForDisplay(raw, token)), layer);
            }
            if (_associatedSource is { } associated)
            {
                using FileStream stream = associated.OpenRead();
                using CvcieDisplaySource source = ReadAssociatedImage(stream, _filePath, associated.Rows, associated.Cols, loadPixels: true, token);
                if (source.File.Channels < 3 && layer.SourceChannelIndex.HasValue) layer = Layers[0];
                return (ConvertForDisplay(source.File, token, source.Decoded), layer);
            }
            if (!_isCie)
            {
                using CVCIEFile source = CVFileUtil.OpenLocalCVFile(_filePath, CVType.Raw);
                return (ConvertForDisplay(source, token), layer);
            }
            using CVCIEFile plane = LoadFileChannel(_filePath, 1, out WriteableBitmap? planeBitmap, token);
            layer = Layers.FirstOrDefault(item => item.Id == "cie-y") ?? Layers[0];
            return (planeBitmap ?? ConvertForDisplay(plane, token), layer);
        }

        private WriteableBitmap LoadCieChannel(int index, CancellationToken token)
        {
            if (_rawColor != null)
            {
                using CVCIEFile rawPlane = new()
                {
                    Cols = _rawColor.Width, Rows = _rawColor.Height, Bpp = 32, Channels = 1, FileExtType = CVType.Raw,
                    Data = _rawColor.CreateChannel(_rawColor.Channels == 1 ? 0 : index, token),
                };
                return ConvertForDisplay(rawPlane, token);
            }
            if (_liveXyz == null)
            {
                using CVCIEFile planeFile = LoadFileChannel(_filePath, index, out WriteableBitmap? bitmap, token);
                return bitmap ?? ConvertForDisplay(planeFile, token);
            }

            if (_liveXyz.Channels == 1) index = 0;
            int length = checked(_liveXyz.Cols * _liveXyz.Rows * (_liveXyz.Bpp / 8));
            int offset = checked(index * length);
            if (_liveXyz.Bpp is 32 or 64)
            {
                // The capture already owns the measurement data. Retain only a read-only view of this plane.
                ReadOnlyMemory<byte> samples = _liveXyz.Data.AsMemory(offset, length);
                return MediaHelper.RenderFloatChannel(samples, _liveXyz.Cols, _liveXyz.Rows, _liveXyz.Bpp, token);
            }

            byte[] data = new byte[length];
            Buffer.BlockCopy(_liveXyz.Data, offset, data, 0, length);
            using CVCIEFile plane = new()
            {
                Cols = _liveXyz.Cols, Rows = _liveXyz.Rows, Bpp = _liveXyz.Bpp,
                Channels = 1, FileExtType = CVType.Raw, Data = data,
            };
            return ConvertForDisplay(plane, token);
        }

        internal static WriteableBitmap ConvertForDisplay(CVCIEFile file, CancellationToken token, OpenCvSharp.Mat? decoded = null)
        {
            token.ThrowIfCancellationRequested();
            if (file.Channels == 1 && file.Bpp is 32 or 64) return MediaHelper.RenderFloatChannel(file, token);
            using OpenCvSharp.Mat? loaded = decoded == null ? file.ToMat(showErrors: false) : null;
            OpenCvSharp.Mat? mat = decoded ?? loaded;
            if (mat == null || mat.Empty()) throw new InvalidDataException("原图或通道数据无法显示。");
            WriteableBitmap bitmap = mat.CreateDisplayBitmap();
            bitmap.Freeze();
            return bitmap;
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
                    DisplayName = LocalizedText.Get("真彩 sRGB（XYZ）"),
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
