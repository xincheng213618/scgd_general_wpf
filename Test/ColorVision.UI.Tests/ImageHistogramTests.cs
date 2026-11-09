using ColorVision.Common.Utilities;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageHistogramTests
{
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
}
