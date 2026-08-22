using ColorVision.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public enum ImageViewSnapshotFormat
    {
        Png = 0,
        Jpeg = 1,
    }

    public sealed class ImageViewSnapshotSaveOptions
    {
        public static ImageViewSnapshotSaveOptions Default { get; } = new();

        public ImageViewSnapshotFormat Format { get; init; } = ImageViewSnapshotFormat.Png;
        public int ScaleDivisor { get; init; } = 1;
        public int JpegQuality { get; init; } = 100;
    }

    public enum ImageViewSourceFormat
    {
        Png = 0,
        Tiff = 1,
        Bmp = 2,
    }

    public enum ImageViewTiffCompression
    {
        Lzw = 0,
        Zip = 1,
    }

    public sealed class ImageViewSourceSaveOptions
    {
        public static ImageViewSourceSaveOptions Default { get; } = new();

        public ImageViewSourceFormat Format { get; init; } = ImageViewSourceFormat.Png;
        public ImageViewTiffCompression TiffCompression { get; init; } = ImageViewTiffCompression.Lzw;
    }

    public sealed class ImageViewSnapshotExportOptions
    {
        public string? RenderedFileName { get; init; }
        public ImageViewSnapshotSaveOptions RenderedOptions { get; init; } = ImageViewSnapshotSaveOptions.Default;
        public string? SourceFileName { get; init; }
        public ImageViewSourceSaveOptions SourceOptions { get; init; } = ImageViewSourceSaveOptions.Default;
    }

    /// <summary>
    /// Owns the immutable image data and drawings required for a background save.
    /// </summary>
    public sealed class ImageViewSnapshot : IDisposable
    {
        private readonly object imageBufferLock = new();
        private SnapshotImageBufferLease? imageBuffer;

        internal DrawingGroup Scene { get; }
        internal BitmapSource? FrozenSource { get; }
        public int PixelWidth { get; }
        public int PixelHeight { get; }
        internal double DpiX { get; }
        internal double DpiY { get; }

        private ImageViewSnapshot(
            DrawingGroup scene,
            int pixelWidth,
            int pixelHeight,
            double dpiX,
            double dpiY,
            BitmapSource? frozenSource = null,
            SnapshotImageBufferLease? imageBuffer = null)
        {
            Scene = scene;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            DpiX = dpiX;
            DpiY = dpiY;
            FrozenSource = frozenSource;
            this.imageBuffer = imageBuffer;
        }

        public static ImageViewSnapshot Create(
            DrawingGroup scene,
            int pixelWidth,
            int pixelHeight,
            double dpiX = 96,
            double dpiY = 96)
        {
            ArgumentNullException.ThrowIfNull(scene);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);
            if (dpiX <= 0 || double.IsNaN(dpiX) || double.IsInfinity(dpiX))
                throw new ArgumentOutOfRangeException(nameof(dpiX));
            if (dpiY <= 0 || double.IsNaN(dpiY) || double.IsInfinity(dpiY))
                throw new ArgumentOutOfRangeException(nameof(dpiY));

            if (!scene.IsFrozen)
                scene.Freeze();
            return new ImageViewSnapshot(scene, pixelWidth, pixelHeight, dpiX, dpiY);
        }

        internal static ImageViewSnapshot Create(
            DrawingGroup overlays,
            SnapshotImageBufferLease imageBuffer,
            int pixelWidth,
            int pixelHeight,
            double dpiX,
            double dpiY)
        {
            if (!overlays.IsFrozen)
                overlays.Freeze();
            return new ImageViewSnapshot(
                overlays,
                pixelWidth,
                pixelHeight,
                dpiX,
                dpiY,
                null,
                imageBuffer);
        }

        internal static ImageViewSnapshot Create(
            DrawingGroup overlays,
            BitmapSource frozenSource,
            int pixelWidth,
            int pixelHeight,
            double dpiX,
            double dpiY)
        {
            if (!overlays.IsFrozen)
                overlays.Freeze();
            if (!frozenSource.IsFrozen)
                frozenSource.Freeze();
            return new ImageViewSnapshot(
                overlays,
                pixelWidth,
                pixelHeight,
                dpiX,
                dpiY,
                frozenSource);
        }

        internal SnapshotImageBufferLease? TakeImageBuffer()
        {
            lock (imageBufferLock)
            {
                SnapshotImageBufferLease? buffer = imageBuffer;
                imageBuffer = null;
                return buffer;
            }
        }

        public void Dispose()
        {
            TakeImageBuffer()?.Dispose();
            GC.SuppressFinalize(this);
        }

        ~ImageViewSnapshot() => Dispose();
    }

    internal sealed class SnapshotImageBufferLease : IDisposable
    {
        private readonly SnapshotImageBufferPool owner;
        private readonly PixelFormat format;
        private readonly Color[]? paletteColors;
        private readonly int generation;
        private HImage? image;

        internal SnapshotImageBufferLease(
            SnapshotImageBufferPool owner,
            HImage image,
            PixelFormat format,
            Color[]? paletteColors,
            int generation)
        {
            this.owner = owner;
            this.image = image;
            this.format = format;
            this.paletteColors = paletteColors;
            this.generation = generation;
        }

        internal HImage Image => image ?? throw new ObjectDisposedException(nameof(SnapshotImageBufferLease));

        internal WriteableBitmap ToWriteableBitmap(double dpiX, double dpiY)
        {
            HImage buffer = Image;
            BitmapPalette? palette = paletteColors == null ? null : new BitmapPalette(paletteColors);
            WriteableBitmap bitmap = new(
                buffer.cols,
                buffer.rows,
                dpiX,
                dpiY,
                format,
                palette);
            int bytesPerRow = GetPackedRowBytes(buffer.cols, format.BitsPerPixel);
            if (buffer.stride < bytesPerRow || bitmap.BackBufferStride < bytesPerRow)
                throw new InvalidOperationException("Snapshot image buffer stride is invalid.");

            int bufferSize = checked(buffer.stride * buffer.rows);
            bitmap.WritePixels(
                new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight),
                buffer.pData,
                bufferSize,
                buffer.stride);
            return bitmap;
        }

        private static int GetPackedRowBytes(int width, int bitsPerPixel)
        {
            return checked((width * bitsPerPixel + 7) / 8);
        }

        public void Dispose()
        {
            HImage? buffer = image;
            image = null;
            if (buffer.HasValue)
                owner.Return(buffer.Value, format, generation);
            GC.SuppressFinalize(this);
        }

        ~SnapshotImageBufferLease() => Dispose();
    }

    internal sealed class SnapshotImageBufferPool
    {
        private readonly object sync = new();
        private HImage? cachedImage;
        private PixelFormat cachedFormat;
        private int generation;

        internal SnapshotImageBufferLease Capture(WriteableBitmap source)
        {
            HImage? buffer = null;
            Color[]? paletteColors = source.Palette == null ? null : [.. source.Palette.Colors];
            int leaseGeneration;
            lock (sync)
            {
                if (cachedImage.HasValue && IsCompatible(cachedImage.Value, cachedFormat, source))
                {
                    buffer = cachedImage;
                    cachedImage = null;
                }
                else if (cachedImage.HasValue)
                {
                    HImage staleImage = cachedImage.Value;
                    cachedImage = null;
                    staleImage.Dispose();
                }
                leaseGeneration = generation;
            }

            if (!buffer.HasValue)
            {
                HImage allocatedImage = AllocateBuffer(source);
                try
                {
                    CopyToBuffer(source, allocatedImage);
                    return new SnapshotImageBufferLease(
                        this,
                        allocatedImage,
                        source.Format,
                        paletteColors,
                        leaseGeneration);
                }
                catch
                {
                    allocatedImage.Dispose();
                    throw;
                }
            }

            HImage image = buffer.Value;
            try
            {
                CopyToBuffer(source, image);
                return new SnapshotImageBufferLease(
                    this,
                    image,
                    source.Format,
                    paletteColors,
                    leaseGeneration);
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }

        internal void Return(HImage image, PixelFormat format, int leaseGeneration)
        {
            lock (sync)
            {
                if (leaseGeneration == generation && !cachedImage.HasValue)
                {
                    cachedImage = image;
                    cachedFormat = format;
                    return;
                }
            }
            image.Dispose();
        }

        internal void Release()
        {
            HImage? image;
            lock (sync)
            {
                generation++;
                image = cachedImage;
                cachedImage = null;
            }
            if (image.HasValue)
            {
                HImage value = image.Value;
                value.Dispose();
            }
        }

        private static bool IsCompatible(HImage image, PixelFormat format, WriteableBitmap source)
        {
            return image.rows == source.PixelHeight
                && image.cols == source.PixelWidth
                && image.stride == GetPackedRowBytes(source.PixelWidth, source.Format.BitsPerPixel)
                && format.Equals(source.Format);
        }

        private static HImage AllocateBuffer(WriteableBitmap source)
        {
            int stride = GetPackedRowBytes(source.PixelWidth, source.Format.BitsPerPixel);
            int length = checked(stride * source.PixelHeight);
            return new HImage
            {
                rows = source.PixelHeight,
                cols = source.PixelWidth,
                channels = 1,
                depth = 8,
                stride = stride,
                pData = Marshal.AllocCoTaskMem(length),
            };
        }

        private static void CopyToBuffer(WriteableBitmap source, HImage image)
        {
            int bytesPerRow = GetPackedRowBytes(source.PixelWidth, source.Format.BitsPerPixel);
            if (source.BackBufferStride < bytesPerRow || image.stride < bytesPerRow)
                throw new InvalidOperationException("Snapshot image buffer stride is invalid.");

            int bufferSize = checked(image.stride * image.rows);
            source.CopyPixels(Int32Rect.Empty, image.pData, bufferSize, image.stride);
        }

        private static int GetPackedRowBytes(int width, int bitsPerPixel)
        {
            return checked((width * bitsPerPixel + 7) / 8);
        }
    }

    public partial class ImageView
    {
        private readonly SnapshotImageBufferPool snapshotBufferPool = new();

        public void ReleaseSnapshotBuffer() => snapshotBufferPool.Release();

        /// <summary>
        /// Copies mutable WriteableBitmap pixels once and clones only the existing drawings.
        /// </summary>
        public ImageViewSnapshot? CaptureSnapshotForBackgroundSave()
        {
            return CaptureSnapshotForBackgroundSave(includeOverlays: true);
        }

        public ImageViewSnapshot? CaptureSnapshotForBackgroundSave(bool includeOverlays)
        {
            Dispatcher.VerifyAccess();
            BitmapSource? source = ViewBitmapSource as BitmapSource
                ?? ImageShow.Source as BitmapSource;
            if (source == null)
                return null;

            SnapshotImageBufferLease? imageBuffer = null;
            BitmapSource? frozenSource = null;
            try
            {
                DrawingGroup scene = new();
                if (source is WriteableBitmap writeableBitmap && !writeableBitmap.IsFrozen)
                {
                    imageBuffer = snapshotBufferPool.Capture(writeableBitmap);
                }
                else
                {
                    frozenSource = source.IsFrozen ? source : source.CloneCurrentValue();
                    if (!frozenSource.IsFrozen)
                        frozenSource.Freeze();
                }

                if (includeOverlays)
                {
                    foreach (Visual visual in ImageShow.Visuals)
                    {
                        DrawingGroup? drawing = CloneVisualDrawing(visual);
                        if (drawing == null)
                        {
                            log.WarnFormat(
                                "ImageView background snapshot does not support visual type {0}.",
                                visual.GetType().FullName);
                            return null;
                        }
                        scene.Children.Add(drawing);
                    }
                }

                double dpiX = GetPositiveDpi(Config.GetProperties<double>("DpiX"));
                double dpiY = GetPositiveDpi(Config.GetProperties<double>("DpiY"));
                if (imageBuffer != null)
                {
                    ImageViewSnapshot snapshot = ImageViewSnapshot.Create(
                        scene,
                        imageBuffer,
                        source.PixelWidth,
                        source.PixelHeight,
                        dpiX,
                        dpiY);
                    imageBuffer = null;
                    return snapshot;
                }

                return ImageViewSnapshot.Create(
                    scene,
                    frozenSource!,
                    source.PixelWidth,
                    source.PixelHeight,
                    dpiX,
                    dpiY);
            }
            catch (Exception ex)
            {
                log.Warn("Unable to prepare an ImageView background snapshot.", ex);
                return null;
            }
            finally
            {
                imageBuffer?.Dispose();
            }
        }

        /// <summary>
        /// Consumes, composes, and encodes a snapshot on a serialized STA worker.
        /// </summary>
        public static Task SaveSnapshotAsync(
            ImageViewSnapshot snapshot,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            return SaveSnapshotWithOptionsAsync(
                snapshot,
                fileName,
                ImageViewSnapshotSaveOptions.Default,
                cancellationToken);
        }

        public static async Task SaveSnapshotWithOptionsAsync(
            ImageViewSnapshot snapshot,
            string fileName,
            ImageViewSnapshotSaveOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            await SaveSnapshotExportsAsync(
                snapshot,
                new ImageViewSnapshotExportOptions
                {
                    RenderedFileName = fileName,
                    RenderedOptions = options,
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Saves the rendered 8-bit scene, the original loaded pixels, or both from one captured snapshot.
        /// The source branch bypasses WPF scene rendering so its original pixel format and bit depth are retained.
        /// </summary>
        public static async Task SaveSnapshotExportsAsync(
            ImageViewSnapshot snapshot,
            ImageViewSnapshotExportOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(options);
            try
            {
                bool saveRendered = !string.IsNullOrWhiteSpace(options.RenderedFileName);
                bool saveSource = !string.IsNullOrWhiteSpace(options.SourceFileName);
                if (!saveRendered && !saveSource)
                    return;
                ArgumentNullException.ThrowIfNull(options.RenderedOptions);
                ArgumentNullException.ThrowIfNull(options.SourceOptions);
                if (saveRendered
                    && saveSource
                    && string.Equals(
                        Path.GetFullPath(options.RenderedFileName!),
                        Path.GetFullPath(options.SourceFileName!),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Rendered and source image exports must use different file paths.", nameof(options));
                }

                await RunOnSnapshotStaThreadAsync(
                    () => RenderAndSaveSnapshotExports(snapshot, options, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static DrawingGroup? CloneVisualDrawing(Visual visual)
        {
            if (visual is not DrawingVisual drawingVisual
                || drawingVisual.Effect != null
                || drawingVisual.CacheMode != null)
            {
                return null;
            }

            DrawingGroup drawing = new();
            drawing.Children.Add(drawingVisual.Drawing.CloneCurrentValue());

            TransformGroup transforms = new();
            if (drawingVisual.Transform != null && !drawingVisual.Transform.Value.IsIdentity)
                transforms.Children.Add(drawingVisual.Transform.CloneCurrentValue());
            Vector offset = drawingVisual.Offset;
            if (offset.X != 0 || offset.Y != 0)
                transforms.Children.Add(new TranslateTransform(offset.X, offset.Y));
            if (transforms.Children.Count > 0)
                drawing.Transform = transforms;

            if (drawingVisual.Clip != null)
                drawing.ClipGeometry = drawingVisual.Clip.CloneCurrentValue();
            drawing.Opacity = drawingVisual.Opacity;
            if (drawingVisual.OpacityMask != null)
                drawing.OpacityMask = drawingVisual.OpacityMask.CloneCurrentValue();
            return drawing;
        }

        private static void RenderAndSaveSnapshotExports(
            ImageViewSnapshot snapshot,
            ImageViewSnapshotExportOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BitmapSource? source = MaterializeSnapshotSource(snapshot);

            if (!string.IsNullOrWhiteSpace(options.RenderedFileName))
            {
                DrawingGroup scene = ComposeSnapshotScene(snapshot, source);
                RenderAndSaveSnapshot(
                    snapshot,
                    scene,
                    options.RenderedFileName,
                    options.RenderedOptions,
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(options.SourceFileName))
            {
                if (source == null)
                    throw new InvalidOperationException("This snapshot does not contain original source pixels.");
                SaveSourceSnapshot(source, options.SourceFileName, options.SourceOptions, cancellationToken);
            }
        }

        private static BitmapSource? MaterializeSnapshotSource(ImageViewSnapshot snapshot)
        {
            SnapshotImageBufferLease? buffer = snapshot.TakeImageBuffer();
            if (buffer != null)
            {
                WriteableBitmap source;
                try
                {
                    source = buffer.ToWriteableBitmap(snapshot.DpiX, snapshot.DpiY);
                    source.Freeze();
                }
                finally
                {
                    buffer.Dispose();
                }
                return source;
            }
            return snapshot.FrozenSource;
        }

        private static DrawingGroup ComposeSnapshotScene(ImageViewSnapshot snapshot, BitmapSource? source)
        {
            if (source == null)
                return snapshot.Scene;

            DrawingGroup composedScene = new();
            composedScene.Children.Add(new ImageDrawing(
                source,
                new Rect(0, 0, snapshot.PixelWidth, snapshot.PixelHeight)));
            composedScene.Children.Add(snapshot.Scene);
            composedScene.Freeze();
            return composedScene;
        }

        private static void RenderAndSaveSnapshot(
            ImageViewSnapshot snapshot,
            DrawingGroup scene,
            string fileName,
            ImageViewSnapshotSaveOptions options,
            CancellationToken cancellationToken)
        {

            (int outputWidth, int outputHeight) = GetSnapshotOutputSize(snapshot, options.ScaleDivisor);
            DrawingVisual visual = new();
            using (DrawingContext context = visual.RenderOpen())
            {
                if (outputWidth != snapshot.PixelWidth || outputHeight != snapshot.PixelHeight)
                {
                    context.PushTransform(new ScaleTransform(
                        outputWidth / (double)snapshot.PixelWidth,
                        outputHeight / (double)snapshot.PixelHeight));
                    context.DrawDrawing(scene);
                    context.Pop();
                }
                else
                {
                    context.DrawDrawing(scene);
                }
            }

            RenderTargetBitmap renderedBitmap = new(
                outputWidth,
                outputHeight,
                snapshot.DpiX,
                snapshot.DpiY,
                PixelFormats.Pbgra32);
            renderedBitmap.Render(visual);
            cancellationToken.ThrowIfCancellationRequested();
            SaveSnapshot(renderedBitmap, fileName, options, cancellationToken);
        }

        private static (int Width, int Height) GetSnapshotOutputSize(
            ImageViewSnapshot snapshot,
            int scaleDivisor)
        {
            int normalizedDivisor = scaleDivisor is 2 or 4 ? scaleDivisor : 1;
            if (normalizedDivisor == 1)
                return (snapshot.PixelWidth, snapshot.PixelHeight);

            return (
                Math.Max(1, (int)Math.Round(snapshot.PixelWidth / (double)normalizedDivisor, MidpointRounding.AwayFromZero)),
                Math.Max(1, (int)Math.Round(snapshot.PixelHeight / (double)normalizedDivisor, MidpointRounding.AwayFromZero)));
        }

        private static Task RunOnSnapshotStaThreadAsync(
            Action action,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            TaskCompletionSource<object?> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Thread thread = new(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    action();
                    completion.TrySetResult(null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "ColorVision Image Snapshot Renderer",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }
    }
}
