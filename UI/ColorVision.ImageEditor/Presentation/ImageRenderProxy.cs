using log4net;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Presentation;

/// <summary>
/// Prepares the composition-ready copy of a frozen frame off the UI thread. WPF otherwise converts
/// these formats on the UI thread when the bitmap first reaches the compositor, which stalls input
/// for a full-frame conversion on every large frame. Only drawing uses the copy; every pixel consumer
/// keeps reading the original source and its bit depth.
/// </summary>
internal static class ImageRenderProxy
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ImageRenderProxy));
    private static uint[]? _gray16Colors;

    private static bool IsRequired(PixelFormat format)
        => format == PixelFormats.Bgr24 || format == PixelFormats.Rgb24 || format == PixelFormats.Gray16 || format == PixelFormats.Rgb48;

    internal static WriteableBitmap? TryCreate(WriteableBitmap source)
    {
        PixelFormat format = source.Format;
        if (!IsRequired(format)) return null;
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        if (width * 4L * height > int.MaxValue) return null;

        try
        {
            WriteableBitmap proxy = new(width, height, source.DpiX, source.DpiY, PixelFormats.Bgr32, null);
            Int32Rect all = new(0, 0, width, height);
            proxy.Lock();
            try
            {
                if (format == PixelFormats.Bgr24 || (format == PixelFormats.Rgb24 && source.IsFrozen))
                    Copy24(source, proxy, sourceIsRgb: format == PixelFormats.Rgb24);
                else if (format == PixelFormats.Rgb48 && HasFrozenBackBuffer(source)) CopyRgb48(source, proxy);
                else if (format == PixelFormats.Gray16 && HasFrozenBackBuffer(source)) CopyGray16(source, proxy);
                else new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0).CopyPixels(all, proxy.BackBuffer, proxy.BackBufferStride * height, proxy.BackBufferStride);
                proxy.AddDirtyRect(all);
            }
            finally { proxy.Unlock(); }
            proxy.Freeze();
            return proxy;
        }
        catch (Exception ex) when (ex is OutOfMemoryException or COMException or ArgumentException)
        {
            // The source stays displayable; only the conversion returns to the compositor path.
            Log.Warn("Unable to prepare a display copy for a continuous frame.", ex);
            return null;
        }
    }

    private static bool HasFrozenBackBuffer(WriteableBitmap source)
        => source.IsFrozen && source.GetType() == typeof(WriteableBitmap);

    private static unsafe void Copy24(WriteableBitmap source, WriteableBitmap target, bool sourceIsRgb)
    {
        byte* sourceBase = (byte*)source.BackBuffer;
        byte* targetBase = (byte*)target.BackBuffer;
        int sourceStride = source.BackBufferStride;
        int targetStride = target.BackBufferStride;
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        if (sourceIsRgb)
        {
            Vector128<byte> shuffle = Vector128.Create((byte)2, 1, 0, 128, 5, 4, 3, 128, 8, 7, 6, 128, 11, 10, 9, 128);
            for (int y = 0; y < height; y++)
                Copy24Row(sourceBase + (long)y * sourceStride, targetBase + (long)y * targetStride, width, shuffle, redOffset: 0);
        }
        else
        {
            Vector128<byte> shuffle = Vector128.Create((byte)0, 1, 2, 128, 3, 4, 5, 128, 6, 7, 8, 128, 9, 10, 11, 128);
            Parallel.For(0, height, y =>
                Copy24Row(sourceBase + (long)y * sourceStride, targetBase + (long)y * targetStride, width, shuffle, redOffset: 2));
        }
        GC.KeepAlive(source);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Copy24Row(byte* from, byte* destination, int width, Vector128<byte> shuffle, int redOffset)
    {
        uint* to = (uint*)destination;
        int x = 0;
        if (Ssse3.IsSupported)
        {
            Vector128<byte> opaque = Vector128.Create(0xFF000000u).AsByte();
            // A 16-byte read fits within the row when at least six 24-bit pixels remain.
            for (; x <= width - 6; x += 4, from += 12)
            {
                Vector128<byte> pixels = Sse2.LoadVector128(from);
                Sse2.Store((byte*)(to + x), Sse2.Or(Ssse3.Shuffle(pixels, shuffle), opaque));
            }
        }
        int blueOffset = 2 - redOffset;
        for (; x < width; x++, from += 3)
            to[x] = 0xFF000000u | (uint)from[redOffset] << 16 | (uint)from[1] << 8 | from[blueOffset];
    }

    private static unsafe void CopyRgb48(WriteableBitmap source, WriteableBitmap target)
    {
        CopyRgb48Rows(source.BackBuffer, source.BackBufferStride,
            target.BackBuffer, target.BackBufferStride, source.PixelWidth, 0, source.PixelHeight);
        GC.KeepAlive(source);
    }

    internal static unsafe void CopyRgb48Rows(nint source, int sourceStride, nint target, int targetStride,
        int width, int firstRow, int rowEnd)
    {
        byte* sourceBase = (byte*)source;
        byte* targetBase = (byte*)target;
        // WIC keeps the high byte of each 16-bit sample. Select and reorder those bytes directly.
        Vector128<byte> firstTwo = Vector128.Create((byte)5, 3, 1, 128, 11, 9, 7, 128, 128, 128, 128, 128, 128, 128, 128, 128);
        Vector128<byte> lastTwo = Vector128.Create((byte)128, 128, 128, 128, 128, 128, 128, 128, 9, 7, 5, 128, 15, 13, 11, 128);
        Vector128<byte> opaque = Vector128.Create(0xFF000000u).AsByte();
        for (int y = firstRow; y < rowEnd; y++)
        {
            byte* from = sourceBase + (long)y * sourceStride;
            uint* to = (uint*)(targetBase + (long)y * targetStride);
            int x = 0;
            if (Ssse3.IsSupported)
            {
                for (; x <= width - 4; x += 4, from += 24)
                {
                    Vector128<byte> low = Ssse3.Shuffle(Sse2.LoadVector128(from), firstTwo);
                    Vector128<byte> high = Ssse3.Shuffle(Sse2.LoadVector128(from + 8), lastTwo);
                    Sse2.Store((byte*)(to + x), Sse2.Or(Sse2.Or(low, high), opaque));
                }
            }
            for (; x < width; x++, from += 6)
                to[x] = 0xFF000000u | (uint)from[1] << 16 | (uint)from[3] << 8 | from[5];
        }
    }

    private static unsafe void CopyGray16(WriteableBitmap source, WriteableBitmap target)
    {
        byte* sourceBase = (byte*)source.BackBuffer;
        byte* targetBase = (byte*)target.BackBuffer;
        int sourceStride = source.BackBufferStride;
        int targetStride = target.BackBufferStride;
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        uint[] colors = LazyInitializer.EnsureInitialized(ref _gray16Colors, CreateGray16Colors);
        for (int y = 0; y < height; y++)
        {
            ushort* from = (ushort*)(sourceBase + (long)y * sourceStride);
            uint* to = (uint*)(targetBase + (long)y * targetStride);
            for (int x = 0; x < width; x++)
                to[x] = colors[from[x]];
        }
        GC.KeepAlive(source);
    }

    private static unsafe uint[] CreateGray16Colors()
    {
        // Use this system's WIC gray transfer table, including its rounding at 8-bit boundaries.
        const int side = 256;
        var samples = new ushort[side * side];
        for (int i = 0; i < samples.Length; i++) samples[i] = (ushort)i;
        WriteableBitmap bitmap = new(side, side, 96, 96, PixelFormats.Gray16, null);
        Int32Rect bounds = new(0, 0, side, side);
        bitmap.WritePixels(bounds, samples, side * sizeof(ushort), 0);
        var colors = new uint[samples.Length];
        fixed (uint* pixels = colors)
            new FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, null, 0).CopyPixels(bounds, (IntPtr)pixels, colors.Length * sizeof(uint), side * sizeof(uint));
        return colors;
    }
}
