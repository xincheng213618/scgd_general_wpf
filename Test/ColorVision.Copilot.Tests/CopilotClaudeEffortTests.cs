using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

// The Claude API rejects an effort or adaptive thinking request on models and gateways without them, so the
// offered levels are the compatibility boundary, and the two request paths must agree on what they send.
public sealed class CopilotClaudeEffortTests
{
    private const string Official = "https://api.anthropic.com";

    [Theory]
    [InlineData("claude-opus-5-5", Official, "auto,low,medium,high,xhigh")]
    [InlineData("claude-fable-5-1", Official, "auto,low,medium,high,xhigh")]
    [InlineData("claude-sonnet-5-5", Official, "auto,low,medium,high,xhigh")]
    [InlineData("claude-haiku-5-5", Official, "auto,low,medium,high,xhigh")]
    [InlineData("claude-opus-4-8", Official, "auto,low,medium,high,xhigh")]
    [InlineData("claude-opus-4-6", Official, "auto,low,medium,high")]
    [InlineData("claude-sonnet-4-6", Official, "auto,low,medium,high")]
    [InlineData("claude-haiku-4-5-20251001", Official, "auto")]
    [InlineData("claude-opus-5-5", "https://gateway.example.test/anthropic", "auto")]
    public void OnlyLevelsTheModelAndEndpointAcceptAreOffered(string model, string endpoint, string expected)
    {
        var profile = CreateProfile(model, endpoint);

        var options = CopilotReasoningCapabilities.GetOptions(profile);

        Assert.Equal(expected, string.Join(",", options.Select(option => CopilotReasoningCapabilities.GetCommandToken(option.Mode))));
        Assert.Equal(options.Count > 1, CopilotReasoningCapabilities.HasConfigurableReasoning(profile));
    }

    [Theory]
    [InlineData("claude-opus-5-5", CopilotReasoningMode.Max, CopilotReasoningMode.XHigh)]
    [InlineData("claude-opus-5-5", CopilotReasoningMode.Disabled, CopilotReasoningMode.Default)]
    [InlineData("claude-sonnet-4-6", CopilotReasoningMode.XHigh, CopilotReasoningMode.High)]
    [InlineData("claude-haiku-4-5-20251001", CopilotReasoningMode.High, CopilotReasoningMode.Default)]
    public void StoredModesConvergeOnWhatTheModelAccepts(string model, CopilotReasoningMode stored, CopilotReasoningMode expected)
    {
        var profile = CreateProfile(model, Official);
        profile.ReasoningMode = stored;

        Assert.Equal(expected, CopilotReasoningCapabilities.GetEffectiveMode(profile));
    }

    // Thinking is silent on these models unless a summary is requested, and a minutes-long silence
    // used to trip the first-content timeout on a request that was still working.
    [Theory]
    [InlineData("claude-opus-5-5", CopilotReasoningMode.Default, null)]
    [InlineData("claude-fable-5-1", CopilotReasoningMode.Default, null)]
    [InlineData("claude-sonnet-5-5", CopilotReasoningMode.Default, null)]
    [InlineData("claude-haiku-5-5", CopilotReasoningMode.Default, null)]
    [InlineData("claude-sonnet-4-6", CopilotReasoningMode.High, "high")]
    [InlineData("claude-opus-5-5", CopilotReasoningMode.XHigh, "xhigh")]
    public async Task AgentAndChatPathsSendTheSameThinkingAndEffort(string model, CopilotReasoningMode mode, string? effort)
    {
        var profile = CreateProfile(model, Official);
        profile.ReasoningMode = mode;
        var transport = new CaptureHandler();
        using var http = new HttpClient(transport);
        using var sdk = new AnthropicClient(new ClientOptions { ApiKey = "test-key", BaseUrl = Official, HttpClient = http, MaxRetries = 0 });
        using var client = CopilotAnthropicPromptCacheChatClient.WrapIfSupported(sdk.AsIChatClient(profile.Model, profile.MaxTokens), profile);

        await client.GetResponseAsync([new(ChatRole.User, "hi")], CopilotMicrosoftAgentFrameworkRuntime.BuildChatOptions(new CopilotAgentRequest { Profile = profile }, []));

        var agent = JsonNode.Parse(transport.Body)!;
        var chat = new Dictionary<string, object?>();
        CopilotReasoningRequestMapper.Apply(profile, chat);
        var mapped = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(chat))!;
        Assert.Equal(effort, agent["output_config"]?["effort"]?.GetValue<string>());
        Assert.Equal("adaptive", agent["thinking"]!["type"]!.GetValue<string>());
        Assert.Equal("summarized", agent["thinking"]!["display"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(agent["output_config"], mapped["output_config"]));
        Assert.True(JsonNode.DeepEquals(agent["thinking"], mapped["thinking"]), $"{agent["thinking"]} / {mapped["thinking"]}");
        // Sonnet 4.6 still takes a temperature, but not together with adaptive thinking.
        Assert.Null(agent["temperature"]);
        Assert.False(CopilotReasoningRequestMapper.ShouldIncludeTemperature(profile));
    }

    [Theory]
    [InlineData("claude-sonnet-4-6", Official)]
    [InlineData("claude-opus-4-8", Official)]
    [InlineData("claude-opus-5-5", "https://gateway.example.test/anthropic")]
    public void DefaultModeLeavesModelsThatMayNotThinkAndGatewaysUnchanged(string model, string endpoint)
    {
        var profile = CreateProfile(model, endpoint);
        var payload = new Dictionary<string, object?>();

        CopilotReasoningRequestMapper.Apply(profile, payload);

        Assert.Empty(payload);
        Assert.Null(CopilotMicrosoftAgentFrameworkRuntime.BuildReasoningOptions(profile));
    }

    private static CopilotProfileConfig CreateProfile(string model, string endpoint) => new()
    {
        ProviderType = CopilotProviderType.AnthropicCompatible,
        VendorType = CopilotVendorType.Claude,
        BaseUrl = endpoint,
        Model = model,
        MaxTokens = 321,
    };

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            const string message = """{"id":"msg_fixture","type":"message","role":"assistant","model":"m","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","usage":{"input_tokens":4,"output_tokens":1}}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(message, Encoding.UTF8, "application/json") };
        }
    }
}
