using ColorVision.Core;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// Owns the immutable image data and drawings required for a background save.
    /// </summary>
    public sealed class ImageViewSnapshot : IDisposable
    {
        private readonly object imageBufferLock = new();
        private SnapshotImageBufferLease? imageBuffer;

        internal DrawingGroup Scene { get; }
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
            SnapshotImageBufferLease? imageBuffer = null)
        {
            Scene = scene;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            DpiX = dpiX;
            DpiY = dpiY;
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
                imageBuffer);
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
        private readonly int generation;
        private HImage? image;

        internal SnapshotImageBufferLease(
            SnapshotImageBufferPool owner,
            HImage image,
            PixelFormat format,
            int generation)
        {
            this.owner = owner;
            this.image = image;
            this.format = format;
            this.generation = generation;
        }

        internal HImage Image => image ?? throw new ObjectDisposedException(nameof(SnapshotImageBufferLease));

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
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        private readonly object sync = new();
        private HImage? cachedImage;
        private PixelFormat cachedFormat;
        private int generation;

        internal SnapshotImageBufferLease Capture(WriteableBitmap source)
        {
            HImage? buffer = null;
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
                return new SnapshotImageBufferLease(this, source.ToHImage(), source.Format, leaseGeneration);

            HImage image = buffer.Value;
            try
            {
                CopyToBuffer(source, image);
                return new SnapshotImageBufferLease(this, image, source.Format, leaseGeneration);
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
                && image.stride == source.PixelWidth * source.Format.BitsPerPixel / 8
                && format.Equals(source.Format);
        }

        private static void CopyToBuffer(WriteableBitmap source, HImage image)
        {
            int bytesPerRow = image.cols * image.channels * (image.depth / 8);
            if (source.BackBufferStride < bytesPerRow || image.stride < bytesPerRow)
                throw new InvalidOperationException("Snapshot image buffer stride is invalid.");

            source.Lock();
            try
            {
                for (int y = 0; y < image.rows; y++)
                {
                    CopyMemory(
                        IntPtr.Add(image.pData, y * image.stride),
                        IntPtr.Add(source.BackBuffer, y * source.BackBufferStride),
                        (uint)bytesPerRow);
                }
            }
            finally
            {
                source.Unlock();
            }
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
            Dispatcher.VerifyAccess();
            if (ImageShow.Source is not BitmapSource source)
                return null;

            SnapshotImageBufferLease? imageBuffer = null;
            try
            {
                DrawingGroup scene = new();
                if (source is WriteableBitmap writeableBitmap && !writeableBitmap.IsFrozen)
                {
                    imageBuffer = snapshotBufferPool.Capture(writeableBitmap);
                }
                else
                {
                    BitmapSource frozenSource = source.IsFrozen ? source : source.CloneCurrentValue();
                    if (!frozenSource.IsFrozen)
                        frozenSource.Freeze();
                    scene.Children.Add(new ImageDrawing(
                        frozenSource,
                        new Rect(0, 0, source.PixelWidth, source.PixelHeight)));
                }

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
        public static async Task SaveSnapshotAsync(
            ImageViewSnapshot snapshot,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    return;

                await SnapshotSaveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await RunOnSnapshotStaThreadAsync(
                        () => RenderAndSaveSnapshot(snapshot, fileName, cancellationToken),
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    SnapshotSaveGate.Release();
                }
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

        private static void RenderAndSaveSnapshot(
            ImageViewSnapshot snapshot,
            string fileName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DrawingGroup scene = snapshot.Scene;
            SnapshotImageBufferLease? buffer = snapshot.TakeImageBuffer();
            if (buffer != null)
            {
                WriteableBitmap source;
                try
                {
                    source = buffer.Image.ToWriteableBitmap(snapshot.DpiX, snapshot.DpiY);
                    source.Freeze();
                }
                finally
                {
                    buffer.Dispose();
                }

                DrawingGroup composedScene = new();
                composedScene.Children.Add(new ImageDrawing(
                    source,
                    new Rect(0, 0, snapshot.PixelWidth, snapshot.PixelHeight)));
                composedScene.Children.Add(scene);
                composedScene.Freeze();
                scene = composedScene;
            }

            DrawingVisual visual = new();
            using (DrawingContext context = visual.RenderOpen())
                context.DrawDrawing(scene);

            RenderTargetBitmap renderedBitmap = new(
                snapshot.PixelWidth,
                snapshot.PixelHeight,
                snapshot.DpiX,
                snapshot.DpiY,
                PixelFormats.Pbgra32);
            renderedBitmap.Render(visual);
            cancellationToken.ThrowIfCancellationRequested();
            SaveSnapshot(renderedBitmap, fileName, cancellationToken);
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
