using ColorVision.ImageEditor.Output;
using System;
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

}
