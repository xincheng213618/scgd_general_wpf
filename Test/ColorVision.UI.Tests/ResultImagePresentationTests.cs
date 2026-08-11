using ColorVision.Engine.Services;

namespace ColorVision.UI.Tests;

public class ResultImagePresentationTests
{
    [Fact]
    public void TryReadFrameInfoReadsDimensionsCaseInsensitively()
    {
        bool parsed = ResultImageDimensions.TryReadFrameInfo("{\"Width\":9680,\"HEIGHT\":5460,\"bpp\":16}", out int width, out int height);

        Assert.True(parsed);
        Assert.Equal(9680, width);
        Assert.Equal(5460, height);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"width\":0,\"height\":5460}")]
    [InlineData("{\"width\":9680}")]
    [InlineData("{\"width\":\"9680\",\"height\":5460}")]
    public void TryReadFrameInfoRejectsMissingOrInvalidDimensions(string? frameInfo)
    {
        Assert.False(ResultImageDimensions.TryReadFrameInfo(frameInfo, out int width, out int height));
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    [Fact]
    public void PlaceholderCacheReusesOnlyMatchingDimensions()
    {
        ResultImagePlaceholderCache cache = new();

        var first = cache.GetOrCreate(9680, 5460);
        var same = cache.GetOrCreate(9680, 5460);
        var resized = cache.GetOrCreate(1920, 1080);

        Assert.Same(first, same);
        Assert.NotSame(first, resized);
        Assert.True(cache.IsCurrent(resized, 1920, 1080));
        Assert.False(cache.IsCurrent(first, 9680, 5460));
    }
}
