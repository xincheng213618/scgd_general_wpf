using ColorVision.ImageEditor.Output;
using OpenCvSharp;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class SnapshotHeatmapTests
{
    [Fact]
    public void ConstantHeatmapUsesTheFirstJetColorWithoutChangingTheSource()
    {
        StaTest.Run(() =>
        {
            const int width = 7, height = 3;
            ushort[] pixels = Enumerable.Repeat((ushort)50000, width * height).ToArray();
            BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, pixels, width * sizeof(ushort));

            BitmapSource heatmap = SnapshotHeatmap.Create(source, width, height, 96, 96, CancellationToken.None);

            using Mat index = new(1, 1, MatType.CV_8UC1, Scalar.Black);
            using Mat firstJetColor = new();
            Cv2.ApplyColorMap(index, firstJetColor, ColormapTypes.Jet);
            Vec3b color = firstJetColor.At<Vec3b>(0, 0);
            byte[] expected = Enumerable.Repeat(new[] { color.Item0, color.Item1, color.Item2 }, width * height).SelectMany(pixel => pixel).ToArray();
            byte[] actual = new byte[expected.Length];
            heatmap.CopyPixels(actual, width * 3, 0);
            Assert.Equal(expected, actual);
            ushort[] sourcePixels = new ushort[pixels.Length];
            source.CopyPixels(sourcePixels, width * sizeof(ushort), 0);
            Assert.Equal(pixels, sourcePixels);
        });
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void DownscaledHeatmapMatchesFullImageAreaSamplingAndGlobalColorRange(int divisor)
    {
        StaTest.Run(() =>
        {
            const int width = 68;
            const int height = 1032;
            float[] pixels = new float[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = y * 0.5f + x % 13 - 100;
            BitmapSource source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray32Float, null,
                pixels, width * sizeof(float));
            int outputWidth = width / divisor;
            int outputHeight = height / divisor;

            using Mat full = Mat.FromPixelData(height, width, MatType.CV_32FC1, pixels);
            using Mat scaled = new();
            Cv2.Resize(full, scaled, new Size(outputWidth, outputHeight), interpolation: InterpolationFlags.Area);
            Cv2.MinMaxLoc(scaled, out double minimum, out double maximum);
            using Mat indices = new();
            double scale = 255 / (maximum - minimum);
            scaled.ConvertTo(indices, MatType.CV_8U, scale, -minimum * scale);
            using Mat jet = new();
            Cv2.ApplyColorMap(indices, jet, ColormapTypes.Jet);
            byte[] expected = new byte[outputWidth * outputHeight * 3];
            Marshal.Copy(jet.Data, expected, 0, expected.Length);

            BitmapSource result = SnapshotHeatmap.Create(source, outputWidth, outputHeight, 96, 96, CancellationToken.None);
            byte[] actual = new byte[expected.Length];
            result.CopyPixels(actual, outputWidth * 3, 0);

            Assert.Equal(expected, actual);
        });
    }
}
