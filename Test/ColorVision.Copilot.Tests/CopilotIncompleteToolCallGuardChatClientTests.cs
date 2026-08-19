using System.Runtime.CompilerServices;
using ColorVision.Copilot;
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

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
