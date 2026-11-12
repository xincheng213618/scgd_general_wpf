using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageHistogramTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void OptimizeKeepsAUsableScaleWhenThereIsOnlyOnePositiveCount(bool color, bool uniform)
    {
        WpfTestHost.Invoke(() =>
        {
            int[] histogram = new int[256];
            if (uniform) Array.Fill(histogram, 2);
            else histogram[128] = 2;
            var window = color ? new HistogramChartWindow(histogram, histogram, histogram) : new HistogramChartWindow(histogram);
            try
            {
                Button optimize = Assert.IsType<Button>(window.FindName("OptimizeButton"));
                optimize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var plot = Assert.IsType<ScottPlot.WPF.WpfPlot>(window.FindName("WpfPlot"));

                Assert.Equal("Normal", optimize.Content);
                Assert.Equal(0, plot.Plot.Axes.GetLimits().Bottom);
                Assert.Equal(color ? 2.4 : 2.2, plot.Plot.Axes.GetLimits().Top, 12);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("Rgb24")]
    [InlineData("Rgba64")]
    [InlineData("Indexed8")]
    public void CountsDisplayedColorChannelsForAdditionalImageFormats(string formatName)
    {
        StaTest.Run(() =>
        {
            BitmapPalette? palette = null;
            PixelFormat format;
            byte[] pixels;
            switch (formatName)
            {
                case "Rgb24":
                    format = PixelFormats.Rgb24;
                    pixels = [11, 22, 33, 44, 55, 66];
                    break;
                case "Rgba64":
                    format = PixelFormats.Rgba64;
                    pixels = [1, 11, 2, 22, 3, 33, 255, 255, 4, 44, 5, 55, 6, 66, 255, 255];
                    break;
                default:
                    format = PixelFormats.Indexed8;
                    palette = new BitmapPalette([Color.FromRgb(11, 22, 33), Color.FromRgb(44, 55, 66)]);
                    pixels = [1, 0];
                    break;
            }
            BitmapSource source = BitmapSource.Create(2, 1, 96, 96, format, palette, pixels, pixels.Length);

            var (red, green, blue) = ImageUtils.RenderHistogram(source);

            Assert.Equal([11, 44], OccupiedBins(red));
            Assert.Equal([22, 55], OccupiedBins(green));
            Assert.Equal([33, 66], OccupiedBins(blue));
        });
    }

    [Fact]
    public void Rgb48CountsHighBytesInRgbOrderIncludingFinalPartialChunk()
    {
        StaTest.Run(() =>
        {
            const int width = 1093;
            const int height = 11;
            const int stride = width * 6 + 2;
            byte[] pixels = new byte[height * stride];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int offset = y * stride + x * 6;
                pixels[offset] = 255;
                pixels[offset + 1] = (byte)y;
                pixels[offset + 2] = 254;
                pixels[offset + 3] = 83;
                pixels[offset + 4] = 253;
                pixels[offset + 5] = (byte)(240 + y);
            }
            BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb48, null, pixels, stride);

            var (red, green, blue) = ImageUtils.RenderHistogram(source);

            Assert.Equal(width * height, green[83]);
            for (int y = 0; y < height; y++)
            {
                Assert.Equal(width, red[y]);
                Assert.Equal(width, blue[240 + y]);
            }
            Assert.Equal(width * height, red.Sum());
            Assert.Equal(width * height, green.Sum());
            Assert.Equal(width * height, blue.Sum());
        });
    }

    private static int[] OccupiedBins(int[] histogram) => [.. histogram.SelectMany((count, bin) => Enumerable.Repeat(bin, count))];
}
