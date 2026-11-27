using ColorVision.Core;
using ColorVision.ImageEditor.Presentation;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Output
{
    internal sealed class SnapshotImageBufferLease : IDisposable
    {
        private const int ParallelConversionMinimumBytes = 128 * 1024 * 1024;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(SnapshotImageBufferLease));

        private readonly SnapshotImageBufferPool owner;
        private readonly PixelFormat format;
        private readonly Color[]? paletteColors;
        private readonly int generation;
        private HImage? image;
        private int disposed;

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

        internal BitmapSource ToFrozenBitmap(double dpiX, double dpiY)
        {
            HImage buffer = Image;
            BitmapPalette? palette = paletteColors == null ? null : new BitmapPalette(paletteColors);
            int bytesPerRow = GetPackedRowBytes(buffer.cols, format.BitsPerPixel);
            if (buffer.stride < bytesPerRow)
                throw new InvalidOperationException("Snapshot image buffer stride is invalid.");

            int bufferSize = checked(buffer.stride * buffer.rows);
            BitmapSource bitmap;
            if (format.BitsPerPixel % 8 == 0)
            {
                // This factory copies the leased pixels into independently owned WIC storage.
                bitmap = BitmapSource.Create(buffer.cols, buffer.rows, dpiX, dpiY, format, palette,
                    buffer.pData, bufferSize, buffer.stride);
            }
            else
            {
                // Preserve WritePixels handling of unused bits at the ends of packed rows.
                WriteableBitmap packed = new(buffer.cols, buffer.rows, dpiX, dpiY, format, palette);
                if (packed.BackBufferStride < bytesPerRow)
                    throw new InvalidOperationException("Snapshot image buffer stride is invalid.");
                packed.WritePixels(new Int32Rect(0, 0, buffer.cols, buffer.rows), buffer.pData, bufferSize, buffer.stride);
                bitmap = packed;
            }
            bitmap.Freeze();
            GC.KeepAlive(this);
            return bitmap;
        }

        internal BitmapSource? TryCreateOpaqueRgb48Bitmap(int width, int height, double dpiX, double dpiY, bool freezeForEncoding)
        {
            HImage buffer = Image;
            if (format != PixelFormats.Rgb48 || !Ssse3.IsSupported || Environment.ProcessorCount < 2
                || buffer.cols != width || buffer.rows != height || buffer.stride < width * 6L
                || (long)buffer.stride * height < ParallelConversionMinimumBytes
                || width * 4L * height > int.MaxValue)
                return null;

            try
            {
                WriteableBitmap? scratch = SnapshotStaWorker.TakeOpaqueBackground(width, height, dpiX, dpiY);
                WriteableBitmap bitmap = scratch ?? new(width, height, dpiX, dpiY, PixelFormats.Pbgra32, null);
                bitmap.Lock();
                try
                {
                    nint target = bitmap.BackBuffer;
                    int targetStride = bitmap.BackBufferStride;
                    ImageRenderProxy.CopyRgb48InTwoBands(buffer.pData, buffer.stride, target, targetStride, width, height);
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                }
                finally { bitmap.Unlock(); }
                if (scratch == null || freezeForEncoding)
                    bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex) when (ex is OutOfMemoryException or COMException or ArgumentException)
            {
                log.Warn("Unable to prepare the opaque RGB48 snapshot. Falling back to the original pixels.", ex);
                return null;
            }
            finally { GC.KeepAlive(this); }
        }

        private static int GetPackedRowBytes(int width, int bitsPerPixel)
        {
            return checked((width * bitsPerPixel + 7) / 8);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            HImage? buffer = image;
            image = null;
            if (buffer.HasValue)
                owner.Return(buffer.Value, format, generation);
            GC.SuppressFinalize(this);
        }

        ~SnapshotImageBufferLease() => Dispose();
    }

    internal sealed class SnapshotImageBufferPool : IDisposable
    {
        private const int ParallelCopyMinimumBytes = 128 * 1024 * 1024;
        private static long nextId;
        private readonly object sync = new();
        private HImage? cachedImage;
        private PixelFormat cachedFormat;
        private int generation;
        private long activeBytes;
        private long pendingReleaseBytes;
        private int activeCount;
        private long hitCount;
        private bool isClosed;
        private string sourceName = string.Empty;

        internal long Id { get; }

        internal SnapshotImageBufferPool()
        {
            Id = Interlocked.Increment(ref nextId);
            SnapshotBufferCache.Register(this);
        }

        internal void SetSourceName(string value)
        {
            lock (sync) sourceName = value;
        }

        internal SnapshotBufferCacheEntry GetSnapshot()
        {
            lock (sync)
                return new(Id, sourceName, cachedImage.HasValue ? GetBytes(cachedImage.Value) : 0, activeBytes,
                    pendingReleaseBytes, activeCount, hitCount, cachedImage?.cols ?? 0, cachedImage?.rows ?? 0,
                    cachedImage.HasValue ? cachedFormat.ToString() : string.Empty, isClosed);
        }

        internal SnapshotImageBufferLease Capture(WriteableBitmap source)
        {
            HImage? buffer = null;
            Color[]? paletteColors = source.Palette == null ? null : [.. source.Palette.Colors];
            int leaseGeneration;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(isClosed, this);
                if (cachedImage.HasValue && IsCompatible(cachedImage.Value, cachedFormat, source))
                {
                    buffer = cachedImage;
                    cachedImage = null;
                    hitCount++;
                }
                else if (cachedImage.HasValue)
                {
                    HImage staleImage = cachedImage.Value;
                    cachedImage = null;
                    staleImage.Dispose();
                }
                buffer ??= AllocateBuffer(source);
                activeBytes += GetBytes(buffer.Value);
                activeCount++;
                leaseGeneration = generation;
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
                Return(image, source.Format, leaseGeneration, reusable: false);
                throw;
            }
        }

        internal void Return(HImage image, PixelFormat format, int leaseGeneration, bool reusable = true)
        {
            lock (sync)
            {
                activeBytes -= GetBytes(image);
                activeCount--;
                if (leaseGeneration != generation) pendingReleaseBytes -= GetBytes(image);
                if (reusable && !isClosed && leaseGeneration == generation && !cachedImage.HasValue)
                {
                    cachedImage = image;
                    cachedFormat = format;
                    return;
                }
            }
            image.Dispose();
        }

        internal SnapshotBufferReleaseResult Release()
        {
            lock (sync)
            {
                return ReleaseCore();
            }
        }

        private SnapshotBufferReleaseResult ReleaseCore()
        {
            generation++;
            pendingReleaseBytes = activeBytes;
            long released = cachedImage.HasValue ? GetBytes(cachedImage.Value) : 0;
            if (cachedImage.HasValue)
            {
                HImage value = cachedImage.Value;
                cachedImage = null;
                value.Dispose();
            }
            return new(released, pendingReleaseBytes);
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (isClosed) return;
                isClosed = true;
                ReleaseCore();
            }
            GC.SuppressFinalize(this);
        }

        ~SnapshotImageBufferPool() => Dispose();

        private static long GetBytes(HImage image) => (long)image.stride * image.rows;

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
            if (!TryCopyLargeRgb48(source, image, bufferSize))
                source.CopyPixels(Int32Rect.Empty, image.pData, bufferSize, image.stride);
        }

        private static unsafe bool TryCopyLargeRgb48(WriteableBitmap source, HImage image, int bufferSize)
        {
            if (bufferSize < ParallelCopyMinimumBytes || Environment.ProcessorCount < 2 || source.IsFrozen
                || source.Format != PixelFormats.Rgb48 || source.BackBufferStride != image.stride
                || !source.TryLock(new Duration(TimeSpan.Zero)))
                return false;

            try
            {
                // Keep the read-only back buffer locked until both copies finish.
                nint pixels = source.BackBuffer;
                nint target = image.pData;
                int first = bufferSize / 2;
                int second = bufferSize - first;
                Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = 2, TaskScheduler = TaskScheduler.Default },
                    () => Buffer.MemoryCopy(pixels.ToPointer(), target.ToPointer(), first, first),
                    () => Buffer.MemoryCopy((pixels + first).ToPointer(), (target + first).ToPointer(), second, second));
                return true;
            }
            finally { source.Unlock(); }
        }

        private static int GetPackedRowBytes(int width, int bitsPerPixel)
        {
            return checked((width * bitsPerPixel + 7) / 8);
        }
    }

}
