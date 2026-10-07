using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotIncompleteToolCallGuardChatClientTests
{
    [Fact]
    public async Task StreamingLengthLimitedCallIsNotInvoked()
    {
        var executions = 0;
        var diagnostics = new List<(int Count, ChatFinishReason FinishReason)>();
        var provider = new ScriptedToolCallChatClient(ChatFinishReason.Length);
        using var client = CreateFunctionInvokingClient(
            provider,
            diagnostics);
        var options = CreateOptions(
            () =>
            {
                executions++;
                return "executed";
            });

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Run the tool.")],
            options))
        {
            updates.Add(update);
        }

        Assert.Equal(0, executions);
        Assert.Equal(1, provider.CallCount);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(1, diagnostic.Count);
        Assert.Equal(ChatFinishReason.Length, diagnostic.FinishReason);
        Assert.True(Assert.Single(updates.SelectMany(update => update.Contents).OfType<FunctionCallContent>()).InformationalOnly);
    }

    [Fact]
    public async Task StreamingToolCallsFinishStillInvokesNormally()
    {
        var executions = 0;
        var diagnostics = new List<(int Count, ChatFinishReason FinishReason)>();
        var provider = new ScriptedToolCallChatClient(ChatFinishReason.ToolCalls);
        using var client = CreateFunctionInvokingClient(
            provider,
            diagnostics);
        var options = CreateOptions(
            () =>
            {
                executions++;
                return "executed";
            });

        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Run the tool.")],
            options))
        {
        }

        Assert.Equal(1, executions);
        Assert.Equal(2, provider.CallCount);
        Assert.Empty(diagnostics);
        Assert.Equal(["partial-call"], provider.ResultCallIds);
    }

    [Fact]
    public async Task NonStreamingContentFilteredCallIsNotInvoked()
    {
        var executions = 0;
        var diagnostics = new List<(int Count, ChatFinishReason FinishReason)>();
        var provider = new ScriptedToolCallChatClient(ChatFinishReason.ContentFilter);
        using var client = CreateFunctionInvokingClient(
            provider,
            diagnostics);
        var options = CreateOptions(
            () =>
            {
                executions++;
                return "executed";
            });

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Run the tool.")],
            options);

        Assert.Equal(0, executions);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(ChatFinishReason.ContentFilter, Assert.Single(diagnostics).FinishReason);
        Assert.True(Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()).InformationalOnly);
    }

    [Fact]
    public async Task ProviderHandledLengthLimitedCallIsNotReportedAsLocallySuppressed()
    {
        var executions = 0;
        var diagnostics = new List<(int Count, ChatFinishReason FinishReason)>();
        var provider = new ScriptedToolCallChatClient(
            ChatFinishReason.Length,
            includeProviderResult: true);
        using var client = CreateFunctionInvokingClient(
            provider,
            diagnostics);
        var options = CreateOptions(
            () =>
            {
                executions++;
                return "executed";
            });

        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Run the tool.")],
            options))
        {
        }

        Assert.Equal(0, executions);
        Assert.Equal(1, provider.CallCount);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestCopiesExcludeUnpairedInformationalCallsAndPreserveReplayMetadata(bool streaming)
    {
        var suppressedCall = new FunctionCallContent("legacy-suppressed", "write_tool")
        {
            InformationalOnly = true,
        };
        var evidence = new ChatMessage(ChatRole.Assistant, [new TextContent("Partial evidence."), suppressedCall])
        {
            AuthorName = "assistant-author",
            CreatedAt = new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero),
            MessageId = "evidence-message",
            AdditionalProperties = new() { ["provider-history"] = "preserved" },
            RawRepresentation = new object(),
        };
        var pairedCall = new FunctionCallContent("paired", "write_tool") { InformationalOnly = true };
        var pendingCall = new FunctionCallContent("pending", "write_tool");
        var nativeCall = new ToolCallContent("native");
        var completedAndPending = new ChatMessage(ChatRole.Assistant, [pairedCall, pendingCall, nativeCall]);
        var result = new ChatMessage(ChatRole.Tool, [new FunctionResultContent("paired", "completed")]);
        var suppressedOnly = new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("legacy-empty", "write_tool") { InformationalOnly = true }]);
        var user = new ChatMessage(ChatRole.User, "Continue.");
        ChatMessage[] history = [evidence, completedAndPending, result, suppressedOnly, user];
        var provider = new ScriptedToolCallChatClient(ChatFinishReason.Stop);
        using var client = new CopilotIncompleteToolCallGuardChatClient(provider);

        if (streaming)
        {
            await foreach (var _ in client.GetStreamingResponseAsync(history))
            {
            }
        }
        else
        {
            await client.GetResponseAsync(history);
        }

        Assert.Equal(4, provider.RequestMessages.Count);
        var copiedEvidence = provider.RequestMessages[0];
        Assert.NotSame(evidence, copiedEvidence);
        Assert.Equal("Partial evidence.", copiedEvidence.Text);
        Assert.Empty(copiedEvidence.Contents.OfType<FunctionCallContent>());
        Assert.Null(copiedEvidence.RawRepresentation);
        Assert.Equal(evidence.Role, copiedEvidence.Role);
        Assert.Equal(evidence.AuthorName, copiedEvidence.AuthorName);
        Assert.Equal(evidence.CreatedAt, copiedEvidence.CreatedAt);
        Assert.Equal(evidence.MessageId, copiedEvidence.MessageId);
        Assert.Equal("preserved", copiedEvidence.AdditionalProperties!["provider-history"]);
        Assert.Same(completedAndPending, provider.RequestMessages[1]);
        Assert.Equal([pairedCall, pendingCall, nativeCall], provider.RequestMessages[1].Contents);
        Assert.Same(result, provider.RequestMessages[2]);
        Assert.Same(user, provider.RequestMessages[3]);
        Assert.Contains(suppressedCall, evidence.Contents);
        Assert.NotNull(evidence.RawRepresentation);
        Assert.Single(suppressedOnly.Contents);
        Assert.Equal(5, history.Length);
    }

    private static FunctionInvokingChatClient CreateFunctionInvokingClient(
        IChatClient provider,
        List<(int Count, ChatFinishReason FinishReason)> diagnostics) =>
        new(
            new CopilotIncompleteToolCallGuardChatClient(
                provider,
                (count, finishReason) => diagnostics.Add((count, finishReason))),
            loggerFactory: null,
            functionInvocationServices: null);

    private static ChatOptions CreateOptions(Func<string> tool) => new()
    {
        Tools = [AIFunctionFactory.Create(tool, "write_tool")],
    };

    private sealed class ScriptedToolCallChatClient : IChatClient
    {
        private readonly ChatFinishReason _firstFinishReason;
        private readonly bool _includeProviderResult;
        private int _callCount;

        public ScriptedToolCallChatClient(
            ChatFinishReason firstFinishReason,
            bool includeProviderResult = false)
        {
            _firstFinishReason = firstFinishReason;
            _includeProviderResult = includeProviderResult;
        }

        public int CallCount => Volatile.Read(ref _callCount);

        public IReadOnlyList<string> ResultCallIds { get; private set; } = [];

        public IReadOnlyList<ChatMessage> RequestMessages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestMessages = messages.ToArray();
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                return Task.FromResult(new ChatResponse(new ChatMessage(
                    ChatRole.Assistant,
                    CreateFirstContents()))
                {
                    FinishReason = _firstFinishReason,
                });
            }

            CaptureResults(messages);
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "Tool finished."))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestMessages = messages.ToArray();
            var call = Interlocked.Increment(ref _callCount);
            await Task.CompletedTask;
            if (call == 1)
            {
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    CreateFirstContents())
                {
                    FinishReason = _firstFinishReason,
                };
                yield break;
            }

            CaptureResults(messages);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Tool finished.")
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private AIContent[] CreateFirstContents()
        {
            var contents = new List<AIContent>
            {
                new FunctionCallContent(
                    "partial-call",
                    "write_tool",
                    new Dictionary<string, object?>()),
            };
            if (_includeProviderResult)
                contents.Add(new FunctionResultContent("partial-call", "server handled"));
            return contents.ToArray();
        }

        private void CaptureResults(IEnumerable<ChatMessage> messages)
        {
            ResultCallIds = messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Select(result => result.CallId)
                .ToArray();
        }
    }
}
