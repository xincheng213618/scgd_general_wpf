using ColorVision.Copilot;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotOpenAiChatRequestPolicyTests
{
    [Fact]
    public async Task OfficialModernModelUsesCurrentOpenAiChatParameters()
    {
        using var handler = new CapturingHandler();
        using var document = await CaptureRequestAsync(
            CreateProfile(CopilotVendorType.OpenAI, "gpt-5.5"),
            handler);
        var root = document.RootElement;

        Assert.Equal(4_096, root.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_tokens", out _));
        Assert.False(root.TryGetProperty("temperature", out _));
        Assert.Equal(
            "developer",
            root.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task OfficialNonReasoningModelKeepsSamplingWithCurrentTokenLimit()
    {
        using var handler = new CapturingHandler();
        using var document = await CaptureRequestAsync(
            CreateProfile(CopilotVendorType.OpenAI, "gpt-4o"),
            handler);
        var root = document.RootElement;

        Assert.Equal(4_096, root.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_tokens", out _));
        Assert.Equal(0.4, root.GetProperty("temperature").GetDouble());
        Assert.Equal(
            "system",
            root.GetProperty("messages")[0].GetProperty("role").GetString());
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

        Assert.Equal(4_096, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_completion_tokens", out _));
        Assert.Equal(0.4, root.GetProperty("temperature").GetDouble());
        Assert.Equal(
            "system",
            root.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    private static async Task<JsonDocument> CaptureRequestAsync(
        CopilotProfileConfig profile,
        CapturingHandler handler)
    {
        using var httpClient = new HttpClient(handler);
        var service = new CopilotChatService(httpClient);

        await service.StreamReplyAsync(
            profile,
            [new CopilotRequestMessage("user", "Test the request shape.")],
            _ => { },
            CancellationToken.None);

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
                        "content": "OK"
                      },
                      "finish_reason": "stop"
                    }
                  ]
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
