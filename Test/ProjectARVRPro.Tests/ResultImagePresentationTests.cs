using ColorVision.FileIO;
using System.IO;
using System.Windows.Media;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ResultImagePresentationTests
{
    [Fact]
    public void PlaceholderCacheReusesTheExactDrawingForTheSameSize()
    {
        ResultImagePlaceholderCache cache = new();

        DrawingImage first = cache.GetOrCreate(9680, 5460);
        DrawingImage second = cache.GetOrCreate(9680, 5460);

        Assert.Same(first, second);
        Assert.True(first.IsFrozen);
        Assert.True(cache.IsCurrent(first, 9680, 5460));
    }

    [Fact]
    public void PlaceholderCacheReplacesTheDrawingWhenTheSizeChanges()
    {
        ResultImagePlaceholderCache cache = new();
        DrawingImage first = cache.GetOrCreate(9680, 5460);

        DrawingImage second = cache.GetOrCreate(5544, 3692);

        Assert.NotSame(first, second);
        Assert.Equal(5544, second.Width);
        Assert.Equal(3692, second.Height);
        Assert.False(cache.IsCurrent(first, 9680, 5460));
    }

    [Theory]
    [InlineData("{\"width\":9680,\"height\":5460}", 9680, 5460)]
    [InlineData("{\"Width\":5544,\"Height\":3692}", 5544, 3692)]
    public void FrameInfoReaderAcceptsPositiveCaseInsensitiveDimensions(string json, int expectedWidth, int expectedHeight)
    {
        bool found = ResultImageDimensions.TryReadFrameInfo(json, out int width, out int height);

        Assert.True(found);
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"width\":0,\"height\":5460}")]
    public void FrameInfoReaderRejectsUnknownOrInvalidDimensions(string? json)
    {
        Assert.False(ResultImageDimensions.TryReadFrameInfo(json, out _, out _));
    }

    [Fact]
    public void FileDimensionReaderUsesTheCvHeaderWithoutOpeningAPixelView()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro.Dimensions.{Guid.NewGuid():N}.cvcie");
        try
        {
            Assert.True(CVFileUtil.WriteCIEFile(filePath, new byte[15], rows: 3, cols: 5, bpp: 8, channels: 1));

            bool found = ResultImageDimensions.TryReadFromFile(filePath, out int width, out int height);

            Assert.True(found);
            Assert.Equal(5, width);
            Assert.Equal(3, height);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
