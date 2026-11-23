using ColorVision.ImageEditor.Algorithms;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Presentation;

/// <summary>Prepares an independent editable bitmap without copying the pixels on the dispatcher.</summary>
public static class EditableBitmapCopy
{
    private const int CopyBlockBytes = 8 * 1024 * 1024;

    public static bool ShouldCopyInBackground(BitmapSource source)
        => ShouldCopyInBackground(source.PixelWidth, source.PixelHeight, source.Format.BitsPerPixel);

    public static bool ShouldCopyInBackground(int width, int height, int bitsPerPixel)
        => ((long)width * bitsPerPixel + 7) / 8 * height >= CopyBlockBytes;

    /// <summary>Call on the destination dispatcher with a frozen source.</summary>
    public static Task<WriteableBitmap> CreateAsync(BitmapSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsFrozen) throw new ArgumentException("The source bitmap must be frozen.", nameof(source));
        var bounds = new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight);
        return CreateAsync(bounds.Width, bounds.Height, source.DpiX, source.DpiY, source.Format, source.Palette,
            (buffer, stride) => CopyPixels(source, bounds, buffer, stride, cancellationToken), cancellationToken);
    }

    /// <summary>Call on the destination dispatcher. The worker callback writes synchronously and must not access the back buffer after returning.</summary>
    public static async Task<WriteableBitmap> CreateAsync(int width, int height, double dpiX, double dpiY,
        PixelFormat format, BitmapPalette? palette, Action<IntPtr, int> copyPixels, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteableBitmap target = new(width, height, dpiX, dpiY, format, palette);
        target.Lock();
        try
        {
            IntPtr buffer = target.BackBuffer;
            int stride = target.BackBufferStride;
            // Await the real writer even after cancellation. It owns the pointer until it returns.
            await Task.Run(() => copyPixels(buffer, stride), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            target.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally { target.Unlock(); }
        return target;
    }

    /// <summary>Copies complete packed rows into a strided destination. Optionally swaps red and blue in packed 16-bit color.</summary>
    public static unsafe void CopyPackedRows(ReadOnlySpan<byte> pixels, Span<byte> destination, int width, int height,
        int bytesPerPixel, int destinationStride, bool swapRedBlue16)
    {
        if (width <= 0 || height <= 0 || bytesPerPixel <= 0 || (swapRedBlue16 && bytesPerPixel != 6))
            throw new ArgumentException("The packed pixel layout is invalid.");
        int rowBytes = checked(width * bytesPerPixel);
        if (destinationStride < rowBytes || pixels.Length != checked(rowBytes * height)
            || destination.Length < checked(destinationStride * height) || pixels.Overlaps(destination))
            throw new ArgumentException("The packed pixel buffers do not match the requested layout.");

        // Once copying begins there is no allocation, I/O, cancellation or callback.
        if (swapRedBlue16)
        {
            fixed (byte* source = pixels)
            fixed (byte* target = destination)
                ImageAlgorithmInputFactory.CopyColor16(source, rowBytes, target, destinationStride, width, height, bytesPerPixel);
        }
        else if (rowBytes == destinationStride) pixels.CopyTo(destination);
        else
        {
            for (int row = 0; row < height; row++)
                pixels.Slice(row * rowBytes, rowBytes).CopyTo(destination.Slice(row * destinationStride, rowBytes));
        }
    }

    internal static unsafe void CopyPixels(BitmapSource source, Int32Rect bounds, IntPtr buffer, int stride,
        CancellationToken cancellationToken)
    {
        int rowsPerBlock = Math.Max(1, CopyBlockBytes / stride);
        for (int top = 0; top < bounds.Height;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int rows = Math.Min(rowsPerBlock, bounds.Height - top);
            var block = new Int32Rect(0, top, bounds.Width, rows);
            source.CopyPixels(block, (IntPtr)((byte*)buffer + (long)top * stride), checked(stride * rows), stride);
            top += rows;
        }
    }
}
