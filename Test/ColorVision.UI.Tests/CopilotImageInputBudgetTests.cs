using ColorVision.Copilot;
using SkiaSharp;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

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

    [Fact]
    public async Task ResizedImagePreparationRemainsAttachedToDurableAnalysis()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"copilot-image-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(
            imagePath,
            CreatePng(6_001, 2, new SKColor(32, 96, 160, 64)));
        try
        {
            using var handler = new ImageAnalysisHandler();
            using var httpClient = new HttpClient(handler);
            var service = new CopilotImageUnderstandingService(new CopilotChatService(httpClient));
            var profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.OpenAI,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://api.openai.com/v1",
                Model = "gpt-4o",
                MaxTokens = 4_096,
            };
            var result = await service.AnalyzeAsync(
                profile,
                "Inspect the chart.",
                [new CopilotAttachmentItem
                {
                    Type = CopilotAttachmentType.Image,
                    Title = "wide-transparent.png",
                    Value = imagePath,
                }],
                CancellationToken.None);

            Assert.Contains("[Image preparation]", result.Context, StringComparison.Ordinal);
            Assert.Contains("wide-transparent.png: 6001×2 -> 6000×2", result.Context, StringComparison.Ordinal);
            Assert.Contains("Only the prepared pixels are available", result.Context, StringComparison.Ordinal);
            Assert.True(
                result.Context.IndexOf("[Image preparation]", StringComparison.Ordinal)
                    < result.Context.IndexOf("Visible chart evidence.", StringComparison.Ordinal));
            Assert.Contains("[Image preparation]", handler.LastPayload, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(imagePath);
        }
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

    private sealed class ImageAnalysisHandler : HttpMessageHandler
    {
        public string LastPayload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastPayload = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            const string response =
                """
                {
                  "choices": [
                    {
                      "message": {
                        "role": "assistant",
                        "content": "Visible chart evidence."
                      },
                      "finish_reason": "stop"
                    }
                  ]
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
