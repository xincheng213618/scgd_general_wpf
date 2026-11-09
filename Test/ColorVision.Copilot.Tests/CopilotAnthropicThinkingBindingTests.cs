using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

// Opus 5.5, Fable 5.1, Sonnet 5.5 and Haiku 5.5 reject thinking replayed after the history changed. Without
// this recovery a run that has to compact its context ends in a provider failure instead of continuing.
public sealed class CopilotAnthropicThinkingBindingTests
{
    private const string RejectionMessage = "messages.1.content.0: Invalid `signature` in `thinking` block. The block is bound to a different conversation. Remove the block, or set `thinking.block_binding.prefix_mismatch_behavior` to \"drop_block\".";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedThinkingIsRemovedOnceAndStaysOutWhileNewThinkingReplays(bool streaming)
    {
        var provider = new FakeProvider((_, payload) => ThinkingSignatures(payload).Contains("sig-old") ? Rejection() : Response("Done."));
        var diagnostics = new List<string>();
        using var client = CreateClient(provider, diagnostics.Add);
        ChatMessage[] history = [new(ChatRole.User, "Run it"), SignedTurn("sig-old")];

        Assert.Equal("Done.", await SendAsync(client, [.. history, new(ChatRole.User, "Continue")], streaming));

        Assert.Equal(2, provider.Payloads.Count);
        Assert.Equal(["sig-old"], ThinkingSignatures(provider.Payloads[0]));
        Assert.Empty(ThinkingSignatures(provider.Payloads[1]));
        var turn = JsonNode.Parse(provider.Payloads[1])!["messages"]!.AsArray()[1]!;
        Assert.Equal("assistant", turn["role"]!.GetValue<string>());
        Assert.Equal("Visible text.", turn["content"]![0]!["text"]!.GetValue<string>());
        Assert.Contains("removed 1 block", Assert.Single(diagnostics), StringComparison.Ordinal);

        Assert.Equal("Done.", await SendAsync(client, [.. history, SignedTurn("sig-new"), new(ChatRole.User, "Again")], streaming));

        Assert.Equal(3, provider.Payloads.Count);
        Assert.Equal(["sig-new"], ThinkingSignatures(provider.Payloads[2]));
        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyOneRecoveryRunsWhenTheResubmissionIsRejectedToo(bool streaming)
    {
        var provider = new FakeProvider((_, _) => Rejection());
        using var client = CreateClient(provider);

        await Assert.ThrowsAnyAsync<AnthropicApiException>(() =>
            SendAsync(client, [new(ChatRole.User, "Run it"), SignedTurn("sig-old"), new(ChatRole.User, "Continue")], streaming));

        Assert.Equal(2, provider.Payloads.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "max_tokens: Field required", 1)]
    [InlineData(HttpStatusCode.InternalServerError, RejectionMessage, 1)]
    [InlineData(HttpStatusCode.BadRequest, RejectionMessage, 2)]
    public async Task OnlyAThinkingSignatureRejectionIsResubmitted(HttpStatusCode status, string message, int expectedCalls)
    {
        var provider = new FakeProvider((call, _) => call == 1 ? Failure(status, message) : Response("Done."));
        using var client = CreateClient(provider);

        var send = SendAsync(client, [new(ChatRole.User, "Run it"), SignedTurn("sig-old"), new(ChatRole.User, "Continue")], streaming: true);

        if (expectedCalls == 2)
            Assert.Equal("Done.", await send);
        else
            await Assert.ThrowsAnyAsync<AnthropicApiException>(() => send);
        Assert.Equal(expectedCalls, provider.Payloads.Count);
    }

    [Fact]
    public async Task HistoryWithoutThinkingIsNotResubmitted()
    {
        var provider = new FakeProvider((_, _) => Rejection());
        using var client = CreateClient(provider);

        await Assert.ThrowsAnyAsync<AnthropicApiException>(() => SendAsync(client, [new(ChatRole.User, "Run it")], streaming: false));

        Assert.Single(provider.Payloads);
    }

    [Fact]
    public void OtherProvidersKeepTheirOwnReasoningRules()
    {
        using var inner = new FakeChatClient();

        Assert.Same(inner, CopilotAnthropicThinkingBindingChatClient.WrapIfSupported(inner, new CopilotProfileConfig { ProviderType = CopilotProviderType.OpenAICompatible }));
    }

    // The defect this guards: context recovery truncates, the provider rejects the kept thinking,
    // and the run used to stop even though the recovery itself had succeeded.
    [Fact]
    public async Task ContextRecoveryThatInvalidatesThinkingStillCompletesTheRun()
    {
        var truncated = false;
        var provider = new FakeProvider((call, payload) =>
        {
            if (call <= 2)
                return Stream(call, toolCall: true);
            if (call == 3)
            {
                truncated = true;
                return Failure(HttpStatusCode.RequestEntityTooLarge, "request too large");
            }
            return truncated && ThinkingSignatures(payload).Count > 0 ? Rejection() : Stream(call, toolCall: false);
        });
        var (result, events) = await RunAsync(provider);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(1, result.Budget.ContextRecoveryCount);
        // Every rejected attempt stays visible in provider accounting.
        Assert.Equal(provider.Payloads.Count, result.Budget.ProviderCalls);
        Assert.Empty(ThinkingSignatures(provider.Payloads[^1]));
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic && item.Text.Contains("rejected replayed thinking", StringComparison.Ordinal));
    }

    private static async Task<(CopilotAgentRunResult Result, List<CopilotAgentEvent> Events)> RunAsync(FakeProvider provider, string endpoint = "https://provider.example.test", string model = "test-model")
    {
        var directory = Directory.CreateTempSubdirectory("CopilotAnthropicThinkingBindingTests-");
        try
        {
            var tool = new ProbeTool();
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "thinking-binding-tests", "Thinking binding tests", [tool]);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([tool]),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                profile => CreateSdkClient(provider).AsIChatClient(profile.Model, profile.MaxTokens),
                new EmptyExternalToolProvider(),
                catalog,
                new CopilotAgentSkillUsageStore(directory.FullName));
            const string prompt = "Run the bounded workspace validation probe and summarize its result.";
            var events = new List<CopilotAgentEvent>();
            var result = await runtime.RunAsync(new CopilotAgentRequest
            {
                Profile = new CopilotProfileConfig { ProviderType = CopilotProviderType.AnthropicCompatible, VendorType = CopilotVendorType.Custom, ApiKey = "test-key", BaseUrl = endpoint, Model = model, MaxTokens = 4_096 },
                ConversationId = "thinking-binding-conversation",
                TaskId = "thinking-binding-task",
                WorkspacePath = directory.FullName,
                UserText = prompt,
                TaskIntentText = prompt,
                Mode = CopilotAgentMode.Code,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Never),
                RunBudgetOverride = new CopilotAgentRunBudgetOverride { RequestTokenBudget = 32_768, MaxToolCalls = 4, MaxAgentPasses = 1, TotalDuration = TimeSpan.FromSeconds(30) },
            }, events.Add, CancellationToken.None);
            return (result, events);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // Summaries are on by default, so every tool round replays them: a block split by streaming
    // or rewritten on the way back would be rejected on the very next request.
    [Fact]
    public async Task StreamedSummaryIsReplayedAsOneSignedBlockBeforeItsToolCall()
    {
        var provider = new FakeProvider((call, _) => Stream(call, call == 1, ["Part one. ", "Part two."]));
        var (result, _) = await RunAsync(provider);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var turn = JsonNode.Parse(provider.Payloads[1])!["messages"]!.AsArray().Single(message => message!["role"]!.GetValue<string>() == "assistant")!;
        var blocks = turn["content"]!.AsArray();
        Assert.Equal(["thinking", "tool_use"], blocks.Select(block => block!["type"]!.GetValue<string>()));
        Assert.Equal("Part one. Part two.", blocks[0]!["thinking"]!.GetValue<string>());
        Assert.Equal("sig-1", blocks[0]!["signature"]!.GetValue<string>());
    }

    // The no-tools finalization is the last chance for an answer. Sent without streaming it has to
    // finish inside the first-content timeout, which a long silent thinking phase can exceed.
    [Theory]
    [InlineData("https://api.anthropic.com", "claude-opus-5-5", true)]
    [InlineData("https://provider.example.test", "test-model", false)]
    public async Task OnlyOfficialClaudeStreamsTheNoToolsFinalization(string endpoint, string model, bool expectStream)
    {
        var provider = new FakeProvider((call, payload) =>
        {
            if (call == 1)
                return Stream(call, toolCall: true);
            var request = JsonNode.Parse(payload)!;
            return request["tools"] == null ? Response("Final answer.") : Stream(call, toolCall: false, emptyAnswer: true);
        });

        var (result, _) = await RunAsync(provider, endpoint, model);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var finalization = JsonNode.Parse(provider.Payloads[^1])!;
        Assert.Null(finalization["tools"]);
        Assert.Equal(expectStream, finalization["stream"]?.GetValue<bool>() == true);
    }

    private static CopilotAnthropicThinkingBindingChatClient CreateClient(FakeProvider provider, Action<string>? onRecovery = null)
        => new(CreateSdkClient(provider).AsIChatClient("test-model", 128), onRecovery);

    private static AnthropicClient CreateSdkClient(FakeProvider provider) => new(new ClientOptions
    {
        ApiKey = "test-key",
        BaseUrl = "https://provider.example.test",
        HttpClient = new HttpClient(provider),
        Handlers = [new CopilotAnthropicHttpErrorHandler("test-key")],
        MaxRetries = 0,
    });

    private static ChatMessage SignedTurn(string signature) => new(ChatRole.Assistant,
    [
        new TextReasoningContent(string.Empty) { ProtectedData = signature },
        new TextContent("Visible text."),
    ]);

    private static async Task<string> SendAsync(IChatClient client, ChatMessage[] messages, bool streaming)
    {
        if (!streaming)
            return (await client.GetResponseAsync(messages)).Text;

        var answer = new StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync(messages))
            answer.Append(update.Text);
        return answer.ToString();
    }

    private static List<string> ThinkingSignatures(string payload)
    {
        var signatures = new List<string>();
        using var document = JsonDocument.Parse(payload);
        foreach (var message in document.RootElement.GetProperty("messages").EnumerateArray())
        {
            if (message.GetProperty("content") is not { ValueKind: JsonValueKind.Array } content)
                continue;
            foreach (var block in content.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                if (type == "thinking")
                    signatures.Add(block.GetProperty("signature").GetString()!);
                else if (type == "redacted_thinking")
                    signatures.Add(block.GetProperty("data").GetString()!);
            }
        }
        return signatures;
    }

    private static HttpResponseMessage Rejection() => Failure(HttpStatusCode.BadRequest, RejectionMessage);

    private static HttpResponseMessage Failure(HttpStatusCode status, string message) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { type = "error", error = new { type = "invalid_request_error", message } }), Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Response(string text)
    {
        var message = new
        {
            id = "msg_test", type = "message", role = "assistant", model = "test-model",
            content = new[] { new { type = "text", text } },
            stop_reason = "end_turn", stop_sequence = (string?)null,
            usage = new { input_tokens = 10, output_tokens = 2 },
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json"),
        };
    }

    private static string Event(string type, object payload) => "event: " + type + "\ndata: " + JsonSerializer.Serialize(payload) + "\n\n";

    private static HttpResponseMessage Stream(int call, bool toolCall, string[]? thinking = null, bool emptyAnswer = false)
    {
        var body = Event("message_start", new
        {
            type = "message_start",
            message = new { id = "msg_" + call, type = "message", role = "assistant", model = "test-model", content = Array.Empty<object>(), stop_reason = (string?)null, stop_sequence = (string?)null, usage = new { input_tokens = 100, output_tokens = 0 } },
        });
        body += Event("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "thinking", thinking = string.Empty, signature = string.Empty } });
        foreach (var part in thinking ?? [])
            body += Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "thinking_delta", thinking = part } });
        body += Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "signature_delta", signature = "sig-" + call } });
        body += Event("content_block_stop", new { type = "content_block_stop", index = 0 });
        body += toolCall
            ? Event("content_block_start", new { type = "content_block_start", index = 1, content_block = new { type = "tool_use", id = "call-" + call, name = "colorvision_run_workspace_validation", input = new { } } })
                + Event("content_block_delta", new { type = "content_block_delta", index = 1, delta = new { type = "input_json_delta", partial_json = "{\"query\":\"q" + call + "\"}" } })
            : Event("content_block_start", new { type = "content_block_start", index = 1, content_block = new { type = "text", text = string.Empty } })
                + Event("content_block_delta", new { type = "content_block_delta", index = 1, delta = new { type = "text_delta", text = emptyAnswer ? string.Empty : "Completed answer." } });
        body += Event("content_block_stop", new { type = "content_block_stop", index = 1 });
        body += Event("message_delta", new { type = "message_delta", delta = new { stop_reason = toolCall ? "tool_use" : "end_turn", stop_sequence = (string?)null }, usage = new { input_tokens = 100, output_tokens = 10 } });
        body += Event("message_stop", new { type = "message_stop" });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };
    }

    private sealed class FakeProvider(Func<int, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            Payloads.Add(payload);
            var response = respond(Payloads.Count, payload);
            using var document = JsonDocument.Parse(payload);
            // A client-level streaming request needs a stream even though the fixture wrote a plain message.
            if (response.IsSuccessStatusCode
                && document.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean()
                && response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                var text = JsonNode.Parse(message)!["content"]![0]!["text"]!.GetValue<string>();
                var start = JsonNode.Parse(message)!.AsObject();
                start["content"] = new JsonArray();
                var body = "event: message_start\ndata: " + new JsonObject { ["type"] = "message_start", ["message"] = start }.ToJsonString() + "\n\n"
                    + Event("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = string.Empty } })
                    + Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text } })
                    + Event("content_block_stop", new { type = "content_block_stop", index = 0 })
                    + Event("message_stop", new { type = "message_stop" });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };
            }
            return response;
        }
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class ProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "RunWorkspaceValidation";
        public string Description => "Returns deterministic validation evidence.";
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
            => Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Validation probe completed.", ProcessOperation = "test", ProcessExitCode = 0 });
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken) => Task.FromResult(new CopilotExternalToolLease());
    }
}
