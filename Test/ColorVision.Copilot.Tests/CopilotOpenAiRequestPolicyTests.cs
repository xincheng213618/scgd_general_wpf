using ColorVision.Copilot;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotOpenAiRequestPolicyTests
{
    [Fact]
    public async Task OfficialAstraUsesResponsesContract()
    {
        using var handler = new CapturingHandler();
        using var document = await CaptureRequestAsync(
            CreateProfile(CopilotVendorType.OpenAI, "gpt-6-astra"),
            handler);
        var root = document.RootElement;

        Assert.Equal(new Uri("https://api.openai.com/v1/responses"), handler.LastRequestUri);
        Assert.Equal(4_096, root.GetProperty("max_output_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_completion_tokens", out _));
        Assert.False(root.TryGetProperty("max_tokens", out _));
        Assert.False(root.TryGetProperty("temperature", out _));
        Assert.False(root.TryGetProperty("messages", out _));
        Assert.False(root.TryGetProperty("stream_options", out _));
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal("Follow the test instruction.", root.GetProperty("instructions").GetString());
        Assert.Equal("user", root.GetProperty("input")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task OfficialNonReasoningModelKeepsSamplingWithCurrentTokenLimit()
    {
        using var handler = new CapturingHandler();
        using var document = await CaptureRequestAsync(
            CreateProfile(CopilotVendorType.OpenAI, "gpt-4o"),
            handler);
        var root = document.RootElement;

        Assert.Equal(new Uri("https://api.openai.com/v1/responses"), handler.LastRequestUri);
        Assert.Equal(4_096, root.GetProperty("max_output_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_tokens", out _));
        Assert.Equal(0.4, root.GetProperty("temperature").GetDouble());
        Assert.Equal("Follow the test instruction.", root.GetProperty("instructions").GetString());
    }

    [Fact]
    public async Task ThirdPartyCompatibleEndpointKeepsLegacyContractAfterVendorInference()
    {
        using var handler = new CapturingHandler();
        var profile = CreateProfile(
            CopilotVendorType.Custom,
            "gpt-5.5-compatible");

        Assert.True(profile.EnsureValid());
        Assert.Equal(CopilotVendorType.OpenAI, profile.VendorType);

        using var document = await CaptureRequestAsync(
            profile,
            handler);
        var root = document.RootElement;

        Assert.Equal(new Uri("https://example.test/v1/chat/completions"), handler.LastRequestUri);
        Assert.Equal(4_096, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_completion_tokens", out _));
        Assert.Equal(0.4, root.GetProperty("temperature").GetDouble());
        Assert.Equal(
            "system",
            root.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task RequestSystemContextIsMergedIntoResponsesInstructions()
    {
        using var handler = new CapturingHandler();
        using var document = await CaptureRequestAsync(
            CreateProfile(CopilotVendorType.OpenAI, "gpt-6-astra"),
            handler,
            "UserPromptSubmit hook context: inspect the reproduction first.");
        var instruction = document.RootElement.GetProperty("instructions").GetString();

        Assert.Contains(
            "Follow the test instruction.",
            instruction,
            StringComparison.Ordinal);
        Assert.Contains(
            "UserPromptSubmit hook context",
            instruction,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopilotReasoningMode.Disabled, "low")]
    [InlineData(CopilotReasoningMode.Low, "low")]
    [InlineData(CopilotReasoningMode.Medium, "medium")]
    [InlineData(CopilotReasoningMode.High, "high")]
    [InlineData(CopilotReasoningMode.XHigh, "xhigh")]
    [InlineData(CopilotReasoningMode.Max, "max")]
    public async Task AstraUsesOnlySupportedReasoningEfforts(
        CopilotReasoningMode configuredMode,
        string expectedEffort)
    {
        using var handler = new CapturingHandler();
        var profile = CreateProfile(CopilotVendorType.OpenAI, "gpt-6-astra");
        profile.ReasoningMode = configuredMode;

        using var document = await CaptureRequestAsync(profile, handler);

        Assert.Equal(
            expectedEffort,
            document.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        var offeredModes = CopilotReasoningCapabilities.GetOptions(profile).Select(option => option.Mode);
        Assert.DoesNotContain(CopilotReasoningMode.Disabled, offeredModes);
        Assert.DoesNotContain(CopilotReasoningMode.Enabled, offeredModes);
    }

    [Fact]
    public async Task AstraStreamingDeltaAndNestedUsageAreParsed()
    {
        using var handler = new CapturingHandler(streamResponses: true);
        using var httpClient = new HttpClient(handler);
        var service = new CopilotChatService(httpClient);
        var content = new StringBuilder();

        var usage = await service.StreamReplyAsync(
            CreateProfile(CopilotVendorType.OpenAI, "gpt-6-astra"),
            [new CopilotRequestMessage("user", "Stream the response.")],
            delta => content.Append(delta.Content),
            CancellationToken.None);

        Assert.Equal("Astra stream.", content.ToString());
        Assert.Equal(12, usage.InputTokens);
        Assert.Equal(3, usage.OutputTokens);
    }

    private static async Task<JsonDocument> CaptureRequestAsync(
        CopilotProfileConfig profile,
        CapturingHandler handler,
        string? requestSystemContext = null)
    {
        using var httpClient = new HttpClient(handler);
        var service = new CopilotChatService(httpClient);

        if (requestSystemContext == null)
        {
            await service.StreamReplyAsync(
                profile,
                [new CopilotRequestMessage("user", "Test the request shape.")],
                _ => { },
                CancellationToken.None);
        }
        else
        {
            await service.StreamReplyAsync(
                profile,
                [new CopilotRequestMessage("user", "Test the request shape.")],
                _ => { },
                onRetry: null,
                onConnectionRecovery: _ => { },
                onUsageChanged: null,
                requestSystemContext: requestSystemContext,
                cancellationToken: CancellationToken.None);
        }

        return JsonDocument.Parse(handler.LastPayload);
    }

    private static CopilotProfileConfig CreateProfile(
        CopilotVendorType vendorType,
        string model)
    {
        var profile = new CopilotProfileConfig
        {
            VendorType = vendorType,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = vendorType == CopilotVendorType.OpenAI
                ? "https://api.openai.com/v1"
                : "https://example.test/v1",
            Model = model,
            MaxTokens = 4_096,
            Temperature = 0.4,
        };
        profile.UseSystemPromptOverride("Follow the test instruction.");
        return profile;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly bool _streamResponses;

        public CapturingHandler(bool streamResponses = false)
        {
            _streamResponses = streamResponses;
        }

        public string LastPayload { get; private set; } = string.Empty;

        public Uri? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastPayload = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (_streamResponses)
            {
                const string stream =
                    "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Astra stream.\"}\n\n"
                    + "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"usage\":{\"input_tokens\":12,\"output_tokens\":3,\"total_tokens\":15}}}\n\n";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(stream, Encoding.UTF8, "text/event-stream"),
                };
            }

            var response = request.RequestUri?.AbsolutePath.EndsWith(
                "/responses",
                StringComparison.OrdinalIgnoreCase) == true
                ? """
                  {
                    "id": "resp_test",
                    "object": "response",
                    "model": "gpt-6-astra",
                    "status": "completed",
                    "output": [{
                      "type": "message",
                      "role": "assistant",
                      "content": [{"type": "output_text", "text": "OK"}]
                    }],
                    "usage": {"input_tokens": 10, "output_tokens": 2, "total_tokens": 12}
                  }
                  """
                : """
                  {
                    "choices": [{
                      "message": {"role": "assistant", "content": "OK"},
                      "finish_reason": "stop"
                    }]
                  }
                  """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    response,
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
