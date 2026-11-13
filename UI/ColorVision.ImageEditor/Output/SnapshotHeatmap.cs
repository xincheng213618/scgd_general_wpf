using OpenCvSharp;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Output;

internal static class SnapshotHeatmap
{
    internal static BitmapSource Create(BitmapSource source, int width, int height, double dpiX, double dpiY, CancellationToken cancellationToken)
    {
        using Mat gray = CopyLuminance(source, cancellationToken);
        using Mat scaled = new();
        Mat samples = gray;
        if (width != gray.Cols || height != gray.Rows)
        {
            Cv2.Resize(gray, scaled, new OpenCvSharp.Size(width, height), interpolation: InterpolationFlags.Area);
            samples = scaled;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Cv2.MinMaxLoc(samples, out double minimum, out double maximum);
        double scale = maximum > minimum && double.IsFinite(maximum - minimum) ? 255 / (maximum - minimum) : 0;
        using Mat indices = new();
        if (scale > 0 && double.IsFinite(scale))
            samples.ConvertTo(indices, MatType.CV_8U, scale, -minimum * scale);
        else
        {
            indices.Create(height, width, MatType.CV_8UC1);
            indices.SetTo(Scalar.Black);
        }
        var bitmap = new WriteableBitmap(width, height, dpiX, dpiY, PixelFormats.Bgr24, null);
        bitmap.Lock();
        try
        {
            using Mat colors = Mat.FromPixelData(height, width, MatType.CV_8UC3, bitmap.BackBuffer, bitmap.BackBufferStride);
            Cv2.ApplyColorMap(indices, colors, ColormapTypes.Jet);
            cancellationToken.ThrowIfCancellationRequested();
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally { bitmap.Unlock(); }
        bitmap.Freeze();
        return bitmap;
    }

    private static Mat CopyLuminance(BitmapSource source, CancellationToken cancellationToken)
    {
        PixelFormat format = source.Format;
        MatType grayType = format == PixelFormats.Gray32Float ? MatType.CV_32FC1
            : format == PixelFormats.Gray16 || format == PixelFormats.Rgb48 || format == PixelFormats.Rgba64
                ? MatType.CV_16UC1 : MatType.CV_8UC1;
        var gray = new Mat(source.PixelHeight, source.PixelWidth, grayType);
        try
        {
            if (format == PixelFormats.Gray8 || format == PixelFormats.Gray16 || format == PixelFormats.Gray32Float)
            {
                CopyRows(source, 0, gray);
            }
            else if (format == PixelFormats.Rgb48 || format == PixelFormats.Rgba64)
            {
                // Convert high-bit color in strips to retain precision without a second full RGB frame.
                const int stripHeight = 128;
                bool hasAlpha = format == PixelFormats.Rgba64;
                using Mat strip = new();
                int width = source.PixelWidth;
                int height = source.PixelHeight;
                for (int top = 0; top < height; top += stripHeight)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int rows = Math.Min(stripHeight, height - top);
                    strip.Create(rows, width, hasAlpha ? MatType.CV_16UC4 : MatType.CV_16UC3);
                    CopyRows(source, top, strip);
                    using Mat destination = gray.RowRange(top, top + rows);
                    Cv2.CvtColor(strip, destination, hasAlpha ? ColorConversionCodes.RGBA2GRAY : ColorConversionCodes.RGB2GRAY);
                }
            }
            else
            {
                CopyRows(new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0), 0, gray);
            }

            return gray;
        }
        catch
        {
            gray.Dispose();
            throw;
        }
    }

    private static void CopyRows(BitmapSource source, int top, Mat destination)
    {
        int stride = checked((int)destination.Step());
        source.CopyPixels(
            new Int32Rect(0, top, destination.Cols, destination.Rows),
            destination.Data, checked(stride * destination.Rows), stride);
    }
}
