using ColorVision.ImageEditor.Draw.Line;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ProfileDataExtractorTests
{
    [Fact]
    public void ExtractAlongPathGray8OpenPathPreservesEverySample()
    {
        StaTest.Run(() =>
        {
            WriteableBitmap bitmap = CreateBitmap(3, 1, PixelFormats.Gray8, new byte[] { 10, 20, 30 }, 3);

            ProfileData result = ProfileDataExtractor.ExtractAlongPath(
                [new Point(0, 0), new Point(2, 0)], bitmap, totalSteps: 3);

            Assert.False(result.IsMultiChannel);
            Assert.Equal(new double[] { 10, 20, 30 }, result.GrayChannel);
            Assert.Empty(result.RedChannel);
            Assert.Empty(result.GreenChannel);
            Assert.Empty(result.BlueChannel);
        });
    }

    [Fact]
    public void ExtractAlongPathBgr24OpenPathPreservesChannelOrderAndLuminance()
    {
        StaTest.Run(() =>
        {
            WriteableBitmap bitmap = CreateBitmap(3, 1, PixelFormats.Bgr24,
                new byte[]
                {
                    0, 0, 100,
                    0, 100, 0,
                    100, 0, 0,
                },
                9);

            ProfileData result = ProfileDataExtractor.ExtractAlongPath(
                [new Point(0, 0), new Point(2, 0)], bitmap, totalSteps: 3);

            Assert.True(result.IsMultiChannel);
            Assert.Equal(new double[] { 100, 0, 0 }, result.RedChannel);
            Assert.Equal(new double[] { 0, 100, 0 }, result.GreenChannel);
            Assert.Equal(new double[] { 0, 0, 100 }, result.BlueChannel);
            AssertValues([29.9, 58.7, 11.4], result.GrayChannel);
        });
    }

    [Fact]
    public void ExtractAlongPathRgb48OpenPathPreservesSixteenBitChannels()
    {
        StaTest.Run(() =>
        {
            ushort[] pixels =
            [
                65535, 0, 0,
                1000, 2000, 3000,
                0, 65535, 65535,
            ];
            WriteableBitmap bitmap = CreateBitmap(3, 1, PixelFormats.Rgb48, pixels, 18);

            ProfileData result = ProfileDataExtractor.ExtractAlongPath(
                [new Point(0, 0), new Point(2, 0)], bitmap, totalSteps: 3);

            Assert.True(result.IsMultiChannel);
            Assert.Equal(new double[] { 65535, 1000, 0 }, result.RedChannel);
            Assert.Equal(new double[] { 0, 2000, 65535 }, result.GreenChannel);
            Assert.Equal(new double[] { 0, 3000, 65535 }, result.BlueChannel);
            AssertValues(
                [0.299 * 65535, 0.299 * 1000 + 0.587 * 2000 + 0.114 * 3000, (0.587 + 0.114) * 65535],
                result.GrayChannel);
        });
    }

    [Fact]
    public void ExtractAlongPathClosedPathSamplesEachVertexWithoutRepeatingTheFirst()
    {
        StaTest.Run(() =>
        {
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Gray8, new byte[] { 10, 20, 30, 40 }, 2);
            Point[] points = [new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1)];

            ProfileData result = ProfileDataExtractor.ExtractAlongPath(points, bitmap, totalSteps: 5, closePath: true);

            Assert.Equal(new double[] { 10, 20, 40, 30 }, result.GrayChannel);
        });
    }

    [Fact]
    public void ExtractAlongPathOutOfBoundsSamplesAreSkipped()
    {
        StaTest.Run(() =>
        {
            WriteableBitmap bitmap = CreateBitmap(3, 1, PixelFormats.Gray8, new byte[] { 10, 20, 30 }, 3);

            ProfileData result = ProfileDataExtractor.ExtractAlongPath(
                [new Point(-1, 0), new Point(1, 0)], bitmap, totalSteps: 3);

            Assert.Equal(new double[] { 10, 20 }, result.GrayChannel);
        });
    }

    private static WriteableBitmap CreateBitmap(
        int width,
        int height,
        PixelFormat format,
        Array pixels,
        int stride)
    {
        WriteableBitmap bitmap = new(width, height, 96, 96, format, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        return bitmap;
    }

    private static void AssertValues(IReadOnlyList<double> expected, List<double> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], actual[i], precision: 10);
        }
    }
}
