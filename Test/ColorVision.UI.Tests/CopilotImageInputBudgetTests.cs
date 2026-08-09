using ColorVision.Copilot;
using SkiaSharp;

namespace ColorVision.UI.Tests;

public sealed class CopilotImageInputBudgetTests
{
    [Theory]
    [InlineData(64, 32, 64, 32)]
    [InlineData(6_401, 100, 6_000, 94)]
    [InlineData(3_201, 3_201, 3_200, 3_200)]
    [InlineData(12_000, 6_000, 4_480, 2_240)]
    public void PreparedDimensionsHonorUnifiedDimensionAndPatchBudgets(
        int sourceWidth,
        int sourceHeight,
        int expectedWidth,
        int expectedHeight)
    {
        var prepared = CopilotImageInputBudget.ResolvePreparedDimensions(sourceWidth, sourceHeight);

        Assert.Equal((expectedWidth, expectedHeight), prepared);
        Assert.InRange(Math.Max(prepared.Width, prepared.Height), 1, CopilotImageInputBudget.MaximumDimension);
        Assert.InRange(
            CopilotImageInputBudget.CountPatches(prepared.Width, prepared.Height),
            1,
            CopilotImageInputBudget.MaximumPatches);
    }

    [Fact]
    public void TotalByteBudgetAcceptsExactBoundaryAndRejectsNextImage()
    {
        var retained = CopilotImagePayloadLoader.AddToTotalBudget(
            CopilotImagePayloadLoader.MaximumTotalBytes - 17,
            17);

        Assert.Equal(CopilotImagePayloadLoader.MaximumTotalBytes, retained);
        var error = Assert.Throws<InvalidOperationException>(() =>
            CopilotImagePayloadLoader.AddToTotalBudget(retained, 1));
        Assert.Contains("12 MB", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OversizedTransparentPngIsResizedAndReportedToTheVisionModel()
    {
        var sourceBytes = CreatePng(6_001, 2, new SKColor(32, 96, 160, 64));

        var prepared = CopilotImageInputBudget.Prepare(
            sourceBytes,
            "image/png",
            "wide-transparent.png",
            CancellationToken.None);

        Assert.Equal("image/png", prepared.MediaType);
        Assert.Equal((6_001, 2), (prepared.SourceWidth, prepared.SourceHeight));
        Assert.Equal((6_000, 2), (prepared.PreparedWidth, prepared.PreparedHeight));
        Assert.InRange(prepared.Bytes.LongLength, 1, CopilotImagePayloadLoader.MaximumImageBytes);
        using var decoded = SKBitmap.Decode(prepared.Bytes);
        Assert.NotNull(decoded);
        Assert.Equal(6_000, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.InRange(decoded.GetPixel(0, 0).Alpha, (byte)60, (byte)68);

        var notice = CopilotChatService.BuildImagePreparationNotice([
            new CopilotImagePayload(
                "wide-transparent.png",
                prepared.MediaType,
                Convert.ToBase64String(prepared.Bytes),
                prepared.SourceWidth,
                prepared.SourceHeight,
                prepared.PreparedWidth,
                prepared.PreparedHeight),
        ]);
        Assert.Contains("wide-transparent.png: 6001×2 -> 6000×2", notice, StringComparison.Ordinal);
        Assert.Contains("10000 patch", notice, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(prepared.Bytes), notice, StringComparison.Ordinal);
    }

    private static byte[] CreatePng(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(color);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        Assert.NotNull(encoded);
        return encoded.ToArray();
    }
}
