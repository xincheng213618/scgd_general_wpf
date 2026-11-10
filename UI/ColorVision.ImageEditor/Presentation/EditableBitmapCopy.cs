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
        => ((long)source.PixelWidth * source.Format.BitsPerPixel + 7) / 8 * source.PixelHeight >= CopyBlockBytes;

    /// <summary>Call on the destination dispatcher with a frozen source.</summary>
    public static Task<WriteableBitmap> CreateAsync(BitmapSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsFrozen) throw new ArgumentException("The source bitmap must be frozen.", nameof(source));
        var bounds = new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight);
        return CreateAsync(bounds.Width, bounds.Height, source.DpiX, source.DpiY, source.Format, source.Palette,
            (buffer, stride) => CopyPixels(source, bounds, buffer, stride, cancellationToken), cancellationToken);
    }

    internal static async Task<WriteableBitmap> CreateAsync(int width, int height, double dpiX, double dpiY,
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
