using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.AI;

namespace ColorVision.UI.Tests;

public sealed class CopilotCoreRuntimeTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Copilot-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RequestSession_ReplacesCancelsAndCompletesTokens()
    {
        using var session = new CopilotRequestSession();

        var first = session.Begin();
        Assert.True(session.IsActive);

        var second = session.Begin();
        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);

        session.Cancel();
        Assert.True(second.IsCancellationRequested);
        Assert.False(session.IsActive);
        Assert.Throws<InvalidOperationException>(() => session.GetRequiredToken());

        var third = session.Begin();
        session.Complete();
        Assert.False(third.IsCancellationRequested);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void StateStore_UsesBackupWhenPrimaryStateIsCorrupt()
    {
        var store = new CopilotChatStateStore(_tempRoot);
        var state = new CopilotChatState();
        state.Conversations.Add(CopilotConversationRecord.CreateEmpty("profile", "Model"));
        store.Save(state);

        state.ActiveProfileId = "updated-profile";
        store.Save(state);
        File.WriteAllText(store.StateFilePath, "{ not valid json", Encoding.UTF8);

        var recovered = store.Load();

        Assert.Single(recovered.Conversations);
        Assert.Equal(CopilotChatState.CurrentSchemaVersion, recovered.SchemaVersion);
        Assert.True(File.Exists(store.BackupStateFilePath));
    }

    [Fact]
    public void StateStore_CleansOnlyUnreferencedManagedAttachments()
    {
        var store = new CopilotChatStateStore(_tempRoot);
        var state = new CopilotChatState();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        state.Conversations.Add(conversation);
        store.Save(state);

        var referencedPath = Path.Combine(store.AttachmentDirectoryPath, "referenced.png");
        var orphanPath = Path.Combine(store.AttachmentDirectoryPath, "orphan.png");
        File.WriteAllText(referencedPath, "referenced");
        File.WriteAllText(orphanPath, "orphan");
        conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(referencedPath, "referenced"));

        var deleted = store.CleanupOrphanedAttachments(state);

        Assert.Equal(1, deleted);
        Assert.True(File.Exists(referencedPath));
        Assert.False(File.Exists(orphanPath));
    }

    [Fact]
    public async Task ChatService_ParsesOpenAiStreamingReasoningContentAndUsage()
    {
        const string responseBody = """
            data: {"choices":[{"delta":{"reasoning_content":"inspect "}}]}

            data: {"choices":[{"delta":{"content":"done"}}]}

            data: {"choices":[],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}

            data: [DONE]

            """;
        using var httpClient = new HttpClient(new StaticResponseHandler(() => CreateEventStreamResponse(responseBody)));
        var service = new CopilotChatService(httpClient);
        var deltas = new List<CopilotStreamDelta>();

        var usage = await service.StreamReplyAsync(CreateProfile(), new[] { new CopilotRequestMessage("user", "test") }, deltas.Add, CancellationToken.None);

        Assert.Equal("inspect ", string.Concat(deltas.Select(delta => delta.ReasoningContent)));
        Assert.Equal("done", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Equal(new CopilotTokenUsage(3, 2, 5), usage);
    }

    [Fact]
    public async Task ChatService_ReportsProviderErrorWithoutLeakingRequestSecret()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"message\":\"invalid model\"}}", Encoding.UTF8, "application/json"),
        }));
        var service = new CopilotChatService(httpClient);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteReplyAsync(
            CreateProfile(),
            new[] { new CopilotRequestMessage("user", "test") },
            CancellationToken.None));

        Assert.Contains("400: invalid model", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatService_PropagatesRequestCancellation()
    {
        using var httpClient = new HttpClient(new DelayedResponseHandler());
        var service = new CopilotChatService(httpClient);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CompleteReplyAsync(
            CreateProfile(),
            new[] { new CopilotRequestMessage("user", "cancel") },
            cts.Token));
    }

    [Fact]
    public async Task McpToolRouterRejectsDuplicatesAndReturnsStableUnknownToolError()
    {
        var router = new CopilotMcpToolRouter()
            .Register("ping", (_, _, _) => Task.FromResult(CopilotMcpToolCallResult.Ok("pong")));

        Assert.Throws<InvalidOperationException>(() => router.Register("PING", (_, _, _) => Task.FromResult(CopilotMcpToolCallResult.Ok("duplicate"))));
        var ping = await router.DispatchAsync("ping", null, "test", CancellationToken.None);
        var missing = await router.DispatchAsync("missing", null, "test", CancellationToken.None);

        Assert.True(ping.Success);
        Assert.Equal("pong", ping.Text);
        Assert.False(missing.Success);
        Assert.Equal("tool_not_found", missing.ErrorCode);
    }

    [Fact]
    public async Task AgentService_ExecutesPlannedToolThenStreamsFinalAnswer()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"{\\\"action\\\":\\\"tool\\\",\\\"toolName\\\":\\\"TestTool\\\",\\\"reason\\\":\\\"collect evidence\\\"}\"}}]}"),
            CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"{\\\"action\\\":\\\"finish\\\",\\\"reason\\\":\\\"enough evidence\\\"}\"}}]}"),
            CreateEventStreamResponse("data: {\"choices\":[{\"delta\":{\"content\":\"final answer\"}}]}\n\ndata: [DONE]\n\n"),
        });
        using var httpClient = new HttpClient(new StaticResponseHandler(() => responses.Dequeue()));
        var chatService = new CopilotChatService(httpClient);
        var tool = new TestAgentTool();
        var service = new CopilotAgentService(chatService, new CopilotToolRegistry(new[] { tool }), new CopilotAgentContextBuilder());
        var events = new List<CopilotAgentEvent>();

        var result = await service.RunAsync(new CopilotAgentRequest
        {
            UserText = "diagnose",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, events.Add, CancellationToken.None);

        Assert.Equal(1, tool.ExecutionCount);
        Assert.Single(result.StepRecords);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == "final answer");
        Assert.Equal(CopilotAgentEventType.Completed, events[^1].Type);
    }

    [Fact]
    public async Task AgentRuntimeRouter_UsesExperimentalRuntimeOnlyForExplicitSupportedProfile()
    {
        var builtIn = new RecordingAgentRuntime("built-in");
        var experimental = new RecordingAgentRuntime("experimental");
        var router = new CopilotAgentRuntimeRouter(builtIn, experimental);
        var profile = CreateProfile();
        var events = new List<CopilotAgentEvent>();

        await router.RunAsync(new CopilotAgentRequest { Profile = profile }, events.Add, CancellationToken.None);
        Assert.Equal(1, builtIn.RunCount);
        Assert.Equal(0, experimental.RunCount);

        profile.UseAgentFramework = true;
        await router.RunAsync(new CopilotAgentRequest { Profile = profile }, events.Add, CancellationToken.None);
        Assert.Equal(1, builtIn.RunCount);
        Assert.Equal(1, experimental.RunCount);

        profile.ProviderType = CopilotProviderType.AnthropicCompatible;
        await router.RunAsync(new CopilotAgentRequest { Profile = profile }, events.Add, CancellationToken.None);
        Assert.Equal(2, builtIn.RunCount);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.Status && item.Text.Contains("using the built-in Agent runtime", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ExecutesGuardedFunctionAndStreamsAnswer()
    {
        var tool = new TestAgentTool("SearchDocs");
        using var fakeChatClient = new FunctionCallingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var profile = CreateProfile();
        profile.UseAgentFramework = true;
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "Search the ColorVision documentation.",
            Profile = profile,
            Mode = CopilotAgentMode.Diagnose,
        }, events.Add, CancellationToken.None);

        Assert.Equal(1, tool.ExecutionCount);
        Assert.Single(result.StepRecords);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == "harness answer");
        Assert.Equal(CopilotAgentEventType.Completed, events[^1].Type);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "secret-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 256,
            MaxToolRounds = 3,
        };
    }

    private static HttpResponseMessage CreateEventStreamResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/event-stream"),
        };
    }

    private static HttpResponseMessage CreateJsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StaticResponseHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory());
        }
    }

    private sealed class DelayedResponseHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private sealed class TestAgentTool : ICopilotTool
    {
        public TestAgentTool(string name = "TestTool")
        {
            Name = name;
        }

        public string Name { get; }

        public string Description => "Collect deterministic test evidence.";

        public int ExecutionCount { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Evidence collected.",
                Content = "deterministic evidence",
            });
        }
    }

    private sealed class RecordingAgentRuntime(string runtimeName) : ICopilotAgentRuntime
    {
        public int RunCount { get; private set; }

        public Task<CopilotAgentRunResult> RunAsync(CopilotAgentRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken)
        {
            RunCount++;
            onEvent(CopilotAgentEvent.Status(runtimeName));
            return Task.FromResult(new CopilotAgentRunResult());
        }
    }

    private sealed class FunctionCallingChatClient : IChatClient
    {
        private int _streamCallCount;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "harness answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            if (Interlocked.Increment(ref _streamCallCount) == 1)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, new List<AIContent>
                {
                    new FunctionCallContent("call-1", "search_colorvision_docs", new Dictionary<string, object?>
                    {
                        ["query"] = "plugin development",
                    }),
                });
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "harness answer");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
