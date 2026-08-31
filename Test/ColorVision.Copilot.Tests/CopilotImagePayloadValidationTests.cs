using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ColorVision.Copilot;
using SkiaSharp;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotImagePayloadValidationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task IncompleteOrInvalidPixelDataRejectsTheWholeAdmissionBatch(bool needsResize, bool corruptData)
    {
        using var files = new TemporaryImages();
        var damagedBytes = CreateDamagedPng(needsResize, corruptData);
        AssertDecodeResult(damagedBytes, corruptData ? SKCodecResult.ErrorInInput : SKCodecResult.IncompleteInput);
        var validPath = await files.WriteAsync("valid.png", CreatePng(4, 3));
        var damagedPath = await files.WriteAsync("damaged.png", damagedBytes);

        var failure = await Record.ExceptionAsync(() => CopilotImageAttachmentAdmission.PersistAsync(
            [CopilotAttachmentItem.CreateImage(validPath), CopilotAttachmentItem.CreateImage(damagedPath)],
            files.StorePath,
            CancellationToken.None));

        Assert.False(Directory.Exists(files.StorePath), "Pixel decoding failed, but the batch created managed image files.");
        var rejected = Assert.IsType<CopilotImageAttachmentAdmissionException>(failure);
        Assert.Equal(CopilotImageAttachmentAdmissionFailureKind.RejectedInput, rejected.FailureKind);
        Assert.Equal(damagedBytes, await File.ReadAllBytesAsync(damagedPath));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task IncompleteOrInvalidPixelDataCannotReachTheImageAnalysisProvider(bool needsResize, bool corruptData)
    {
        using var files = new TemporaryImages();
        var damagedBytes = CreateDamagedPng(needsResize, corruptData);
        AssertDecodeResult(damagedBytes, corruptData ? SKCodecResult.ErrorInInput : SKCodecResult.IncompleteInput);
        var path = await files.WriteAsync("damaged.png", damagedBytes);
        using var handler = new RecordingImageAnalysisHandler();
        using var httpClient = new HttpClient(handler);
        var service = new CopilotImageUnderstandingService(new CopilotChatService(httpClient));

        var failure = await Record.ExceptionAsync(() => service.AnalyzeAsync(
            CreateProfile(), "Inspect the image.", [CopilotAttachmentItem.CreateImage(path)], CancellationToken.None));

        Assert.Equal(0, handler.RequestCount);
        Assert.IsType<InvalidOperationException>(failure);
    }

    [Theory]
    [InlineData("complete-png")]
    [InlineData("png-without-iend")]
    [InlineData("animated-gif")]
    public async Task SuccessfullyDecodedFirstFramePreservesTheOriginalBytesThroughStorageAndProvider(string input)
    {
        using var files = new TemporaryImages();
        var sourceBytes = input == "animated-gif"
            ? Convert.FromBase64String("R0lGODlhAQABAIAAAP8AAAAA/yH5BAABAAAALAAAAAABAAEAAAICRAEAIfkEAAEAAAAsAAAAAAEAAQAAAgJMAQA7")
            : CreatePng(16, 16);
        if (input == "png-without-iend")
        {
            Assert.Equal("IEND", Encoding.ASCII.GetString(sourceBytes.AsSpan(sourceBytes.Length - 8, 4)));
            sourceBytes = sourceBytes[..^12];
        }
        AssertDecodeResult(sourceBytes, SKCodecResult.Success, input == "animated-gif" ? 2 : null);
        var mediaType = input == "animated-gif" ? "image/gif" : "image/png";
        var sourcePath = await files.WriteAsync(input == "animated-gif" ? "source.gif" : "source.png", sourceBytes);

        var attachments = await CopilotImageAttachmentAdmission.PersistAsync(
            [CopilotAttachmentItem.CreateImage(sourcePath)], files.StorePath, CancellationToken.None);
        var stored = Assert.Single(attachments);
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(stored.Value));

        using var handler = new RecordingImageAnalysisHandler();
        using var httpClient = new HttpClient(handler);
        var service = new CopilotImageUnderstandingService(new CopilotChatService(httpClient));
        var analysis = await service.AnalyzeAsync(
            CreateProfile(), "Inspect the image.", attachments, CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        var imageUrl = Assert.Single(handler.ImageUrls);
        var prefix = "data:" + mediaType + ";base64,";
        Assert.StartsWith(prefix, imageUrl, StringComparison.Ordinal);
        Assert.Equal(sourceBytes, Convert.FromBase64String(imageUrl[prefix.Length..]));
        Assert.Contains("Visible image evidence.", analysis.Context, StringComparison.Ordinal);
    }

    private static byte[] CreateDamagedPng(bool needsResize, bool corruptData)
    {
        var bytes = CreatePng(needsResize ? 6_250 : 16, needsResize ? 2 : 16);
        AssertDecodeResult(bytes, SKCodecResult.Success);
        for (var offset = 8; offset < bytes.Length;)
        {
            var chunkLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            var chunkType = Encoding.ASCII.GetString(bytes.AsSpan(offset + 4, 4));
            if (chunkType == "IDAT")
            {
                Assert.True(chunkLength > 2);
                var dataStart = offset + 8;
                if (corruptData)
                {
                    // Keep all headers and dimensions, but invalidate the zlib header.
                    bytes[dataStart] = 0;
                    return bytes;
                }
                // Stop inside compressed pixel data, not merely before the optional
                // trailer. SKCodec still discovers dimensions but cannot decode pixels.
                return bytes[..(dataStart + chunkLength / 2)];
            }
            offset += chunkLength + 12;
        }
        throw new InvalidOperationException("The encoded fixture has no PNG pixel-data chunk.");
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(32, 96, 160, 255));
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        Assert.NotNull(encoded);
        return encoded.ToArray();
    }

    private static void AssertDecodeResult(byte[] bytes, SKCodecResult expected, int? frameCount = null)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        Assert.NotNull(codec);
        Assert.True(codec.Info.Width > 0 && codec.Info.Height > 0);
        if (frameCount.HasValue)
            Assert.Equal(frameCount.Value, codec.FrameCount);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var pixels = new byte[info.BytesSize];
        Assert.Equal(expected, codec.GetPixels(info, pixels));
    }

    private static CopilotProfileConfig CreateProfile() => new()
    {
        VendorType = CopilotVendorType.OpenAI,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "image-validation-test-key",
        BaseUrl = "https://api.openai.com/v1",
        Model = "gpt-4o",
        SupportsImageInput = true,
        MaxTokens = 4_096,
    };

    private sealed class RecordingImageAnalysisHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public List<string> ImageUrls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            foreach (var message in payload.RootElement.GetProperty("messages").EnumerateArray())
            {
                if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("image_url", out var image))
                        ImageUrls.Add(image.GetProperty("url").GetString()!);
                }
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"choices":[{"message":{"role":"assistant","content":"Visible image evidence."},"finish_reason":"stop"}]}
                    """, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class TemporaryImages : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("CopilotImagePayloadValidation-").FullName;
        public string StorePath => Path.Combine(_root, "stored");

        public async Task<string> WriteAsync(string name, byte[] bytes)
        {
            var path = Path.Combine(_root, name);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }

        public void Dispose()
        {
            var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_root));
            var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(fullRoot), tempRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullRoot).StartsWith("CopilotImagePayloadValidation-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected temporary image directory.");
            }
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}
