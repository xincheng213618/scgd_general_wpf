using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Core;
using ColorVision.Copilot;
using Microsoft.Extensions.AI;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotDeepSeekAnthropicChatClientTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptySignatureDoesNotBecomeUnsupportedContentOnToolFollowUp(bool streaming)
    {
        using var transport = new CaptureHandler(streaming);
        using var http = new HttpClient(transport);
        using var sdk = new AnthropicClient(new ClientOptions { ApiKey = "test-key", BaseUrl = "https://api.deepseek.com/anthropic", HttpClient = http, MaxRetries = 0 });
        using var client = CopilotDeepSeekAnthropicChatClient.WrapIfRequired(sdk.AsIChatClient("test-model", 128), Profile("https://api.deepseek.com/anthropic/"));
        var emptySignature = new TextReasoningContent("") { ProtectedData = "synthetic-empty-block-signature" };
        var visibleReasoning = new TextReasoningContent("Synthetic reasoning fixture") { ProtectedData = "synthetic-visible-signature" };
        var assistant = new ChatMessage(ChatRole.Assistant,
            [emptySignature, visibleReasoning, new FunctionCallContent("call_read", "read_file", new Dictionary<string, object?> { ["path"] = "results.csv" })]);
        ChatMessage[] history =
        [
            new(ChatRole.User, "Read the supplied data."),
            new(ChatRole.Assistant, [new TextReasoningContent(null) { ProtectedData = "empty-only-message" }]),
            assistant,
            new(ChatRole.Tool, [new FunctionResultContent("call_read", "observed result")]),
        ];

        if (streaming)
        {
            await foreach (var update in client.GetStreamingResponseAsync(history)) { }
        }
        else
            await client.GetResponseAsync(history);

        using var payload = JsonDocument.Parse(Assert.Single(transport.Payloads));
        var messages = payload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        var blocks = messages.SelectMany(m => m.GetProperty("content").EnumerateArray()).ToArray();
        Assert.DoesNotContain(blocks, b => b.GetProperty("type").GetString() == "redacted_thinking");
        var thinking = Assert.Single(blocks, b => b.GetProperty("type").GetString() == "thinking");
        Assert.Equal("Synthetic reasoning fixture", thinking.GetProperty("thinking").GetString());
        Assert.Equal("synthetic-visible-signature", thinking.GetProperty("signature").GetString());
        Assert.Contains(blocks, b => b.GetProperty("type").GetString() == "tool_use" && b.GetProperty("id").GetString() == "call_read");
        Assert.Contains(blocks, b => b.GetProperty("type").GetString() == "tool_result" && b.GetProperty("tool_use_id").GetString() == "call_read");
        Assert.Equal(3, assistant.Contents.Count);
        Assert.Same(emptySignature, assistant.Contents[0]);
        Assert.Single(history[1].Contents);
    }

    [Theory]
    [InlineData("https://api.anthropic.com", CopilotProviderType.AnthropicCompatible)]
    [InlineData("https://api.deepseek.com.example.test/anthropic", CopilotProviderType.AnthropicCompatible)]
    [InlineData("https://api.deepseek.com/other", CopilotProviderType.AnthropicCompatible)]
    [InlineData("https://api.deepseek.com/anthropic", CopilotProviderType.OpenAICompatible)]
    public async Task OtherEndpointsRetainOpaqueThinking(string endpoint, CopilotProviderType provider)
    {
        using var transport = new CaptureHandler(false);
        using var http = new HttpClient(transport);
        using var sdk = new AnthropicClient(new ClientOptions { ApiKey = "test-key", BaseUrl = endpoint, HttpClient = http, MaxRetries = 0 });
        using var inner = sdk.AsIChatClient("test-model", 128);
        var profile = Profile(endpoint);
        profile.ProviderType = provider;
        var client = CopilotDeepSeekAnthropicChatClient.WrapIfRequired(inner, profile);
        Assert.Same(inner, client);
        await client.GetResponseAsync([new(ChatRole.User, "Continue"), new(ChatRole.Assistant,
            [new TextReasoningContent("") { ProtectedData = "opaque-fixture" }, new TextContent("Prior answer")])]);
        using var payload = JsonDocument.Parse(Assert.Single(transport.Payloads));
        Assert.Contains(payload.RootElement.GetProperty("messages").EnumerateArray().SelectMany(m => m.GetProperty("content").EnumerateArray()),
            b => b.GetProperty("type").GetString() == "redacted_thinking" && b.GetProperty("data").GetString() == "opaque-fixture");
    }

    private static CopilotProfileConfig Profile(string endpoint) => new() { ProviderType = CopilotProviderType.AnthropicCompatible, BaseUrl = endpoint };

    private sealed class CaptureHandler(bool streaming) : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            const string message = "{\"id\":\"msg_fixture\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"ok\"}],\"stop_reason\":\"end_turn\",\"usage\":{\"input_tokens\":4,\"output_tokens\":1}}";
            var body = streaming
                ? "event: message_start\ndata: {\"type\":\"message_start\",\"message\":" + message + "}\n\nevent: message_stop\ndata: {\"type\":\"message_stop\"}\n\n"
                : message;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, streaming ? "text/event-stream" : "application/json") };
        }
    }
}
