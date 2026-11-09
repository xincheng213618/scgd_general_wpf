using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;

namespace ColorVision.Copilot.Tests;

// The SDK reads the request from the raw parameters that carry cache_control, and gateways may reject
// the field, so a mistake here silently changes the model, output cap, reasoning or provider request.
public sealed class CopilotAnthropicPromptCacheTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfficialEndpointOnlyAddsCacheControlToTheRequest(bool streaming)
    {
        var (cachedBody, usage) = await SendAsync("https://api.anthropic.com/", streaming, wrap: true);
        var (plainBody, _) = await SendAsync("https://api.anthropic.com/", streaming, wrap: false);

        var cached = JsonNode.Parse(cachedBody)!.AsObject();
        Assert.Equal("ephemeral", cached["cache_control"]!["type"]!.GetValue<string>());
        var system = cached["system"]!.AsArray()[0]!.AsObject();
        Assert.Equal("ephemeral", system["cache_control"]!["type"]!.GetValue<string>());
        system.Remove("cache_control");
        Assert.Equal("profile-model", cached["model"]!.GetValue<string>());
        Assert.Equal(321, cached["max_tokens"]!.GetValue<int>());
        cached.Remove("cache_control");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(plainBody), cached), $"{plainBody}\n{cached.ToJsonString()}");

        // Budgets rely on the input count including cached tokens.
        Assert.Equal(1204, usage.InputTokenCount);
        Assert.Equal(1000, usage.CachedInputTokenCount);
    }

    // A changed active document, Git HEAD or date must not rewrite the marked block, or
    // every later turn pays to write the tools and instructions again.
    [Fact]
    public async Task RequestSpecificEnvironmentStaysBehindTheCachedInstructions()
    {
        string[] Blocks(string payload) => JsonNode.Parse(payload)!["system"]!.AsArray().Select(block => block!["text"]!.GetValue<string>()).ToArray();
        string instructions(string document)
        {
            var request = new CopilotAgentRequest { Profile = CopilotProfileConfig.CreateDefault(), UserText = "Explain the workspace.", Mode = CopilotAgentMode.Code, ActiveDocumentPath = document };
            var environment = CopilotAgentEnvironmentContext.Capture(request);
            return CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(request, [], environment, taskLedgerEnabled: false, agentModeEnabled: false);
        }

        var (first, _) = await SendAsync("https://api.anthropic.com/", streaming: false, wrap: true, instructions: instructions(@"C:\worka.cs"));
        var (second, _) = await SendAsync("https://api.anthropic.com/", streaming: false, wrap: true, instructions: instructions(@"C:\workb.cs"));
        var (plain, _) = await SendAsync("https://api.anthropic.com/", streaming: false, wrap: false, instructions: instructions(@"C:\worka.cs"));

        var firstBlocks = Blocks(first);
        Assert.Equal(2, firstBlocks.Length);
        Assert.Equal(firstBlocks[0], Blocks(second)[0]);
        Assert.NotEqual(firstBlocks[1], Blocks(second)[1]);
        Assert.StartsWith(CopilotMicrosoftAgentFrameworkRuntime.RequestSpecificInstructionsStart + "runtime_environment>", firstBlocks[1], StringComparison.Ordinal);
        Assert.Equal("ephemeral", JsonNode.Parse(first)!["system"]![0]!["cache_control"]!["type"]!.GetValue<string>());
        Assert.Null(JsonNode.Parse(first)!["system"]![1]!["cache_control"]);
        // The model reads the same text as an uncached request.
        Assert.Equal(string.Concat(Blocks(plain)), string.Concat(firstBlocks));
    }

    [Fact]
    public async Task InstructionsStayAfterSystemMessagesWithoutTheirMarker()
    {
        var (body, _) = await SendAsync("https://api.anthropic.com/", streaming: false, wrap: true, [new(ChatRole.System, "first"), new(ChatRole.User, "hi")]);

        var system = JsonNode.Parse(body)!["system"]!.AsArray();
        Assert.Equal(["first", "sys"], system.Select(block => block!["text"]!.GetValue<string>()));
        Assert.All(system, block => Assert.Null(block!["cache_control"]));
    }

    // The final request of a turn is not a prefix of the next turn's history, so without the instructions
    // marker a new turn reads nothing and pays the write premium again on the unchanged tools and system.
    [Fact]
    public async Task NextTurnCanReadTheUnchangedPrefixAndABareQuestion()
    {
        var builder = new CopilotAgentContextBuilder();
        var request = new CopilotAgentRequest { Profile = new CopilotProfileConfig(), UserText = "  Hello there \r\n" };

        var first = builder.BuildHarnessMessages(request, [], minimalDelegatedFinalization: false);
        var next = builder.BuildHarnessMessages(new CopilotAgentRequest
        {
            Profile = request.Profile,
            UserText = "Second question",
            History = [first.Messages[^1], new CopilotRequestMessage("assistant", "Answer.")],
        }, [], minimalDelegatedFinalization: false);

        Assert.Equal("Hello there", first.Messages[^1].Content);
        Assert.Equal(first.Messages[^1].Content, next.Messages[0].Content);
        Assert.Equal("Second question", next.Messages[^1].Content);
    }

    [Fact]
    public void AQuestionKeepsItsHeadingWhenAGoalOrContextSurroundsIt()
    {
        var builder = new CopilotAgentContextBuilder();

        var goal = builder.BuildHarnessMessages(new CopilotAgentRequest { Profile = new CopilotProfileConfig(), UserText = "Question", ActiveGoalText = "Ship the release" }, [], minimalDelegatedFinalization: false);
        var empty = builder.BuildHarnessMessages(new CopilotAgentRequest { Profile = new CopilotProfileConfig(), UserText = "  " }, [], minimalDelegatedFinalization: false);

        Assert.Contains("# User question", goal.Messages[^1].Content, StringComparison.Ordinal);
        Assert.Contains("# User question", empty.Messages[^1].Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://api.deepseek.com/anthropic")]
    [InlineData("https://api.anthropic.com.example.test")]
    [InlineData("https://gateway.example.test/anthropic")]
    public async Task CompatibleGatewaysNeverReceiveCacheControl(string endpoint)
    {
        var (body, _) = await SendAsync(endpoint, streaming: false, wrap: true);

        using var payload = JsonDocument.Parse(body);
        Assert.False(payload.RootElement.TryGetProperty("cache_control", out _));
        Assert.DoesNotContain("cache_control", payload.RootElement.GetProperty("system").GetRawText(), StringComparison.Ordinal);
    }

    private static async Task<(string Body, UsageDetails Usage)> SendAsync(string endpoint, bool streaming, bool wrap, ChatMessage[]? messages = null, string instructions = "sys")
    {
        var profile = new CopilotProfileConfig
        {
            ProviderType = CopilotProviderType.AnthropicCompatible,
            BaseUrl = endpoint,
            Model = "profile-model",
            MaxTokens = 321,
        };
        var transport = new CaptureHandler(streaming);
        using var http = new HttpClient(transport);
        using var sdk = new AnthropicClient(new ClientOptions { ApiKey = "test-key", BaseUrl = endpoint.TrimEnd('/'), HttpClient = http, MaxRetries = 0 });
        var adapter = sdk.AsIChatClient(profile.Model, profile.MaxTokens);
        using var client = wrap ? CopilotAnthropicPromptCacheChatClient.WrapIfSupported(adapter, profile) : adapter;
        var options = new ChatOptions
        {
            Instructions = instructions,
            Temperature = 0.25f,
            MaxOutputTokens = 321,
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High },
            Tools = [AIFunctionFactory.Create(() => "ok", "Lookup")],
        };
        messages ??= [new(ChatRole.User, "hi")];
        UsageDetails? usage = null;
        if (streaming)
        {
            await foreach (var update in client.GetStreamingResponseAsync(messages, options))
                usage = update.Contents.OfType<UsageContent>().LastOrDefault()?.Details ?? usage;
        }
        else
            usage = (await client.GetResponseAsync(messages, options)).Usage;
        return (transport.Body, usage!);
    }

    private sealed class CaptureHandler(bool streaming) : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            const string message = """{"id":"msg_fixture","type":"message","role":"assistant","model":"profile-model","content":[{"type":"text","text":"ok"}],"stop_reason":"end_turn","usage":{"input_tokens":4,"output_tokens":1,"cache_read_input_tokens":1000,"cache_creation_input_tokens":200}}""";
            var body = streaming
                ? "event: message_start\ndata: {\"type\":\"message_start\",\"message\":" + message + "}\n\nevent: message_stop\ndata: {\"type\":\"message_stop\"}\n\n"
                : message;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, streaming ? "text/event-stream" : "application/json") };
        }
    }
}
