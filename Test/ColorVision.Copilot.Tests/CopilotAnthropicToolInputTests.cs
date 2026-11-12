#pragma warning disable MAAI001
using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

// Writing a large file is one long tool input. It must not look like a dead stream to the provider
// layers, and input that arrives broken must not run the tool.
public sealed class CopilotAnthropicToolInputTests
{
    private const string ToolName = "colorvision_run_workspace_validation";

    private static readonly string[] ValidFragments = ["{\"query\":\"", .. Enumerable.Repeat("a", 100), "\"}"];

    // Before these counted as progress a call with no text or thinking in front of it failed after
    // 64 fragments, and a long one hit the inactivity timeout with data still arriving.
    [Fact]
    public async Task InputFragmentsAreProgress()
    {
        using var client = new CopilotProviderRetryChatClient(new CopilotProviderConnectionRecoveryChatClient(
            CreateChatClient(new FakeProvider((_, _) => ToolStream(ValidFragments)))));

        var response = await client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Run it")]).ToChatResponseAsync();

        var call = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        Assert.Equal(new string('a', 100), call.Arguments!["query"]?.ToString());
    }

    // A tool with no required parameters would otherwise run on its defaults and report success.
    [Fact]
    public async Task ToolDoesNotRunOnInvalidInputAndTheModelIsToldToResend()
    {
        var provider = new FakeProvider((call, _) => call == 1 ? ToolStream(["{\"query\":\"say \"hi\" there\"}"]) : TextStream("Done."));
        var tool = new ProbeTool();
        var directory = Directory.CreateTempSubdirectory("CopilotAnthropicToolInputTests-");
        try
        {
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "tool-input-tests", "Tool input tests", [tool]);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([tool]),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                profile => CreateSdkClient(provider).AsIChatClient(profile.Model, profile.MaxTokens),
                new EmptyExternalToolProvider(),
                catalog,
                new CopilotAgentSkillUsageStore(directory.FullName));
            const string prompt = "Run the bounded workspace validation probe and summarize its result.";

            var result = await runtime.RunAsync(new CopilotAgentRequest
            {
                Profile = new CopilotProfileConfig { ProviderType = CopilotProviderType.AnthropicCompatible, VendorType = CopilotVendorType.Custom, ApiKey = "test-key", BaseUrl = "https://provider.example.test", Model = "test-model", MaxTokens = 4_096 },
                ConversationId = "tool-input-conversation",
                TaskId = "tool-input-task",
                WorkspacePath = directory.FullName,
                UserText = prompt,
                TaskIntentText = prompt,
                Mode = CopilotAgentMode.Code,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Never),
                RunBudgetOverride = new CopilotAgentRunBudgetOverride { RequestTokenBudget = 32_768, MaxToolCalls = 4, MaxAgentPasses = 1, TotalDuration = TimeSpan.FromSeconds(30) },
            }, _ => { }, CancellationToken.None);

            Assert.Equal(0, tool.Runs);
            Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
            var messages = JsonNode.Parse(provider.Payloads[1])!["messages"]!.AsArray();
            var toolResult = messages[^1]!["content"]![0]!;
            Assert.Equal("tool_result", toolResult["type"]!.GetValue<string>());
            Assert.Contains("not valid JSON", toolResult["content"]!.GetValue<string>(), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // A protected tool reaches the approval step first; the user must not be asked to approve an empty call.
    [Fact]
    public void ApprovalIsNotOpenedForInvalidInput()
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "tool-input-approval",
            TaskId = CopilotAgentTaskEventIds.CreateRunId(),
            WorkspacePath = Path.GetTempPath(),
            UserText = "Run one protected test action.",
            TaskIntentText = "Run one protected test action.",
            Mode = CopilotAgentMode.Auto,
        };
        var tool = new ProtectedTool();
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(
            request,
            CopilotExecutionScope.ForAgentRequest(request),
            [tool],
            1,
            new CopilotToolExecutor(),
            new CopilotFrameworkApprovalCoordinator(),
            _ => { },
            capabilityRevisionProvider: () => 1);
        var functionName = CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ToFunctionName(tool.Name);

        var valid = bridge.TryBeginApproval(new ToolApprovalRequestContent("valid", new FunctionCallContent("call-valid", functionName)), out _, out var validError);
        Assert.True(valid, validError);

        var call = new FunctionCallContent("call-invalid", functionName) { Exception = new InvalidOperationException("Invalid JSON.") };
        var opened = bridge.TryBeginApproval(new ToolApprovalRequestContent("invalid", call), out _, out var error);

        Assert.False(opened);
        Assert.Contains("not valid JSON", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EagerHandlerMarksClientToolsAndKeepsTheRestOfTheRequest()
    {
        var provider = new FakeProvider((_, _) => TextStream("Done."));
        var function = AIFunctionFactory.Create((string path) => path, "write_file", "写入文件");
        using var plain = CreateChatClient(provider);
        using var eager = CreateSdkClient(provider, [new CopilotAnthropicHttpErrorHandler("test-key"), new CopilotAnthropicEagerToolInputHandler()]).AsIChatClient("test-model", 128);
        var options = new ChatOptions { Tools = [function] };

        await plain.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "路径")], options).ToChatResponseAsync();
        await eager.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "路径")], options).ToChatResponseAsync();

        var expected = JsonNode.Parse(provider.Payloads[0])!;
        expected["tools"]![0]!["eager_input_streaming"] = true;
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(provider.Payloads[1])));
        Assert.Contains("路径", provider.Payloads[1], StringComparison.Ordinal);

        Assert.True(CopilotAnthropicEagerToolInputHandler.TryEnable("""{"tools":[{"type":"web_search_20260209","name":"web_search"},{"name":"a","input_schema":{}}]}""", out var mixed));
        var tools = JsonNode.Parse(mixed)!["tools"]!.AsArray();
        Assert.Null(tools[0]!["eager_input_streaming"]);
        Assert.True(tools[1]!["eager_input_streaming"]!.GetValue<bool>());
        Assert.False(CopilotAnthropicEagerToolInputHandler.TryEnable("""{"model":"m"}""", out _));
    }

    // A tool loop re-reads its whole history every round. Charged in full, a run at this size would stop on
    // the second call although almost nothing was billed; reported usage must still show the raw numbers.
    [Fact]
    public async Task BudgetChargesCachedReadsAtTheirCost()
    {
        var calls = 0;
        var provider = new FakeProvider((_, _) =>
        {
            calls++;
            return Sse(Start().Replace("\"input_tokens\":10,", "\"input_tokens\":100000,\"cache_read_input_tokens\":0,", StringComparison.Ordinal)
                + Event("message_delta", new { type = "message_delta", delta = new { stop_reason = "end_turn", stop_sequence = (string?)null }, usage = new { input_tokens = 10_000, cache_read_input_tokens = 90_000, output_tokens = 100 } })
                + Event("message_stop", new { type = "message_stop" }));
        });
        using var client = new CopilotTokenBudgetChatClient(CreateChatClient(provider), new CopilotAgentTokenBudget { ContextWindowTokens = 1_000_000, MaxOutputTokens = 128, RequestTokenBudget = 100_000 });

        for (var round = 0; round < 5; round++)
            await client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Continue.")]).ToChatResponseAsync();

        Assert.Equal(5, calls);
        Assert.False(client.Snapshot.BudgetExhausted);
        Assert.Equal(500_500, client.Snapshot.ConsumedTokens);
        Assert.Equal(500_500, client.Snapshot.ReportedTotalTokens);
        Assert.Equal(450_000, client.Snapshot.ReportedCachedInputTokens);
    }

    // Writing to the cache costs 25% extra, which a compaction summary of a long history would pay for nothing.
    [Fact]
    public async Task OnlyConversationRepliesAskForCaching()
    {
        var provider = new FakeProvider((_, _) => TextStream("Done."));
        var service = new CopilotChatService(new HttpClient(provider));
        var profile = new CopilotProfileConfig { ProviderType = CopilotProviderType.AnthropicCompatible, VendorType = CopilotVendorType.Claude, ApiKey = "test-key", BaseUrl = "https://api.anthropic.com", Model = "claude-opus-5-5", MaxTokens = 1_024 };
        CopilotRequestMessage[] messages = [new("user", "Hello")];

        await service.StreamReplyAsync(profile, messages, _ => { }, CancellationToken.None);
        await service.CompleteReplyDetailedAsync(profile, messages, CancellationToken.None);

        Assert.NotNull(JsonNode.Parse(provider.Payloads[0])!["cache_control"]);
        Assert.Null(JsonNode.Parse(provider.Payloads[1])!["cache_control"]);
    }

    [Fact]
    public void ContextWindowStopIsALengthLimit() => Assert.Equal(CopilotChatFinishKind.LengthLimit, CopilotProviderFinishReasonClassifier.Classify("model_context_window_exceeded"));

    // 32 tokens are enough for a title but not for the thinking these models always do first.
    [Theory]
    [InlineData("claude-opus-5-5", "https://api.anthropic.com", 4_128, CopilotReasoningMode.Low)]
    [InlineData("claude-opus-5-5", "https://gateway.example.test", 4_128, CopilotReasoningMode.Default)]
    [InlineData("claude-opus-4-6", "https://api.anthropic.com", 32, CopilotReasoningMode.Low)]
    public async Task TitleLeavesRoomForThinkingOnModelsThatCannotTurnItOff(string model, string endpoint, int expectedTokens, CopilotReasoningMode expectedMode)
    {
        var profile = new CopilotProfileConfig { ProviderType = CopilotProviderType.AnthropicCompatible, VendorType = CopilotVendorType.Claude, ApiKey = "test-key", BaseUrl = endpoint, Model = model, MaxTokens = 64_000, ReasoningMode = CopilotReasoningMode.High };
        CopilotProfileConfig? sent = null;
        var generator = new CopilotConversationTitleGenerator((used, _, _) =>
        {
            sent = used;
            return Task.FromResult(new CopilotCompletedReplyResult(
                new CopilotChatReply(new CopilotStreamDelta(string.Empty, "Title"), CopilotTokenUsage.Empty),
                new CopilotChatStreamResult(CopilotTokenUsage.Empty, CopilotChatFinishKind.Complete, "stop"),
                IsContentTruncated: false));
        });

        await generator.GenerateAsync(new CopilotConversationTitleRequest(profile, "Prompt"), CancellationToken.None);

        Assert.Equal(expectedTokens, sent!.MaxTokens);
        Assert.Equal(expectedMode, CopilotReasoningCapabilities.GetEffectiveMode(sent));
    }

    private static IChatClient CreateChatClient(FakeProvider provider) => CreateSdkClient(provider).AsIChatClient("test-model", 128);

    private static AnthropicClient CreateSdkClient(FakeProvider provider, DelegatingHandler[]? handlers = null) => new(new ClientOptions
    {
        ApiKey = "test-key",
        BaseUrl = "https://provider.example.test",
        HttpClient = new HttpClient(provider),
        Handlers = handlers ?? [],
        MaxRetries = 0,
    });

    private static string Event(string type, object payload) => "event: " + type + "\ndata: " + JsonSerializer.Serialize(payload) + "\n\n";

    private static string Start() => Event("message_start", new
    {
        type = "message_start",
        message = new { id = "msg", type = "message", role = "assistant", model = "test-model", content = Array.Empty<object>(), stop_reason = (string?)null, stop_sequence = (string?)null, usage = new { input_tokens = 10, output_tokens = 0 } },
    });

    private static string End(string stopReason) => Event("message_delta", new { type = "message_delta", delta = new { stop_reason = stopReason, stop_sequence = (string?)null }, usage = new { input_tokens = 10, output_tokens = 5 } })
        + Event("message_stop", new { type = "message_stop" });

    private static HttpResponseMessage ToolStream(string[] fragments)
    {
        var body = new StringBuilder(Start());
        body.Append(Event("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "tool_use", id = "call-1", name = ToolName, input = new { } } }));
        foreach (var fragment in fragments)
            body.Append(Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "input_json_delta", partial_json = fragment } }));
        body.Append(Event("content_block_stop", new { type = "content_block_stop", index = 0 })).Append(End("tool_use"));
        return Sse(body.ToString());
    }

    private static HttpResponseMessage TextStream(string text) => Sse(Start()
        + Event("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = string.Empty } })
        + Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text } })
        + Event("content_block_stop", new { type = "content_block_stop", index = 0 })
        + End("end_turn"));

    private static HttpResponseMessage Sse(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    private sealed class FakeProvider(Func<int, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            Payloads.Add(payload);
            return respond(Payloads.Count, payload);
        }
    }

    private sealed class ProbeTool : ICopilotAgentDrivenTool
    {
        public int Runs { get; private set; }
        public string Name => "RunWorkspaceValidation";
        public string Description => "Returns deterministic validation evidence.";
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            Runs++;
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Validation probe completed.", ProcessOperation = "test", ProcessExitCode = 0 });
        }
    }

    private sealed class ProtectedTool : ICopilotTool
    {
        public string Name => "ProtectedProbe";
        public string Description => "A protected tool.";
        public CopilotToolCapabilityDescriptor Capability { get; } = new()
        {
            Access = CopilotToolAccess.Write,
            RiskLevel = CopilotToolRiskLevel.High,
            ApprovalMode = CopilotToolApprovalMode.Always,
            Idempotency = CopilotToolIdempotency.NonIdempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
        };
        public CopilotToolInputSchema InputSchema => CopilotToolInputSchema.Empty;
        public bool CanHandle(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The protected tool must not execute.");
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken) => Task.FromResult(new CopilotExternalToolLease());
    }
}
