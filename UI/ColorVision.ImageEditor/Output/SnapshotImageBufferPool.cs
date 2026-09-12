using ColorVision.Core;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Output
{
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

}
