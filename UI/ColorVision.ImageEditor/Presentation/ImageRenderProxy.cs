using log4net;
using System;
using System.Runtime.InteropServices;
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

    private static bool IsRequired(PixelFormat format)
        => format == PixelFormats.Bgr24 || format == PixelFormats.Rgb24 || format == PixelFormats.Gray16 || format == PixelFormats.Rgb48;

    internal static WriteableBitmap? TryCreate(WriteableBitmap source)
    {
        if (!IsRequired(source.Format)) return null;
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
                if (source.Format == PixelFormats.Bgr24) CopyBgr24(source, proxy);
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

    private static unsafe void CopyBgr24(WriteableBitmap source, WriteableBitmap target)
    {
        byte* sourceBase = (byte*)source.BackBuffer;
        byte* targetBase = (byte*)target.BackBuffer;
        int sourceStride = source.BackBufferStride;
        int targetStride = target.BackBufferStride;
        int width = source.PixelWidth;
        Parallel.For(0, source.PixelHeight, y =>
        {
            byte* from = sourceBase + (long)y * sourceStride;
            uint* to = (uint*)(targetBase + (long)y * targetStride);
            for (int x = 0; x < width; x++, from += 3)
                to[x] = 0xFF000000u | (uint)from[2] << 16 | (uint)from[1] << 8 | from[0];
        });
    }
}
