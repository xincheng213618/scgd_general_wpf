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

    [Fact]
    public void HImageConversionRequiresMatchingExplicitCanonicalFormat()
    {
        HImage image = new()
        {
            rows = 1,
            cols = 1,
            channels = 3,
            depth = 8,
            stride = 3,
            pData = Marshal.AllocCoTaskMem(3),
        };
        try
        {
            Marshal.Copy(new byte[] { 1, 2, 3 }, 0, image.pData, 3);
            using AlgorithmImageBuffer normalized = ImageAlgorithmInputFactory.Copy(image, AlgorithmImageFormat.Bgr24);

            Assert.Equal(new byte[] { 1, 2, 3 }, normalized.Data.ToArray());
            Assert.Throws<ArgumentException>(() => ImageAlgorithmInputFactory.Copy(image, AlgorithmImageFormat.Bgra32));
        }
        finally
        {
            image.Dispose();
        }
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
