using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms.Mtf;
using OpenCvSharp;

namespace ColorVision.UI.Tests;

public sealed class StripeMtfLocatorTests
{
    [Fact]
    public void CompletePatternSurvivesBackgroundAmplificationAtSearchBoundary()
    {
        using Mat image = PatternWithDarkTexture();
        using Mat before = image.Clone();
        var result = StripeMtfAnalyzer.Calculate(Borrow(image), new(), [new("Target", 0, 0, image.Cols, image.Rows)]);
        Assert.Equal(4, result["result"]!.Count());
        foreach (var row in result["result"]!)
        {
            Assert.InRange(row.Value<int>("x"), 80, 220);
            Assert.InRange(row.Value<int>("y"), 40, 180);
            Assert.InRange(row.Value<double>("mtfValue"), .6, .7);
        }
        Assert.Equal(0, Cv2.Norm(before, image, NormTypes.INF));
    }

    [Fact]
    public void CroppedPatternStillFailsInsteadOfProducingFourMeasurements()
    {
        using Mat image = PatternWithDarkTexture();
        Assert.Throws<InvalidOperationException>(() => StripeMtfAnalyzer.Calculate(Borrow(image), new(), [new("Cropped", 180, 40, 140, 200)]));
    }

    [Theory]
    [InlineData(8, 1, 5000)]
    [InlineData(16, .02, 5000)]
    [InlineData(16, 1, 65535)]
    [InlineData(16, 1, 0)]
    public void AutomaticLocationHandlesBitDepthExposureAndUnsuitableReferenceThreshold(int bits, double exposure, int threshold)
    {
        using Mat image = PatternWithDarkTexture(bits, exposure);
        var result = StripeMtfAnalyzer.Calculate(Borrow(image), new() { Threshold = threshold }, [new("Target", 0, 0, image.Cols, image.Rows)]);
        Assert.Equal(4, result["result"]!.Count());
        foreach (var row in result["result"]!) Assert.Equal(2.0 / 3, row.Value<double>("mtfValue"), 12);
    }

    private static Mat PatternWithDarkTexture(int bits = 16, double exposure = 1)
    {
        using Mat source = new(280, 340, MatType.CV_16UC1);
        int rows = source.Rows, columns = source.Cols;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                bool inside = x is >= 80 and < 280 && y is >= 40 and < 240;
                bool horizontal = (x < 180) == (y < 140);
                source.Set(y, x, (ushort)(inside ? ((horizontal ? y : x) / 6 % 2 == 0 ? 50000 : 10000) : 500 + (x * 73 + y * 37) % 1000));
            }
        Mat image = new();
        source.ConvertTo(image, bits == 8 ? MatType.CV_8U : MatType.CV_16U, exposure / (bits == 8 ? 256 : 1));
        return image;
    }

    private static HImage Borrow(Mat image) => new()
    {
        cols = image.Cols, rows = image.Rows, depth = image.Depth() == MatType.CV_8U ? 8 : 16, channels = 1,
        stride = checked((int)image.Step()), pData = image.Data
    };
}
