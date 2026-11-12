using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageMouseSamplingTests
{
    [Theory]
    [InlineData(7.5, 3.0, 0, 0, 11)]
    [InlineData(19.5, 19.5, 1, 1, 44)]
    public void HoverReadsThePixelCellUnderThePointer(double x, double y, int pixelX, int pixelY, byte value)
    {
        WpfTestHost.Invoke(() =>
        {
            BitmapSource source = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[] { 11, 22, 33, 44 }, 2);
            using var canvas = CreateCanvas(source);
            using var provider = new DrawEditorContext(canvas, new Zoombox()).MouseInfoProvider;
            ImagePixelSample sample = Assert.IsType<ImagePixelSample>(provider.SampleAt(new Point(x, y)));

            Assert.Equal((pixelX, pixelY), (sample.PixelX, sample.PixelY));
            Assert.Equal($"Gray:{value}", sample.ValueText);
            Assert.Null(provider.SampleAt(new Point(-0.1, 3)));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DarkHighDepthHoverKeepsItsColorInTheCieWindow(bool rgba)
    {
        WpfTestHost.Invoke(() =>
        {
            ushort[] channels = rgba ? [128, 256, 512, 65535] : [128, 256, 512];
            BitmapSource source = BitmapSource.Create(1, 1, 96, 96, rgba ? PixelFormats.Rgba64 : PixelFormats.Rgb48,
                null, channels, channels.Length * sizeof(ushort));
            using var canvas = CreateCanvas(source);
            using var provider = new DrawEditorContext(canvas, new Zoombox()).MouseInfoProvider;
            ImagePixelSample sample = Assert.IsType<ImagePixelSample>(provider.SampleAt(new Point(2, 2)));
            var window = new WindowCIE();
            try
            {
                window.ChangeSelect(sample);
                var selected = Assert.IsType<CieAnalysisSample>(typeof(WindowCIE)
                    .GetField("_analysisSample", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window));
                double expectedY = (128 * 0.2126729 + 256 * 0.7151522 + 512 * 0.0721750) / 65535 / 12.92 * 100;
                Assert.Equal(expectedY, selected.Xyz.Y, 12);
                Assert.Equal(CieSampleBasis.Relative, selected.Basis);
                Assert.Equal(Color.FromRgb(0, 1, 2), sample.PreviewColor);
                Assert.Equal("R:128  G:256  B:512", sample.ValueText);
            }
            finally { window.Close(); }
        });
    }

    private static DrawCanvas CreateCanvas(BitmapSource source)
    {
        var canvas = new DrawCanvas { Source = source };
        canvas.Measure(new Size(20, 20));
        canvas.Arrange(new Rect(0, 0, 20, 20));
        return canvas;
    }
}
