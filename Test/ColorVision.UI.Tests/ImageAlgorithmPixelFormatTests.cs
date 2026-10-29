using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageAlgorithmPixelFormatTests
{
    [Fact]
    public void Rgb24NormalizesToCanonicalBgr24()
    {
        WriteableBitmap source = Bitmap(PixelFormats.Rgb24, [10, 20, 30, 40, 50, 60], 2, 1, 6);

        using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(source);
        WriteableBitmap roundTrip = ImageAlgorithmInputFactory.ToWriteableBitmap(normalized);

        Assert.Equal(AlgorithmImageFormat.Bgr24, normalized.Format);
        Assert.Equal(new byte[] { 30, 20, 10, 60, 50, 40 }, normalized.Data.ToArray());
        Assert.Equal(PixelFormats.Bgr24, roundTrip.Format);
        Assert.Equal(normalized.Data.ToArray(), Pixels(roundTrip, 6));
    }

    [Fact]
    public void Rgb48NormalizesToBgr48AndRoundTripsWithoutChannelLoss()
    {
        byte[] rgb = [0x02, 0x01, 0x04, 0x03, 0x06, 0x05];
        WriteableBitmap source = Bitmap(PixelFormats.Rgb48, rgb, 1, 1, 6);

        using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(source);
        WriteableBitmap roundTrip = ImageAlgorithmInputFactory.ToWriteableBitmap(normalized);

        Assert.Equal(AlgorithmImageFormat.Bgr48, normalized.Format);
        Assert.Equal(new byte[] { 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 }, normalized.Data.ToArray());
        Assert.Equal(PixelFormats.Rgb48, roundTrip.Format);
        Assert.Equal(rgb, Pixels(roundTrip, 6));
    }

    [Fact]
    public void Bgr32UnusedByteBecomesOpaqueStraightAlpha()
    {
        WriteableBitmap source = Bitmap(PixelFormats.Bgr32, [10, 20, 30, 7], 1, 1, 4);

        using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(source);

        Assert.Equal(AlgorithmImageFormat.Bgra32, normalized.Format);
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, normalized.Data.ToArray());
        Assert.Equal(PixelFormats.Bgra32, ImageAlgorithmInputFactory.ToWriteableBitmap(normalized).Format);
    }

    [Fact]
    public void Bgra32PreservesStraightAlpha()
    {
        WriteableBitmap source = Bitmap(PixelFormats.Bgra32, [10, 20, 30, 40], 1, 1, 4);

        using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(source);

        Assert.Equal(AlgorithmImageFormat.Bgra32, normalized.Format);
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, normalized.Data.ToArray());
    }

    [Fact]
    public void Pbgra32UnpremultipliesToStraightBgraAndClearsTransparentColor()
    {
        WriteableBitmap source = Bitmap(PixelFormats.Pbgra32, [30, 20, 10, 85, 9, 8, 7, 0], 2, 1, 8);

        using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(source);

        Assert.Equal(AlgorithmImageFormat.Bgra32, normalized.Format);
        Assert.Equal(new byte[] { 90, 60, 30, 85, 0, 0, 0, 0 }, normalized.Data.ToArray());
    }

    [Fact]
    public void Indexed8ExpandsPaletteColorsAndAlpha()
    {
        BitmapPalette palette = new([
            Color.FromArgb(255, 10, 20, 30),
            Color.FromArgb(128, 40, 50, 60),
        ]);
        WriteableBitmap source = new(2, 1, 144, 120, PixelFormats.Indexed8, palette);
        source.WritePixels(new System.Windows.Int32Rect(0, 0, 2, 1), new byte[] { 1, 0 }, 2, 0);

        using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(source);

        Assert.Equal(AlgorithmImageFormat.Bgra32, normalized.Format);
        Assert.Equal(144, normalized.DpiX);
        Assert.Equal(120, normalized.DpiY);
        Assert.Equal(new byte[] { 60, 50, 40, 128, 30, 20, 10, 255 }, normalized.Data.ToArray());
    }

    [Fact]
    public void Rgba64NormalizesToStraightBgra64AndRoundTrips()
    {
        byte[] rgba = [0x02, 0x01, 0x04, 0x03, 0x06, 0x05, 0x08, 0x07];
        WriteableBitmap source = Bitmap(PixelFormats.Rgba64, rgba, 1, 1, 8);

        using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(source);
        WriteableBitmap roundTrip = ImageAlgorithmInputFactory.ToWriteableBitmap(normalized);

        Assert.Equal(AlgorithmImageFormat.Bgra64, normalized.Format);
        Assert.Equal(new byte[] { 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x08, 0x07 }, normalized.Data.ToArray());
        Assert.Equal(PixelFormats.Rgba64, roundTrip.Format);
        Assert.Equal(rgba, Pixels(roundTrip, 8));
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Bgr48, 2)]
    [InlineData(AlgorithmImageFormat.Bgr48, 3)]
    [InlineData(AlgorithmImageFormat.Bgr48, 4)]
    [InlineData(AlgorithmImageFormat.Bgra64, 3)]
    public void SixteenBitInputNormalizesPrivateSnapshotAtVectorAndTailBoundaries(AlgorithmImageFormat format, int width)
    {
        bool hasAlpha = format == AlgorithmImageFormat.Bgra64;
        int pixelBytes = hasAlpha ? 8 : 6;
        byte[] sourceRows = hasAlpha
            ? [
                0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0xF0, 0xDE,
                0xDC, 0xFE, 0x98, 0xBA, 0x54, 0x76, 0x10, 0x32,
                0x01, 0x00, 0x00, 0x01, 0xFF, 0xFF, 0x00, 0x00,
                0x57, 0x13, 0x68, 0x24, 0xCD, 0xAB, 0x01, 0x00,
                0xEF, 0x89, 0x45, 0x67, 0x01, 0x23, 0x00, 0x80,
                0xFF, 0xFF, 0x00, 0x00, 0x34, 0x12, 0xFF, 0xFF,
            ]
            : [
                0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A,
                0xDC, 0xFE, 0x98, 0xBA, 0x54, 0x76,
                0x01, 0x00, 0x00, 0x01, 0xFF, 0xFF,
                0x10, 0x32, 0x54, 0x76, 0x98, 0xBA,
                0x57, 0x13, 0x68, 0x24, 0xCD, 0xAB,
                0xEF, 0x89, 0x45, 0x67, 0x01, 0x23,
                0xFF, 0xFF, 0x00, 0x00, 0x34, 0x12,
                0x68, 0x24, 0xCD, 0xAB, 0x01, 0xEF,
            ];
        byte[] expectedRows = hasAlpha
            ? [
                0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, 0xF0, 0xDE,
                0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE, 0x10, 0x32,
                0xFF, 0xFF, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00,
                0xCD, 0xAB, 0x68, 0x24, 0x57, 0x13, 0x01, 0x00,
                0x01, 0x23, 0x45, 0x67, 0xEF, 0x89, 0x00, 0x80,
                0x34, 0x12, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF,
            ]
            : [
                0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12,
                0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE,
                0xFF, 0xFF, 0x00, 0x01, 0x01, 0x00,
                0x98, 0xBA, 0x54, 0x76, 0x10, 0x32,
                0xCD, 0xAB, 0x68, 0x24, 0x57, 0x13,
                0x01, 0x23, 0x45, 0x67, 0xEF, 0x89,
                0x34, 0x12, 0x00, 0x00, 0xFF, 0xFF,
                0x01, 0xEF, 0xCD, 0xAB, 0x68, 0x24,
            ];
        int stride = width * pixelBytes;
        byte[] sourcePixels = [.. sourceRows.AsSpan(0, stride), .. sourceRows.AsSpan(24, stride)];
        byte[] expected = [.. expectedRows.AsSpan(0, stride), .. expectedRows.AsSpan(24, stride)];
        WriteableBitmap source = new(width, 2, 144, 120, hasAlpha ? PixelFormats.Rgba64 : PixelFormats.Rgb48, null);
        source.WritePixels(new Int32Rect(0, 0, width, 2), sourcePixels, stride, 0);

        using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(source);

        Assert.Equal(format, normalized.Format);
        Assert.Equal(width, normalized.Width);
        Assert.Equal(2, normalized.Height);
        Assert.Equal(stride, normalized.Stride);
        Assert.Equal(144, normalized.DpiX);
        Assert.Equal(120, normalized.DpiY);
        Assert.Equal(expected, normalized.Data.ToArray());
        Assert.Equal(sourcePixels, Pixels(source, stride));

        byte[] replacement = hasAlpha
            ? [0xFE, 0xCA, 0xBE, 0xBA, 0xEF, 0xBE, 0x78, 0x56]
            : [0xFE, 0xCA, 0xBE, 0xBA, 0xEF, 0xBE];
        source.WritePixels(new Int32Rect(0, 0, 1, 1), replacement, pixelBytes, 0);
        replacement.CopyTo(sourcePixels, 0);

        Assert.Equal(sourcePixels, Pixels(source, stride));
        Assert.Equal(expected, normalized.Data.ToArray());
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Bgr48, 8)]
    [InlineData(AlgorithmImageFormat.Bgr48, 7)]
    [InlineData(AlgorithmImageFormat.Bgra64, 8)]
    [InlineData(AlgorithmImageFormat.Bgra64, 7)]
    public void SixteenBitOutputPreservesPaddedRowsAndReturnsIndependentWritablePixels(AlgorithmImageFormat format, int rowPadding)
    {
        bool hasAlpha = format == AlgorithmImageFormat.Bgra64;
        int pixelBytes = hasAlpha ? 8 : 6;
        int rowBytes = 3 * pixelBytes;
        int sourceStride = rowBytes + rowPadding;
        byte[] rowsWithEightBytePadding = hasAlpha
            ? [
                0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0xF0, 0xDE,
                0xDC, 0xFE, 0x98, 0xBA, 0x54, 0x76, 0x10, 0x32,
                0x01, 0x00, 0x00, 0x01, 0xFF, 0xFF, 0x00, 0x00,
                0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6, 0x07, 0x18,
                0x57, 0x13, 0x68, 0x24, 0xCD, 0xAB, 0x01, 0x00,
                0xEF, 0x89, 0x45, 0x67, 0x01, 0x23, 0x00, 0x80,
                0xFF, 0xFF, 0x00, 0x00, 0x34, 0x12, 0xFF, 0xFF,
                0x29, 0x3A, 0x4B, 0x5C, 0x6D, 0x7E, 0x8F, 0x90,
            ]
            : [
                0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A,
                0xDC, 0xFE, 0x98, 0xBA, 0x54, 0x76,
                0x01, 0x00, 0x00, 0x01, 0xFF, 0xFF,
                0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6, 0x07, 0x18,
                0x57, 0x13, 0x68, 0x24, 0xCD, 0xAB,
                0xEF, 0x89, 0x45, 0x67, 0x01, 0x23,
                0xFF, 0xFF, 0x00, 0x00, 0x34, 0x12,
                0x29, 0x3A, 0x4B, 0x5C, 0x6D, 0x7E, 0x8F, 0x90,
            ];
        byte[] sourcePixels = [
            .. rowsWithEightBytePadding.AsSpan(0, sourceStride),
            .. rowsWithEightBytePadding.AsSpan(rowBytes + 8, sourceStride),
        ];
        byte[] expected = hasAlpha
            ? [
                0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, 0xF0, 0xDE,
                0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE, 0x10, 0x32,
                0xFF, 0xFF, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00,
                0xCD, 0xAB, 0x68, 0x24, 0x57, 0x13, 0x01, 0x00,
                0x01, 0x23, 0x45, 0x67, 0xEF, 0x89, 0x00, 0x80,
                0x34, 0x12, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF,
            ]
            : [
                0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12,
                0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE,
                0xFF, 0xFF, 0x00, 0x01, 0x01, 0x00,
                0xCD, 0xAB, 0x68, 0x24, 0x57, 0x13,
                0x01, 0x23, 0x45, 0x67, 0xEF, 0x89,
                0x34, 0x12, 0x00, 0x00, 0xFF, 0xFF,
            ];
        byte[] sourceBefore = sourcePixels.ToArray();
        using AlgorithmImageBuffer source = new(3, 2, sourceStride, format, sourcePixels, 144, 120);

        WriteableBitmap output = ImageAlgorithmInputFactory.ToWriteableBitmap(source);

        Assert.Equal(hasAlpha ? PixelFormats.Rgba64 : PixelFormats.Rgb48, output.Format);
        Assert.Equal(3, output.PixelWidth);
        Assert.Equal(2, output.PixelHeight);
        Assert.Equal(144, output.DpiX);
        Assert.Equal(120, output.DpiY);
        Assert.NotEqual(sourceStride, output.BackBufferStride);
        Assert.Equal(expected, Pixels(output, rowBytes));
        Assert.False(output.IsFrozen);

        byte[] replacement = hasAlpha
            ? [0xFE, 0xCA, 0xBE, 0xBA, 0xEF, 0xBE, 0x78, 0x56]
            : [0xFE, 0xCA, 0xBE, 0xBA, 0xEF, 0xBE];
        output.WritePixels(new Int32Rect(0, 0, 1, 1), replacement, pixelBytes, 0);
        replacement.CopyTo(expected, 0);

        Assert.Equal(expected, Pixels(output, rowBytes));
        Assert.Equal(sourceBefore, source.Data.ToArray());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void Bgr48PackedRowsPreservePixelsAtVectorReadAndTailBoundaries(int width)
    {
        byte[] sourceRows = [
            0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A,
            0xDC, 0xFE, 0x98, 0xBA, 0x54, 0x76,
            0x01, 0x00, 0x00, 0x01, 0xFF, 0xFF,
            0x10, 0x32, 0x54, 0x76, 0x98, 0xBA,
            0x57, 0x13, 0x68, 0x24, 0xCD, 0xAB,
            0xEF, 0x89, 0x45, 0x67, 0x01, 0x23,
            0xFF, 0xFF, 0x00, 0x00, 0x34, 0x12,
            0x68, 0x24, 0xCD, 0xAB, 0x01, 0xEF,
        ];
        byte[] expectedRows = [
            0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12,
            0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE,
            0xFF, 0xFF, 0x00, 0x01, 0x01, 0x00,
            0x98, 0xBA, 0x54, 0x76, 0x10, 0x32,
            0xCD, 0xAB, 0x68, 0x24, 0x57, 0x13,
            0x01, 0x23, 0x45, 0x67, 0xEF, 0x89,
            0x34, 0x12, 0x00, 0x00, 0xFF, 0xFF,
            0x01, 0xEF, 0xCD, 0xAB, 0x68, 0x24,
        ];
        int stride = width * 6;
        byte[] sourcePixels = [.. sourceRows.AsSpan(0, stride), .. sourceRows.AsSpan(24, stride)];
        byte[] expected = [.. expectedRows.AsSpan(0, stride), .. expectedRows.AsSpan(24, stride)];
        byte[] sourceBefore = sourcePixels.ToArray();
        using AlgorithmImageBuffer source = new(width, 2, stride, AlgorithmImageFormat.Bgr48, sourcePixels);

        WriteableBitmap output = ImageAlgorithmInputFactory.ToWriteableBitmap(source);

        Assert.Equal(expected, Pixels(output, stride));
        Assert.Equal(sourceBefore, source.Data.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(8)]
    public void HImageConversionRequiresMatchingExplicitCanonicalFormat(int stride)
    {
        int physicalStride = stride == 0 ? 6 : stride;
        byte[] pixels = new byte[physicalStride * 2];
        new byte[] { 1, 2, 3, 4, 5, 6 }.CopyTo(pixels, 0);
        new byte[] { 7, 8, 9, 10, 11, 12 }.CopyTo(pixels, physicalStride);
        HImage image = new()
        {
            rows = 2,
            cols = 2,
            channels = 3,
            depth = 8,
            stride = stride,
            pData = Marshal.AllocCoTaskMem(pixels.Length),
        };
        try
        {
            Marshal.Copy(pixels, 0, image.pData, pixels.Length);
            using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(image, AlgorithmImageFormat.Bgr24);

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, normalized.Data.ToArray());
            Assert.Throws<ArgumentException>(() => ImageAlgorithmInputFactory.Copy(image, AlgorithmImageFormat.Bgra32));
        }
        finally
        {
            image.Dispose();
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void HImageConversionRejectsInvalidStrideInsteadOfAssumingPackedRows(int stride)
    {
        HImage image = new()
        {
            rows = 2, cols = 2, channels = 3, depth = 8, stride = stride,
            pData = Marshal.AllocCoTaskMem(12),
        };
        try
        {
            Marshal.Copy(new byte[12], 0, image.pData, 12);
            Assert.Throws<ArgumentException>(() => ImageAlgorithmInputFactory.Copy(image, AlgorithmImageFormat.Bgr24));
        }
        finally { image.Dispose(); }
    }

    [Fact]
    public void PixelFormatProjectionReportsCanonicalLayouts()
    {
        Assert.Equal(AlgorithmImageFormat.Bgr24, ImageAlgorithmInputFactory.FromPixelFormat(PixelFormats.Rgb24));
        Assert.Equal(AlgorithmImageFormat.Bgra32, ImageAlgorithmInputFactory.FromPixelFormat(PixelFormats.Bgr32));
        Assert.Equal(AlgorithmImageFormat.Bgra32, ImageAlgorithmInputFactory.FromPixelFormat(PixelFormats.Pbgra32));
        Assert.Equal(AlgorithmImageFormat.Bgra32, ImageAlgorithmInputFactory.FromPixelFormat(PixelFormats.Indexed8));
    }

    [Fact]
    public void ImageViewAcquisitionUsesWpfSemanticsInsteadOfHImageDepthAndChannels()
    {
        ImageView view = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView created = new();
            created.SetImageSource(
                Bitmap(PixelFormats.Rgb24, [10, 20, 30], 1, 1, 3),
                enableEditorImageServices: false,
                configureDefaultLayerController: false);
            return created;
        });

        try
        {
            AlgorithmInput input = WpfTestHost.Invoke(() =>
                ImageAlgorithmInputFactory.Acquire(view.EditorContext.ProcessingContext));
            using (input.Image)
            {
                Assert.Equal(AlgorithmImageFormat.Bgr24, input.Image.Format);
                Assert.Equal(new byte[] { 30, 20, 10 }, input.Image.Data.ToArray());
                Assert.Equal(view.ImageRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), input.SourceRevision);
            }
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    private static WriteableBitmap Bitmap(PixelFormat format, byte[] pixels, int width, int height, int stride)
    {
        WriteableBitmap bitmap = new(width, height, 96, 96, format, null);
        bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, stride, 0);
        return bitmap;
    }

    private static byte[] Pixels(BitmapSource bitmap, int stride)
    {
        byte[] pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
