using OpenCvSharp;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Output;

internal static class SnapshotHeatmap
{
    private const int ResizeStripRows = 512;
    private const int ColorStripRows = 128;

    internal static BitmapSource Create(BitmapSource source, int width, int height, double dpiX, double dpiY, CancellationToken cancellationToken)
    {
        using Mat gray = new();
        using Mat scaled = new();
        Mat samples = SampleLuminance(source, width, height, gray, scaled, cancellationToken);
        if (ReferenceEquals(samples, scaled))
            gray.Release();

        cancellationToken.ThrowIfCancellationRequested();
        Cv2.MinMaxLoc(samples, out double minimum, out double maximum);
        double scale = maximum > minimum && double.IsFinite(maximum - minimum) ? 255 / (maximum - minimum) : 0;
        bool hasRange = scale > 0 && double.IsFinite(scale);
        using Mat normalized = new();
        Mat indices = samples;
        if (hasRange && samples.Type() == MatType.CV_8UC1)
        {
            // Byte luminance already occupies the index buffer. Normalize it in place if needed.
            if (minimum != 0 || maximum != byte.MaxValue)
                samples.ConvertTo(samples, MatType.CV_8U, scale, -minimum * scale);
        }
        else
        {
            if (hasRange)
                samples.ConvertTo(normalized, MatType.CV_8U, scale, -minimum * scale);
            indices = normalized;
            samples.Release();
        }
        cancellationToken.ThrowIfCancellationRequested();
        var bitmap = new WriteableBitmap(width, height, dpiX, dpiY, PixelFormats.Bgr24, null);
        bitmap.Lock();
        try
        {
            using Mat colors = Mat.FromPixelData(height, width, MatType.CV_8UC3, bitmap.BackBuffer, bitmap.BackBufferStride);
            if (hasRange)
                Cv2.ApplyColorMap(indices, colors, ColormapTypes.Jet);
            else
            {
                // OpenCV's first JET entry in BGR order.
                colors.SetTo(new Scalar(128, 0, 0));
            }
            cancellationToken.ThrowIfCancellationRequested();
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally { bitmap.Unlock(); }
        bitmap.Freeze();
        return bitmap;
    }

    private static Mat SampleLuminance(BitmapSource source, int width, int height, Mat gray, Mat scaled,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PixelFormat format = source.Format;
        bool isHighBitColor = format == PixelFormats.Rgb48 || format == PixelFormats.Rgba64;
        MatType grayType = format == PixelFormats.Gray32Float ? MatType.CV_32FC1
            : format == PixelFormats.Gray16 || isHighBitColor
                ? MatType.CV_16UC1 : MatType.CV_8UC1;
        BitmapSource pixels = !isHighBitColor && grayType == MatType.CV_8UC1 && format != PixelFormats.Gray8
            ? new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0)
            : source;
        using Mat? colorStrip = isHighBitColor ? new Mat() : null;
        int sourceWidth = source.PixelWidth;
        int sourceHeight = source.PixelHeight;
        int divisor = width > 0 ? sourceWidth / width : 0;
        if (divisor is 2 or 4 && sourceWidth % width == 0 && height == sourceHeight / divisor
            && sourceHeight % divisor == 0 && sourceHeight > ResizeStripRows)
        {
            // At integer scale factors every AREA sample lies in one aligned strip.
            // Keep the resampled frame for global normalization and reuse the source strip.
            scaled.Create(height, width, grayType);
            int outputRowsPerStrip = ResizeStripRows / divisor;
            for (int top = 0; top < height; top += outputRowsPerStrip)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int outputRows = Math.Min(outputRowsPerStrip, height - top);
                gray.Create(outputRows * divisor, sourceWidth, grayType);
                CopyLuminance(pixels, top * divisor, gray, colorStrip, cancellationToken);
                using Mat destination = scaled.RowRange(top, top + outputRows);
                Cv2.Resize(gray, destination, new OpenCvSharp.Size(width, outputRows), interpolation: InterpolationFlags.Area);
            }
            return scaled;
        }

        gray.Create(sourceHeight, sourceWidth, grayType);
        CopyLuminance(pixels, 0, gray, colorStrip, cancellationToken);
        if (width == sourceWidth && height == sourceHeight)
            return gray;

        Cv2.Resize(gray, scaled, new OpenCvSharp.Size(width, height), interpolation: InterpolationFlags.Area);
        return scaled;
    }

    private static void CopyLuminance(BitmapSource source, int sourceTop, Mat gray, Mat? colorStrip,
        CancellationToken cancellationToken)
    {
        if (colorStrip == null)
        {
            CopyRows(source, sourceTop, gray);
            return;
        }

        // Convert high-bit color in strips to retain precision without a full RGB frame.
        bool hasAlpha = source.Format == PixelFormats.Rgba64;
        MatType colorType = hasAlpha ? MatType.CV_16UC4 : MatType.CV_16UC3;
        ColorConversionCodes conversion = hasAlpha ? ColorConversionCodes.RGBA2GRAY : ColorConversionCodes.RGB2GRAY;
        int width = gray.Cols;
        int height = gray.Rows;
        for (int top = 0; top < height; top += ColorStripRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int rows = Math.Min(ColorStripRows, height - top);
            colorStrip.Create(rows, width, colorType);
            CopyRows(source, sourceTop + top, colorStrip);
            using Mat destination = gray.RowRange(top, top + rows);
            Cv2.CvtColor(colorStrip, destination, conversion);
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
